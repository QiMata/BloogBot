# bake-tile.ps1 -- regenerate one or more mmtile files via MmapGen.exe with
# snapshot/restore safety. Part of the pathfinding iteration loop.
#
# Usage:
#   .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "29,40" -Variant climb-baseline
#   .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "29,40;28,40" -Variant slope-55 -DataDir D:\wwow-bot\test-data
#   .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "29,40" -Variant test -SkipBake   # snapshot only
#
# Args:
#   -Map        (int, required)  Map ID (1=Eastern Kingdoms)
#   -Tiles      (string, required) Semicolon-separated "X,Y;X,Y;..."
#   -Variant    (string, required) Experiment label; output dir
#                                  tmp\bake-sweeps\<variant>-<UTC>\
#   -DataDir    (string, optional) Override target data dir. Default
#                                  $env:WWOW_TEST_DATA_DIR or D:\wwow-bot\test-data
#   -ConfigPath (string, optional) Override path to config.json
#   -OffmeshPath (string, optional) Override path to offmesh.txt
#   -SkipBake   (switch) Snapshot + write effective config only; skip MmapGen
#   -Quiet      (switch) Suppress per-tile progress chatter
#
# Output:
#   tmp\bake-sweeps\<variant>-<UTC>\
#     snapshot\<map:03d><tileY:02d><tileX:02d>.mmtile.bak (one per affected tile)
#     config-effective.json   (a copy of the config used for this bake)
#     offmesh-effective.txt   (a copy of the offmesh.txt used)
#     bake-report.json        (machine-readable: tile sizes/mtimes before+after,
#                              MmapGen exit code, elapsed seconds)
#     bake.log                (raw MmapGen stdout/stderr)
#
# Notes:
#   - The mmtile filename convention is <map:03d><tileY:02d><tileX:02d>.mmtile.
#     Order is mapId first, then tileY, then tileX (NOT tileX before tileY).
#     See memory/project_pathfinding_tile_coords.md.
#   - MmapGen.exe --silent does NOT skip the interactive thread-count prompt;
#     we always pass --threads 1 explicitly.
#   - The PathfindingService caches loaded tiles at startup. After a bake you
#     must restart the service (Docker prod) or relaunch the test fixture.
#   - For Docker prod-data, run promote-mmaps.ps1 then docker restart afterward.
#
# PFS-OVERHAUL-006 -- iteration loop step 1.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [int]$Map,
    [Parameter(Mandatory)] [string]$Tiles,
    [Parameter(Mandatory)] [string]$Variant,
    [string]$DataDir,
    [string]$ConfigPath,
    [string]$OffmeshPath,
    [switch]$SkipBake,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# --- Resolve paths ---
# $PSScriptRoot is .../Westworld of Warcraft/tools/scripts; parent.parent
# is the WWoW project root (Westworld of Warcraft/).
$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

$mmapGenExe = Join-Path $wwowRoot 'tools\MmapGen\build\MmapGen.exe'
if (-not (Test-Path $mmapGenExe)) {
    throw "MmapGen.exe not found at $mmapGenExe. Run tools\MmapGen\build-mmapgen.ps1 first."
}

if (-not $DataDir) {
    $DataDir = $env:WWOW_TEST_DATA_DIR
    if (-not $DataDir) { $DataDir = 'D:\wwow-bot\test-data' }
}
$mmapsDir = Join-Path $DataDir 'mmaps'
if (-not (Test-Path $mmapsDir)) {
    throw "mmaps dir not found: $mmapsDir. Set up test-data per docs\physics\MMAP_DATA_FLOW.md."
}

if (-not $ConfigPath)  { $ConfigPath  = Join-Path $wwowRoot 'tools\MmapGen\config.json' }
if (-not $OffmeshPath) { $OffmeshPath = Join-Path $wwowRoot 'tools\MmapGen\offmesh.txt' }
if (-not (Test-Path $ConfigPath))  { throw "config.json not found at $ConfigPath" }
if (-not (Test-Path $OffmeshPath)) { Write-Host "WARNING: offmesh.txt not found at $OffmeshPath -- continuing without it" -ForegroundColor Yellow; $OffmeshPath = $null }

# --- Parse tiles ---
$tilePairs = @()
foreach ($pair in ($Tiles -split ';')) {
    $xy = $pair.Trim() -split ','
    if ($xy.Count -ne 2) { throw "Invalid -Tiles entry '$pair' -- expected 'X,Y'" }
    $tilePairs += ,@([int]$xy[0], [int]$xy[1])
}

# --- Variant output dir ---
$ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$variantDirName = "$Variant-$ts"
$outDir = Join-Path $wwowRoot "tmp\bake-sweeps\$variantDirName"
$snapshotDir = Join-Path $outDir 'snapshot'
New-Item -ItemType Directory -Force -Path $snapshotDir | Out-Null

if (-not $Quiet) {
    Write-Host "[bake-tile] variant=$Variant ts=$ts dataDir=$DataDir" -ForegroundColor Cyan
    Write-Host "[bake-tile] outDir=$outDir" -ForegroundColor Cyan
    $tilesDisplay = ($tilePairs | ForEach-Object { "($($_[0]),$($_[1]))" }) -join ' '
    Write-Host "[bake-tile] tiles: $tilesDisplay" -ForegroundColor Cyan
}

# --- Snapshot the affected tile files BEFORE bake ---
$tileEntries = @()
foreach ($t in $tilePairs) {
    $tileX = $t[0]; $tileY = $t[1]
    $name = "{0:D3}{1:D2}{2:D2}.mmtile" -f $Map, $tileY, $tileX
    $srcPath = Join-Path $mmapsDir $name
    $bakPath = Join-Path $snapshotDir "$name.bak"
    $beforeLen = $null; $beforeMtime = $null
    if (Test-Path $srcPath) {
        $info = Get-Item $srcPath
        Copy-Item $srcPath $bakPath -Force
        $beforeLen = $info.Length
        $beforeMtime = $info.LastWriteTimeUtc.ToString('o')
        if (-not $Quiet) { Write-Host "[bake-tile] snapshot ($tileX,$tileY) $name $beforeLen bytes" -ForegroundColor DarkGray }
    } else {
        if (-not $Quiet) { Write-Host "[bake-tile] snapshot ($tileX,$tileY) $name MISSING (will be created)" -ForegroundColor Yellow }
    }
    $tileEntries += [pscustomobject]@{
        tileX = $tileX
        tileY = $tileY
        fileName = $name
        srcPath = $srcPath
        bakPath = $bakPath
        beforeLen = $beforeLen
        beforeMtime = $beforeMtime
        afterLen = $null
        afterMtime = $null
        deltaBytes = $null
    }
}

# --- Snapshot the effective config + offmesh ---
$configEffective = Join-Path $outDir 'config-effective.json'
Copy-Item $ConfigPath $configEffective -Force
if ($OffmeshPath) {
    $offmeshEffective = Join-Path $outDir 'offmesh-effective.txt'
    Copy-Item $OffmeshPath $offmeshEffective -Force
}

if ($SkipBake) {
    Write-Host "[bake-tile] -SkipBake set; snapshot complete, no MmapGen invocation." -ForegroundColor Yellow
    $report = [pscustomobject]@{
        variant = $Variant
        timestamp = $ts
        dataDir = $DataDir
        configPath = $ConfigPath
        offmeshPath = $OffmeshPath
        map = $Map
        skipBake = $true
        tiles = $tileEntries
        bakeExitCode = $null
        bakeElapsedSeconds = $null
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $outDir 'bake-report.json') -Encoding utf8
    Write-Host "[bake-tile] outDir=$outDir" -ForegroundColor Green
    return $outDir
}

# --- Run MmapGen.exe ---
$logPath = Join-Path $outDir 'bake.log'
$argsList = @($Map.ToString(), '--silent', '--threads', '1', '--configInputPath', $ConfigPath)
if ($OffmeshPath) {
    $argsList += @('--offMeshInput', $OffmeshPath)
}
foreach ($t in $tilePairs) {
    $argsList += @('--tile', "$($t[0]),$($t[1])")
}

if (-not $Quiet) {
    Write-Host "[bake-tile] running: $mmapGenExe $($argsList -join ' ')" -ForegroundColor Cyan
    Write-Host "[bake-tile] cwd=$DataDir log=$logPath" -ForegroundColor Cyan
}

$swStart = Get-Date
$prevLocation = Get-Location
Set-Location $DataDir
try {
    # Tee to log file AND console, but Out-Host so the script's return ($outDir)
    # isn't polluted by MmapGen's stdout streaming through the pipeline.
    & $mmapGenExe @argsList *>&1 | Tee-Object -FilePath $logPath | Out-Host
    $exitCode = $LASTEXITCODE
} finally {
    Set-Location $prevLocation
}
$elapsed = ((Get-Date) - $swStart).TotalSeconds

# --- Capture after-bake state ---
foreach ($entry in $tileEntries) {
    if (Test-Path $entry.srcPath) {
        $info = Get-Item $entry.srcPath
        $entry.afterLen = $info.Length
        $entry.afterMtime = $info.LastWriteTimeUtc.ToString('o')
        if ($null -ne $entry.beforeLen) {
            $entry.deltaBytes = $entry.afterLen - $entry.beforeLen
        }
    }
}

$report = [pscustomobject]@{
    variant = $Variant
    timestamp = $ts
    dataDir = $DataDir
    configPath = $ConfigPath
    offmeshPath = $OffmeshPath
    map = $Map
    skipBake = $false
    bakeExitCode = $exitCode
    bakeElapsedSeconds = [math]::Round($elapsed, 2)
    tiles = $tileEntries
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $outDir 'bake-report.json') -Encoding utf8

if ($exitCode -eq 0) {
    $analysisDir = Join-Path $outDir 'analysis'
    New-Item -ItemType Directory -Force -Path $analysisDir | Out-Null
    $navDataAuditProj = Join-Path $wwowRoot 'tools\NavDataAudit\NavDataAudit.csproj'

    foreach ($entry in $tileEntries) {
        $manifestStem = "map{0:D3}{1:D2}{2:D2}" -f $Map, $entry.tileY, $entry.tileX
        $manifestSource = Join-Path $DataDir ("meshes\{0}_anchor_stage_manifest.json" -f $manifestStem)
        if (-not (Test-Path $manifestSource)) {
            continue
        }

        $manifestDest = Join-Path $analysisDir ("{0}_anchor_stage_manifest.json" -f $manifestStem)
        Copy-Item $manifestSource $manifestDest -Force

        foreach ($suffix in @('stage_heightfield_spans.csv', 'stage_compact_spans.csv', 'stage_contours.csv')) {
            $stageSource = Join-Path $DataDir ("meshes\{0}_{1}" -f $manifestStem, $suffix)
            if (Test-Path $stageSource) {
                Copy-Item $stageSource (Join-Path $analysisDir ("{0}_{1}" -f $manifestStem, $suffix)) -Force
            }
        }

        $summaryJson = Join-Path $analysisDir ("{0}_anchor_stage_summary.json" -f $manifestStem)
        $summaryCsv = Join-Path $analysisDir ("{0}_anchor_stage_summary.csv" -f $manifestStem)
        if (-not $Quiet) {
            Write-Host "[bake-tile] summarizing anchor stage manifest $manifestDest" -ForegroundColor Cyan
        }

        & dotnet run --project $navDataAuditProj --configuration Release --no-restore -- `
            --stage-summary-only `
            --stage-manifest $manifestDest `
            --write-stage-summary $summaryJson `
            --write-stage-summary-csv $summaryCsv

        if ($LASTEXITCODE -ne 0) {
            throw "Anchor stage manifest summary failed for $manifestDest"
        }
    }
}

# --- Console summary ---
if (-not $Quiet) {
    Write-Host ""
    $doneColor = if ($exitCode -eq 0) { 'Green' } else { 'Red' }
    Write-Host "[bake-tile] DONE exit=$exitCode elapsed=$([math]::Round($elapsed, 1))s" -ForegroundColor $doneColor
    foreach ($entry in $tileEntries) {
        $delta = if ($null -ne $entry.deltaBytes) { "{0:+#;-#;0} bytes" -f $entry.deltaBytes } else { 'NEW' }
        $beforeStr = if ($null -ne $entry.beforeLen) { $entry.beforeLen } else { '-' }
        $afterStr  = if ($null -ne $entry.afterLen)  { $entry.afterLen  } else { 'MISSING' }
        Write-Host ("  ({0},{1}) {2}: before={3} after={4} delta={5}" -f `
            $entry.tileX, $entry.tileY, $entry.fileName, $beforeStr, $afterStr, $delta) -ForegroundColor DarkGray
    }
    Write-Host "[bake-tile] outDir=$outDir" -ForegroundColor Green
}

if ($exitCode -ne 0) {
    Write-Host "[bake-tile] MmapGen FAILED -- snapshot is in $snapshotDir; restore via tools\scripts\restore-mmaps.ps1 -Variant $variantDirName" -ForegroundColor Red
    exit $exitCode
}

return $outDir
