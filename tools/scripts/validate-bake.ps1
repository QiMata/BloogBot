# Runs NavMeshPhysicsValidator over a list of tiles, aggregates the per-tile
# JSON reports, and emits a summary that's grep-able for problem hotspots.
# Slice A of the physics-validated mmap pipeline (per memory
# project_pfs_overhaul_006_brm_phase4_findings.md).
#
# Usage:
#   tools/scripts/validate-bake.ps1                                # default tile set
#   tools/scripts/validate-bake.ps1 -Tiles "0:34,46;1:29,40"       # explicit list
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
    [switch]$Quiet,
    # Slice B R3: after validation, cull each tile's rejected polyrefs from
    # the .mmtile via NavMeshTileEditor. Off by default — Slice A's diagnostic
    # is non-destructive. Pass -Cull to actually modify the .mmtile files.
    [switch]$Cull,
    # Cull only edges whose bad-segment count >= this. Defaults to 1 (any
    # detected unrecoverable edge), but a higher threshold (e.g. 3) is
    # conservative for sparse sampling.
    [int]$CullMinCount = 1,
    # When -Cull is set, point at the directory holding the .mmtile files
    # to modify. Defaults to <DataDir>/mmaps. Always BACK UP first since
    # this is a destructive operation.
    [string]$MmapsDir = "",
    # Targeted coords (semicolon-separated "X,Y,Z" tuples). For each, the
    # validator queries GetPolyAtCoord to find the polyref at that coord
    # and adds it to the cull list. Use when sampling alone can't find a
    # known-bad trap polygon (e.g., the BRM stall coord — reachable from
    # many directions, so per-edge cull keeps missing the trap itself).
    [string]$CullCoords = "",
    # Z-stack enumeration radius (yards). When > 0, each --cull-coord seed
    # is probed at multiple Z offsets (0.25y step) and ALL distinct polygons
    # found in that Z range are culled. Required for WMO-interior traps
    # that stack multiple walkable polys at slightly different Z values
    # (the BRM trap had at least 3 polys within 0.1y of each other).
    [float]$CullCoordZRadius = 0.0,
    # XY-stack radius (yards). When > 0, each --cull-coord is probed at a
    # 3x3 XY grid with offsets {-R, 0, +R}, multiplying the Z-stack
    # enumeration. Used when the bot stall fluctuates by 0.1-1y in XY
    # across runs.
    [float]$CullCoordXyRadius = 0.0
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$validatorExe = Join-Path $repoRoot "Bot\Release\net8.0\NavMeshPhysicsValidator.exe"
if (-not (Test-Path $validatorExe)) {
    throw "Validator not built: $validatorExe (run tools/MmapGen/build-mmapgen.ps1)"
}
$editorExe = Join-Path $repoRoot "tools\MmapGen\build\NavMeshTileEditor.exe"
if ($Cull -and -not (Test-Path $editorExe)) {
    throw "Tile editor not built: $editorExe (run tools/MmapGen/build-mmapgen.ps1)"
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
        @{ Map = 1; X = 29; Y = 40 }      # OG zeppelin tower
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
    if ($CullCoords) {
        foreach ($coord in $CullCoords -split ';') {
            if ($coord -match '^\s*[-\d.]+,[-\d.]+,[-\d.]+\s*$') {
                $validatorArgs += @('--cull-coord', $coord.Trim())
            }
        }
        if ($CullCoordZRadius -gt 0) {
            $validatorArgs += @('--cull-coord-z-radius', $CullCoordZRadius.ToString([System.Globalization.CultureInfo]::InvariantCulture))
        }
        if ($CullCoordXyRadius -gt 0) {
            $validatorArgs += @('--cull-coord-xy-radius', $CullCoordXyRadius.ToString([System.Globalization.CultureInfo]::InvariantCulture))
        }
    }
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
            Culled = 0
        }
        if (-not $Quiet) {
            Write-Host ("[validate-bake]   pathsFound={0} segs={1} nonWalk={2} unrecoverable={3} ({4}%)" `
                -f $report.PathsFound, $report.TotalSegments, $report.NonWalkSegments, `
                   $report.UnrecoverableNonWalk, $unrecPct)
        }

        # --- Slice B R3: cull pass --------------------------------------------
        # If -Cull is set, feed every RejectedEdges polyA whose bad-segment
        # count >= CullMinCount into NavMeshTileEditor. We cull the START
        # polygon (PolyA) because it's the polygon the bot WAS ON when the
        # runtime detected the unrecoverable transition. Culling PolyB
        # would prevent entry to the destination but might also cull
        # legitimate polys reachable from other directions; PolyA-cull is
        # more surgical (only the polys that produce bad outgoing edges
        # get culled).
        if ($Cull -and $report.RejectedEdges.Count -gt 0) {
            $mmapsRoot = if ($MmapsDir) { $MmapsDir } else { Join-Path $DataDir "mmaps" }
            $mmtileName = "{0:D3}{1:D2}{2:D2}.mmtile" -f $t.Map, $t.Y, $t.X
            $mmtilePath = Join-Path $mmapsRoot $mmtileName
            if (-not (Test-Path $mmtilePath)) {
                Write-Warning "  -Cull: .mmtile not found at $mmtilePath; skipping"
            }
            else {
                $cullPolys = $report.RejectedEdges `
                    | Where-Object { $_.BadSegmentCount -ge $CullMinCount } `
                    | ForEach-Object { $_.PolyA } `
                    | Sort-Object -Unique
                if ($cullPolys.Count -gt 0) {
                    if (-not $Quiet) {
                        Write-Host ("[validate-bake]   -Cull: {0} unique PolyA refs (>= {1} bad segs each) -> {2}" `
                            -f $cullPolys.Count, $CullMinCount, $mmtilePath)
                    }
                    $cullCsv = $cullPolys -join ','
                    $editorArgs = @($mmtilePath, '--cull-polys', $cullCsv)
                    $prevEAP = $ErrorActionPreference
                    $ErrorActionPreference = 'Continue'
                    & $editorExe @editorArgs | Out-Null
                    $editorExit = $LASTEXITCODE
                    $ErrorActionPreference = $prevEAP
                    if ($editorExit -ne 0) {
                        Write-Warning "  NavMeshTileEditor exited $editorExit on $mmtilePath"
                    }
                    else {
                        $summary[-1].Culled = $cullPolys.Count
                    }
                }
            }
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
    Write-Host ("{0,-6} {1,-9} {2,9} {3,9} {4,12} {5,8} {6,8}" `
        -f "Map", "Tile", "Paths", "Segs", "Unrecover.", "Pct", "Culled") -ForegroundColor Cyan
    foreach ($s in $summary) {
        $color = if ($s.UnrecoverablePct -ge 20) { "Red" }
                 elseif ($s.UnrecoverablePct -ge 10) { "Yellow" }
                 else { "Green" }
        Write-Host ("{0,-6} {1,-9} {2,9} {3,9} {4,12} {5,8} {6,8}" `
            -f $s.Map, $s.Tile, $s.PathsFound, $s.TotalSegments, $s.Unrecoverable, $s.UnrecoverablePct, $s.Culled) `
            -ForegroundColor $color
    }
}
