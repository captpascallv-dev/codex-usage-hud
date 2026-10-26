const stateText = {
  ok: "正常",
  stale: "更新延迟",
  unavailable: "不可用",
};

async function refresh() {
  const stored = await chrome.storage.local.get(["enabled", "lastState", "lastObservedAt", "lastRemaining", "lastGrant"]);
  const enabled = stored.enabled === true;
  const toggle = document.getElementById("toggle");
  toggle.textContent = enabled ? "停止同步" : "开始同步";
  toggle.classList.toggle("stop", enabled);
  const state = document.getElementById("state");
  state.textContent = enabled ? (stateText[stored.lastState] || "已开启") : "未开始";
  const amount = document.getElementById("amount");
  amount.textContent = stored.lastRemaining && stored.lastGrant
    ? "US$" + stored.lastRemaining + " / US$" + stored.lastGrant
    : "没有读数";
  const observed = document.getElementById("observed");
  observed.textContent = stored.lastObservedAt ? "上次观测 " + stored.lastObservedAt : "";
}

document.getElementById("toggle").addEventListener("click", async () => {
  const stored = await chrome.storage.local.get("enabled");
  const enabled = stored.enabled !== true;
  await chrome.runtime.sendMessage({ type: "claude-api-credit-control", enabled });
  await refresh();
});

refresh().catch(() => {
  document.getElementById("state").textContent = "不可用";
});
