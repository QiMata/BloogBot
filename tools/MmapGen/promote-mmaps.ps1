# promote-mmaps.ps1 — copy mmtile files from the test-data dir to the
# prod-data dir, with optional tile filtering.
#
# Usage:
#   .\tools\MmapGen\promote-mmaps.ps1                         # promote ALL tiles
#   .\tools\MmapGen\promote-mmaps.ps1 -Map 1                  # only map 1
#   .\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "29,40"   # only tile (29,40) on map 1
#   .\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "29,40;28,40"  # multiple tiles
#   .\tools\MmapGen\promote-mmaps.ps1 -DryRun                 # show what would copy, do nothing
#
# Source / destination dirs are picked up from env vars when set:
#   WWOW_TEST_DATA_DIR         (default: D:\wwow-bot\test-data)
#   WWOW_BOT_PROD_DATA_DIR     (default: D:\wwow-bot\prod-data — matches docker-compose)
#
# After promotion, restart the bot containers so they reload tiles:
#   docker restart wwow-pathfinding wwow-scene-data
#
# PFS-OVERHAUL-006: this is the canonical "release a bake" step. Tests run
# against test-data/mmaps; once green, promote to prod-data/mmaps; restart
# Docker; production picks up the new bake. No more mixing with the MaNGOS
# server's data dir.

[CmdletBinding()]
param(
    [int]$Map = -1,
    [string]$Tiles = "",
    [switch]$DryRun,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$testRoot = $env:WWOW_TEST_DATA_DIR
if (-not $testRoot) { $testRoot = "D:\wwow-bot\test-data" }
$prodRoot = $env:WWOW_BOT_PROD_DATA_DIR
if (-not $prodRoot) { $prodRoot = "D:\wwow-bot\prod-data" }

$src = Join-Path $testRoot "mmaps"
$dst = Join-Path $prodRoot "mmaps"

if (-not (Test-Path $src)) {
    Write-Error "Source mmaps dir not found: $src. Run MmapGen against the test-data dir first."
}
if (-not (Test-Path $dst)) {
    Write-Host "Creating prod mmaps dir: $dst" -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
}

# Build candidate list
$candidates = New-Object System.Collections.Generic.List[string]

if ($Tiles) {
    if ($Map -lt 0) { Write-Error "-Tiles requires -Map" }
    foreach ($pair in ($Tiles -split ';')) {
        $xy = $pair.Trim() -split ','
        if ($xy.Count -ne 2) { Write-Error "Invalid -Tiles entry '$pair' - expected 'X,Y'" }
        $tileX = [int]$xy[0]
        $tileY = [int]$xy[1]
        # mmtile filename convention: <map:03d><tileY:02d><tileX:02d>.mmtile
        $name = "{0:D3}{1:D2}{2:D2}.mmtile" -f $Map, $tileY, $tileX
        $candidates.Add((Join-Path $src $name))
    }
} elseif ($Map -ge 0) {
    $prefix = "{0:D3}" -f $Map
    Get-ChildItem $src -File -Filter ($prefix + "*.mmtile") | ForEach-Object {
        $candidates.Add($_.FullName)
    }
    $headerCandidate = Join-Path $src ("{0:D3}.mmap" -f $Map)
    if (Test-Path $headerCandidate) { $candidates.Add($headerCandidate) }
} else {
    Get-ChildItem $src -File | Where-Object { $_.Extension -eq ".mmap" -or $_.Extension -eq ".mmtile" } | ForEach-Object {
        $candidates.Add($_.FullName)
    }
}

if ($candidates.Count -eq 0) {
    Write-Host "No matching files in $src." -ForegroundColor Yellow
    exit 0
}

$action = "Copying"
if ($DryRun) { $action = "Would copy" }
Write-Host ("$action {0} file(s)  $src  ->  $dst" -f $candidates.Count) -ForegroundColor Cyan

$copied = 0
$skipped = 0
$totalBytes = [long]0
foreach ($srcPath in $candidates) {
    if (-not (Test-Path $srcPath)) {
        Write-Host "  MISSING: $srcPath" -ForegroundColor Red
        continue
    }
    $name = Split-Path $srcPath -Leaf
    $dstPath = Join-Path $dst $name
    $srcInfo = Get-Item $srcPath
    $isCurrent = $false
    if (Test-Path $dstPath) {
        $dstInfo = Get-Item $dstPath
        if ($dstInfo.Length -eq $srcInfo.Length -and $dstInfo.LastWriteTime -ge $srcInfo.LastWriteTime) {
            $isCurrent = $true
        }
    }
    if ($isCurrent) {
        $skipped++
        if (-not $Quiet) { Write-Host ("  skip {0} (dst is newer or identical)" -f $name) -ForegroundColor DarkGray }
        continue
    }
    if (-not $DryRun) { Copy-Item $srcPath $dstPath -Force }
    $copied++
    $totalBytes += $srcInfo.Length
    if (-not $Quiet) { Write-Host ("  {0} ({1:N0} bytes)" -f $name, $srcInfo.Length) }
}

$mb = ($totalBytes / 1MB)
Write-Host ("$action complete: {0} promoted, {1} already current ({2:N1} MB)" -f $copied, $skipped, $mb) -ForegroundColor Green

if (-not $DryRun -and $copied -gt 0) {
    Write-Host ""
    Write-Host "REMINDER: restart Docker so the running services reload tiles:" -ForegroundColor Yellow
    Write-Host "  docker restart wwow-pathfinding wwow-scene-data" -ForegroundColor Yellow
}
