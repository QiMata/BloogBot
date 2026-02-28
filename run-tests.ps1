# Runs test layers in dependency order with shared hang-timeout settings.
#
# Usage:
#   .\run-tests.ps1
#   .\run-tests.ps1 -Layer 1
#   .\run-tests.ps1 -Layer 3 -SkipBuild
#   .\run-tests.ps1 -TestTimeoutMinutes 10
#   .\run-tests.ps1 -CleanupRepoScopedOnly
#   .\run-tests.ps1 -ListRepoScopedProcesses

param(
    [int]$Layer = 0,
    [switch]$SkipBuild,
    [int]$TestTimeoutMinutes = 10,
    [switch]$CleanupRepoScopedOnly,
    [switch]$ListRepoScopedProcesses,
    [switch]$UseStartTimeCleanup
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

$repoRoot = (Resolve-Path $PSScriptRoot).Path
$trackedRepoProcessNames = @(
    "dotnet.exe",
    "testhost.exe",
    "testhost.x86.exe",
    "testhost.net8.0.exe",
    "vstest.console.exe",
    "StateManager.exe",
    "WoWStateManager.exe",
    "WoW.exe",
    "ForegroundBotRunner.exe",
    "BackgroundBotRunner.exe",
    "PathfindingService.exe"
)

function Convert-ToProcessArgumentString {
    param([string[]]$Values)

    return [string]::Join(" ", ($Values | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        } else {
            $_
        }
    }))
}

function Stop-ProcessTreeById {
    param(
        [int]$ProcessId,
        [string]$Reason = "cleanup"
    )

    if ($ProcessId -le 0 -or $ProcessId -eq $PID) {
        return $false
    }

    $output = & taskkill /PID $ProcessId /T /F 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  CLEANUP($Reason): taskkill /PID $ProcessId /T /F" -ForegroundColor DarkYellow
        return $true
    }

    if ($output -match "not found" -or $output -match "There is no running instance") {
        return $true
    }

    throw "taskkill failed for PID ${ProcessId}: $output"
}

function Get-RepoScopedTestProcesses {
    param(
        [int[]]$SeedProcessIds = @()
    )

    $raw = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    if (-not $raw) {
        return @()
    }

    $processById = @{}
    $childrenByParent = @{}
    foreach ($proc in $raw) {
        $procId = [int]$proc.ProcessId
        $parentId = [int]$proc.ParentProcessId
        $processById[$procId] = $proc
        if (-not $childrenByParent.ContainsKey($parentId)) {
            $childrenByParent[$parentId] = New-Object System.Collections.Generic.List[int]
        }
        $childrenByParent[$parentId].Add($procId)
    }

    $seedSet = New-Object "System.Collections.Generic.HashSet[int]"
    foreach ($proc in $raw) {
        if ($trackedRepoProcessNames -notcontains $proc.Name) {
            continue
        }

        if ($proc.CommandLine -and $proc.CommandLine -like "*$repoRoot*") {
            [void]$seedSet.Add([int]$proc.ProcessId)
        }
    }

    foreach ($seedId in $SeedProcessIds) {
        $id = [int]$seedId
        if ($id -gt 0) {
            [void]$seedSet.Add($id)
        }
    }

    if ($seedSet.Count -eq 0) {
        return @()
    }

    $scopedSet = New-Object "System.Collections.Generic.HashSet[int]"
    $queue = New-Object "System.Collections.Generic.Queue[int]"
    foreach ($seed in $seedSet) {
        [void]$scopedSet.Add($seed)
        $queue.Enqueue($seed)
    }

    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if (-not $childrenByParent.ContainsKey($current)) {
            continue
        }

        foreach ($childId in $childrenByParent[$current]) {
            if ($scopedSet.Add($childId)) {
                $queue.Enqueue($childId)
            }
        }
    }

    $scopedSet |
        Where-Object { $processById.ContainsKey($_) } |
        ForEach-Object {
            $proc = $processById[$_]
            [PSCustomObject]@{
                Id = [int]$proc.ProcessId
                ParentId = [int]$proc.ParentProcessId
                Name = $proc.Name
                CommandLine = $proc.CommandLine
            }
        }
}

function Write-RepoScopedProcessList {
    param([string]$Label)

    Write-Host ""
    Write-Host "$Label" -ForegroundColor Yellow
    $procs = @(Get-RepoScopedTestProcesses | Sort-Object Id)
    if ($procs.Count -eq 0) {
        Write-Host "  none" -ForegroundColor DarkGray
        return
    }

    foreach ($proc in $procs) {
        $cmd = $proc.CommandLine
        if ($cmd.Length -gt 140) {
            $cmd = $cmd.Substring(0, 140) + "..."
        }
        Write-Host "  pid=$($proc.Id) ppid=$($proc.ParentId) name=$($proc.Name) cmd=$cmd" -ForegroundColor DarkYellow
    }
}

function Stop-RepoScopedTestProcesses {
    param(
        [string]$Label = "Repo-scoped cleanup",
        [int]$MaxPasses = 5,
        [int[]]$SeedProcessIds = @()
    )

    $results = @()
    for ($pass = 1; $pass -le $MaxPasses; $pass++) {
        $lingering = @(Get-RepoScopedTestProcesses -SeedProcessIds $SeedProcessIds | Where-Object { $_.Id -ne $PID })
        if ($lingering.Count -eq 0) {
            break
        }

        $idSet = @{}
        foreach ($proc in $lingering) {
            $idSet[[int]$proc.Id] = $true
        }

        $rootProcesses = @($lingering |
            Where-Object { -not $idSet.ContainsKey([int]$_.ParentId) } |
            Sort-Object Id -Descending)
        if ($rootProcesses.Count -eq 0) {
            $rootProcesses = @($lingering | Sort-Object Id -Descending)
        }

        foreach ($proc in $rootProcesses) {
            $outcome = "stopped"
            try {
                Stop-ProcessTreeById -ProcessId $proc.Id -Reason "pass $pass,repo-scoped,$Label" | Out-Null
            } catch {
                $outcome = "failed: $($_.Exception.Message)"
                Write-Host "  CLEANUP(pass $pass,repo-scoped): failed to stop $($proc.Name) pid=$($proc.Id): $($_.Exception.Message)" -ForegroundColor DarkYellow
            }

            $results += [PSCustomObject]@{
                Pass = $pass
                Name = $proc.Name
                Id = $proc.Id
                Outcome = $outcome
                Label = $Label
            }
        }

        if ($pass -lt $MaxPasses) {
            Start-Sleep -Seconds 1
        }
    }

    # Emit structured evidence summary
    if ($results.Count -gt 0) {
        Write-Host ""
        Write-Host "  Cleanup evidence ($Label):" -ForegroundColor Yellow
        foreach ($r in $results) {
            $color = if ($r.Outcome -eq "stopped") { "Green" } else { "Red" }
            Write-Host "    pass=$($r.Pass) pid=$($r.Id) name=$($r.Name) outcome=$($r.Outcome)" -ForegroundColor $color
        }
        Write-Host "  Total: $($results.Count) process(es) targeted" -ForegroundColor Yellow
    }

    return $results
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
        "--logger", "console;verbosity=normal",
        "--blame-hang",
        "--blame-hang-timeout", "$($TestTimeoutMinutes)m"
    )

    if ($Filter) {
        $args += "--filter"
        $args += $Filter
    }

    $exitCode = 1
    $runPid = 0
    $timedOut = $false
    $timeoutMs = [Math]::Max(1, $TestTimeoutMinutes) * 60 * 1000
    $observedSeedIds = New-Object "System.Collections.Generic.HashSet[int]"
    $argumentString = Convert-ToProcessArgumentString -Values $args
    $process = $null
    try {
        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = "dotnet"
        $psi.Arguments = $argumentString
        $psi.WorkingDirectory = $repoRoot
        $psi.UseShellExecute = $false
        $process = [System.Diagnostics.Process]::Start($psi)
        if (-not $process) {
            throw "Failed to start dotnet test process."
        }

        $runPid = $process.Id
        [void]$observedSeedIds.Add($runPid)

        $deadline = (Get-Date).AddMilliseconds($timeoutMs)
        while (-not $process.HasExited) {
            $liveTree = @(Get-RepoScopedTestProcesses -SeedProcessIds @($runPid))
            foreach ($proc in $liveTree) {
                [void]$observedSeedIds.Add([int]$proc.Id)
            }

            $remainingMs = [int][Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds)
            if ($remainingMs -le 0) {
                $timedOut = $true
                break
            }

            $sliceMs = [Math]::Min(1000, $remainingMs)
            [void]$process.WaitForExit($sliceMs)
        }

        if ($timedOut) {
            Write-Host "  TIMED OUT after $TestTimeoutMinutes minute(s). Forcing process tree shutdown for pid=$runPid." -ForegroundColor Red
            try {
                Stop-ProcessTreeById -ProcessId $runPid -Reason "layer:$Name-timeout" | Out-Null
            } catch {
                Write-Host "  CLEANUP(timeout): failed to stop timed-out runner pid=${runPid}: $($_.Exception.Message)" -ForegroundColor DarkYellow
            }
            $exitCode = 124
        } else {
            $process.WaitForExit()
            $exitCode = $process.ExitCode
        }
    } finally {
        $finalTree = @(Get-RepoScopedTestProcesses -SeedProcessIds @($observedSeedIds))
        foreach ($proc in $finalTree) {
            [void]$observedSeedIds.Add([int]$proc.Id)
        }

        if ($process) {
            $process.Dispose()
        }

        $seedIdsForCleanup = @($observedSeedIds)
        Stop-RepoScopedTestProcesses -Label "layer:$Name" -MaxPasses 5 -SeedProcessIds $seedIdsForCleanup | Out-Null
    }

    if ($timedOut) {
        Write-Host "  FAILED (layer timeout: $TestTimeoutMinutes minute(s))" -ForegroundColor Red
    }

    if ($ListRepoScopedProcesses) {
        Write-RepoScopedProcessList -Label "Repo-scoped test processes after layer cleanup: $Name"
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
Write-Host " Layer: $Layer  SkipBuild: $SkipBuild  Timeout(min): $TestTimeoutMinutes  CleanupRepoScopedOnly: $CleanupRepoScopedOnly  ListRepoScopedProcesses: $ListRepoScopedProcesses  UseStartTimeCleanup: $UseStartTimeCleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($CleanupRepoScopedOnly) {
    if ($ListRepoScopedProcesses) {
        Write-RepoScopedProcessList -Label "Repo-scoped test processes before cleanup:"
    }
    Stop-RepoScopedTestProcesses -Label "requested:cleanup-only" -MaxPasses 5 | Out-Null
    if ($ListRepoScopedProcesses) {
        Write-RepoScopedProcessList -Label "Repo-scoped test processes after cleanup:"
    }
    exit 0
}

$explicitRunRequested = $PSBoundParameters.ContainsKey("Layer") -or
    $PSBoundParameters.ContainsKey("SkipBuild") -or
    $PSBoundParameters.ContainsKey("TestTimeoutMinutes")

if ($ListRepoScopedProcesses -and -not $explicitRunRequested) {
    Write-RepoScopedProcessList -Label "Repo-scoped test processes:"
    exit 0
}

if ($ListRepoScopedProcesses) {
    Write-RepoScopedProcessList -Label "Repo-scoped test processes before run:"
}

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

if ($ListRepoScopedProcesses) {
    Write-RepoScopedProcessList -Label "Repo-scoped test processes after run:"
}

exit 0
