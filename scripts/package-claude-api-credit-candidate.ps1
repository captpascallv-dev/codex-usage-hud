# Builds the candidate HUD exe, console native host, and extension bundle.
# Does not install the extension, write the registry, or replace an installed HUD.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli-home'
$dotnet = & (Join-Path $PSScriptRoot 'resolve-dotnet.ps1') -ProjectRoot $root
$sdkRoot = Split-Path -Parent (Split-Path -Parent $dotnet)
$nuget = Join-Path $sdkRoot 'nuget'
if (Test-Path -LiteralPath $nuget -PathType Container) { $env:NUGET_PACKAGES = $nuget }
$bundle = Join-Path $root 'dist\claude-api-credit-candidate'
$hudOut = Join-Path $root 'dist\claude-api-credit-publish\hud'
$hostOut = Join-Path $root 'dist\claude-api-credit-publish\host'
New-Item -ItemType Directory -Force -Path $bundle | Out-Null

& $dotnet publish (Join-Path $root 'src\CodexUsageHud.App\CodexUsageHud.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None -o $hudOut --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet publish (Join-Path $root 'src\CodexUsageHud.ClaudeApiCreditHost\CodexUsageHud.ClaudeApiCreditHost.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None -o $hostOut --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$hudExe = Join-Path $hudOut 'CodexUsageHud.App.exe'
$hostExe = Join-Path $hostOut 'ClaudeApiCreditHost.exe'
if (-not (Test-Path -LiteralPath $hudExe -PathType Leaf)) { throw 'hud_exe_missing' }
if (-not (Test-Path -LiteralPath $hostExe -PathType Leaf)) { throw 'host_exe_missing' }
Copy-Item -LiteralPath $hudExe -Destination (Join-Path $bundle 'CodexUsageHud.App.exe') -Force
Copy-Item -LiteralPath $hostExe -Destination (Join-Path $bundle 'ClaudeApiCreditHost.exe') -Force

$extensionDest = Join-Path $bundle 'extension'
$installDest = Join-Path $bundle 'install'
New-Item -ItemType Directory -Force -Path $extensionDest, $installDest | Out-Null
Copy-Item -Path (Join-Path $root 'extension\claude-api-credit\*') -Destination $extensionDest -Recurse -Force
Copy-Item -Path (Join-Path $root 'extension\claude-api-credit\install\*') -Destination $installDest -Force
Copy-Item -LiteralPath (Join-Path $root 'extension\claude-api-credit\INSTALL.md') -Destination (Join-Path $bundle 'INSTALL.md') -Force
Write-Output "bundle=$bundle"
