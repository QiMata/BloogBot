# Usage:
#   .\run-loop.ps1 -MaxIterations 30 -InitialPrompt "Your prompt here"
#   .\run-loop.ps1  # Uses defaults: 20 iterations, reads from TASKS.md

param(
    [int]$MaxIterations = 20,
    [string]$InitialPrompt = "Read TASKS.md and begin work on the next priority item."
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$HandoffFile = Join-Path $ProjectRoot "next-session-prompt.md"
$HistoryDir = Join-Path $ProjectRoot ".session-history"

function Extract-PromptFromFile {
    param([string]$FilePath)

    $content = Get-Content -Path $FilePath -Raw
    # Match ```prompt ... ``` fenced code block
    if ($content -match '(?s)```prompt\s*\r?\n(.*?)\r?\n```') {
        return $Matches[1].Trim()
    }
    # Fallback: use entire file content
    Write-Host "  [WARN] No ```prompt``` code block found, using entire file content." -ForegroundColor Yellow
    return $content.Trim()
}

function Archive-HandoffFile {
    if (Test-Path $HandoffFile) {
        if (-not (Test-Path $HistoryDir)) {
            New-Item -ItemType Directory -Path $HistoryDir -Force | Out-Null
        }
        $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
        $archivePath = Join-Path $HistoryDir "session-$timestamp.md"
        Copy-Item -Path $HandoffFile -Destination $archivePath
        Write-Host "  Archived handoff to: $archivePath" -ForegroundColor DarkGray
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Claude Code Session Loop" -ForegroundColor Cyan
Write-Host " Max Iterations: $MaxIterations" -ForegroundColor Cyan
Write-Host " Project Root:   $ProjectRoot" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

for ($i = 1; $i -le $MaxIterations; $i++) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "--- Iteration $i of $MaxIterations [$timestamp] ---" -ForegroundColor Green

    # Determine the prompt
    $prompt = $null
    if (Test-Path $HandoffFile) {
        Write-Host "  Source: next-session-prompt.md (handoff)" -ForegroundColor Cyan
        $prompt = Extract-PromptFromFile -FilePath $HandoffFile

        # Check for ALL_TASKS_COMPLETE before running
        if ($prompt -eq "ALL_TASKS_COMPLETE") {
            Write-Host ""
            Write-Host "ALL_TASKS_COMPLETE found in handoff file. Exiting loop." -ForegroundColor Yellow
            Archive-HandoffFile
            break
        }

        # Archive before running
        Archive-HandoffFile
        Remove-Item -Path $HandoffFile -Force
    }
    else {
        Write-Host "  Source: Initial prompt (no handoff file found)" -ForegroundColor Magenta
        $prompt = $InitialPrompt
    }

    Write-Host "  Prompt preview: $($prompt.Substring(0, [Math]::Min(120, $prompt.Length)))..." -ForegroundColor DarkGray
    Write-Host ""

    # Run Claude
    try {
        claude --dangerously-skip-permissions -p $prompt
    }
    catch {
        Write-Host "  [ERROR] Claude exited with an error: $_" -ForegroundColor Red
    }

    Write-Host ""

    # Post-run: check for completion signal
    if (Test-Path $HandoffFile) {
        $postContent = Extract-PromptFromFile -FilePath $HandoffFile
        if ($postContent -eq "ALL_TASKS_COMPLETE") {
            Write-Host "ALL_TASKS_COMPLETE signaled after iteration $i. Exiting loop." -ForegroundColor Yellow
            Archive-HandoffFile
            break
        }
        Write-Host "  Handoff file generated. Continuing to next iteration." -ForegroundColor Green
    }
    else {
        Write-Host "  [WARN] No next-session-prompt.md generated. Claude may not have followed the handoff protocol." -ForegroundColor Yellow
        Write-Host "  Stopping loop to avoid running without context." -ForegroundColor Yellow
        break
    }

    # Brief pause between iterations
    if ($i -lt $MaxIterations) {
        Write-Host "  Sleeping 5 seconds before next iteration..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 5
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Loop finished after $i iteration(s)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
