# Delivered for a later approved uninstall. Do not run it in this candidate round.
# Removes only the user-level native-host key, the bridge directory, and the one credit cache file.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$regPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit'
if (Test-Path -LiteralPath $regPath) {
    Remove-Item -LiteralPath $regPath -Force
}

$destRoot = Join-Path $env:LOCALAPPDATA 'CodexUsageHUD\claude-api-credit-bridge'
if (Test-Path -LiteralPath $destRoot) {
    Remove-Item -LiteralPath $destRoot -Recurse -Force
}

$dataRoot = Join-Path $env:LOCALAPPDATA 'CodexUsageHUD'
foreach ($name in @('claude-api-credit.json', 'claude-api-credit.json.tmp')) {
    $path = Join-Path $dataRoot $name
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}

Write-Output 'removed the native-host registration, claude-api-credit-bridge, and the one credit cache'
Write-Output 'left the installed HUD program, its database, and quota-snapshot.json in place'
Write-Output 'disable the unpacked extension from the browser extensions page'
