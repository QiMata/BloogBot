# Runs NavMeshPhysicsValidator over a list of tiles, aggregates the per-tile
# JSON reports, and emits a summary that's grep-able for problem hotspots.
# Slice A of the physics-validated mmap pipeline (per memory
# project_pfs_overhaul_006_brm_phase4_findings.md).
#
# Usage:
#   tools/scripts/validate-bake.ps1                                # default tile set
#   tools/scripts/validate-bake.ps1 -Tiles "0:34,46;1:40,29"       # explicit list
#   tools/scripts/validate-bake.ps1 -Manifest tools/scripts/routes/og-zeppelin.json
#   tools/scripts/validate-bake.ps1 -Samples 50 -DataDir D:/wwow-bot/test-data
#
# Per-tile reports go to tmp/bake-validation/<UTC>/<map>_<tileX>_<tileY>.json.
# The summary is at tmp/bake-validation/<UTC>/summary.json with a one-line
# recap printed to stdout per tile.
#
# This is a DIAGNOSTIC pass. It does NOT modify .mmtile files. Slice B (next
# session) will add an edge-culling rewrite that removes physics-rejected
# edges from the navmesh.

param(
    [string]$Tiles = "",
    [string]$Manifest = "",
    [int]$Samples = 30,
    [int]$Seed = 0,
    [string]$DataDir = "",
    [int]$WorstTop = 20,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$validatorExe = Join-Path $repoRoot "Bot\Release\net8.0\NavMeshPhysicsValidator.exe"
if (-not (Test-Path $validatorExe)) {
    throw "Validator not built: $validatorExe (run tools/MmapGen/build-mmapgen.ps1)"
}

if (-not $DataDir) {
    $DataDir = if ($env:WWOW_DATA_DIR) { $env:WWOW_DATA_DIR } else { "D:/wwow-bot/test-data" }
}
if (-not (Test-Path $DataDir)) {
    throw "DataDir does not exist: $DataDir"
}

# --- Resolve tile list ------------------------------------------------------
$tilePairs = @()
if ($Manifest) {
    if (-not (Test-Path $Manifest)) { throw "Manifest not found: $Manifest" }
    $manifestJson = Get-Content $Manifest -Raw | ConvertFrom-Json
    foreach ($entry in $manifestJson.tilesAffected) {
        # Manifest format: array of [tileX,tileY] pairs (one map at a time).
        $mapId = if ($manifestJson.map) { [int]$manifestJson.map } else { 0 }
        $tilePairs += @{ Map = $mapId; X = [int]$entry[0]; Y = [int]$entry[1] }
    }
}
elseif ($Tiles) {
    foreach ($t in $Tiles -split ';') {
        if ($t -match '^(\d+):(\d+),(\d+)$') {
            $tilePairs += @{ Map = [int]$matches[1]; X = [int]$matches[2]; Y = [int]$matches[3] }
        }
        elseif ($t -match '^(\d+),(\d+)$') {
            $tilePairs += @{ Map = 0; X = [int]$matches[1]; Y = [int]$matches[2] }
        }
        else {
            throw "Invalid -Tiles entry '$t' (expected 'mapId:tileX,tileY' or 'tileX,tileY')"
        }
    }
}
else {
    # Default tile set: known-problem tiles from the BRM/OG-zep work.
    $tilePairs = @(
        @{ Map = 0; X = 34; Y = 46 }      # BRM south-face
        @{ Map = 1; X = 40; Y = 29 }      # OG zeppelin tower
        @{ Map = 0; X = 31; Y = 49 }      # Elwynn (healthy baseline)
    )
}

# --- Output dir -------------------------------------------------------------
$utcStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$outDir = Join-Path $repoRoot "tmp\bake-validation\$utcStamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# --- Run validator per tile -------------------------------------------------
$summary = @()
$env:WWOW_DATA_DIR = $DataDir
foreach ($t in $tilePairs) {
    $reportPath = Join-Path $outDir ("{0}_{1}_{2}.json" -f $t.Map, $t.X, $t.Y)
    $tileArg = "{0},{1}" -f $t.X, $t.Y
    $validatorArgs = @(
        $t.Map.ToString(),
        '--tile', $tileArg,
        '--samples', $Samples.ToString(),
        '--seed', $Seed.ToString(),
        '--worst-top', $WorstTop.ToString(),
        '--out', $reportPath,
        '--silent'
    )
    if (-not $Quiet) {
        Write-Host ("[validate-bake] map={0} tile=({1},{2}) samples={3}" -f $t.Map, $t.X, $t.Y, $Samples)
    }
    # Run the validator. PowerShell 5.1 wraps native-stderr lines in
    # ErrorRecords when $ErrorActionPreference=Stop, which we tripped on the
    # validator's [SceneCache] init log. Suppress the wrapping locally and
    # let the validator's stderr just go to the parent terminal — the progress
    # lines are useful for triage and we capture the JSON via --out anyway.
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $validatorExe @validatorArgs | Out-Null
    $exit = $LASTEXITCODE
    $ErrorActionPreference = $prevEAP

    if (Test-Path $reportPath) {
        $report = Get-Content $reportPath -Raw | ConvertFrom-Json
        $unrecPct = if ($report.TotalSegments -gt 0) {
            [math]::Round(100.0 * $report.UnrecoverableNonWalk / $report.TotalSegments, 2)
        } else { 0 }
        $summary += @{
            Map = $t.Map
            Tile = "{0},{1}" -f $t.X, $t.Y
            PathsFound = $report.PathsFound
            TotalSegments = $report.TotalSegments
            NonWalk = $report.NonWalkSegments
            Unrecoverable = $report.UnrecoverableNonWalk
            UnrecoverablePct = $unrecPct
            ExitCode = $exit
            Report = $reportPath
        }
        if (-not $Quiet) {
            Write-Host ("[validate-bake]   pathsFound={0} segs={1} nonWalk={2} unrecoverable={3} ({4}%)" `
                -f $report.PathsFound, $report.TotalSegments, $report.NonWalkSegments, `
                   $report.UnrecoverableNonWalk, $unrecPct)
        }
    }
    else {
        Write-Warning "no report at $reportPath (validator exit=$exit)"
    }
}

# --- Aggregated summary -----------------------------------------------------
$summaryPath = Join-Path $outDir "summary.json"
$summary | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -Path $summaryPath

if (-not $Quiet) {
    Write-Host ""
    Write-Host "Summary at $summaryPath" -ForegroundColor Green
    Write-Host ""
    Write-Host ("{0,-6} {1,-9} {2,9} {3,9} {4,12} {5,8}" -f "Map", "Tile", "Paths", "Segs", "Unrecover.", "Pct") -ForegroundColor Cyan
    foreach ($s in $summary) {
        $color = if ($s.UnrecoverablePct -ge 20) { "Red" }
                 elseif ($s.UnrecoverablePct -ge 10) { "Yellow" }
                 else { "Green" }
        Write-Host ("{0,-6} {1,-9} {2,9} {3,9} {4,12} {5,8}" `
            -f $s.Map, $s.Tile, $s.PathsFound, $s.TotalSegments, $s.Unrecoverable, $s.UnrecoverablePct) `
            -ForegroundColor $color
    }
}
