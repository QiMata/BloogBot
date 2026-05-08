# verify-strict-gate.ps1 -- exercise the WWOW_DATA_DIR strict gate from
# PFS-OVERHAUL-006. Runs a small Navigation.dll consumer (PathPhysicsProbe.exe)
# under a sequence of bad env-var configurations and asserts each FATAL-exits
# with the expected message.
#
# Usage:
#   .\tools\scripts\verify-strict-gate.ps1
#   .\tools\scripts\verify-strict-gate.ps1 -OutDir tmp\verify-gate
#
# Args:
#   -OutDir   (optional) Where to drop per-case stderr logs. Default
#             tmp\verify-gate\<UTC>\.
#
# Exit codes:
#   0 = all gate cases behaved as expected
#   1 = at least one gate case did NOT FATAL when it should have
#
# Each case prints PASS/FAIL with the captured stderr.
#
# PFS-OVERHAUL-006 -- iteration loop, defense-in-depth verification.

[CmdletBinding()]
param(
    [string]$OutDir
)

$ErrorActionPreference = 'Continue'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

$probeExe = Join-Path $wwowRoot 'Bot\Release\net8.0\PathPhysicsProbe.exe'
if (-not (Test-Path $probeExe)) {
    throw "PathPhysicsProbe.exe not found at $probeExe. Build the WWoW solution first."
}

if (-not $OutDir) {
    $ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $OutDir = Join-Path $wwowRoot "tmp\verify-gate\$ts"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Use a benign route from the manifest -- doesn't matter what we probe; we
# expect the process to FATAL-exit before reaching path computation.
$probeArgs = @('--map','1','--start','1677,-4315,61.4','--end','1331.11,-4649.45,53.6269','--json')

$cases = @(
    @{
        name = 'unset'
        envValue = $null
        expectExitNonZero = $true
        expectStderrMatch = 'WWOW_DATA_DIR is not set'
    },
    @{
        name = 'empty-string'
        envValue = ''
        expectExitNonZero = $true
        expectStderrMatch = 'WWOW_DATA_DIR is not set'
    },
    @{
        name = 'nonexistent-dir'
        envValue = 'D:\__nonexistent_pathfinding_test_dir__'
        expectExitNonZero = $true
        expectStderrMatch = 'does not contain mmaps'
    },
    @{
        name = 'missing-mmaps'
        envValue = $env:TEMP   # exists but has no mmaps/maps/vmaps
        expectExitNonZero = $true
        expectStderrMatch = 'does not contain mmaps'
    }
)

$prevDataDir = $env:WWOW_DATA_DIR
$results = @()
$allPassed = $true

foreach ($c in $cases) {
    if ($null -eq $c.envValue) {
        Remove-Item Env:WWOW_DATA_DIR -ErrorAction SilentlyContinue
        $envDisplay = '<unset>'
    } else {
        $env:WWOW_DATA_DIR = $c.envValue
        $envDisplay = "'$($c.envValue)'"
    }

    $stderrPath = Join-Path $OutDir ("case-{0}.stderr" -f $c.name)
    $stdoutPath = Join-Path $OutDir ("case-{0}.stdout" -f $c.name)
    Write-Host ("[verify-gate] case={0} WWOW_DATA_DIR={1}" -f $c.name, $envDisplay) -ForegroundColor Cyan

    & $probeExe @probeArgs 1>$stdoutPath 2>$stderrPath
    $exit = $LASTEXITCODE
    # Read the captured stderr. Note: PowerShell's NativeCommandError
    # wrapping (a) writes the file in UTF-16 LE, (b) inserts terminal-width
    # newlines mid-line. We normalize whitespace to a single space before
    # the substring check so "does not contain" matches even when wrapping
    # split it as "does not\n contain".
    $stderrText = ''
    $stderrNormalized = ''
    if (Test-Path $stderrPath) {
        try { $stderrText = [System.IO.File]::ReadAllText($stderrPath) }  # auto-detects BOM
        catch { $stderrText = Get-Content $stderrPath -Raw -ErrorAction SilentlyContinue }
        if ($stderrText) {
            $stderrNormalized = ($stderrText -replace '\s+', ' ').Trim()
        }
    }

    $exitOk = -not ($c.expectExitNonZero -and $exit -eq 0)
    # Substring match against the whitespace-normalized stderr -- avoids
    # PowerShell's NativeCommandError wrapping splitting our needle across
    # a synthesized line break (e.g. "does not\n contain" instead of
    # "does not contain"). We pre-normalized the same way for the needle.
    $msgOk = $false
    if ($stderrNormalized) {
        $needle = (([string]$c.expectStderrMatch) -replace '\s+', ' ').Trim()
        $msgOk = $stderrNormalized.Contains($needle)
    }
    $passed = $exitOk -and $msgOk

    if ($passed) {
        Write-Host ("  PASS exit={0} stderr-contains '{1}'" -f $exit, $c.expectStderrMatch) -ForegroundColor Green
    } else {
        Write-Host ("  FAIL exit={0} stderr-match={1} expected-substring='{2}'" -f $exit, $msgOk, $c.expectStderrMatch) -ForegroundColor Red
        if ($stderrText.Length -lt 800) {
            Write-Host "  --- stderr ---" -ForegroundColor DarkGray
            $stderrText -split "`r?`n" | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
            Write-Host "  --------------" -ForegroundColor DarkGray
        }
        $allPassed = $false
    }

    $results += [pscustomobject]@{
        case = $c.name
        envValue = $c.envValue
        exit = $exit
        passed = $passed
        stderrPath = $stderrPath
    }
}

# Restore env
if ($null -ne $prevDataDir) { $env:WWOW_DATA_DIR = $prevDataDir } else { Remove-Item Env:WWOW_DATA_DIR -ErrorAction SilentlyContinue }

$report = [pscustomobject]@{
    schemaVersion = 1
    timestamp = [DateTime]::UtcNow.ToString('o')
    probeExe = $probeExe
    cases = $results
    allPassed = $allPassed
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $OutDir 'verify-gate-report.json') -Encoding utf8

Write-Host ""
if ($allPassed) {
    Write-Host "[verify-gate] ALL PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[verify-gate] FAILURES" -ForegroundColor Red
    exit 1
}
