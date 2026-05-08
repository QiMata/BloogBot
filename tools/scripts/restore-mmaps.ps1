# restore-mmaps.ps1 -- restore mmtile files from a bake-sweep snapshot.
# Pair with bake-tile.ps1.
#
# Usage:
#   .\tools\scripts\restore-mmaps.ps1 -Variant climb-baseline-20260507T180000Z
#   .\tools\scripts\restore-mmaps.ps1 -SweepDir "tmp\bake-sweeps\climb-baseline-..."
#   .\tools\scripts\restore-mmaps.ps1 -Variant ... -DryRun
#
# Args:
#   -Variant   (string) Variant directory name under tmp\bake-sweeps\
#                       (the full <variant>-<UTC> form). Mutually exclusive
#                       with -SweepDir.
#   -SweepDir  (string) Full path to the bake-sweep dir.
#   -DataDir   (string) Override target data dir (default reads bake-report.json
#                       or falls back to $env:WWOW_TEST_DATA_DIR).
#   -DryRun    (switch) Show what would be restored, do nothing.

[CmdletBinding(DefaultParameterSetName='Variant')]
param(
    [Parameter(ParameterSetName='Variant', Mandatory)] [string]$Variant,
    [Parameter(ParameterSetName='SweepDir', Mandatory)] [string]$SweepDir,
    [string]$DataDir,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

if (-not $SweepDir) {
    $SweepDir = Join-Path $wwowRoot "tmp\bake-sweeps\$Variant"
}
if (-not (Test-Path $SweepDir)) {
    throw "Sweep dir not found: $SweepDir"
}

$reportPath = Join-Path $SweepDir 'bake-report.json'
if (-not (Test-Path $reportPath)) {
    throw "bake-report.json not found in $SweepDir -- not a valid bake-sweep dir"
}
$report = Get-Content $reportPath -Raw | ConvertFrom-Json

if (-not $DataDir) {
    $DataDir = $report.dataDir
}
$mmapsDir = Join-Path $DataDir 'mmaps'
$snapshotDir = Join-Path $SweepDir 'snapshot'
if (-not (Test-Path $snapshotDir)) {
    throw "snapshot dir not found: $snapshotDir"
}

$action = if ($DryRun) { 'Would restore' } else { 'Restoring' }
Write-Host "$action snapshots from $snapshotDir -> $mmapsDir" -ForegroundColor Cyan

$restored = 0
$missing = 0
foreach ($entry in $report.tiles) {
    $bakName = "$($entry.fileName).bak"
    $bakPath = Join-Path $snapshotDir $bakName
    if (-not (Test-Path $bakPath)) {
        # Tile was new (created by the bake); restoring means deleting it
        if (Test-Path $entry.srcPath) {
            Write-Host ("  delete (was new): {0}" -f $entry.fileName) -ForegroundColor Yellow
            if (-not $DryRun) { Remove-Item $entry.srcPath -Force }
            $restored++
        } else {
            $missing++
        }
        continue
    }
    Write-Host ("  restore: {0}" -f $entry.fileName) -ForegroundColor DarkGray
    if (-not $DryRun) { Copy-Item $bakPath $entry.srcPath -Force }
    $restored++
}

Write-Host ("$action complete: {0} restored, {1} missing in snapshot" -f $restored, $missing) -ForegroundColor Green
if (-not $DryRun) {
    Write-Host "REMINDER: restart pathfinding service so it reloads the restored tiles:" -ForegroundColor Yellow
    Write-Host "  docker restart wwow-pathfinding wwow-scene-data    # if testing prod" -ForegroundColor Yellow
    Write-Host "  (test fixtures spawn fresh PathfindingService.exe per fixture; no manual restart needed)" -ForegroundColor Yellow
}
