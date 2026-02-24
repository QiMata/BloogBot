# Runs test layers in dependency order with shared hang-timeout settings.
#
# Usage:
#   .\run-tests.ps1
#   .\run-tests.ps1 -Layer 1
#   .\run-tests.ps1 -Layer 3 -SkipBuild
#   .\run-tests.ps1 -TestTimeoutMinutes 3

param(
    [int]$Layer = 0,
    [switch]$SkipBuild,
    [int]$TestTimeoutMinutes = 3
)

$ErrorActionPreference = "Stop"
$script:failCount = 0
$script:passCount = 0

$dotnetHome = Join-Path $PSScriptRoot "tmp\dotnethome"
if (-not (Test-Path $dotnetHome)) {
    New-Item -ItemType Directory -Path $dotnetHome -Force | Out-Null
}
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = "0"

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

    $layerStart = Get-Date
    $args = @(
        "test",
        $Project,
        "--no-build",
        "--logger", "console;verbosity=normal",
        "--blame-hang",
        "--blame-hang-timeout", "$($TestTimeoutMinutes)m"
    )

    if ($Filter) {
        $args += "--filter"
        $args += $Filter
    }

    & dotnet @args
    $exitCode = $LASTEXITCODE

    # Defensive cleanup: dotnet/vstest can leave child runners alive after command return.
    $cleanupCutoff = $layerStart.AddSeconds(-1)
    for ($pass = 1; $pass -le 5; $pass++) {
        $lingering = Get-Process dotnet,testhost,testhost.x86 -ErrorAction SilentlyContinue |
            Where-Object { $_.StartTime -ge $cleanupCutoff }
        foreach ($proc in $lingering) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                Write-Host "  CLEANUP(pass $pass): stopped lingering $($proc.ProcessName) pid=$($proc.Id)" -ForegroundColor DarkYellow
            } catch {
                Write-Host "  CLEANUP(pass $pass): failed to stop $($proc.ProcessName) pid=$($proc.Id): $($_.Exception.Message)" -ForegroundColor DarkYellow
            }
        }

        if ($pass -lt 5) {
            Start-Sleep -Seconds 1
        }
    }

    if ($exitCode -eq 0) {
        Write-Host "  PASSED" -ForegroundColor Green
        $script:passCount++
    } else {
        Write-Host "  FAILED (exit code $exitCode)" -ForegroundColor Red
        $script:failCount++
        if ($StopOnFail) {
            Write-Host "Stopping: $Name failed." -ForegroundColor Red
            return $false
        }
    }

    return $true
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " run-tests.ps1" -ForegroundColor Cyan
Write-Host " Layer: $Layer  SkipBuild: $SkipBuild  Timeout(min): $TestTimeoutMinutes" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build WestworldOfWarcraft.sln --configuration Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded." -ForegroundColor Green
}

if ($Layer -eq 0 -or $Layer -eq 1) {
    Run-TestLayer `
        -Name "Layer 1 - Native DLL availability" `
        -Project "Tests/Navigation.Physics.Tests" `
        -Filter "Feature=NativeDll" `
        -StopOnFail $false | Out-Null
}

if ($Layer -eq 0 -or $Layer -eq 2) {
    Run-TestLayer `
        -Name "Layer 2 - Physics engine tests" `
        -Project "Tests/Navigation.Physics.Tests" `
        -StopOnFail $false | Out-Null

    Run-TestLayer `
        -Name "Layer 2 - Pathfinding tests" `
        -Project "Tests/PathfindingService.Tests" `
        -StopOnFail $false | Out-Null
}

if ($Layer -eq 0 -or $Layer -eq 3) {
    Run-TestLayer `
        -Name "Layer 3 - WoWSharpClient unit tests" `
        -Project "Tests/WoWSharpClient.Tests" `
        -Filter "Category!=Integration" `
        -StopOnFail $false | Out-Null

    if (Test-Path "Tests/PromptHandlingService.Tests") {
        Run-TestLayer `
            -Name "Layer 3 - PromptHandlingService tests" `
            -Project "Tests/PromptHandlingService.Tests" `
            -StopOnFail $false | Out-Null
    }

    Run-TestLayer `
        -Name "Layer 3 - BotRunner unit tests" `
        -Project "Tests/BotRunner.Tests" `
        -Filter 'Category!=Integration&RequiresService!=MangosStack&RequiresService!=WoWServer&RequiresService!=AllServices' `
        -StopOnFail $false | Out-Null
}

if ($Layer -eq 0 -or $Layer -eq 4) {
    Run-TestLayer `
        -Name "Layer 4 - BotRunner integration tests" `
        -Project "Tests/BotRunner.Tests" `
        -Filter "Category=Integration" `
        -StopOnFail $false | Out-Null

    Run-TestLayer `
        -Name "Layer 4 - WoWSharpClient integration tests" `
        -Project "Tests/WoWSharpClient.Tests" `
        -Filter "Category=Integration" `
        -StopOnFail $false | Out-Null
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Passed: $($script:passCount)" -ForegroundColor Green
if ($script:failCount -gt 0) {
    Write-Host "  Failed: $($script:failCount)" -ForegroundColor Red
    exit 1
}

Write-Host "  Failed: 0" -ForegroundColor Green
exit 0
