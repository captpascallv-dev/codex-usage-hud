# Delivered for a later approved install. Do not run it against the real profile in this candidate round.
# Writes only HKCU\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit
# and copies this bundle into %LOCALAPPDATA%\CodexUsageHUD\claude-api-credit-bridge.
# Pass -DestinationRoot to copy into an isolated directory and skip the registry.
[CmdletBinding()]
param(
    [string]$BundleRoot,
    [string]$DestinationRoot
)

$ErrorActionPreference = 'Stop'
$ExtensionFiles = @(
    'manifest.json',
    'background.js',
    'content.js',
    'parser.js',
    'popup.html',
    'popup.js'
)

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
$extensionSource = Join-Path $BundleRoot 'extension'
if (-not (Test-Path -LiteralPath $hostExe -PathType Leaf)) { throw "host_exe_missing:$hostExe" }

$register = [string]::IsNullOrWhiteSpace($DestinationRoot)
if ($register) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'local_app_data_missing' }
    $DestinationRoot = Join-Path ([System.IO.Path]::GetFullPath($env:LOCALAPPDATA)) 'CodexUsageHUD\claude-api-credit-bridge'
}
$destRoot = [System.IO.Path]::GetFullPath($DestinationRoot)
$destExtension = Join-Path $destRoot 'extension'
New-Item -ItemType Directory -Force -Path $destExtension | Out-Null

$destExe = Join-Path $destRoot 'ClaudeApiCreditHost.exe'
Copy-Item -LiteralPath $hostExe -Destination $destExe -Force
foreach ($name in $ExtensionFiles) {
    $source = Join-Path $extensionSource $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "extension_file_missing:$source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $destExtension $name) -Force
}

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

if ($register) {
    $regPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit'
    New-Item -Path $regPath -Force | Out-Null
    New-ItemProperty -Path $regPath -Name '(default)' -Value $destJson -PropertyType String -Force | Out-Null
    Write-Output "registered=$regPath"
}

Write-Output "manifest=$destJson"
Write-Output "host=$destExe"
Write-Output "extension=$destExtension"
Write-Output 'next=load the extension directory as an unpacked extension, then use its popup. This script does not start a browser and does not replace the installed HUD.'
