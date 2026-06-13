#!/usr/bin/env pwsh
# verify-mmap-data-roots.ps1
#
# Read-only verifier for MaNGOS server mmaps and WWoW bot data roots.
#
# Manual validation:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\scripts\verify-mmap-data-roots.ps1 -DryRun
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\scripts\verify-mmap-data-roots.ps1
#
# The script only reads filesystem metadata with Test-Path/Get-ChildItem/Get-Item.
# It does not delete, rename, copy, regenerate, restart, or write any data.

[CmdletBinding()]
param(
    [string]$ServerDataRoot = $(if ($env:WWOW_VMANGOS_DATA_DIR) { $env:WWOW_VMANGOS_DATA_DIR } else { 'D:\MaNGOS\data' }),
    [string]$BotTestDataRoot = $(if ($env:WWOW_TEST_DATA_DIR) { $env:WWOW_TEST_DATA_DIR } else { 'D:\wwow-bot\test-data' }),
    [string]$BotProdDataRoot = $(if ($env:WWOW_BOT_PROD_DATA_DIR) { $env:WWOW_BOT_PROD_DATA_DIR } else { 'D:\wwow-bot\prod-data' }),
    [string[]]$RequiredRouteFile = @(
        '0004533.mmtile', # BRD approach reference route
        '0004635.mmtile', # Flame Crest / BRM reference route
        '0012940.mmtile'  # Orgrimmar zeppelin tower reference route
    ),
    # Bot-route coverage is informational by default (mission desired-state #5
    # keeps bot route analysis separate from server rescue). Pass this switch to
    # turn a missing bot-route tile into a hard server-health failure instead.
    [switch]$RequireRouteCoverage,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$script:RequireRouteCoverage = [bool]$RequireRouteCoverage

$script:Failures = New-Object System.Collections.Generic.List[string]
$script:Warnings = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$Message) {
    $script:Failures.Add($Message)
    Write-Host "FAIL: $Message" -ForegroundColor Red
}

function Add-VerifierWarning([string]$Message) {
    $script:Warnings.Add($Message)
    Write-Warning $Message
}

function Normalize-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    try {
        return ([IO.Path]::GetFullPath($Path)).TrimEnd('\', '/')
    } catch {
        return $Path.TrimEnd('\', '/')
    }
}

function Format-Percent([double]$Value) {
    return $Value.ToString('P1', [Globalization.CultureInfo]::InvariantCulture)
}

function Get-MmapFiles([string]$MmapsPath) {
    return @(
        Get-ChildItem -LiteralPath $MmapsPath -File -ErrorAction Stop |
            Where-Object {
                $_.Extension -ieq '.mmap' -or $_.Extension -ieq '.mmtile'
            }
    )
}

function Get-PrefixCounts([object[]]$Files) {
    $counts = @{}
    $unmatched = 0
    foreach ($file in $Files) {
        if ($file.Name -match '^(\d{3})') {
            $prefix = $Matches[1]
            if (-not $counts.ContainsKey($prefix)) {
                $counts[$prefix] = 0
            }
            $counts[$prefix]++
        } else {
            $unmatched++
        }
    }

    return [pscustomobject]@{
        Counts = $counts
        Unmatched = $unmatched
    }
}

function Format-PrefixCounts($PrefixInfo) {
    if ($PrefixInfo.Counts.Count -eq 0) {
        return 'none'
    }

    $parts = @(
        $PrefixInfo.Counts.Keys |
            Sort-Object |
            ForEach-Object { '{0}={1}' -f $_, $PrefixInfo.Counts[$_] }
    )

    if ($PrefixInfo.Unmatched -gt 0) {
        $parts += ('other={0}' -f $PrefixInfo.Unmatched)
    }

    return ($parts -join ', ')
}

function Get-MmapRootSummary(
    [string]$Label,
    [string]$DataRoot,
    [bool]$RequiredRoot
) {
    $normalizedRoot = Normalize-FullPath $DataRoot
    $mmapsPath = Join-Path $normalizedRoot 'mmaps'

    Write-Host ""
    Write-Host "[$Label]" -ForegroundColor Cyan
    Write-Host "data root:  $normalizedRoot"
    Write-Host "mmaps dir:  $mmapsPath"

    if (-not (Test-Path -LiteralPath $mmapsPath -PathType Container)) {
        $message = "mmaps dir missing for $Label`: $mmapsPath"
        if ($RequiredRoot) {
            Add-Failure $message
        } else {
            Add-VerifierWarning $message
        }

        return [pscustomobject]@{
            Label = $Label
            DataRoot = $normalizedRoot
            MmapsPath = $mmapsPath
            Exists = $false
            Files = @()
            PrefixInfo = (Get-PrefixCounts @())
        }
    }

    $files = Get-MmapFiles $mmapsPath
    $prefixInfo = Get-PrefixCounts $files
    $totalBytes = [int64]0
    foreach ($file in $files) {
        $totalBytes += $file.Length
    }

    Write-Host ("files:      {0:N0}" -f $files.Count)
    Write-Host ("bytes:      {0:N0}" -f $totalBytes)
    Write-Host ("prefixes:   {0}" -f (Format-PrefixCounts $prefixInfo))

    return [pscustomobject]@{
        Label = $Label
        DataRoot = $normalizedRoot
        MmapsPath = $mmapsPath
        Exists = $true
        Files = $files
        PrefixInfo = $prefixInfo
    }
}

function Test-ServerRootHealth($Summary, [string[]]$RouteFiles) {
    if (-not $Summary.Exists) {
        return
    }

    if ($Summary.Files.Count -eq 0) {
        Add-Failure "server mmaps dir exists but contains no .mmap/.mmtile files"
        return
    }

    $prefixKeys = @($Summary.PrefixInfo.Counts.Keys)
    if ($prefixKeys.Count -eq 1 -and $prefixKeys[0] -eq '001') {
        Add-Failure "server mmaps root is 001-only; map 000 coverage is absent"
    }

    foreach ($header in @('000.mmap', '001.mmap')) {
        $path = Join-Path $Summary.MmapsPath $header
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Add-Failure "server header missing: $header"
        } else {
            Write-Host "OK: server header present: $header" -ForegroundColor Green
        }
    }

    # Bot-route coverage is INFORMATIONAL only and must NOT gate server health.
    # Mission desired-state #5 keeps bot route analysis separate from MaNGOS
    # server rescue, so a missing bot-route tile is a warning, not a server
    # FAIL. (Pass -RequireRouteCoverage to gate on it explicitly.)
    foreach ($routeFile in $RouteFiles) {
        if ([string]::IsNullOrWhiteSpace($routeFile)) {
            continue
        }

        $path = Join-Path $Summary.MmapsPath $routeFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            if ($script:RequireRouteCoverage) {
                Add-Failure "server route coverage file missing: $routeFile"
            } else {
                Add-VerifierWarning "bot-route coverage tile not present in server root (informational, not a server-health failure): $routeFile"
            }
        } else {
            Write-Host "OK: bot-route coverage tile present in server root: $routeFile" -ForegroundColor Green
        }
    }
}

function Test-RootSeparation($Server, [object[]]$BotRoots) {
    foreach ($botRoot in $BotRoots) {
        if ($botRoot.MmapsPath -eq $Server.MmapsPath) {
            Add-Failure "$($botRoot.Label) mmaps path is the same as the server mmaps path"
        }
    }
}

function Get-FileSignatureSet([object[]]$Files) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Files) {
        [void]$set.Add(('{0}|{1}' -f $file.Name, $file.Length))
    }
    return $set
}

function Test-OverlapWarning($Server, $Bot) {
    if (-not $Server.Exists -or -not $Bot.Exists) {
        return
    }

    if ($Server.Files.Count -eq 0 -or $Bot.Files.Count -eq 0) {
        return
    }

    $serverSignatures = Get-FileSignatureSet $Server.Files
    $overlap = New-Object System.Collections.Generic.List[object]
    foreach ($file in $Bot.Files) {
        $signature = '{0}|{1}' -f $file.Name, $file.Length
        if ($serverSignatures.Contains($signature)) {
            $overlap.Add($file)
        }
    }

    $botCoverage = [double]$overlap.Count / [double]$Bot.Files.Count
    $serverCoverage = [double]$overlap.Count / [double]$Server.Files.Count
    Write-Host (
        "overlap with {0}: {1:N0} exact filename/size match(es), bot coverage {2}, server coverage {3}" -f
        $Bot.Label,
        $overlap.Count,
        (Format-Percent $botCoverage),
        (Format-Percent $serverCoverage)
    )

    if ($overlap.Count -gt 0 -and ($botCoverage -ge 0.95 -or $serverCoverage -ge 0.95)) {
        $sample = @($overlap | Select-Object -First 8 | ForEach-Object { $_.Name })
        Add-VerifierWarning (
            "high exact filename/size overlap between server and $($Bot.Label) mmaps; " +
            "investigate possible bot-data copy into MaNGOS. Sample: $($sample -join ', ')"
        )
    }
}

if ($DryRun) {
    Write-Host "DRY RUN: no filesystem enumeration will be performed." -ForegroundColor Yellow
    Write-Host "Would verify server root:   $(Normalize-FullPath $ServerDataRoot)"
    Write-Host "Would report bot test root: $(Normalize-FullPath $BotTestDataRoot)"
    Write-Host "Would report bot prod root: $(Normalize-FullPath $BotProdDataRoot)"
    Write-Host "Would require server headers: 000.mmap, 001.mmap"
    Write-Host "Would fail if server mmaps are 001-only."
    Write-Host "Would check route coverage files: $($RequiredRouteFile -join ', ')"
    Write-Host "Would compare exact filename/size overlap between server and bot roots."
    exit 0
}

Write-Host "Read-only mmap data root verifier" -ForegroundColor Cyan
Write-Host "Started: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))"

$server = Get-MmapRootSummary 'server' $ServerDataRoot $true
$botTest = Get-MmapRootSummary 'bot test' $BotTestDataRoot $false
$botProd = Get-MmapRootSummary 'bot prod' $BotProdDataRoot $false

Test-RootSeparation $server @($botTest, $botProd)
Test-ServerRootHealth $server $RequiredRouteFile

Write-Host ""
Write-Host "[bot/server filename-size overlap]" -ForegroundColor Cyan
Test-OverlapWarning $server $botTest
Test-OverlapWarning $server $botProd

Write-Host ""
if ($script:Failures.Count -gt 0) {
    Write-Host "FAILED: $($script:Failures.Count) failure(s), $($script:Warnings.Count) warning(s)." -ForegroundColor Red
    exit 1
}

Write-Host "OK: mmap data root checks passed with $($script:Warnings.Count) warning(s)." -ForegroundColor Green
exit 0
