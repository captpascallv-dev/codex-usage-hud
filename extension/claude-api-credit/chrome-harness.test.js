const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const directory = __dirname;
const BILLING_URL = "https://platform.claude.com/settings/billing";
const ORG = "Example Organization";
const DISPLAY = "Ada Example";
const GLYPH = "\ue08f";
const AVATAR = "AVATAR_GLYPH";

function node(tag, attrs, children, text) {
  const element = {
    tag,
    tagName: String(tag).toUpperCase(),
    attrs: attrs || {},
    children: children || [],
    _text: text,
    getAttribute(name) {
      return Object.prototype.hasOwnProperty.call(this.attrs, name) ? this.attrs[name] : null;
    },
    querySelector(selector) {
      return this.querySelectorAll(selector)[0] || null;
    },
    querySelectorAll(selector) {
      const found = [];
      for (const child of this.children) collect(child, selector, found);
      return found;
    },
  };
  Object.defineProperty(element, "textContent", {
    get() {
      if (this.children.length) return this.children.map(child => child.textContent).join("");
      return this._text || "";
    },
  });
  return element;
}

function collect(element, selector, found) {
  if (matches(element, selector)) found.push(element);
  for (const child of element.children) collect(child, selector, found);
}

function matches(element, selector) {
  if (selector.startsWith("[")) {
    const match = /^\[([^=]+)="(.*)"\]$/.exec(selector);
    return match !== null && element.getAttribute(match[1]) === match[2];
  }
  return element.tag === selector;
}

function identityButton() {
  return node("button", { "aria-haspopup": "menu" }, [
    node("div", {}, [node("span", { "aria-hidden": "true" }, [], AVATAR)]),
    node("div", {}, [node("span", {}, [], DISPLAY), node("span", {}, [], ORG)]),
    node("span", {}, [], "DECORATION"),
  ]);
}

function promotionalTable() {
  const headers = ["剩餘餘額", "適用範圍", "到期日", "授予日期"].map((label, index) =>
    index === 1
      ? node("th", {}, [node("text", {}, [], label), node("span", { "aria-hidden": "true" }, [], GLYPH)])
      : node("th", {}, [], label));
  const cells = ["US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15"].map(value => node("td", {}, [], value));
  return node("table", { "aria-label": "促銷額度" }, [
    node("thead", {}, [node("tr", {}, headers)]),
    node("tbody", {}, [node("tr", {}, cells)]),
  ]);
}

function loadingDocument() {
  const tables = [];
  const nav = node("div", { "aria-label": "主要導覽" }, [identityButton()]);
  return {
    tables,
    querySelectorAll(selector) {
      const found = [];
      collect(nav, selector, found);
      for (const table of tables) collect(table, selector, found);
      return found;
    },
  };
}

function createChrome(hostMode) {
  const local = {};
  const session = {};
  const tabs = new Map();
  const created = [];
  const reloads = [];
  const removed = [];
  const nativeMessages = [];
  const alarmListeners = [];
  const messageListeners = [];
  const alarmNames = new Set();
  let nextId = 1;

  function area(store) {
    return {
      async get(keys) {
        if (typeof keys === "string") return { [keys]: store[keys] };
        return { ...store };
      },
      async set(values) { Object.assign(store, values); },
      async remove(keys) {
        for (const key of [].concat(keys)) delete store[key];
      },
    };
  }

  const chrome = {
    storage: { local: area(local), session: area(session) },
    tabs: {
      async create(props) {
        const tab = { id: nextId++, url: props.url, active: props.active === true };
        tabs.set(tab.id, tab);
        created.push(tab.id);
        return tab;
      },
      async get(id) {
        if (!tabs.has(id)) throw new Error("missing tab");
        return tabs.get(id);
      },
      async reload(id) { reloads.push(id); },
      async remove(id) {
        removed.push(id);
        tabs.delete(id);
      },
      onUpdated: { addListener() {} },
    },
    alarms: {
      async create(name) { alarmNames.add(name); },
      async clear(name) {
        alarmNames.delete(name);
        return true;
      },
      onAlarm: { addListener(fn) { alarmListeners.push(fn); } },
    },
    scripting: { async executeScript() {} },
    runtime: {
      lastError: null,
      onInstalled: { addListener() {} },
      onStartup: { addListener() {} },
      onMessage: { addListener(fn) { messageListeners.push(fn); } },
      connectNative() {
        const onMessage = [];
        const onDisconnect = [];
        return {
          onMessage: { addListener(fn) { onMessage.push(fn); } },
          onDisconnect: { addListener(fn) { onDisconnect.push(fn); } },
          postMessage(payload) {
            if (hostMode === "ack") {
              nativeMessages.push(payload);
              onMessage.forEach(fn => fn({ schema: 1, ok: true }));
              onDisconnect.forEach(fn => fn());
              return;
            }
            if (hostMode === "reject") {
              nativeMessages.push(payload);
              onMessage.forEach(fn => fn({ schema: 1, ok: false, error: "rejected" }));
              onDisconnect.forEach(fn => fn());
              return;
            }
            chrome.runtime.lastError = { message: "Specified native messaging host not found." };
            onDisconnect.forEach(fn => fn());
          },
        };
      },
    },
  };
  return { chrome, local, created, reloads, removed, nativeMessages, alarmListeners, messageListeners, alarmNames };
}

function loadBackground(chrome) {
  const context = {
    chrome,
    console,
    setTimeout,
    clearTimeout,
    URL,
    crypto,
    TextEncoder,
    performance,
    module: undefined,
  };
  context.globalThis = context;
  context.importScripts = () => {
    vm.runInContext(fs.readFileSync(path.join(directory, "parser.js"), "utf8"), context, { filename: "parser.js" });
  };
  vm.createContext(context);
  vm.runInContext(fs.readFileSync(path.join(directory, "background.js"), "utf8"), context, { filename: "background.js" });
  return context;
}

function dispatch(messageListeners, message, sender) {
  return new Promise(resolve => {
    let pending = 0;
    let response;
    const sendResponse = value => {
      response = value;
      pending -= 1;
      if (pending <= 0) resolve(response);
    };
    for (const listener of messageListeners) {
      if (listener(message, sender || {}, sendResponse) === true) pending += 1;
    }
    if (pending === 0) resolve(response);
  });
}

async function runContent(document) {
  const sent = [];
  const context = {
    document,
    location: { href: BILLING_URL },
    performance,
    setTimeout,
    clearTimeout,
    URL,
    crypto,
    TextEncoder,
    console,
    module: undefined,
    chrome: { runtime: { async sendMessage(message) { sent.push(message); } } },
  };
  context.globalThis = context;
  vm.createContext(context);
  vm.runInContext(fs.readFileSync(path.join(directory, "parser.js"), "utf8"), context, { filename: "parser.js" });
  vm.runInContext(fs.readFileSync(path.join(directory, "content.js"), "utf8"), context, { filename: "content.js" });
  const started = Date.now();
  while (sent.length === 0 && Date.now() - started < 7000) {
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  return sent;
}

(async () => {
  const repeated = createChrome("ack");
  loadBackground(repeated.chrome);
  await dispatch(repeated.messageListeners, { type: "claude-api-credit-control", enabled: true });
  await dispatch(repeated.messageListeners, { type: "claude-api-credit-control", enabled: true });
  assert.equal(repeated.created.length, 1);
  assert.equal(repeated.reloads.length, 0);
  assert.equal(repeated.chrome.tabs.update, undefined);

  const disabled = createChrome("ack");
  loadBackground(disabled.chrome);
  await dispatch(disabled.messageListeners, { type: "claude-api-credit-control", enabled: true });
  const queued = disabled.alarmListeners[0];
  await dispatch(disabled.messageListeners, { type: "claude-api-credit-control", enabled: false });
  const createdAfterStop = disabled.created.length;
  queued({ name: "claude-api-credit-refresh" });
  await new Promise(resolve => setTimeout(resolve, 50));
  assert.equal(disabled.created.length, createdAfterStop);
  assert.equal(disabled.alarmNames.has("claude-api-credit-refresh"), false);

  const loading = loadingDocument();
  const appeared = new Promise(resolve => setTimeout(() => {
    loading.tables.push(promotionalTable());
    resolve();
  }, 180));
  const sent = await runContent(loading);
  await appeared;
  assert.equal(sent.length, 1);
  const encoded = JSON.stringify(sent[0]);
  assert.equal(sent[0].message.kind, "observation");
  assert.equal(sent[0].message.remaining, "99.20");
  assert.equal(encoded.includes(ORG), false);
  assert.equal(encoded.includes(DISPLAY), false);
  assert.equal(encoded.includes(AVATAR), false);
  assert.equal(encoded.includes(GLYPH), false);

  const synced = createChrome("ack");
  loadBackground(synced.chrome);
  await dispatch(synced.messageListeners, { type: "claude-api-credit-control", enabled: true });
  const accepted = await dispatch(synced.messageListeners, {
    type: "claude-api-credit-read",
    navigationToken: "navigation-1",
    message: sent[0].message,
  }, { tab: { id: synced.created[0] } });
  assert.equal(accepted.accepted, true);
  assert.equal(synced.nativeMessages.length, 1);
  assert.equal(synced.nativeMessages[0].remaining, "99.20");
  assert.equal(synced.local.lastRemaining, "99.20");
  assert.equal(synced.local.lastState, "ok");

  const missing = createChrome("missing");
  loadBackground(missing.chrome);
  await dispatch(missing.messageListeners, { type: "claude-api-credit-control", enabled: true });
  const rejected = await dispatch(missing.messageListeners, {
    type: "claude-api-credit-read",
    navigationToken: "navigation-2",
    message: sent[0].message,
  }, { tab: { id: missing.created[0] } });
  assert.equal(rejected.accepted, false);
  assert.equal(missing.nativeMessages.length, 0);
  assert.equal(missing.local.lastRemaining, "");
  assert.equal(missing.local.lastGrant, "");
  assert.equal(missing.local.lastState, "unavailable");

  const denied = createChrome("reject");
  loadBackground(denied.chrome);
  await dispatch(denied.messageListeners, { type: "claude-api-credit-control", enabled: true });
  const deniedReply = await dispatch(denied.messageListeners, {
    type: "claude-api-credit-read",
    navigationToken: "navigation-3",
    message: sent[0].message,
  }, { tab: { id: denied.created[0] } });
  assert.equal(deniedReply.accepted, false);
  assert.equal(denied.local.lastRemaining, "");
  assert.equal(denied.local.lastState, "unavailable");

  const timedOut = loadingDocument();
  const timeoutSent = await runContent(timedOut);
  assert.equal(timeoutSent.length, 1);
  assert.equal(timeoutSent[0].message.kind, "status");
  assert.equal(timeoutSent[0].message.reason, "source_lost");
  assert.equal(Object.hasOwn(timeoutSent[0].message, "remaining"), false);
  assert.equal(JSON.stringify(timeoutSent[0]).includes(ORG), false);

  console.log("chrome harness passed");
})().catch(error => {
  console.error(error);
  process.exit(1);
});
