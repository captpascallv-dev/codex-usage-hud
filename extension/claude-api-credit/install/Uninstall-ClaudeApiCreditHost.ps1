# Delivered for a later approved uninstall. Do not run it in this candidate round.
# Removes only the user-level native-host key. Files are recycled from exact validated paths.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ClaudeApiCreditUninstallPlan.ps1')

$folder = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($folder)) { throw 'local_app_data_missing' }
$resolved = [System.IO.Path]::GetFullPath($folder)
$envResolved = [System.IO.Path]::GetFullPath($env:LOCALAPPDATA)
if (-not [string]::Equals($resolved, $envResolved, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'local_app_data_mismatch'
}

$plan = @(Get-ClaudeApiCreditUninstallPlan -LocalApplicationData $resolved)
foreach ($target in $plan) {
    Write-Output ("literal=" + $target.LiteralPath)
}

$regPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.codexusagehud.claude_api_credit'
if (Test-Path -LiteralPath $regPath) {
    Remove-Item -LiteralPath $regPath -Force
}

$python = 'C:\Users\Pascal\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$guard = 'C:\Users\Pascal\Tools\AgentFileSafety\file_guard.py'
if ($plan.Count -gt 0) {
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { throw 'recycler_missing' }
    if (-not (Test-Path -LiteralPath $guard -PathType Leaf)) { throw 'recycler_missing' }
}
foreach ($target in $plan) {
    & $python $guard recycle --path $target.LiteralPath
    if ($LASTEXITCODE -ne 0) { throw "recycle_failed:$($target.LiteralPath)" }
}

Write-Output 'removed the native-host registration and recycled the validated bridge and credit cache paths'
Write-Output 'left the installed HUD program, its database, and quota-snapshot.json in place'
Write-Output 'disable the unpacked extension from the browser extensions page'
