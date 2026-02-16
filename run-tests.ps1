# run-tests.ps1 — Runs all tests in dependency order with early bail-out.
#
# Usage:
#   .\run-tests.ps1              # Run all test layers
#   .\run-tests.ps1 -Layer 1     # Run only Layer 1 (DLL availability)
#   .\run-tests.ps1 -Layer 2     # Run only Layer 2 (physics & pathfinding)
#   .\run-tests.ps1 -Layer 3     # Run only Layer 3 (unit tests)
#   .\run-tests.ps1 -Layer 4     # Run only Layer 4 (integration tests)
#   .\run-tests.ps1 -SkipBuild   # Skip the initial solution build

param(
    [int]$Layer = 0,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$script:failCount = 0
$script:passCount = 0
$script:skipCount = 0

function Run-TestLayer {
    param(
        [string]$Name,
        [string]$Project,
        [string]$Filter = "",
        [bool]$StopOnFail = $true
    )

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  $Name" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan

    $args = @("test", $Project, "--no-build", "--logger", "console;verbosity=normal")
    if ($Filter) {
        $args += "--filter"
        $args += $Filter
    }

    & dotnet @args
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host "  PASSED" -ForegroundColor Green
        $script:passCount++
    } else {
        Write-Host "  FAILED (exit code $exitCode)" -ForegroundColor Red
        $script:failCount++
        if ($StopOnFail) {
            Write-Host ""
            Write-Host "Stopping: $Name failed. Fix before proceeding to next layer." -ForegroundColor Red
            return $false
        }
    }
    return $true
}

# ── Build ──────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build WestworldOfWarcraft.sln --configuration Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed! Fix build errors before running tests." -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded." -ForegroundColor Green
}

# ── Layer 1: DLL availability (no services needed) ─────────────────────
if ($Layer -eq 0 -or $Layer -eq 1) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║  LAYER 1: Native DLL Availability                          ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    $ok = Run-TestLayer `
        -Name "Navigation.dll smoke tests" `
        -Project "Tests/Navigation.Physics.Tests" `
        -Filter "Feature=NativeDll"

    if (-not $ok -and $Layer -eq 0) { exit 1 }
}

# ── Layer 2: Physics & Pathfinding (needs Navigation.dll + nav data) ──
if ($Layer -eq 0 -or $Layer -eq 2) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║  LAYER 2: Physics & Pathfinding                            ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    $ok = Run-TestLayer `
        -Name "Physics engine tests" `
        -Project "Tests/Navigation.Physics.Tests" `
        -StopOnFail $false

    $ok = Run-TestLayer `
        -Name "Pathfinding tests" `
        -Project "Tests/PathfindingService.Tests" `
        -StopOnFail $false
}

# ── Layer 3: Unit tests (no services needed) ──────────────────────────
if ($Layer -eq 0 -or $Layer -eq 3) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║  LAYER 3: Unit Tests (no services)                         ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    Run-TestLayer `
        -Name "WoWSharpClient unit tests" `
        -Project "Tests/WoWSharpClient.Tests" `
        -Filter "Category!=Integration" `
        -StopOnFail $false | Out-Null

    # PromptHandlingService tests (if the project exists)
    if (Test-Path "Tests/PromptHandlingService.Tests") {
        Run-TestLayer `
            -Name "PromptHandlingService tests" `
            -Project "Tests/PromptHandlingService.Tests" `
            -StopOnFail $false | Out-Null
    }

    Run-TestLayer `
        -Name "BotRunner unit tests" `
        -Project "Tests/BotRunner.Tests" `
        -Filter "Category!=Integration&RequiresService!=MangosStack&RequiresService!=WoWServer&RequiresService!=AllServices" `
        -StopOnFail $false | Out-Null
}

# ── Layer 4: Integration tests (needs MaNGOS stack) ──────────────────
if ($Layer -eq 0 -or $Layer -eq 4) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║  LAYER 4: Integration Tests (MaNGOS stack auto-launch)     ║" -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    Run-TestLayer `
        -Name "BotRunner integration tests" `
        -Project "Tests/BotRunner.Tests" `
        -Filter "Category=Integration" `
        -StopOnFail $false | Out-Null

    Run-TestLayer `
        -Name "WoWSharpClient integration tests" `
        -Project "Tests/WoWSharpClient.Tests" `
        -Filter "Category=Integration" `
        -StopOnFail $false | Out-Null
}

# ── Summary ───────────────────────────────────────────────────────────
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:passCount)" -ForegroundColor Green
if ($script:failCount -gt 0) {
    Write-Host "  Failed:  $($script:failCount)" -ForegroundColor Red
} else {
    Write-Host "  Failed:  0" -ForegroundColor Green
}
Write-Host ""

if ($script:failCount -gt 0) {
    exit 1
}
exit 0
