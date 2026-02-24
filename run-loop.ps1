# Usage:
#   .\run-loop.ps1 -MaxIterations 30
#   .\run-loop.ps1 -MaxIterations 10 -InitialPrompt "Focus on Tests/BotRunner.Tests/TASKS.md"

param(
    [int]$MaxIterations = 20,
    [string]$InitialPrompt = "Read docs/TASKS.md plus relevant directory TASKS.md files and continue the highest-priority incomplete task. Do not ask for approval. Update TASKS.md files before ending the session."
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$MasterTasksFile = Join-Path $ProjectRoot "docs/TASKS.md"

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
