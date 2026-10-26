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

  const deadline = performance.now() + parser.READY_TIMEOUT_MS;
  while (!parser.surfaceReady(document) && performance.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, parser.READY_INTERVAL_MS));
  }

  const read = parser.readPromotionalCredit(document, location.href);
  if (Array.isArray(read.organizationLabels)) {
    read.organizationBinding = await parser.organizationBinding(read.organizationLabels);
    delete read.organizationLabels;
  }
  const message = parser.toNativeMessage(read, new Date().toISOString());
  await chrome.runtime.sendMessage({
    type: "claude-api-credit-read",
    navigationToken: String(performance.timeOrigin),
    message,
  });
})();
