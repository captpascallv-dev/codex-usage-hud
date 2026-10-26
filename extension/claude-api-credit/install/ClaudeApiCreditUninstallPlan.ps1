# Path preparation only. This file does not delete, recycle, or change the registry.
$ErrorActionPreference = 'Stop'

function Assert-ClaudeApiCreditInside {
    param([string]$Path, [string]$Root)
    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $prefix = $root + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "path_escapes_boundary:$full"
    }
}

function Assert-ClaudeApiCreditNoReparse {
    param([string]$LiteralPath)
    if (-not (Test-Path -LiteralPath $LiteralPath)) { return }
    $attributes = [System.IO.File]::GetAttributes($LiteralPath)
    if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "reparse_point:$LiteralPath"
    }
}

function Assert-ClaudeApiCreditReparseFree {
    param([string]$Root, [string]$Target)
    $current = [System.IO.Path]::GetFullPath($Target)
    $root = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    while ($true) {
        Assert-ClaudeApiCreditNoReparse $current
        if ([string]::Equals($current.TrimEnd('\'), $root, [System.StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path -Parent $current
        if (-not $parent -or $parent -eq $current) { throw "left_root:$current" }
        $current = $parent
    }
    if (-not (Test-Path -LiteralPath $Target -PathType Container)) { return }
    $pending = New-Object System.Collections.Generic.Queue[string]
    $pending.Enqueue([System.IO.Path]::GetFullPath($Target))
    while ($pending.Count -gt 0) {
        $dir = $pending.Dequeue()
        foreach ($child in [System.IO.Directory]::EnumerateFileSystemEntries($dir)) {
            $full = [System.IO.Path]::GetFullPath($child)
            Assert-ClaudeApiCreditInside $full $Root
            $attributes = [System.IO.File]::GetAttributes($full)
            if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "reparse_point:$full"
            }
            if (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                $pending.Enqueue($full)
            }
        }
    }
}

function Get-ClaudeApiCreditUninstallPlan {
    param([Parameter(Mandatory = $true)][string]$LocalApplicationData)

    $rootInput = [System.IO.Path]::GetFullPath($LocalApplicationData)
    if (-not [System.IO.Path]::IsPathRooted($rootInput)) { throw 'local_app_data_not_absolute' }
    if (-not (Test-Path -LiteralPath $rootInput -PathType Container)) { throw 'local_app_data_missing' }
    Assert-ClaudeApiCreditNoReparse $rootInput

    $folder = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if (-not [string]::IsNullOrWhiteSpace($folder)) {
        $folderFull = [System.IO.Path]::GetFullPath($folder)
        $envFull = [System.IO.Path]::GetFullPath($env:LOCALAPPDATA)
        if (-not [string]::Equals($folderFull, $envFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'local_app_data_mismatch'
        }
    }

    $dataRoot = [System.IO.Path]::GetFullPath((Join-Path $rootInput 'CodexUsageHUD'))
    Assert-ClaudeApiCreditInside $dataRoot $rootInput
    $expected = @(
        @{ Name = 'claude-api-credit-bridge'; Kind = 'directory' },
        @{ Name = 'claude-api-credit.json'; Kind = 'file' },
        @{ Name = 'claude-api-credit.json.tmp'; Kind = 'file' }
    )
    $plan = @()
    foreach ($item in $expected) {
        $literal = [System.IO.Path]::GetFullPath((Join-Path $dataRoot $item.Name))
        if ((Split-Path -Leaf $literal) -ne $item.Name) { throw "unexpected_name:$literal" }
        Assert-ClaudeApiCreditInside $literal $dataRoot
        if (-not (Test-Path -LiteralPath $literal)) { continue }
        Assert-ClaudeApiCreditReparseFree $rootInput $literal
        $info = Get-Item -LiteralPath $literal -Force
        if (-not [string]::Equals($info.FullName, $literal, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "resolved_path_mismatch:$($info.FullName)"
        }
        $isDirectory = Test-Path -LiteralPath $literal -PathType Container
        if ($item.Kind -eq 'directory' -and -not $isDirectory) { throw "bridge_not_directory:$literal" }
        if ($item.Kind -eq 'file' -and $isDirectory) { throw "cache_is_directory:$literal" }
        $plan += [pscustomobject]@{ LiteralPath = $info.FullName; Kind = $item.Kind }
    }
    return $plan
}
