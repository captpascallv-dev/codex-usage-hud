# Delivered for a later approved install. Do not run it in this candidate round.
# Writes only HKCU\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit
# and copies this bundle into %LOCALAPPDATA%\CodexUsageHUD\claude-api-credit-bridge.
[CmdletBinding()]
param(
    [string]$BundleRoot
)

$ErrorActionPreference = 'Stop'
function Find-BundleRoot([string]$Start) {
    $dir = [System.IO.Path]::GetFullPath($Start)
    while ($dir) {
        $exe = Join-Path $dir 'ClaudeApiCreditHost.exe'
        $manifest = Join-Path $dir 'extension\manifest.json'
        if ((Test-Path -LiteralPath $exe -PathType Leaf) -and (Test-Path -LiteralPath $manifest -PathType Leaf)) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { return $null }
        $dir = $parent
    }
    return $null
}

if (-not $BundleRoot) { $BundleRoot = Find-BundleRoot $PSScriptRoot }
if (-not $BundleRoot) { throw 'bundle_root_not_found' }
$BundleRoot = [System.IO.Path]::GetFullPath($BundleRoot)
$hostExe = Join-Path $BundleRoot 'ClaudeApiCreditHost.exe'
$extensionManifest = Join-Path $BundleRoot 'extension\manifest.json'
if (-not (Test-Path -LiteralPath $hostExe -PathType Leaf)) { throw "host_exe_missing:$hostExe" }
if (-not (Test-Path -LiteralPath $extensionManifest -PathType Leaf)) { throw "extension_manifest_missing:$extensionManifest" }

$destRoot = Join-Path $env:LOCALAPPDATA 'CodexUsageHUD\claude-api-credit-bridge'
$destExtension = Join-Path $destRoot 'extension'
New-Item -ItemType Directory -Force -Path $destExtension | Out-Null
Copy-Item -LiteralPath $hostExe -Destination (Join-Path $destRoot 'ClaudeApiCreditHost.exe') -Force
Copy-Item -LiteralPath (Join-Path $BundleRoot 'extension\*') -Destination $destExtension -Recurse -Force

$destExe = Join-Path $destRoot 'ClaudeApiCreditHost.exe'
$destJson = Join-Path $destRoot 'native-host.json'
$manifest = [ordered]@{
    name = 'com.codexusagehud.claude_api_credit'
    description = 'Codex Usage HUD Claude API credit receiver'
    path = $destExe
    type = 'stdio'
    allowed_origins = @('chrome-extension://jnfblichipcdliigeooegbehgnennbfa/')
}
$json = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($destJson, $json + "`n")

$regPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit'
New-Item -Path $regPath -Force | Out-Null
New-ItemProperty -Path $regPath -Name '(default)' -Value $destJson -PropertyType String -Force | Out-Null
Write-Output "registered=$regPath"
Write-Output "manifest=$destJson"
Write-Output "host=$destExe"
Write-Output "extension=$destExtension"
Write-Output 'next=load the extension directory as an unpacked extension, then use its popup. This script does not start a browser and does not replace the installed HUD.'
