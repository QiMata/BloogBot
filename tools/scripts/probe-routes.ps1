# probe-routes.ps1 -- run PathPhysicsProbe.exe over a named route manifest.
# Part of the pathfinding iteration loop.
#
# Usage:
#   .\tools\scripts\probe-routes.ps1 -Manifest tools\scripts\routes\og-zeppelin.json
#   .\tools\scripts\probe-routes.ps1 -Manifest ... -Variant climb-baseline-... -OutDir tmp\bake-sweeps\<variant>
#   .\tools\scripts\probe-routes.ps1 -Manifest ... -Routes "ClimbOrgrimmarTowerToFrezza,FlightMasterDescentControl"
#   .\tools\scripts\probe-routes.ps1 -Manifest ... -DetourResolve
#
# Args:
#   -Manifest        (string, required) Path to a route-manifest JSON. See
#                    tools/scripts/routes/og-zeppelin.json for the schema.
#   -Variant         (string, optional) Bake-sweep variant tag, used as part of
#                    the default OutDir.
#   -OutDir          (string, optional) Where to write probe-results.json +
#                    per-route JSON. Default tmp\bake-sweeps\<variant>\
#                    OR tmp\probe-runs\<UTC>\ if no variant given.
#   -DataDir         (string, optional) WWOW_DATA_DIR override for PathPhysicsProbe.
#                    Default $env:WWOW_TEST_DATA_DIR or D:\wwow-bot\test-data.
#   -Routes          (string, optional) Comma-separated route names; if omitted,
#                    runs every route in the manifest.
#   -DetourResolve   (switch) Pass --detour-resolve to PathPhysicsProbe so each
#                    route's start/end is fed through FindPath first; the probe
#                    then classifies the resolved corner sequence.
#   -SmoothPath      (switch) With -DetourResolve, also pass --smooth.
#   -Verbose         (switch) Pass --verbose to PathPhysicsProbe.
#   -Quiet           (switch) Suppress per-route progress chatter.
#
# Output:
#   <OutDir>\probe-results.json    -- aggregate: {variant, manifest, routes:[{name, exitCode, segments, firstFailure, ...}]}
#   <OutDir>\probe-<route>.json    -- raw PathPhysicsProbe --json output for one route
#   <OutDir>\probe-<route>.stderr  -- the probe's diagnostic stderr (firstFailure narration)
#
# PFS-OVERHAUL-006 -- iteration loop step 2.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Manifest,
    [string]$Variant,
    [string]$OutDir,
    [string]$DataDir,
    [string]$Routes,
    [switch]$DetourResolve,
    [switch]$SmoothPath,
    [switch]$ProbeVerbose,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

$probeExe = Join-Path $wwowRoot 'Bot\Release\net8.0\PathPhysicsProbe.exe'
if (-not (Test-Path $probeExe)) {
    throw "PathPhysicsProbe.exe not found at $probeExe. Build the WWoW solution first (dotnet build WestworldOfWarcraft.sln)."
}

if (-not (Test-Path $Manifest)) {
    throw "Route manifest not found: $Manifest"
}
$manifestData = Get-Content $Manifest -Raw | ConvertFrom-Json
if (-not $manifestData.routes) {
    throw "Route manifest has no 'routes' array: $Manifest"
}

if (-not $DataDir) {
    $DataDir = $env:WWOW_TEST_DATA_DIR
    if (-not $DataDir) { $DataDir = 'D:\wwow-bot\test-data' }
}
foreach ($sub in 'mmaps','maps','vmaps') {
    if (-not (Test-Path (Join-Path $DataDir $sub))) {
        throw "DataDir '$DataDir' missing $sub/ subdir; strict gate will FATAL. See docs\physics\MMAP_DATA_FLOW.md."
    }
}

if (-not $OutDir) {
    if ($Variant) {
        $OutDir = Join-Path $wwowRoot "tmp\bake-sweeps\$Variant"
    } else {
        $ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
        $OutDir = Join-Path $wwowRoot "tmp\probe-runs\$ts"
    }
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Filter route list
$routeFilter = $null
if ($Routes) {
    $routeFilter = @($Routes -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

if (-not $Quiet) {
    Write-Host "[probe-routes] manifest=$Manifest" -ForegroundColor Cyan
    Write-Host "[probe-routes] dataDir=$DataDir outDir=$OutDir" -ForegroundColor Cyan
    if ($DetourResolve) { Write-Host "[probe-routes] mode=detour-resolve smooth=$SmoothPath" -ForegroundColor Cyan }
}

# --- Run probes ---
$aggregate = @()
$prevDataDir       = $env:WWOW_DATA_DIR
$prevPhysMask      = $env:VMAP_PHYS_LOG_MASK
$prevPhysLevel     = $env:VMAP_PHYS_LOG_LEVEL
$env:WWOW_DATA_DIR = $DataDir
# Silence VMAP_PHYS init banner -- it goes to stdout and corrupts the JSON
# parse otherwise. Default gPhysLogMask=0xffffffff prints "[PHYS][INIT]" even
# at level=0.
$env:VMAP_PHYS_LOG_MASK  = '0'
$env:VMAP_PHYS_LOG_LEVEL = '0'
try {
    foreach ($route in $manifestData.routes) {
        if ($routeFilter -and ($route.name -notin $routeFilter)) { continue }

        $startStr = "{0},{1},{2}" -f $route.start[0], $route.start[1], $route.start[2]
        $endStr   = "{0},{1},{2}" -f $route.end[0],   $route.end[1],   $route.end[2]
        $argsList = @('--map', $route.map, '--start', $startStr, '--end', $endStr, '--json')
        if ($DetourResolve) { $argsList += '--detour-resolve' }
        if ($SmoothPath)    { $argsList += '--smooth' }
        if ($ProbeVerbose)  { $argsList += '--verbose' }
        if ($manifestData.agent.radius) { $argsList += @('--radius', $manifestData.agent.radius) }
        if ($manifestData.agent.height) { $argsList += @('--height', $manifestData.agent.height) }

        $stdoutPath = Join-Path $OutDir ("probe-{0}.json" -f $route.name)
        $stderrPath = Join-Path $OutDir ("probe-{0}.stderr" -f $route.name)
        if (-not $Quiet) { Write-Host ("[probe-routes] {0}: map={1} {2} -> {3}" -f $route.name, $route.map, $startStr, $endStr) -ForegroundColor DarkGray }

        $swStart = Get-Date
        # PowerShell 5.1 wraps native-cmd stderr in ErrorRecord (NativeCommandError),
        # which under EAP='Stop' aborts the script. Lower EAP just for the call.
        $savedEAP = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & $probeExe @argsList 1>$stdoutPath 2>$stderrPath
            $exit = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $savedEAP
        }
        $elapsed = ((Get-Date) - $swStart).TotalSeconds

        $segCount = $null; $firstFail = $null; $firstFailAffordance = $null
        try {
            if ((Test-Path $stdoutPath) -and ((Get-Item $stdoutPath).Length -gt 0)) {
                $rawText = [System.IO.File]::ReadAllText($stdoutPath)
                # Strip non-JSON prefix AND suffix. Navigation.dll printfs
                # ("[PHYS][INIT]...", "[DynObjReg] Loaded ...") leak into
                # stdout both BEFORE the JSON (init banners) and AFTER it
                # (DynamicObjectRegistry::LoadDisplayIdMapping prints after
                # InitializeAllSystems' Navigation::Initialize section). Slice
                # on first '{' to last '}' so the parser only sees the object.
                $jsonStart = $rawText.IndexOf('{')
                $jsonEnd   = $rawText.LastIndexOf('}')
                if ($jsonStart -ge 0 -and $jsonEnd -gt $jsonStart) {
                    $jsonText = $rawText.Substring($jsonStart, $jsonEnd - $jsonStart + 1)
                    $probeJson = $jsonText | ConvertFrom-Json
                    $segCount = $probeJson.segmentCount
                    $firstFail = $probeJson.firstFailure
                    if ($null -ne $firstFail -and $firstFail -ge 0 -and $probeJson.segments) {
                        $firstFailAffordance = $probeJson.segments[$firstFail].affordance
                    }
                }
            }
        } catch {
            Write-Host "[probe-routes] WARNING: failed to parse $stdoutPath -- $($_.Exception.Message)" -ForegroundColor Yellow
        }

        $aggregate += [pscustomobject]@{
            name            = $route.name
            map             = $route.map
            start           = $route.start
            end             = $route.end
            exitCode        = $exit
            elapsedSeconds  = [math]::Round($elapsed, 3)
            segmentCount    = $segCount
            firstFailure    = $firstFail
            firstFailAffordance = $firstFailAffordance
            stdoutPath      = $stdoutPath
            stderrPath      = $stderrPath
            expected        = [pscustomobject]@{
                corners       = $route.expectedCorners
                affordance    = $route.expectedAffordance
                stallCoord    = $route.expectedStallCoord
                stallReason   = $route.expectedStallReason
            }
        }

        if (-not $Quiet) {
            $color = switch ($exit) { 0 { 'Green' } 1 { 'Yellow' } 2 { 'Red' } default { 'Red' } }
            $hint = switch ($exit) { 0 { 'clean Walk' } 1 { 'StepUp/JumpGap/SafeDrop' } 2 { 'Blocked/UnsafeDrop' } default { 'arg/init failure' } }
            Write-Host ("  -> exit={0} ({1}) segments={2} firstFail={3} elapsed={4}s" -f $exit, $hint, $segCount, $firstFail, [math]::Round($elapsed, 2)) -ForegroundColor $color
        }
    }
} finally {
    if ($null -ne $prevDataDir)   { $env:WWOW_DATA_DIR = $prevDataDir }       else { Remove-Item Env:WWOW_DATA_DIR -ErrorAction SilentlyContinue }
    if ($null -ne $prevPhysMask)  { $env:VMAP_PHYS_LOG_MASK = $prevPhysMask } else { Remove-Item Env:VMAP_PHYS_LOG_MASK -ErrorAction SilentlyContinue }
    if ($null -ne $prevPhysLevel) { $env:VMAP_PHYS_LOG_LEVEL = $prevPhysLevel } else { Remove-Item Env:VMAP_PHYS_LOG_LEVEL -ErrorAction SilentlyContinue }
}

$report = [pscustomobject]@{
    schemaVersion   = 1
    timestamp       = [DateTime]::UtcNow.ToString('o')
    manifest        = $Manifest
    dataDir         = $DataDir
    detourResolve   = [bool]$DetourResolve
    smoothPath      = [bool]$SmoothPath
    routes          = $aggregate
}
$reportPath = Join-Path $OutDir 'probe-results.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8

if (-not $Quiet) {
    Write-Host ""
    Write-Host "[probe-routes] DONE -> $reportPath" -ForegroundColor Green
    $cleanCount  = ($aggregate | Where-Object { $_.exitCode -eq 0 }).Count
    $stepCount   = ($aggregate | Where-Object { $_.exitCode -eq 1 }).Count
    $blockCount  = ($aggregate | Where-Object { $_.exitCode -eq 2 }).Count
    $errCount    = ($aggregate | Where-Object { $_.exitCode -ge 3 }).Count
    Write-Host ("  summary: clean={0} step={1} blocked={2} error={3}" -f $cleanCount, $stepCount, $blockCount, $errCount) -ForegroundColor Cyan
}

return $reportPath
