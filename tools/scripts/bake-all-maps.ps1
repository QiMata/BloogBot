# bake-all-maps.ps1 -- regenerate ALL tiles for a list of map IDs by invoking
# MmapGen.exe with no tile list (full-map bake).
#
# Companion to bake-tile.ps1. Use this script when you want a clean rebake of
# every tile for one or more maps (e.g. after a major MmapGen change, or as
# the Phase 0/1 "regenerate all tiles" step in the comprehensive test plan).
#
# Usage:
#   .\tools\scripts\bake-all-maps.ps1                       # bakes the default list
#   .\tools\scripts\bake-all-maps.ps1 -Maps 0,1             # bakes EK + Kalimdor only
#   .\tools\scripts\bake-all-maps.ps1 -Maps 36,43 -Threads 4
#
# Defaults to baking the full vanilla pathfinding test surface:
#   0 (Eastern Kingdoms), 1 (Kalimdor), plus 21 vanilla 5-man instance maps:
#   33, 34, 36, 43, 47, 48, 70, 90, 109, 129, 189, 209, 229, 230, 289, 329,
#   349, 389, 429.
#
# Per-map output:
#   $DataDir\mmaps\<mapId:03d>.mmap + <mapId:03d><tileY:02d><tileX:02d>.mmtile
#
# Log output (one log per map):
#   $LogDir\bake-all-<UTC>\map-<mapId:03d>.log
#   $LogDir\bake-all-<UTC>\bake-summary.json

[CmdletBinding()]
param(
    [int[]]$Maps = @(0, 1, 33, 34, 36, 43, 47, 48, 70, 90, 109, 129, 189, 209, 229, 230, 289, 329, 349, 389, 429),
    [int]$Threads = 8,
    [string]$DataDir,
    [string]$ConfigPath,
    [string]$OffmeshPath,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

$mmapGenExe = Join-Path $wwowRoot 'tools\MmapGen\build\MmapGen.exe'
if (-not (Test-Path $mmapGenExe)) {
    throw "MmapGen.exe not found at $mmapGenExe. Run tools\MmapGen\build-mmapgen.ps1 first."
}

if (-not $DataDir) {
    $DataDir = $env:WWOW_TEST_DATA_DIR
    if (-not $DataDir) { $DataDir = 'D:\wwow-bot\test-data' }
}
if (-not (Test-Path (Join-Path $DataDir 'mmaps'))) {
    throw "mmaps dir not found under $DataDir."
}

if (-not $ConfigPath)  { $ConfigPath  = Join-Path $wwowRoot 'tools\MmapGen\config.json' }
if (-not $OffmeshPath) { $OffmeshPath = Join-Path $wwowRoot 'tools\MmapGen\offmesh.txt' }
if (-not (Test-Path $ConfigPath))  { throw "config.json not found at $ConfigPath" }

$ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
if (-not $LogDir) { $LogDir = Join-Path $wwowRoot "tmp\bake-sweeps\bake-all-$ts" }
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

Write-Host "[bake-all] DataDir   = $DataDir"
Write-Host "[bake-all] ConfigPath= $ConfigPath"
Write-Host "[bake-all] OffmeshPath= $OffmeshPath"
Write-Host "[bake-all] Threads   = $Threads"
Write-Host "[bake-all] LogDir    = $LogDir"
Write-Host "[bake-all] Maps      = $($Maps -join ', ')"
Write-Host ""

$summary = @()
$overallStart = Get-Date

foreach ($mapId in $Maps) {
    $perMapStart = Get-Date
    $logPath = Join-Path $LogDir ("map-{0:D3}.log" -f $mapId)
    Write-Host ("[bake-all][{0:D3}] starting (--threads {1}) -> {2}" -f $mapId, $Threads, $logPath)

    Push-Location $DataDir
    try {
        $args = @($mapId, '--threads', $Threads, '--silent', '--configInputPath', $ConfigPath)
        if ($OffmeshPath -and (Test-Path $OffmeshPath)) {
            $args += @('--offMeshInput', $OffmeshPath)
        }

        # Run MmapGen.exe. Capture stdout+stderr to per-map log.
        & $mmapGenExe @args *> $logPath
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    $elapsed = (Get-Date) - $perMapStart
    $tilesOut = Get-ChildItem -Path (Join-Path $DataDir 'mmaps') -Filter ("{0:D3}*.mmtile" -f $mapId) -ErrorAction SilentlyContinue
    $tileCount = if ($tilesOut) { $tilesOut.Count } else { 0 }
    $totalBytes = if ($tilesOut) { ($tilesOut | Measure-Object -Property Length -Sum).Sum } else { 0 }

    $status = if ($exitCode -eq 0) { 'OK' } else { "FAIL(exit=$exitCode)" }
    Write-Host ("[bake-all][{0:D3}] {1} elapsed={2:N1}s tiles={3} bytes={4:N0}" -f $mapId, $status, $elapsed.TotalSeconds, $tileCount, $totalBytes)

    $summary += [PSCustomObject]@{
        Map = $mapId
        Status = $status
        ExitCode = $exitCode
        ElapsedSeconds = [math]::Round($elapsed.TotalSeconds, 1)
        TileCount = $tileCount
        TotalBytes = $totalBytes
        Log = $logPath
    }
}

$overallElapsed = (Get-Date) - $overallStart
$summaryPath = Join-Path $LogDir 'bake-summary.json'
$summary | ConvertTo-Json -Depth 4 | Out-File $summaryPath -Encoding utf8

Write-Host ""
Write-Host "[bake-all] === SUMMARY ==="
$summary | Format-Table -AutoSize | Out-String | Write-Host
Write-Host ("[bake-all] wall time: {0:N1} minutes" -f $overallElapsed.TotalMinutes)
Write-Host "[bake-all] summary written to $summaryPath"

$failed = $summary | Where-Object { $_.ExitCode -ne 0 }
if ($failed) {
    Write-Host "[bake-all] FAILED maps: $($failed.Map -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "[bake-all] all maps OK" -ForegroundColor Green
