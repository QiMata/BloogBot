# Phase 0 D2 — All-tiles rough sweep launcher.
#
# Iterates all .mmtile files for a given map ID and runs
# NavMeshPhysicsValidator on each at low --samples (rough sweep).
# Writes one JSON per tile to tmp/iter-overhaul-phase0/sweep-map<M>/.
# Idempotent: skips tiles whose output JSON already exists, so the
# script can be interrupted (Ctrl+C) and resumed without losing work.
#
# Usage:
#   pwsh tools/scripts/phase0-sweep-map.ps1 -MapId 1
#   pwsh tools/scripts/phase0-sweep-map.ps1 -MapId 1 -Samples 5 -MaxTiles 50
#
# Background invocation (for /loop iter-3):
#   Start-Process -FilePath pwsh -ArgumentList '-File','tools/scripts/phase0-sweep-map.ps1','-MapId','1'

param(
    [Parameter(Mandatory = $true)] [int]$MapId,
    [int]$Samples = 5,
    [int]$MaxTiles = 0,                                                   # 0 = all
    [string]$DataDir = 'D:\MaNGOS\data',
    [string]$ValidatorExe = 'e:\repos\Westworld of Warcraft\Bot\Release\net8.0\NavMeshPhysicsValidator.exe',
    [string]$OutRoot = 'e:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0'
)

$ErrorActionPreference = 'Stop'
$env:WWOW_DATA_DIR = $DataDir

$mapPad = '{0:D3}' -f $MapId
$mmapsDir = Join-Path $DataDir 'mmaps'
$outDir = Join-Path $OutRoot ('sweep-map' + $MapId)
$logFile = Join-Path $OutRoot ('sweep-map' + $MapId + '.log')

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if (-not (Test-Path $mmapsDir)) { throw "mmaps dir not found: $mmapsDir" }
if (-not (Test-Path $ValidatorExe)) { throw "validator exe not found: $ValidatorExe" }

function Log([string]$msg) {
    $line = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK'), $msg
    Add-Content -Path $logFile -Value $line -Encoding utf8
    Write-Host $line
}

# Enumerate tiles. Filename pattern MMMYYXX.mmtile = map MMM, tileY YY, tileX XX.
$tilePattern = '^' + $mapPad + '(\d{2})(\d{2})\.mmtile$'
$tiles = @(Get-ChildItem -Path $mmapsDir -Filter ($mapPad + '*.mmtile') |
    Where-Object { $_.Name -match $tilePattern } |
    ForEach-Object {
        $null = $_.Name -match $tilePattern
        [pscustomobject]@{
            FileName = $_.Name
            TileY    = [int]$Matches[1]
            TileX    = [int]$Matches[2]
        }
    } |
    Sort-Object TileY, TileX)

if ($MaxTiles -gt 0) { $tiles = @($tiles | Select-Object -First $MaxTiles) }

Log ('# phase0-sweep-map start map={0} tiles={1} samples={2} outDir={3}' -f $MapId, $tiles.Count, $Samples, $outDir)

$startedAt = Get-Date
$completed = 0
$skipped = 0
$errored = 0

foreach ($t in $tiles) {
    $outJson = Join-Path $outDir ('tile-{0}-{1}.json' -f $t.TileX, $t.TileY)
    if (Test-Path $outJson) {
        $skipped++
        continue
    }

    $tileStart = Get-Date
    # Native command output: send stdout to $null. DO NOT use 2>&1 — PS 5.1
    # wraps native stderr as ErrorRecord which trips $ErrorActionPreference=Stop.
    # The validator's per-tile stderr is verbose ([OFFLINK] lines) and useful
    # only when a specific tile needs debugging; for the bulk sweep we drop it.
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $ValidatorExe $MapId --tile ('{0},{1}' -f $t.TileX, $t.TileY) --samples $Samples --silent --out $outJson 2>$null | Out-Null
        $code = $LASTEXITCODE
        $dur = ((Get-Date) - $tileStart).TotalSeconds
        if ($code -ge 0 -and (Test-Path $outJson)) {
            $completed++
            $elapsed = ((Get-Date) - $startedAt).TotalMinutes
            $eta = if ($completed -gt 0) { ($elapsed / $completed) * ($tiles.Count - $completed - $skipped) } else { 0 }
            Log ('  tile=({0},{1}) exit={2} dur={3:F1}s   progress={4}/{5} skipped={6} errored={7} elapsedMin={8:F1} etaMin={9:F1}' -f `
                $t.TileX, $t.TileY, $code, $dur, $completed, $tiles.Count, $skipped, $errored, $elapsed, $eta)
        }
        else {
            $errored++
            Log ('  tile=({0},{1}) exit={2} dur={3:F1}s NO_OUTPUT' -f $t.TileX, $t.TileY, $code, $dur)
        }
    }
    catch {
        $errored++
        Log ('  tile=({0},{1}) FAILED: {2}' -f $t.TileX, $t.TileY, $_.Exception.Message)
    }
    finally {
        $ErrorActionPreference = $prevEAP
    }
}

Log ('# phase0-sweep-map done map={0} completed={1} skipped={2} errored={3} totalMin={4:F1}' -f `
    $MapId, $completed, $skipped, $errored, ((Get-Date) - $startedAt).TotalMinutes)
