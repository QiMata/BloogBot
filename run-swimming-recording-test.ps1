<#
.SYNOPSIS
    Orchestrates the swimming recording test end-to-end, avoiding DLL lock races.

.DESCRIPTION
    This script handles the full lifecycle for running the SwimmingRecordingSessionTest:
      1. Kills any conflicting processes (dotnet, WoW, WoWStateManager)
      2. Builds the solution while nothing holds DLL locks
      3. Verifies FastCall.dll has the LuaCall export (copies correct version if needed)
      4. Starts StateManager FIRST and waits for port 8088
      5. Runs the recording test in a separate elevated terminal
      6. Monitors test-output.log for progress

    MUST be run from an elevated (Administrator) PowerShell prompt for DLL injection.

    ROOT CAUSE THIS SCRIPT FIXES:
    - FastCall.dll in Bot\Debug\net8.0\ must be the 62KB version with the LuaCall export.
      The build may overwrite it with a stale 12KB version. This script always verifies.
    - StateManager and the test must NOT run simultaneously during build, or DLL file locks
      cause the build to fail or copy stale DLLs.

.PARAMETER SkipBuild
    Skip the dotnet build step (use if build is already up to date).

.PARAMETER SkipKill
    Skip the kill-processes step (use if you want to keep existing services).

.PARAMETER MonitorOnly
    Skip everything and just tail the test-output.log from a previous run.

.PARAMETER Help
    Show this help message.

.EXAMPLE
    .\run-swimming-recording-test.ps1

.EXAMPLE
    .\run-swimming-recording-test.ps1 -SkipBuild

.EXAMPLE
    .\run-swimming-recording-test.ps1 -MonitorOnly
#>

param(
    [switch]$Help,
    [switch]$MonitorOnly,
    [switch]$SkipBuild,
    [switch]$SkipKill
)

$ErrorActionPreference = "Stop"

# ============================================================
# Internal logging — writes to both console and log file so
# progress is visible even when launched via Start-Process -Verb RunAs
# ============================================================
$script:_progressLog = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "test-progress.log"
if (-not $MonitorOnly -and -not $Help) {
    "" | Set-Content $script:_progressLog -Force -ErrorAction SilentlyContinue
}
function Log($message, [string]$color = "White") {
    $ts = "[{0:HH:mm:ss}]" -f (Get-Date)
    Write-Host "$ts $message" -ForegroundColor $color
    try { Add-Content -Path $script:_progressLog -Value "$ts $message" -ErrorAction SilentlyContinue } catch {}
}

function Show-Help {
    Write-Host @"
Swimming Recording Test Runner
==============================

This script runs the automated swimming recording session test that:
1. Kills conflicting processes to release DLL locks
2. Builds the solution cleanly
3. Verifies FastCall.dll has the LuaCall export
4. Starts WoWStateManager FIRST and waits for port 8088
5. Runs the SwimmingRecordingSessionTest in an elevated terminal
6. Monitors test output for key milestones

PREREQUISITES (must already be running):
- MySQL on port 3306
- realmd on port 3724
- mangosd on port 8085
- WoW 1.12.1 client at E:\Elysium Project Game Client\WoW.exe
- realmlist.wtf set to 127.0.0.1:3724
- Admin privileges (for DLL injection)

DO NOT start StateManager manually — this script starts it.

USAGE:
    .\run-swimming-recording-test.ps1              # Full run
    .\run-swimming-recording-test.ps1 -SkipBuild   # Skip build step
    .\run-swimming-recording-test.ps1 -SkipKill     # Keep existing processes
    .\run-swimming-recording-test.ps1 -MonitorOnly  # Tail existing test log
    .\run-swimming-recording-test.ps1 -Help         # Show this help

OUTPUT:
- Test output: E:\repos\BloogBot\test-output.log
- Diagnostic logs: <WoW.exe dir>\WWoWLogs\
- Recordings: ~\Documents\BloogBot\MovementRecordings\

"@
}

if ($Help) {
    Show-Help
    exit 0
}

# ============================================================
# Paths (derived from script location = repo root)
# ============================================================
$solutionRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path (Join-Path $solutionRoot "WestworldOfWarcraft.sln"))) {
    # Fallback to hardcoded path if script is invoked from elsewhere
    $solutionRoot = "E:\repos\BloogBot"
}
$solutionPath      = Join-Path $solutionRoot "WestworldOfWarcraft.sln"
$botDebugDir       = Join-Path $solutionRoot "Bot\Debug\net8.0"
$botNetDir         = Join-Path $solutionRoot "Bot\net8.0"
$fastCallSource    = Join-Path $botNetDir    "FastCall.dll"
$fastCallTarget    = Join-Path $botDebugDir  "FastCall.dll"
$fgResources       = Join-Path $solutionRoot "Services\ForegroundBotRunner\Resources\FastCall.dll"
$smBuild           = Join-Path $solutionRoot "Services\WoWStateManager\Build\Debug\net8.0-windows\FastCall.dll"
$smProject         = Join-Path $solutionRoot "Services\WoWStateManager\WoWStateManager.csproj"
$testOutputLog     = Join-Path $solutionRoot "test-output.log"
$testErrorLog      = Join-Path $solutionRoot "test-error.log"
$testProject       = Join-Path $solutionRoot "Tests\Navigation.Physics.Tests\Navigation.Physics.Tests.csproj"
$recordingsDir     = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "BloogBot\MovementRecordings"

# ============================================================
# Helpers
# ============================================================
function Write-Step($step, $message) {
    Log ""
    Log "[$step] $message" Cyan
    Log ("-" * 60) DarkGray
}

function Test-Port($ip, $port, $timeoutMs = 2000) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $result = $client.BeginConnect($ip, $port, $null, $null)
        $waited = $result.AsyncWaitHandle.WaitOne($timeoutMs)
        if ($waited -and $client.Connected) { $client.Close(); return $true }
        $client.Close(); return $false
    } catch { return $false }
}

function Test-FastCallExport($dllPath) {
    try {
        $output = & dumpbin /exports $dllPath 2>&1 | Select-String "LuaCall"
        return ($null -ne $output -and $output.Count -gt 0)
    } catch {
        # Fallback: check for LuaCall string in binary
        $bytes = [System.IO.File]::ReadAllBytes($dllPath)
        $text = [System.Text.Encoding]::ASCII.GetString($bytes)
        return $text -match "LuaCall"
    }
}

# ============================================================
# Monitor-only mode
# ============================================================
if ($MonitorOnly) {
    Write-Host "Monitoring test output at: $testOutputLog" -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Gray
    Write-Host ""
    # Also check progress log if test-output.log is empty
    if (Test-Path $testOutputLog) {
        Get-Content $testOutputLog -Wait -Tail 40
    } elseif (Test-Path $script:_progressLog) {
        Write-Host "(No test-output.log — showing progress log instead)" -ForegroundColor Yellow
        Get-Content $script:_progressLog -Wait -Tail 40
    } else {
        Write-Host "No test output log found yet. Run the test first." -ForegroundColor Yellow
    }
    exit 0
}

# ============================================================
# Banner
# ============================================================
Log "========================================" Yellow
Log "  Swimming Recording Test Orchestrator" Yellow
Log "========================================" Yellow
Log "  Repo: $solutionRoot" Gray

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Log "Admin: $isAdmin" Gray
if (-not $isAdmin) {
    Log "" Red
    Log "WARNING: Not running as Administrator. DLL injection will fail." Red
    Log "Re-run from an elevated PowerShell prompt." Red
    $confirm = Read-Host "Continue anyway? (y/N)"
    if ($confirm -ne 'y') { exit 1 }
}

# ============================================================
# STEP 1: Kill conflicting processes
# ============================================================
if (-not $SkipKill) {
    Write-Step "1" "Killing conflicting processes (release DLL locks)..."

    foreach ($name in @("WoW", "WoWStateManager")) {
        $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
        if ($procs) {
            Log "  Stopping $name..." Yellow
            Stop-Process -Name $name -Force -ErrorAction SilentlyContinue
        }
    }

    # Kill dotnet processes that might hold DLL locks in our output dirs
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)" -ErrorAction SilentlyContinue).CommandLine
            if ($cmdLine -and ($cmdLine -match "WoWStateManager" -or $cmdLine -match "Navigation\.Physics\.Tests")) {
                Log "  Stopping dotnet PID $($_.Id)" Yellow
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        } catch { }
    }

    Log "  Waiting 3s for cleanup..." Gray
    Start-Sleep -Seconds 3
} else {
    Write-Step "1" "Skipping process kill (-SkipKill)"
}

# ============================================================
# STEP 2: Check MaNGOS prerequisites
# ============================================================
Write-Step "2" "Checking prerequisites..."

$prereqOk = $true
foreach ($svc in @(
    @{ Name = "MySQL";   Port = 3306 },
    @{ Name = "realmd";  Port = 3724 },
    @{ Name = "mangosd"; Port = 8085 }
)) {
    if (Test-Port "127.0.0.1" $svc.Port) {
        Log "  [OK] $($svc.Name) (port $($svc.Port))" Green
    } else {
        Log "  [FAIL] $($svc.Name) (port $($svc.Port)) not running" Red
        $prereqOk = $false
    }
}

# WoW client
$wowPath = $env:WWOW_TEST_WOW_PATH
if (-not $wowPath) { $wowPath = "E:\Elysium Project Game Client\WoW.exe" }
if (Test-Path $wowPath) {
    Log "  [OK] WoW.exe: $wowPath" Green
} else {
    Log "  [FAIL] WoW.exe not found: $wowPath" Red
    $prereqOk = $false
}

if (-not $prereqOk) {
    Log "" Red
    Log "Prerequisites not met. Fix the issues above and re-run." Red
    exit 1
}

# ============================================================
# STEP 3: Build the solution (nothing should hold DLL locks)
# ============================================================
if (-not $SkipBuild) {
    Write-Step "3" "Building solution..."

    # The solution contains C++ .vcxproj projects (FastCall, Loader, Navigation)
    # that require the full MSBuild from Visual Studio, not the dotnet CLI.
    # Locate MSBuild.exe via vswhere.
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $msbuildPath = $null
    if (Test-Path $vsWhere) {
        $msbuildPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
    }

    Push-Location $solutionRoot
    try {
        if ($msbuildPath -and (Test-Path $msbuildPath)) {
            Log "  Using MSBuild: $msbuildPath" Gray
            & $msbuildPath $solutionPath /p:Configuration=Debug /m /nologo /v:minimal
            if ($LASTEXITCODE -ne 0) {
                Log "  BUILD FAILED" Red
                exit 1
            }
        } else {
            Log "  MSBuild.exe not found — falling back to dotnet build (C++ projects will be skipped)" Yellow
            & dotnet build $solutionPath --configuration Debug
            if ($LASTEXITCODE -ne 0) {
                # dotnet build may fail on .vcxproj — check if the key DLLs already exist
                $loaderOk  = Test-Path (Join-Path $botDebugDir "Loader.dll")
                $fastOk    = Test-Path $fastCallTarget
                $fgBotOk   = Test-Path (Join-Path $botDebugDir "ForegroundBotRunner.dll")
                if ($loaderOk -and $fastOk -and $fgBotOk) {
                    Log "  dotnet build had errors (C++ projects expected), but key DLLs exist. Continuing..." Yellow
                } else {
                    Log "  BUILD FAILED and key DLLs are missing" Red
                    exit 1
                }
            }
        }
        Log "  Build succeeded." Green
    } finally {
        Pop-Location
    }
} else {
    Write-Step "3" "Skipping build (-SkipBuild)"
}

# ============================================================
# STEP 4: Verify / fix FastCall.dll LuaCall export
# ============================================================
Write-Step "4" "Verifying FastCall.dll has LuaCall export..."

if (-not (Test-Path $fastCallTarget)) {
    Log "  FastCall.dll not found in $botDebugDir" Red
    if (Test-Path $fastCallSource) {
        Log "  Copying from $fastCallSource..." Yellow
        Copy-Item $fastCallSource $fastCallTarget -Force
    } else {
        Log "  FATAL: No source FastCall.dll at $fastCallSource" Red
        exit 1
    }
}

if (Test-FastCallExport $fastCallTarget) {
    $sz = (Get-Item $fastCallTarget).Length
    Log "  [OK] FastCall.dll has LuaCall export ($sz bytes)" Green
} else {
    Log "  [FAIL] LuaCall export missing — copying correct version" Red
    if (-not (Test-Path $fastCallSource)) {
        Log "  FATAL: Correct FastCall.dll not found at $fastCallSource" Red
        exit 1
    }
    Copy-Item $fastCallSource $fastCallTarget -Force
    if (Test-Path (Split-Path $fgResources -Parent)) { Copy-Item $fastCallSource $fgResources -Force }
    if (Test-Path (Split-Path $smBuild -Parent))      { Copy-Item $fastCallSource $smBuild -Force }

    if (Test-FastCallExport $fastCallTarget) {
        Log "  [OK] FastCall.dll fixed" Green
    } else {
        Log "  FATAL: Still no LuaCall export after copy" Red
        exit 1
    }
}

# ============================================================
# STEP 5: Start StateManager FIRST, wait for port 8088
# ============================================================
Write-Step "5" "Starting StateManager..."

if (Test-Port "127.0.0.1" 8088) {
    Log "  StateManager already listening on port 8088" Green
} else {
    Log "  Launching StateManager..." Yellow
    $smProc = Start-Process dotnet `
        -ArgumentList "run","--project",$smProject,"--configuration","Debug" `
        -WorkingDirectory $solutionRoot `
        -PassThru
    Log "  StateManager PID: $($smProc.Id)" Gray

    $maxWait = 60; $waited = 0
    while ($waited -lt $maxWait) {
        if (Test-Port "127.0.0.1" 8088) {
            Log "  [OK] StateManager ready on port 8088 (${waited}s)" Green
            break
        }
        Start-Sleep -Seconds 2; $waited += 2
        if ($waited % 10 -eq 0) { Log "    Waiting... (${waited}s)" Gray }
    }
    if ($waited -ge $maxWait) {
        Log "  [FAIL] StateManager did not start within ${maxWait}s" Red
        exit 1
    }
}

# ============================================================
# STEP 6: Run the swimming recording test
# ============================================================
Write-Step "6" "Running SwimmingRecordingSessionTest..."

if (Test-Path $testOutputLog) { Remove-Item $testOutputLog -Force }
if (Test-Path $testErrorLog)  { Remove-Item $testErrorLog -Force }

$env:BLOOGBOT_AUTOMATED_RECORDING = "1"

Log "  Test project: $testProject" Gray
Log "  Output log:   $testOutputLog" Gray
Log ""

# Run the test (--no-build since we already built in step 3)
$buildFlag = if ($SkipBuild) { "" } else { "--no-build" }
$testArgs = @("test", $testProject, "--configuration", "Debug",
    "--filter", "FullyQualifiedName~SwimmingRecordingSessionTest",
    "--logger", "console;verbosity=detailed")
if ($buildFlag) { $testArgs += $buildFlag }

$testProc = Start-Process dotnet `
    -ArgumentList $testArgs `
    -WorkingDirectory $solutionRoot `
    -RedirectStandardOutput $testOutputLog `
    -RedirectStandardError $testErrorLog `
    -PassThru `
    -NoNewWindow

Log "  Test PID: $($testProc.Id)" Gray
Log "  Monitoring (Ctrl+C to stop)..." Yellow
Log ""

$lastPos = 0
try {
    while (-not $testProc.HasExited) {
        Start-Sleep -Seconds 2
        if (Test-Path $testOutputLog) {
            $content = Get-Content $testOutputLog -Raw -ErrorAction SilentlyContinue
            if ($content -and $content.Length -gt $lastPos) {
                $new = $content.Substring($lastPos)
                $lastPos = $content.Length
                if ($new -match "All services are ready")            { Log "  > All services ready!" Green }
                if ($new -match "Found WoW process")                 { Log "  > WoW process detected" Green }
                if ($new -match "SCREEN_STATE_CHANGED.*InWorld")     { Log "  > Character entered world!" Green }
                if ($new -match "SCENARIO_COMPLETE.*08_swim_forward") { Log "  > Swim scenario completed!" Green }
                if ($new -match "New recording:")                     { Log "  > New recording captured" Cyan }
            }
        }
    }
} catch { <# Ctrl+C #> }

$exitColor = if ($testProc.ExitCode -eq 0) { "Green" } else { "Red" }
Log ""
Log "  Test exited with code: $($testProc.ExitCode)" $exitColor

# ============================================================
# STEP 7: Report results
# ============================================================
Write-Step "7" "Results"

if (Test-Path $testOutputLog) {
    $logContent = Get-Content $testOutputLog -Raw -ErrorAction SilentlyContinue
    if ($logContent -match "Passed")  { Log "  TEST PASSED" Green }
    elseif ($logContent -match "Failed")  { Log "  TEST FAILED — see $testOutputLog" Red }
    elseif ($logContent -match "Skipped") { Log "  TEST SKIPPED — services may not be available" Yellow }
}

if (Test-Path $recordingsDir) {
    $recent = Get-ChildItem $recordingsDir -Filter "*.json" |
        Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-30) }
    if ($recent) {
        Log ""
        Log "  New recordings:" Green
        foreach ($r in $recent) {
            Log "    $($r.Name) ($([math]::Round($r.Length/1024))KB)" Cyan
        }
        Log ""
        Log "  NEXT: Update Tests\Navigation.Physics.Tests\Helpers\TestConstants.cs" Yellow
        Log "        with the recording filename, then run SwimmingValidationTests." Yellow
    } else {
        Log "  No new recordings in last 30 minutes." Yellow
    }
}

Log ""
Log "Done." Green
