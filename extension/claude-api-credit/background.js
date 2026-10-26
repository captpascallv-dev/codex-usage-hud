importScripts("parser.js");

const BILLING_URL = "https://platform.claude.com/settings/billing";
const HOST_NAME = "com.codexusagehud.claude_api_credit";
const ALARM = "claude-api-credit-refresh";
const PERIOD_MINUTES = 5;
const parser = globalThis.ClaudeApiCreditParser;

let enabled = false;
let ownedTabId = null;
let lastNavigationToken = "";

async function loadState() {
  const local = await chrome.storage.local.get("enabled");
  const session = await chrome.storage.session.get("ownedTabId");
  enabled = local.enabled === true;
  ownedTabId = Number.isInteger(session.ownedTabId) ? session.ownedTabId : null;
}

async function saveOwnedTab(tabId) {
  ownedTabId = tabId;
  if (Number.isInteger(tabId)) await chrome.storage.session.set({ ownedTabId: tabId });
  else await chrome.storage.session.remove("ownedTabId");
}

function sendNative(message) {
  const payload = parser.sanitizeNativeMessage(message);
  if (payload.source !== BILLING_URL || (payload.kind !== "observation" && payload.kind !== "status")) return;
  const port = chrome.runtime.connectNative(HOST_NAME);
  port.onDisconnect.addListener(() => {});
  port.postMessage(payload);
}

async function sessionBinding() {
  const session = await chrome.storage.session.get("organizationBinding");
  return typeof session.organizationBinding === "string" ? session.organizationBinding : "";
}

async function rememberSession(message) {
  if (!message) return;
  if (message.kind === "observation" && typeof message.organization_binding === "string") {
    await chrome.storage.session.set({ organizationBinding: message.organization_binding });
    return;
  }
  if (message.reason === "login_changed" || message.reason === "binding_missing" || message.reason === "account_changed") {
    await chrome.storage.session.remove("organizationBinding");
  }
}

async function rememberPopup(message) {
  const summary = {
    lastState: message.state || "unavailable",
    lastObservedAt: message.observed_at_utc || "",
    lastRemaining: message.kind === "observation" ? message.remaining || "" : "",
    lastGrant: message.kind === "observation" ? message.grant_amount || "" : "",
  };
  await chrome.storage.local.set(summary);
}

async function sendStatus(reason) {
  const binding = await sessionBinding();
  const read = { ok: false, reason };
  if (binding && (reason === "source_lost" || reason === "malformed" || reason === "stopped" || reason === "expired")) {
    read.organizationBinding = binding;
  }
  const message = parser.toNativeMessage(read, new Date().toISOString());
  sendNative(message);
  await rememberPopup(message);
  await rememberSession(message);
}

async function openOwnedTab() {
  const created = await chrome.tabs.create({ url: BILLING_URL, active: false });
  await saveOwnedTab(created.id);
}

async function reloadOwnedTab() {
  await loadState();
  if (!enabled || !Number.isInteger(ownedTabId)) {
    await openOwnedTab();
    return;
  }
  let tab;
  try { tab = await chrome.tabs.get(ownedTabId); }
  catch { tab = null; }
  if (!tab || tab.url !== BILLING_URL) {
    await sendStatus("login_changed");
    await saveOwnedTab(null);
    await openOwnedTab();
    return;
  }
  await chrome.tabs.reload(ownedTabId);
}

async function readOwnedTab(tabId) {
  await loadState();
  if (!enabled || tabId !== ownedTabId) return;
  let tab;
  try { tab = await chrome.tabs.get(tabId); }
  catch { tab = null; }
  if (!tab || tab.url !== BILLING_URL) {
    await sendStatus("login_changed");
    return;
  }
  try {
    await chrome.scripting.executeScript({
      target: { tabId },
      files: ["parser.js", "content.js"],
    });
  } catch {
    await sendStatus("source_lost");
  }
}

async function closeOwnedTab() {
  await loadState();
  if (!Number.isInteger(ownedTabId)) return;
  let tab = null;
  try { tab = await chrome.tabs.get(ownedTabId); }
  catch { tab = null; }
  if (tab && tab.url === BILLING_URL) {
    try { await chrome.tabs.remove(ownedTabId); }
    catch { /* The reader tab is already gone. */ }
  }
  await saveOwnedTab(null);
}

async function startReader() {
  enabled = true;
  await chrome.storage.local.set({ enabled: true });
  await chrome.alarms.create(ALARM, { periodInMinutes: PERIOD_MINUTES });
  await openOwnedTab();
}

async function stopReader() {
  enabled = false;
  await chrome.storage.local.set({ enabled: false });
  await chrome.alarms.clear(ALARM);
  await sendStatus("stopped");
  await closeOwnedTab();
}

chrome.runtime.onInstalled.addListener(async () => {
  const stored = await chrome.storage.local.get("enabled");
  if (typeof stored.enabled !== "boolean") await chrome.storage.local.set({ enabled: false });
});

chrome.runtime.onStartup.addListener(async () => {
  await loadState();
  if (!enabled) return;
  await chrome.alarms.create(ALARM, { periodInMinutes: PERIOD_MINUTES });
  await openOwnedTab();
});

chrome.alarms.onAlarm.addListener(alarm => {
  if (!alarm || alarm.name !== ALARM) return;
  reloadOwnedTab().catch(() => {});
});

chrome.tabs.onUpdated.addListener((tabId, info) => {
  if (info.status !== "complete") return;
  readOwnedTab(tabId).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || message.type !== "claude-api-credit-read" || !sender.tab) return;
  loadState().then(async () => {
    if (!enabled || sender.tab.id !== ownedTabId) return;
    if (!message.navigationToken || message.navigationToken === lastNavigationToken) return;
    lastNavigationToken = message.navigationToken;
    const nativeMessage = parser.sanitizeNativeMessage(message.message || {});
    if (nativeMessage.source !== BILLING_URL) return;
    sendNative(nativeMessage);
    await rememberPopup(nativeMessage);
    await rememberSession(nativeMessage);
    sendResponse({ accepted: true });
  }).catch(() => {});
  return true;
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (!message || message.type !== "claude-api-credit-control") return;
  const action = message.enabled === true ? startReader() : stopReader();
  action.then(() => sendResponse({ enabled: message.enabled === true })).catch(() => sendResponse({ enabled: false }));
  return true;
});
