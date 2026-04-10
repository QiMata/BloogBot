# Runs test layers in dependency order with shared hang-timeout settings.
#
# Usage:
#   .\run-tests.ps1
#   .\run-tests.ps1 -Layer 1
#   .\run-tests.ps1 -Layer 3 -SkipBuild
#   .\run-tests.ps1 -TestTimeoutMinutes 3
#   .\run-tests.ps1 -ListRepoScopedProcesses
#   .\run-tests.ps1 -CleanupRepoScopedOnly

param(
    [int]$Layer = 0,
    [switch]$SkipBuild,
    [int]$TestTimeoutMinutes = 3,
    [switch]$ListRepoScopedProcesses,
    [switch]$CleanupRepoScopedOnly
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

# Keep test artifacts/temp files on the repo drive instead of C:\Users\...\AppData\Local\Temp.
$testRuntimeRoot = Join-Path $PSScriptRoot "tmp\test-runtime"
$testResultsDir = Join-Path $testRuntimeRoot "results"
$testTempDir = Join-Path $testRuntimeRoot "temp"
foreach ($dir in @($testRuntimeRoot, $testResultsDir, $testTempDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
$env:VSTEST_RESULTS_DIRECTORY = $testResultsDir
$env:TEMP = $testTempDir
$env:TMP = $testTempDir
$env:WWOW_REPO_ROOT = $PSScriptRoot
$env:WWOW_TEST_RUNTIME_ROOT = $testRuntimeRoot

function Get-RepoScopedProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $candidateNames = @(
        "dotnet.exe",
        "testhost.exe",
        "testhost.x86.exe",
        "BackgroundBotRunner.exe",
        "WoWStateManager.exe",
        "WoW.exe"
    )

    $repoToken = $RepoRoot.ToLowerInvariant()
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $candidateNames -contains $_.Name }

    $scoped = foreach ($proc in $processes) {
        $cmd = $proc.CommandLine
        $exe = $proc.ExecutablePath

        $cmdHasRepo = -not [string]::IsNullOrWhiteSpace($cmd) -and $cmd.ToLowerInvariant().Contains($repoToken)
        $exeHasRepo = -not [string]::IsNullOrWhiteSpace($exe) -and $exe.ToLowerInvariant().Contains($repoToken)
        $hasRuntimeToken = -not [string]::IsNullOrWhiteSpace($cmd) -and $cmd.ToLowerInvariant().Contains("tmp\\test-runtime")
        if (-not ($cmdHasRepo -or $exeHasRepo -or $hasRuntimeToken)) {
            continue
        }

        $startTime = $null
        try {
            $running = Get-Process -Id $proc.ProcessId -ErrorAction Stop
            $startTime = $running.StartTime
        } catch {
            # Process exited between CIM query and lookup.
        }

        [pscustomobject]@{
            Id = [int]$proc.ProcessId
            Name = $proc.Name
            StartTime = $startTime
            ExecutablePath = $exe
            CommandLine = $cmd
        }
    }

    $scoped | Sort-Object Name, StartTime, Id
}

if ($ListRepoScopedProcesses -or $CleanupRepoScopedOnly) {
    $scopedProcesses = @(Get-RepoScopedProcesses -RepoRoot $PSScriptRoot)

    if ($ListRepoScopedProcesses) {
        if ($scopedProcesses.Count -eq 0) {
            Write-Host "No repo-scoped processes found." -ForegroundColor Green
        } else {
            $scopedProcesses |
                Select-Object Id, Name, StartTime, @{
                    Name = "CommandLine";
                    Expression = {
                        if ([string]::IsNullOrWhiteSpace($_.CommandLine)) { return "" }
                        if ($_.CommandLine.Length -le 160) { return $_.CommandLine }
                        return $_.CommandLine.Substring(0, 157) + "..."
                    }
                } |
                Format-Table -AutoSize
        }
    }

    if ($CleanupRepoScopedOnly) {
        if ($scopedProcesses.Count -eq 0) {
            Write-Host "No repo-scoped processes to stop." -ForegroundColor Green
        } else {
            foreach ($proc in $scopedProcesses) {
                try {
                    Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                    Write-Host "Stopped repo-scoped process $($proc.Name) pid=$($proc.Id)" -ForegroundColor DarkYellow
                } catch {
                    Write-Host "Failed to stop pid=$($proc.Id) ($($proc.Name)): $($_.Exception.Message)" -ForegroundColor Red
                    $script:failCount++
                }
            }
        }
    }

    if ($script:failCount -gt 0) {
        exit 1
    }

    exit 0
}

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
        "--results-directory", $testResultsDir,
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
