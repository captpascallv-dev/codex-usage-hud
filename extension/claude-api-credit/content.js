(async function () {
  const parser = globalThis.ClaudeApiCreditParser;
  if (!parser) return;
  let url;
  try { url = new URL(location.href); }
  catch { return; }
  if (url.origin !== "https://platform.claude.com" || url.pathname !== "/settings/billing" ||
      url.search !== "" || url.hash !== "") {
    return;
  }

  const read = parser.readPromotionalCredit(document, location.href);
  if (read.organizationLabel) {
    read.organizationBinding = await parser.organizationBinding(read.organizationLabel);
    delete read.organizationLabel;
  }
  const message = parser.toNativeMessage(read, new Date().toISOString());
  await chrome.runtime.sendMessage({
    type: "claude-api-credit-read",
    navigationToken: String(performance.timeOrigin),
    message,
  });
})();
