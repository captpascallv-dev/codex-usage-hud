(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  root.ClaudeApiCreditParser = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  const SOURCE_URL = "https://platform.claude.com/settings/billing";
  const TABLE_LABELS = ["促銷額度", "促销额度", "Promotional credits"];
  const HEADER_SETS = [
    ["剩餘餘額", "適用範圍", "到期日", "授予日期"],
    ["剩余余额", "适用范围", "到期日", "授予日期"],
    ["Remaining balance", "Applies to", "Expires", "Granted"],
  ];
  const NAV_LABEL = "主要導覽";
  const READY_TIMEOUT_MS = 5000;
  const READY_INTERVAL_MS = 100;
  const OBSERVATION_FIELDS = [
    "schema", "kind", "source", "observed_at_utc", "organization_binding", "state",
    "currency", "remaining", "grant_amount", "granted_on_utc", "expires_on_utc",
  ];
  const STATUS_FIELDS = [
    "schema", "kind", "source", "observed_at_utc", "organization_binding", "state", "reason",
  ];

  function normalizeSpace(value) {
    return String(value || "").replace(/\s+/g, " ").trim();
  }

  function pageAllowed(href) {
    try {
      const url = new URL(href);
      return url.origin === "https://platform.claude.com" &&
        url.pathname === "/settings/billing" &&
        url.search === "" &&
        url.hash === "";
    } catch {
      return false;
    }
  }

  function headersMatch(cells) {
    return HEADER_SETS.some(expected =>
      expected.length === cells.length && expected.every((header, index) => header === cells[index]));
  }

  function canonMoney(text) {
    const parts = text.split(".");
    if (parts.length > 2) return null;
    const whole = parts[0];
    const frac = parts[1] || "";
    if (!/^\d{1,7}$/.test(whole) || !/^\d{0,2}$/.test(frac)) return null;
    return whole + "." + (frac + "00").slice(0, 2);
  }

  function parseAmount(text) {
    const match = /^(?:US\$|\$|USD\s*)(\d{1,7}(?:\.\d{1,2})?)\s*\/\s*(?:US\$|\$|USD\s*)(\d{1,7}(?:\.\d{1,2})?)$/.exec(normalizeSpace(text));
    if (!match) return null;
    const remaining = canonMoney(match[1]);
    const grantAmount = canonMoney(match[2]);
    if (!remaining || !grantAmount) return null;
    if (Number(remaining) > Number(grantAmount)) return null;
    return { remaining, grantAmount };
  }

  function parseDate(text) {
    const normalized = normalizeSpace(text);
    if (normalized.includes(":")) return null;
    const match = /^(\d{4})[/-](\d{2})[/-](\d{2})$/.exec(normalized);
    if (!match) return null;
    const year = Number(match[1]);
    const month = Number(match[2]);
    const day = Number(match[3]);
    const utc = new Date(Date.UTC(year, month - 1, day));
    if (utc.getUTCFullYear() !== year || utc.getUTCMonth() !== month - 1 || utc.getUTCDate() !== day) return null;
    return match[1] + "-" + match[2] + "-" + match[3];
  }

  function isAriaHidden(node) {
    return Boolean(node && typeof node.getAttribute === "function" && node.getAttribute("aria-hidden") === "true");
  }

  function visibleText(node) {
    if (!node || isAriaHidden(node)) return "";
    const nodes = node.childNodes && node.childNodes.length ? Array.from(node.childNodes) : null;
    if (nodes && nodes.length) return nodes.map(visibleText).join("");
    if (node.children && node.children.length) return Array.from(node.children).map(visibleText).join("");
    return node.textContent || "";
  }

  function headerCells(table) {
    const head = table.querySelector("thead");
    const row = head ? head.querySelector("tr") : table.querySelector("tr");
    if (!row) return [];
    return Array.from(row.querySelectorAll("th")).map(cell => normalizeSpace(visibleText(cell)));
  }

  function bodyRows(table) {
    const body = table.querySelector("tbody") || table;
    return Array.from(body.querySelectorAll("tr")).filter(row => row.querySelectorAll("td").length > 0);
  }

  function tagName(node) {
    return String((node && (node.tagName || node.tag)) || "").toUpperCase();
  }

  function organizationLabels(document) {
    const navs = Array.from(document.querySelectorAll('[aria-label="' + NAV_LABEL + '"]'));
    if (navs.length !== 1) return { reason: navs.length === 0 ? "binding_missing" : "binding_ambiguous" };
    const buttons = Array.from(navs[0].querySelectorAll("button")).filter(button =>
      button.getAttribute("aria-haspopup") === "menu");
    if (buttons.length !== 1) return { reason: buttons.length === 0 ? "binding_missing" : "binding_ambiguous" };
    const children = Array.from(buttons[0].children || []);
    if (children.length !== 3 || tagName(children[1]) !== "DIV") return { reason: "binding_missing" };
    const spans = Array.from(children[1].children || []);
    if (spans.length !== 2 || spans.some(span => tagName(span) !== "SPAN")) return { reason: "binding_missing" };
    const displayName = normalizeSpace(visibleText(spans[0])).normalize("NFC");
    const organizationName = normalizeSpace(visibleText(spans[1])).normalize("NFC");
    if (!displayName || !organizationName || displayName.length > 200 || organizationName.length > 200) {
      return { reason: "binding_missing" };
    }
    return { labels: [displayName, organizationName] };
  }

  function withBinding(result, identity) {
    if (identity && identity.labels) result.organizationLabels = identity.labels.slice();
    return result;
  }

  function promotionalTables(document) {
    return Array.from(document.querySelectorAll("table")).filter(table =>
      TABLE_LABELS.includes(normalizeSpace(table.getAttribute("aria-label"))));
  }

  function surfaceReady(document) {
    if (!organizationLabels(document).labels) return false;
    const tables = promotionalTables(document);
    if (tables.length !== 1 || !headersMatch(headerCells(tables[0]))) return false;
    const rows = bodyRows(tables[0]);
    return rows.length === 1 && Array.from(rows[0].querySelectorAll("td")).length === 4;
  }

  function readPromotionalCredit(document, href) {
    if (!pageAllowed(href)) return { ok: false, reason: "wrong_page" };
    const tables = promotionalTables(document);
    const binding = organizationLabels(document);
    if (tables.length === 0) {
      return withBinding({ ok: false, reason: binding.reason || "missing" }, binding);
    }
    if (tables.length !== 1) return withBinding({ ok: false, reason: "ambiguous" }, binding);
    const headers = headerCells(tables[0]);
    if (!headersMatch(headers)) return withBinding({ ok: false, reason: "unknown_headers" }, binding);
    const rows = bodyRows(tables[0]);
    if (rows.length !== 1) return withBinding({ ok: false, reason: rows.length === 0 ? "missing" : "ambiguous" }, binding);
    const cells = Array.from(rows[0].querySelectorAll("td"));
    if (cells.length !== 4) return withBinding({ ok: false, reason: "malformed_row" }, binding);
    const amount = parseAmount(cells[0].textContent);
    const expiresOnUtc = parseDate(cells[2].textContent);
    const grantedOnUtc = parseDate(cells[3].textContent);
    if (!amount || !expiresOnUtc || !grantedOnUtc || grantedOnUtc > expiresOnUtc) {
      return withBinding({ ok: false, reason: "malformed_row" }, binding);
    }
    if (!binding.labels) return { ok: false, reason: binding.reason || "binding_missing" };
    return {
      ok: true,
      currency: "USD",
      remaining: amount.remaining,
      grantAmount: amount.grantAmount,
      grantedOnUtc,
      expiresOnUtc,
      organizationLabels: binding.labels.slice(),
    };
  }

  async function organizationBinding(labels) {
    const parts = (Array.isArray(labels) ? labels : [labels])
      .map(part => normalizeSpace(part).normalize("NFC"));
    const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(parts.join("\n")));
    return Array.from(new Uint8Array(digest)).map(byte => byte.toString(16).padStart(2, "0")).join("");
  }

  const DIRECT_REASONS = {
    account_changed: "account_changed",
    login_changed: "login_changed",
    source_lost: "source_lost",
    malformed: "malformed",
    expired: "expired",
    stopped: "stopped",
    binding_missing: "binding_missing",
    binding_ambiguous: "binding_missing",
    wrong_page: "binding_missing",
    missing: "source_lost",
    ambiguous: "malformed",
    unknown_headers: "malformed",
    malformed_row: "malformed",
  };

  function toNativeMessage(read, observedAtUtc) {
    const binding = read && typeof read.organizationBinding === "string" ? read.organizationBinding : "";
    if (read && read.ok === true && binding && read.remaining && read.grantAmount && read.grantedOnUtc && read.expiresOnUtc) {
      return sanitizeNativeMessage({
        schema: 1,
        kind: "observation",
        source: SOURCE_URL,
        observed_at_utc: observedAtUtc,
        organization_binding: binding,
        state: "ok",
        currency: "USD",
        remaining: read.remaining,
        grant_amount: read.grantAmount,
        granted_on_utc: read.grantedOnUtc,
        expires_on_utc: read.expiresOnUtc,
      });
    }

    const raw = read && typeof read.reason === "string" ? read.reason : "";
    let reason = Object.prototype.hasOwnProperty.call(DIRECT_REASONS, raw) ? DIRECT_REASONS[raw] : "malformed";
    if (!binding && (raw === "missing" || raw === "ambiguous" || raw === "unknown_headers" || raw === "malformed_row" || raw === "malformed")) {
      reason = "binding_missing";
    }
    const status = {
      schema: 1,
      kind: "status",
      source: SOURCE_URL,
      observed_at_utc: observedAtUtc,
      state: "unavailable",
      reason,
    };
    if (binding && reason !== "binding_missing" && reason !== "login_changed") {
      status.organization_binding = binding;
    }
    return sanitizeNativeMessage(status);
  }

  function sanitizeNativeMessage(message) {
    const fields = message && message.kind === "observation" ? OBSERVATION_FIELDS : STATUS_FIELDS;
    const copy = {};
    for (const field of fields) {
      if (message && Object.prototype.hasOwnProperty.call(message, field)) copy[field] = message[field];
    }
    return copy;
  }

  return {
    SOURCE_URL,
    HEADER_SETS,
    TABLE_LABELS,
    NAV_LABEL,
    READY_TIMEOUT_MS,
    READY_INTERVAL_MS,
    readPromotionalCredit,
    surfaceReady,
    organizationBinding,
    toNativeMessage,
    sanitizeNativeMessage,
  };
});
