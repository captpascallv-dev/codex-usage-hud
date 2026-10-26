const assert = require("node:assert/strict");
const parser = require("./parser.js");

const ORG = "Example Organization";
const APPLIES = "Agent SDK API 批次 API Playground 託管代理程式";
const SECRET = "COOKIE_SECRET_SHOULD_NOT_EXPORT";

function node(tag, attrs, children, text) {
  const element = {
    tag,
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
      if (this._text !== undefined) return this._text;
      return this.children.map(child => child.textContent).join("");
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

function cell(tag, text) {
  return node(tag, {}, [], text);
}

function row(values, tag) {
  return node("tr", {}, values.map(value => cell(tag, value)));
}

function table(label, headers, rows) {
  const head = node("thead", {}, [row(headers, "th")]);
  const body = node("tbody", {}, rows.map(values => row(values, "td")));
  return node("table", { "aria-label": label }, [head, body]);
}

function page(tables, labels) {
  const orgNodes = (labels || [ORG]).map(label =>
    node("button", { "data-testid": "organization-switcher" }, [], label));
  return {
    querySelectorAll(selector) {
      const found = [];
      for (const element of [...tables, ...orgNodes]) collect(element, selector, found);
      return found;
    },
  };
}

function promotional(headers, amount, applies, expires, granted) {
  return table("促銷額度", headers, [[amount, applies, expires, granted]]);
}

const zhTw = ["剩餘餘額", "適用範圍", "到期日", "授予日期"];
const observed = "2026-10-26T08:00:00.000Z";

async function bind(read) {
  if (read.organizationLabel) {
    read.organizationBinding = await parser.organizationBinding(read.organizationLabel);
    delete read.organizationLabel;
  }
  return read;
}

(async () => {
  const fixture = await bind(parser.readPromotionalCredit(
    page([
      promotional(zhTw, "US$99.20 / US$100.00", APPLIES, "2026/11/12", "2026/10/15"),
      table("購買額度", ["餘額"], [["US$0.00"]]),
      table("支出上限", ["月份", "上限"], [["2026/10", "US$500.00"]]),
      table("發票", ["金額"], [["US$12.00"]]),
    ]),
    parser.SOURCE_URL));
  assert.equal(fixture.ok, true);
  assert.equal(fixture.remaining, "99.20");
  assert.equal(fixture.grantAmount, "100.00");
  assert.equal(fixture.grantedOnUtc, "2026-10-15");
  assert.equal(fixture.expiresOnUtc, "2026-11-12");
  assert.equal(fixture.currency, "USD");
  const message = parser.toNativeMessage(fixture, observed);
  assert.equal(message.kind, "observation");
  assert.equal(message.remaining, "99.20");
  assert.equal(message.grant_amount, "100.00");
  assert.equal(message.granted_on_utc, "2026-10-15");
  assert.equal(message.expires_on_utc, "2026-11-12");
  assert.equal(message.organization_binding.length, 64);
  const encoded = JSON.stringify(message);
  assert.equal(encoded.includes(ORG), false);
  assert.equal(encoded.includes(APPLIES), false);
  assert.equal(encoded.includes(SECRET), false);
  assert.equal(Object.hasOwn(message, "organizationLabel"), false);
  assert.equal(Object.hasOwn(message, "html"), false);

  const english = await bind(parser.readPromotionalCredit(page([
    table("Promotional credits", ["Remaining balance", "Applies to", "Expires", "Granted"],
      [["USD 99.20 / USD 100.00", "API", "2026-11-12", "2026-10-15"]]),
  ]), parser.SOURCE_URL));
  assert.equal(english.remaining, "99.20");
  assert.equal(english.grantAmount, "100.00");

  const simplified = await bind(parser.readPromotionalCredit(page([
    table("促销额度", ["剩余余额", "适用范围", "到期日", "授予日期"],
      [["$0.00 / $80.00", "API", "2026/12/01", "2026/10/01"]]),
  ]), parser.SOURCE_URL));
  assert.equal(simplified.remaining, "0.00");
  assert.equal(simplified.grantAmount, "80.00");

  const changed = await bind(parser.readPromotionalCredit(page([
    promotional(zhTw, "US$12.50 / US$250.00", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL));
  assert.equal(changed.remaining, "12.50");
  assert.equal(changed.grantAmount, "250.00");

  const zeroGrant = await bind(parser.readPromotionalCredit(page([
    promotional(zhTw, "US$0.00 / US$0.00", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL));
  assert.equal(zeroGrant.ok, true);
  assert.equal(zeroGrant.remaining, "0.00");

  const decimals = await bind(parser.readPromotionalCredit(page([
    promotional(zhTw, "US$1.5 / US$2.5", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL));
  assert.equal(decimals.remaining, "1.50");
  assert.equal(decimals.grantAmount, "2.50");

  const ambiguousRows = await bind(parser.readPromotionalCredit(page([
    table("促銷額度", zhTw, [
      ["US$99.20 / US$100.00", APPLIES, "2026/11/12", "2026/10/15"],
      ["US$1.00 / US$100.00", APPLIES, "2026/11/12", "2026/10/15"],
    ]),
  ]), parser.SOURCE_URL));
  assert.equal(ambiguousRows.ok, false);
  assert.equal(ambiguousRows.reason, "ambiguous");
  const ambiguousMessage = parser.toNativeMessage(ambiguousRows, observed);
  assert.equal(ambiguousMessage.kind, "status");
  assert.equal(ambiguousMessage.reason, "malformed");
  assert.equal(Object.hasOwn(ambiguousMessage, "remaining"), false);
  assert.equal(JSON.stringify(ambiguousMessage).includes("99.20"), false);

  const twoTables = parser.readPromotionalCredit(page([
    promotional(zhTw, "US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15"),
    promotional(zhTw, "US$1.00 / US$5.00", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL);
  assert.equal(twoTables.ok, false);
  assert.equal(twoTables.reason, "ambiguous");

  const purchasedOnly = await bind(parser.readPromotionalCredit(page([
    table("購買額度", ["餘額"], [["US$0.00"]]),
    table("Purchased credits", ["Balance"], [["US$20.00"]]),
    table("Invoice", ["Amount"], [["US$8.00"]]),
  ]), parser.SOURCE_URL));
  assert.equal(purchasedOnly.ok, false);
  assert.equal(purchasedOnly.reason, "missing");
  const purchasedMessage = parser.toNativeMessage(purchasedOnly, observed);
  assert.equal(purchasedMessage.kind, "status");
  assert.equal(purchasedMessage.reason, "source_lost");
  assert.equal(Object.hasOwn(purchasedMessage, "remaining"), false);
  assert.equal(Object.hasOwn(purchasedMessage, "grant_amount"), false);
  assert.equal(JSON.stringify(purchasedMessage).includes("20.00"), false);

  const unknown = parser.readPromotionalCredit(page([
    promotional(["余额", "范围", "到期", "授予"], "US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL);
  assert.equal(unknown.ok, false);
  assert.equal(unknown.reason, "unknown_headers");

  const timed = parser.readPromotionalCredit(page([
    promotional(zhTw, "US$99.20 / US$100.00", "API", "2026/11/12 00:00", "2026/10/15"),
  ]), parser.SOURCE_URL);
  assert.equal(timed.ok, false);
  assert.equal(timed.reason, "malformed_row");

  const over = parser.readPromotionalCredit(page([
    promotional(zhTw, "US$100.01 / US$100.00", "API", "2026/11/12", "2026/10/15"),
  ]), parser.SOURCE_URL);
  assert.equal(over.ok, false);

  const wrongPage = parser.readPromotionalCredit(page([
    promotional(zhTw, "US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15"),
  ]), "https://platform.claude.com/settings/billing?account=1");
  assert.equal(wrongPage.reason, "wrong_page");
  assert.equal(parser.toNativeMessage(wrongPage, observed).reason, "binding_missing");

  const missingBinding = parser.readPromotionalCredit({
    querySelectorAll(selector) {
      return selector === "table"
        ? [promotional(zhTw, "US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15")]
        : [];
    },
  }, parser.SOURCE_URL);
  assert.equal(missingBinding.reason, "binding_missing");
  const missingMessage = parser.toNativeMessage(missingBinding, observed);
  assert.equal(missingMessage.reason, "binding_missing");
  assert.equal(Object.hasOwn(missingMessage, "remaining"), false);
  assert.equal(Object.hasOwn(missingMessage, "organization_binding"), false);

  const ambiguousBinding = parser.readPromotionalCredit(
    page([promotional(zhTw, "US$99.20 / US$100.00", "API", "2026/11/12", "2026/10/15")], [ORG, ORG + " two"]),
    parser.SOURCE_URL);
  assert.equal(ambiguousBinding.reason, "binding_ambiguous");
  assert.equal(parser.toNativeMessage(ambiguousBinding, observed).reason, "binding_missing");

  const dirty = parser.sanitizeNativeMessage({
    schema: 1,
    kind: "observation",
    source: parser.SOURCE_URL,
    observed_at_utc: observed,
    organization_binding: "ab".repeat(32),
    state: "ok",
    currency: "USD",
    remaining: "99.20",
    grant_amount: "100.00",
    granted_on_utc: "2026-10-15",
    expires_on_utc: "2026-11-12",
    html: "<table>" + SECRET + "</table>",
    organizationLabel: ORG,
    cookie: SECRET,
  });
  assert.equal(JSON.stringify(dirty).includes(SECRET), false);
  assert.equal(JSON.stringify(dirty).includes(ORG), false);
  assert.equal(Object.hasOwn(dirty, "html"), false);

  const lost = parser.toNativeMessage({ ok: false, reason: "source_lost", organizationBinding: "ab".repeat(32) }, observed);
  assert.equal(lost.reason, "source_lost");
  assert.equal(lost.organization_binding, "ab".repeat(32));
  const stopped = parser.toNativeMessage({ ok: false, reason: "stopped" }, observed);
  assert.equal(stopped.reason, "stopped");
  assert.equal(Object.hasOwn(stopped, "organization_binding"), false);

  console.log("parser tests passed");
})().catch(error => {
  console.error(error);
  process.exit(1);
});
