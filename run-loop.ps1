# Usage:
#   .\run-loop.ps1 -MaxIterations 30
#   .\run-loop.ps1 -MaxIterations 10 -InitialPrompt "Focus on Tests/BotRunner.Tests/TASKS.md"

param(
    [int]$MaxIterations = 20,
    # Long-lived working branch for this loop (R15 / docs/BRANCHING_WORKFLOW.md).
    # Auto-commits land here, NEVER on main; CI/linters gate the merge via PR.
    # This checkout already works on the pre-existing 'wwow-pathfinding-loop'
    # branch (base origin/main). Keep auto-commits there, never on main.
    [string]$Branch = "wwow-pathfinding-loop",
    [string]$InitialPrompt = "Read docs/TASKS.md plus relevant directory TASKS.md files and continue the highest-priority incomplete task. Do not ask for approval. Update TASKS.md files before ending the session. Per R15 / docs/BRANCHING_WORKFLOW.md: commit and push every iteration to the '$Branch' branch (NEVER main); open an auto-merging PR per milestone so CI/linters gate the merge to main."
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$MasterTasksFile = Join-Path $ProjectRoot "docs/TASKS.md"

# Ensure the long-lived working branch exists and is checked out off up-to-date
# main before any iteration commits. NEVER auto-commit on main (R15).
Push-Location $ProjectRoot
git fetch origin main *> $null
git rev-parse --verify $Branch *> $null
if ($LASTEXITCODE -eq 0) { git checkout $Branch | Out-Null }
else { git checkout -B $Branch origin/main | Out-Null }
$current = (git rev-parse --abbrev-ref HEAD).Trim()
Pop-Location
if ($current -ne $Branch) {
    Write-Host "  [ERROR] Could not switch to working branch '$Branch' (on '$current'). Stopping." -ForegroundColor Red
    exit 1
}
Write-Host " Working branch: $Branch (auto-commits land here, not main)" -ForegroundColor Cyan

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Codex Session Loop" -ForegroundColor Cyan
Write-Host " Max Iterations: $MaxIterations" -ForegroundColor Cyan
Write-Host " Project Root:   $ProjectRoot" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

for ($i = 1; $i -le $MaxIterations; $i++) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "--- Iteration $i of $MaxIterations [$timestamp] ---" -ForegroundColor Green

    if (-not (Test-Path $MasterTasksFile)) {
        Write-Host "  [ERROR] Missing docs/TASKS.md. Stopping loop." -ForegroundColor Red
        break
    }

    $prompt = $InitialPrompt
    Write-Host "  Prompt source: docs/TASKS.md + directory TASKS.md files" -ForegroundColor Cyan
    Write-Host "  Prompt preview: $($prompt.Substring(0, [Math]::Min(140, $prompt.Length)))..." -ForegroundColor DarkGray
    Write-Host ""

    try {
        claude --dangerously-skip-permissions -p $prompt
    }
    catch {
        Write-Host "  [ERROR] Claude exited with an error: $_" -ForegroundColor Red
    }

    Write-Host ""

    if ($i -lt $MaxIterations) {
        Write-Host "  Sleeping 5 seconds before next iteration..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 5
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Loop finished" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
