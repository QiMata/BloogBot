# run-pathfinding-tests.ps1 -- uniform pathfinding test runner. Sets all the
# env vars consistently so iterations don't drift, captures trx + console log,
# returns pass/fail summary.
#
# Usage:
#   .\tools\scripts\run-pathfinding-tests.ps1 -TestSet unit
#   .\tools\scripts\run-pathfinding-tests.ps1 -TestSet climb -OutDir tmp\bake-sweeps\<variant>
#   .\tools\scripts\run-pathfinding-tests.ps1 -TestSet pathfinding -DataDir D:\MaNGOS\data
#   .\tools\scripts\run-pathfinding-tests.ps1 -TestSet all
#
# Test sets:
#   unit           -- the 213+7 baseline filter (NavigationPath, NavigationPathFactory,
#                     TransportWaitingLogic, TravelTask, CrossMapRouter)
#   physics        -- Navigation.Physics.Tests smoke (DllAvailability, DetourCompat,
#                     PhysicsConstants -- 19 tests, ~60ms total)
#   pathfinding    -- Tests/PathfindingService.Tests Category!=RequiresInfrastructure
#                     (offmesh, tile-loader, route diagnostics)
#   climb          -- the Phase 5.3.6 OG zeppelin tower climb sub-test
#   all            -- unit + physics + pathfinding (no climb -- climb is live)
#
# Args:
#   -TestSet   (string, required) See above.
#   -DataDir   (string, optional) WWOW_DATA_DIR override. Default
#              $env:WWOW_DATA_DIR or D:\MaNGOS\data (the climb-iteration
#              fallback per docs\physics\MMAP_DATA_FLOW.md).
#   -OutDir    (string, optional) Where to drop trx + console.log.
#              Default tmp\test-runs\<UTC>\.
#   -ExtraEnv  (hashtable, optional) Additional env vars to set, e.g.
#              @{WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='5'}.
#   -Filter    (string, optional) Override the default test filter for the set.
#   -BudgetMs  (int, optional) RunConfiguration.TestSessionTimeout.
#              Default 600000ms (10min). Use longer for full long-pathing.
#
# Output:
#   <OutDir>\<testset>-<UTC>.trx
#   <OutDir>\<testset>-<UTC>.console.log
#   <OutDir>\test-summary.json   (one entry per run)
#
# Exit code: 0 = tests passed; non-zero = at least one test failed.
#
# PFS-OVERHAUL-006 -- iteration loop step 3.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('unit','physics','pathfinding','climb','all')] [string]$TestSet,
    [string]$DataDir,
    [string]$OutDir,
    [hashtable]$ExtraEnv,
    [string]$Filter,
    [int]$BudgetMs = 600000
)

$ErrorActionPreference = 'Stop'

$wwowRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

if (-not $DataDir) {
    $DataDir = $env:WWOW_DATA_DIR
    if (-not $DataDir) { $DataDir = 'D:\MaNGOS\data' }
}
foreach ($sub in 'mmaps','maps','vmaps') {
    if (-not (Test-Path (Join-Path $DataDir $sub))) {
        throw "DataDir '$DataDir' missing $sub/ subdir; strict gate will FATAL. See docs\physics\MMAP_DATA_FLOW.md."
    }
}

if (-not $OutDir) {
    $ts0 = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $OutDir = Join-Path $wwowRoot "tmp\test-runs\$ts0"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# --- Resolve project + filter for the test set ---
$runs = @()
switch ($TestSet) {
    'unit' {
        $runs += [pscustomobject]@{
            label = 'unit'
            project = Join-Path $wwowRoot 'Tests\BotRunner.Tests\BotRunner.Tests.csproj'
            filter = if ($Filter) { $Filter } else {
                'FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~CrossMapRouterTests'
            }
        }
    }
    'physics' {
        $runs += [pscustomobject]@{
            label = 'physics'
            project = Join-Path $wwowRoot 'Tests\Navigation.Physics.Tests\Navigation.Physics.Tests.csproj'
            filter = if ($Filter) { $Filter } else { 'FullyQualifiedName~DllAvailabilityTests|FullyQualifiedName~DetourCompatibilityTests|FullyQualifiedName~PhysicsConstantsValidationTests' }
        }
    }
    'pathfinding' {
        $runs += [pscustomobject]@{
            label = 'pathfinding'
            project = Join-Path $wwowRoot 'Tests\PathfindingService.Tests\PathfindingService.Tests.csproj'
            filter = if ($Filter) { $Filter } else { 'Category!=RequiresInfrastructure' }
        }
    }
    'climb' {
        $runs += [pscustomobject]@{
            label = 'climb'
            project = Join-Path $wwowRoot 'Tests\BotRunner.Tests\BotRunner.Tests.csproj'
            filter = if ($Filter) { $Filter } else { 'FullyQualifiedName~LongPathingTests.ClimbOrgrimmarZeppelinTowerRampToFrezza' }
        }
    }
    'all' {
        $runs += [pscustomobject]@{ label='unit'; project = Join-Path $wwowRoot 'Tests\BotRunner.Tests\BotRunner.Tests.csproj'; filter = 'FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~CrossMapRouterTests' }
        $runs += [pscustomobject]@{ label='physics'; project = Join-Path $wwowRoot 'Tests\Navigation.Physics.Tests\Navigation.Physics.Tests.csproj'; filter = 'FullyQualifiedName~DllAvailabilityTests|FullyQualifiedName~DetourCompatibilityTests|FullyQualifiedName~PhysicsConstantsValidationTests' }
        $runs += [pscustomobject]@{ label='pathfinding'; project = Join-Path $wwowRoot 'Tests\PathfindingService.Tests\PathfindingService.Tests.csproj'; filter = 'Category!=RequiresInfrastructure' }
    }
}

# --- Set env vars ---
$envSnapshot = @{}
$prevValues = @{}
$envSnapshot['WWOW_DATA_DIR'] = $DataDir
if ($TestSet -eq 'climb') {
    # Mirror the handoff command's env vars for the climb sub-test.
    $envSnapshot['WWOW_TEST_DATA_DIR'] = $DataDir
    $envSnapshot['WWOW_TEST_PRESERVE_EXISTING_PATHFINDING'] = '1'
    $envSnapshot['WWOW_OFFMESH_NATIVE_BOARDING'] = '1'
    $envSnapshot['WWOW_OG_RAMP_CLIMB_TEST'] = '1'
    $envSnapshot['WWOW_LONG_PATHING_TIMELINE'] = '1'
    $envSnapshot['WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS'] = '5'
    $envSnapshot['VMAP_PHYS_LOG_MASK'] = '0'
    $envSnapshot['VMAP_PHYS_LOG_LEVEL'] = '0'
    $envSnapshot['WWOW_USE_LOCAL_PATHFINDING_SERVICE'] = '1'
    $envSnapshot['WWOW_TEST_PATHFINDING_PORT'] = '5101'
}
if ($ExtraEnv) {
    foreach ($k in $ExtraEnv.Keys) { $envSnapshot[$k] = [string]$ExtraEnv[$k] }
}

foreach ($k in $envSnapshot.Keys) {
    $prevValues[$k] = [Environment]::GetEnvironmentVariable($k)
    [Environment]::SetEnvironmentVariable($k, $envSnapshot[$k], 'Process')
}

$summary = @()
$overallExit = 0
try {
    foreach ($run in $runs) {
        $ts = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
        $logName = "$($run.label)-$ts.trx"
        $consoleLog = Join-Path $OutDir "$($run.label)-$ts.console.log"
        Write-Host ""
        Write-Host "[run-pathfinding-tests] $($run.label) project=$($run.project)" -ForegroundColor Cyan
        Write-Host "[run-pathfinding-tests] filter=$($run.filter) WWOW_DATA_DIR=$DataDir" -ForegroundColor Cyan

        $swStart = Get-Date
        # PowerShell 5.1 wraps native-cmd stderr in ErrorRecord (NativeCommandError),
        # which under EAP='Stop' aborts the script BEFORE the summary is written.
        # Failed tests stream xUnit "[FAIL]" notes to stderr; they aren't fatal.
        $savedEAP = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & dotnet test $run.project `
                --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false `
                --filter $run.filter `
                --logger "trx;LogFileName=$logName" `
                --results-directory $OutDir `
                -- "RunConfiguration.TestSessionTimeout=$BudgetMs" 2>&1
            $exit = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $savedEAP
        }
        $elapsed = ((Get-Date) - $swStart).TotalSeconds
        $output | Set-Content -Path $consoleLog -Encoding utf8

        # Parse the dotnet test "Failed: N, Passed: N, Skipped: N" line
        $passed = $null; $failed = $null; $skipped = $null
        $sumLine = $output | Where-Object { $_ -match 'Failed:\s+\d+,\s+Passed:\s+\d+,\s+Skipped:\s+\d+' } | Select-Object -First 1
        if ($sumLine) {
            if ($sumLine -match 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)') {
                $failed = [int]$matches[1]; $passed = [int]$matches[2]; $skipped = [int]$matches[3]
            }
        }

        $color = if ($exit -eq 0) { 'Green' } else { 'Red' }
        Write-Host ("[run-pathfinding-tests] {0} -> exit={1} passed={2} failed={3} skipped={4} elapsed={5}s" -f $run.label, $exit, $passed, $failed, $skipped, [math]::Round($elapsed,1)) -ForegroundColor $color

        $summary += [pscustomobject]@{
            label = $run.label
            project = $run.project
            filter = $run.filter
            trx = Join-Path $OutDir $logName
            consoleLog = $consoleLog
            exit = $exit
            passed = $passed
            failed = $failed
            skipped = $skipped
            elapsedSeconds = [math]::Round($elapsed, 2)
        }
        if ($exit -ne 0) { $overallExit = $exit }
    }
} finally {
    foreach ($k in $prevValues.Keys) {
        [Environment]::SetEnvironmentVariable($k, $prevValues[$k], 'Process')
    }
}

$reportObj = [pscustomobject]@{
    schemaVersion = 1
    timestamp = [DateTime]::UtcNow.ToString('o')
    testSet = $TestSet
    dataDir = $DataDir
    runs = $summary
    overallExit = $overallExit
}
$reportPath = Join-Path $OutDir 'test-summary.json'
$reportObj | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding utf8

Write-Host ""
$doneColor = if ($overallExit -eq 0) { 'Green' } else { 'Red' }
Write-Host ("[run-pathfinding-tests] DONE -> {0}" -f $reportPath) -ForegroundColor $doneColor
exit $overallExit
