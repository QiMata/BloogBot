<#
.SYNOPSIS
  Headless live-integration test runner for the WWoW autonomous churn loop.

.DESCRIPTION
  Wrapper that runs a single WWoW live (Category=Integration) test headless
  against an already-up VMaNGOS docker stack. It:
    1. Docker-preflights the 5 stack containers (unless -SkipDockerCheck).
    2. Sets the headless env gates (WWOW_SKIP_SERVER_RESTART, WWOW_DISABLE_UI)
       and points TEMP/results/NuGet at repo-local tmp/ (mirrors run-tests.ps1).
    3. Builds the solution once (unless -NoBuild).
    4. Runs `dotnet test Tests/BotRunner.Tests --filter "FullyQualifiedName~<Filter>"`
       with --blame-hang.
    5. Retries up to -MaxAttempts on a SETUP timeout only (flaky boot/inject),
       never on a real behavioral failure.

  This is the tracker A.2 wrapper. The two env gates are consumed by the
  fixture once tracker A.1 lands; until then they are harmless no-ops.

  See docs/Plan/Autonomous/LIVE_RUNBOOK.md.

.PARAMETER Filter
  FullyQualifiedName substring (a class name or single method).

.EXAMPLE
  pwsh tools/run-live.ps1 -Filter ForegroundLoginSmoke
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Filter,
    [string]$Project = "Tests/BotRunner.Tests",
    [int]$MaxAttempts = 3,
    [int]$TestTimeoutMinutes = 5,
    [switch]$NoBuild,
    [switch]$WithUi,
    [switch]$RestartServers,
    [switch]$SkipDockerCheck
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " run-live.ps1  Filter: $Filter" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---- 1. Docker preflight -------------------------------------------------
$requiredContainers = @("wow-mangosd", "wow-realmd", "maria-db", "wwow-pathfinding", "wwow-scene-data")
if (-not $SkipDockerCheck) {
    Write-Host "Docker preflight..." -ForegroundColor Yellow
    $running = @()
    try {
        $running = docker ps --format "{{.Names}}" 2>$null
    } catch {
        Write-Host "  docker not reachable: $($_.Exception.Message)" -ForegroundColor Red
        exit 2
    }
    $missing = @()
    foreach ($c in $requiredContainers) {
        if ($running -notcontains $c) { $missing += $c }
    }
    if ($missing.Count -gt 0) {
        Write-Host "  MISSING/not-Up containers: $($missing -join ', ')" -ForegroundColor Red
        Write-Host "  Bring the VMaNGOS stack up, then re-run. This script never starts docker." -ForegroundColor Red
        exit 2
    }
    Write-Host "  All 5 stack containers Up." -ForegroundColor Green
}

# ---- 2. Env gates + repo-local temp -------------------------------------
if (-not $RestartServers) { $env:WWOW_SKIP_SERVER_RESTART = "1" }
if (-not $WithUi)         { $env:WWOW_DISABLE_UI = "1" }

$dotnetHome = Join-Path $repoRoot "tmp\dotnethome"
$testRuntimeRoot = Join-Path $repoRoot "tmp\test-runtime"
$testResultsDir = Join-Path $testRuntimeRoot "results"
$testTempDir = Join-Path $testRuntimeRoot "temp"
$nugetDir = Join-Path $repoRoot "tmp\nuget"
foreach ($dir in @($dotnetHome, $testRuntimeRoot, $testResultsDir, $testTempDir, $nugetDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = $nugetDir
$env:VSTEST_RESULTS_DIRECTORY = $testResultsDir
$env:TEMP = $testTempDir
$env:TMP = $testTempDir
$env:WWOW_REPO_ROOT = $repoRoot
$env:WWOW_TEST_RUNTIME_ROOT = $testRuntimeRoot

# ---- 3. Build once -------------------------------------------------------
if (-not $NoBuild) {
    Write-Host "Building WestworldOfWarcraft.sln (Debug)..." -ForegroundColor Yellow
    dotnet build WestworldOfWarcraft.sln --configuration Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded." -ForegroundColor Green
}

# ---- 4. Run with setup-timeout-only retry --------------------------------
# A "setup timeout" (flaky client boot / DLL injection) is retryable; a real
# behavioral failure is not. We detect the retryable mode from the test output
# (SetupTimedOut / "timed out waiting" during fixture setup). Refine the marker
# in tracker A.5 (telemetry/observability) once the failure-reason ledger lands.
$exitCode = 1
for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    Write-Host ""
    Write-Host "---- attempt $attempt / $MaxAttempts ----" -ForegroundColor Cyan
    $logFile = Join-Path $testResultsDir ("run-live-attempt-$attempt.log")

    & dotnet test $Project --no-build `
        --filter "FullyQualifiedName~$Filter" `
        --results-directory $testResultsDir `
        --logger "console;verbosity=normal" `
        --blame-hang --blame-hang-timeout "$($TestTimeoutMinutes)m" 2>&1 |
        Tee-Object -FilePath $logFile
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host "  RESULT: PASS (attempt $attempt)" -ForegroundColor Green
        break
    }

    $logText = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
    $isSetupFlake = $false
    if ($logText) {
        if ($logText -match "SetupTimedOut" -or
            $logText -match "timed out waiting for .*(connect|in.?world|login|inject)" -or
            $logText -match "Setup.*timed out") {
            $isSetupFlake = $true
        }
    }

    if ($isSetupFlake -and $attempt -lt $MaxAttempts) {
        Write-Host "  setup-timeout flake detected; retrying..." -ForegroundColor DarkYellow
        continue
    }

    Write-Host "  RESULT: FAIL (exit $exitCode; not a retryable setup flake)" -ForegroundColor Red
    break
}

# ---- 5. Artifact summary -------------------------------------------------
Write-Host ""
Write-Host "Artifacts:" -ForegroundColor Cyan
Write-Host "  screenshots: $(Join-Path $testRuntimeRoot 'screenshots')"
Write-Host "  traces:      $(Join-Path $testRuntimeRoot 'traces')"
Write-Host "  results:     $testResultsDir"
Write-Host "  R16: read the captured PNG for any Task/Action diagnosis."

exit $exitCode
