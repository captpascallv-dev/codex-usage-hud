# Claude API 赠送额度候选安装

这是候选包，不是现行 HUD 的安装程序。在 Lead 验收且 Pascal 批准之前，不要运行本目录的安装脚本，不要加载扩展，也不要替换已经安装的 HUD。

## 扩展权限

Manifest V3，扩展 ID `jnfblichipcdliigeooegbehgnennbfa`（由 manifest 里的 `key` 固定）。

- `nativeMessaging`：只连接 `com.codexusagehud.claude_api_credit`
- `storage`：记住开关；弹出页只保存状态、观测时间和金额
- `alarms`：每 5 分钟刷新自己的后台标签
- `scripting`：只向自己打开的账单页注入读取脚本
- 主机权限只有 `https://platform.claude.com/settings/billing`

没有 `cookies`、`tabs`、`history`、`webRequest`。不读密钥、Cookie 或浏览器存储里的登录。不调用付费模型接口。

## 安装脚本会写什么

`install/Install-ClaudeApiCreditHost.ps1` 只做这两件事：

1. 把本包复制到 `%LOCALAPPDATA%\CodexUsageHUD\claude-api-credit-bridge\`（宿主 exe、扩展目录、一份绝对路径的 `native-host.json`）。
2. 写入当前用户注册表 `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit`，默认值是上面这份 json 的绝对路径。`allowed_origins` 只有 `chrome-extension://jnfblichipcdliigeooegbehgnennbfa/`。

不写 HKLM，不启动浏览器，不替换 HUD 主程序，不改 `usage.db`。

## 启用、停止、卸载

1. 浏览器扩展页选择“加载已解压的扩展程序”，选中桥接目录里的 `extension`。ID 必须是上面的值。
2. 扩展弹出页默认是“未开始”。点“开始同步”后，它只新建一个后台账单标签，并在每次真实加载后读促销额度表。
3. 点“停止同步”会关掉这个仍停在账单页的标签，并让 HUD 丢掉金额、保留不可用状态。
4. 卸载：先在扩展页移除该扩展，再运行 `install/Uninstall-ClaudeApiCreditHost.ps1`。它删除上述注册表项、`claude-api-credit-bridge` 目录，以及数据目录里的 `claude-api-credit.json` 和它的 `.tmp`。HUD 程序、数据库和 `quota-snapshot.json` 保留。

## 读数边界

金额来自页面上 `aria-label` 为“促銷額度 / 促销额度 / Promotional credits”的那一张表。购买额度、月度预算和发票不会当成赠送额度。未知表头、多行或多张促销表会拒绝，不猜测。到期日是 UTC 日历日，不会被写成精确重置时间。

组织绑定只接受一个明确的组织切换控件文本，并在离开页面前哈希。已核对的 DOM 没有包含这个控件；选择器是 `[data-testid="organization-switcher"]` 和 `[data-testid="organization-menu-button"]`。读不到或读到多个名称时，不保存活的余额。这一项要等批准后的真实页面核对。

公开快照里的 `claude_api_credit` 是美元余额，不是每周剩余百分比，也不进入一眼最小剩余的计算。它表示组织共享余额，不限 Cursor。

## 仍待批准的检查

本候选没有加载扩展，也没有连接真实账户。Chrome 登录页 → 本机接收程序 → 已安装 HUD 的整条链路还没有跑过。合成界面截图只证明候选窗口能画出这一行。
