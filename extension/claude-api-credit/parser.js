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
  const ORGANIZATION_SELECTORS = [
    '[data-testid="organization-switcher"]',
    '[data-testid="organization-menu-button"]',
  ];
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

  function headerCells(table) {
    const head = table.querySelector("thead");
    const row = head ? head.querySelector("tr") : table.querySelector("tr");
    if (!row) return [];
    return Array.from(row.querySelectorAll("th")).map(cell => normalizeSpace(cell.textContent));
  }

  function bodyRows(table) {
    const body = table.querySelector("tbody") || table;
    return Array.from(body.querySelectorAll("tr")).filter(row => row.querySelectorAll("td").length > 0);
  }

  function organizationLabel(document) {
    const texts = [];
    for (const selector of ORGANIZATION_SELECTORS) {
      for (const node of Array.from(document.querySelectorAll(selector))) {
        const text = normalizeSpace(node.textContent).normalize("NFC");
        if (text && text.length <= 200) texts.push(text);
      }
    }
    const unique = Array.from(new Set(texts));
    if (unique.length === 1) return { label: unique[0] };
    if (unique.length === 0) return { reason: "binding_missing" };
    return { reason: "binding_ambiguous" };
  }

  function withBinding(result, binding) {
    if (binding && binding.label) result.organizationLabel = binding.label;
    return result;
  }

  function readPromotionalCredit(document, href) {
    if (!pageAllowed(href)) return { ok: false, reason: "wrong_page" };
    const tables = Array.from(document.querySelectorAll("table")).filter(table =>
      TABLE_LABELS.includes(normalizeSpace(table.getAttribute("aria-label"))));
    const binding = organizationLabel(document);
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
    if (!binding.label) return { ok: false, reason: binding.reason || "binding_missing" };
    return {
      ok: true,
      currency: "USD",
      remaining: amount.remaining,
      grantAmount: amount.grantAmount,
      grantedOnUtc,
      expiresOnUtc,
      organizationLabel: binding.label,
    };
  }

  async function organizationBinding(label) {
    const normalized = normalizeSpace(label).normalize("NFC");
    const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(normalized));
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
    ORGANIZATION_SELECTORS,
    readPromotionalCredit,
    organizationBinding,
    toNativeMessage,
    sanitizeNativeMessage,
  };
});
