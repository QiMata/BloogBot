# iterate-pathfinding.ps1 -- end-to-end pathfinding bake/test/iterate
# orchestrator. Glues together bake-tile, probe-routes, and run-pathfinding-tests
# into a single auditable variant directory.
#
# Usage:
#   # Re-bake tile (1,29,40), probe canonical routes, run unit + physics + climb tests
#   .\tools\scripts\iterate-pathfinding.ps1 -Variant baseline -Map 1 -Tiles "29,40" `
#       -RouteManifest tools\scripts\routes\og-zeppelin.json `
#       -RunTests "unit,physics,climb"
#
#   # Probe + tests only (skip bake -- use the current data dir as-is)
#   .\tools\scripts\iterate-pathfinding.ps1 -Variant snapshot-only -SkipBake `
#       -RouteManifest tools\scripts\routes\og-zeppelin.json -RunTests "unit"
#
#   # Bake + probe with a different test-data dir
#   .\tools\scripts\iterate-pathfinding.ps1 -Variant slope-55 -Map 1 -Tiles "29,40" `
#       -DataDir D:\wwow-bot\test-data `
#       -RouteManifest tools\scripts\routes\og-zeppelin.json
#
# Args:
#   -Variant         (string, required) Experiment label.
#   -Map             (int, optional)    Required if rebaking (omit for -SkipBake).
#   -Tiles           (string, optional) "X,Y;X,Y" -- required if rebaking.
#   -DataDir         (string, optional) Default $env:WWOW_DATA_DIR or D:\MaNGOS\data.
#                                       For test-data isolation set
#                                       D:\wwow-bot\test-data and the bake/probe
#                                       both target it; tests read it via env.
#   -ConfigPath      (string, optional) Override path to MmapGen config.json.
#   -OffmeshPath     (string, optional) Override path to MmapGen offmesh.txt.
#   -SkipBake        (switch)           Don't rebake; just probe + test.
#   -RouteManifest   (string, optional) JSON manifest for probe-routes. Skip the
#                                       probe step if omitted.
#   -DetourResolve   (switch)           Pass --detour-resolve to PathPhysicsProbe.
#   -RunTests        (string, optional) Comma-separated test sets:
#                                       unit,physics,pathfinding,climb. Default
#                                       none.
#   -BudgetMs        (int, optional)    Test session timeout. Default 600000.
#   -Quiet           (switch)
#
# Output:
#   tmp\bake-sweeps\<variant>-<UTC>\
#     bake-report.json               (from bake-tile.ps1)
#     snapshot\*.mmtile.bak          (rollback safety)
#     config-effective.json          (snapshot of config.json used)
#     offmesh-effective.txt          (snapshot of offmesh.txt used)
#     probe-results.json             (from probe-routes.ps1)
#     probe-<route>.json/.stderr     (per-route probe output)
#     tests\<set>-<UTC>.trx          (from run-pathfinding-tests.ps1)
#     tests\<set>-<UTC>.console.log
#     tests\test-summary.json
#     iterate-report.md              (human-readable summary)
#     iterate-report.json            (machine-readable summary)
#
# Exit code: 0 on success; non-zero if bake/test failed.
#
# PFS-OVERHAUL-006 -- iteration loop orchestrator.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Variant,
    [int]$Map = -1,
    [string]$Tiles,
    [string]$DataDir,
    [string]$ConfigPath,
    [string]$OffmeshPath,
    [switch]$SkipBake,
    [string]$RouteManifest,
    [switch]$DetourResolve,
    [string]$RunTests,
    [int]$BudgetMs = 600000,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

if (-not $DataDir) {
    $DataDir = $env:WWOW_DATA_DIR
    if (-not $DataDir) { $DataDir = 'D:\MaNGOS\data' }
}

$bakeScript = Join-Path $PSScriptRoot 'bake-tile.ps1'
$probeScript = Join-Path $PSScriptRoot 'probe-routes.ps1'
$testScript = Join-Path $PSScriptRoot 'run-pathfinding-tests.ps1'
foreach ($s in $bakeScript, $probeScript, $testScript) {
    if (-not (Test-Path $s)) { throw "missing helper: $s" }
}

$ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$variantDirName = "$Variant-$ts"
$variantDir = Join-Path $wwowRoot "tmp\bake-sweeps\$variantDirName"
New-Item -ItemType Directory -Force -Path $variantDir | Out-Null

if (-not $Quiet) {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "[iterate-pathfinding] variant=$Variant ts=$ts dataDir=$DataDir" -ForegroundColor Cyan
    Write-Host "[iterate-pathfinding] outDir=$variantDir" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
}

$report = [ordered]@{
    variant = $Variant
    timestamp = [DateTime]::UtcNow.ToString('o')
    dataDir = $DataDir
    variantDir = $variantDir
    bake = $null
    probe = $null
    tests = $null
    overallExit = 0
}

# --- Step 1: bake -----------------------------------------------------------
if ($SkipBake) {
    Write-Host "[iterate] step 1/3 BAKE -- skipped" -ForegroundColor Yellow
} else {
    if ($Map -lt 0 -or -not $Tiles) {
        throw "Bake requested but -Map and -Tiles are required (use -SkipBake to skip the bake step)."
    }
    Write-Host "[iterate] step 1/3 BAKE map=$Map tiles=$Tiles" -ForegroundColor Cyan

    $bakeArgs = @{
        Map = $Map
        Tiles = $Tiles
        Variant = $variantDirName  # so bake puts its outputs INSIDE the iterate variant dir
        DataDir = $DataDir
        Quiet = $Quiet.IsPresent
    }
    if ($ConfigPath) { $bakeArgs.ConfigPath = $ConfigPath }
    if ($OffmeshPath) { $bakeArgs.OffmeshPath = $OffmeshPath }

    # Note: bake-tile.ps1 creates tmp\bake-sweeps\<variantDirName-NEW-UTC>\.
    # We point it at our pre-made dir by passing -Variant equal to the iterate
    # variantDirName WITHOUT the timestamp. Adjust by setting an env var the
    # bake script honors? Simpler: let bake create its own dir; we move outputs.
    # For now: invoke bake with a unique sub-variant tag, then collect.
    $bakeSubvariant = "bake-$Variant"
    $bakeArgs.Variant = $bakeSubvariant
    $bakeOutDir = & $bakeScript @bakeArgs
    if (-not $bakeOutDir -or -not (Test-Path $bakeOutDir)) {
        throw "bake-tile.ps1 did not return a valid output dir; check console output above."
    }
    # Symlink-ish: copy bake artifacts into iterate variantDir
    Copy-Item (Join-Path $bakeOutDir 'bake-report.json') (Join-Path $variantDir 'bake-report.json') -Force
    Copy-Item (Join-Path $bakeOutDir 'config-effective.json') (Join-Path $variantDir 'config-effective.json') -Force
    if (Test-Path (Join-Path $bakeOutDir 'offmesh-effective.txt')) {
        Copy-Item (Join-Path $bakeOutDir 'offmesh-effective.txt') (Join-Path $variantDir 'offmesh-effective.txt') -Force
    }
    if (Test-Path (Join-Path $bakeOutDir 'snapshot')) {
        Copy-Item (Join-Path $bakeOutDir 'snapshot') (Join-Path $variantDir 'snapshot') -Recurse -Force
    }
    if (Test-Path (Join-Path $bakeOutDir 'bake.log')) {
        Copy-Item (Join-Path $bakeOutDir 'bake.log') (Join-Path $variantDir 'bake.log') -Force
    }
    $bakeReport = Get-Content (Join-Path $variantDir 'bake-report.json') -Raw | ConvertFrom-Json
    $report.bake = $bakeReport
    if ($bakeReport.bakeExitCode -ne 0) {
        Write-Host "[iterate] BAKE FAILED -- aborting; restore via tools\scripts\restore-mmaps.ps1 -SweepDir '$bakeOutDir'" -ForegroundColor Red
        $report.overallExit = $bakeReport.bakeExitCode
        $report | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $variantDir 'iterate-report.json') -Encoding utf8
        exit $bakeReport.bakeExitCode
    }
}

# --- Step 2: probe ----------------------------------------------------------
if ($RouteManifest) {
    Write-Host "[iterate] step 2/3 PROBE manifest=$RouteManifest" -ForegroundColor Cyan
    $probeArgs = @{
        Manifest = $RouteManifest
        OutDir   = $variantDir
        DataDir  = $DataDir
        Quiet    = $Quiet.IsPresent
    }
    if ($DetourResolve) { $probeArgs.DetourResolve = $true }
    & $probeScript @probeArgs | Out-Null
    if (Test-Path (Join-Path $variantDir 'probe-results.json')) {
        $report.probe = Get-Content (Join-Path $variantDir 'probe-results.json') -Raw | ConvertFrom-Json
    }
} else {
    Write-Host "[iterate] step 2/3 PROBE -- skipped (no -RouteManifest)" -ForegroundColor Yellow
}

# --- Step 3: tests ----------------------------------------------------------
if ($RunTests) {
    Write-Host "[iterate] step 3/3 TESTS sets=$RunTests" -ForegroundColor Cyan
    $testsDir = Join-Path $variantDir 'tests'
    New-Item -ItemType Directory -Force -Path $testsDir | Out-Null

    $testRuns = @()
    foreach ($set in ($RunTests -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        Write-Host "[iterate]   running $set" -ForegroundColor DarkCyan
        $testArgs = @{
            TestSet = $set
            DataDir = $DataDir
            OutDir = $testsDir
            BudgetMs = $BudgetMs
        }
        try {
            & $testScript @testArgs
        } catch {
            Write-Host "[iterate]   $set threw: $($_.Exception.Message)" -ForegroundColor Red
        }
        $testRuns += $set
    }
    if (Test-Path (Join-Path $testsDir 'test-summary.json')) {
        $report.tests = Get-Content (Join-Path $testsDir 'test-summary.json') -Raw | ConvertFrom-Json
        if ($report.tests.overallExit -ne 0) { $report.overallExit = $report.tests.overallExit }
    }
} else {
    Write-Host "[iterate] step 3/3 TESTS -- skipped (no -RunTests)" -ForegroundColor Yellow
}

# --- Final report -----------------------------------------------------------
$report | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $variantDir 'iterate-report.json') -Encoding utf8

# Markdown summary
$md = @()
$md += "# iterate-pathfinding -- $Variant"
$md += ""
$md += "- timestamp: $($report.timestamp)"
$md += "- dataDir: $($report.dataDir)"
$md += "- outDir: $variantDir"
$md += ""
if ($report.bake) {
    $md += "## Bake"
    $md += "- exit: $($report.bake.bakeExitCode)"
    $md += "- elapsed: $($report.bake.bakeElapsedSeconds)s"
    $md += "- tiles:"
    foreach ($t in $report.bake.tiles) {
        $delta = if ($null -ne $t.deltaBytes) { "{0:+#;-#;0}" -f $t.deltaBytes } else { 'NEW' }
        $md += "  - ($($t.tileX),$($t.tileY)) $($t.fileName): before=$($t.beforeLen) after=$($t.afterLen) delta=$delta"
    }
    $md += ""
}
if ($report.probe) {
    $md += "## Probe"
    $md += "- manifest: $($report.probe.manifest)"
    $md += "- routes:"
    foreach ($r in $report.probe.routes) {
        $md += "  - **$($r.name)** exit=$($r.exitCode) segments=$($r.segmentCount) firstFail=$($r.firstFailure) elapsed=$($r.elapsedSeconds)s"
    }
    $md += ""
}
if ($report.tests) {
    $md += "## Tests"
    foreach ($r in $report.tests.runs) {
        $md += "- **$($r.label)** exit=$($r.exit) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped) elapsed=$($r.elapsedSeconds)s"
    }
    $md += ""
}
$md += "## Final"
$md += "- overall exit: $($report.overallExit)"
$md -join "`r`n" | Set-Content -Path (Join-Path $variantDir 'iterate-report.md') -Encoding utf8

Write-Host ""
$doneColor = if ($report.overallExit -eq 0) { 'Green' } else { 'Red' }
Write-Host "[iterate-pathfinding] DONE -> $variantDir" -ForegroundColor $doneColor
Write-Host "[iterate-pathfinding] report: $(Join-Path $variantDir 'iterate-report.md')" -ForegroundColor Green

exit $report.overallExit
