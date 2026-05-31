#Requires -Version 7.0
# scripts/_common.ps1
# Shared helpers for the stable script interface. Dot-sourced by every scripts/*.ps1.
# Not meant to be run directly.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repo root (scripts/ sits at the repo root) and run from there, so every
# script behaves identically no matter what directory it was invoked from.
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

$Solution = Join-Path $RepoRoot 'WestworldOfWarcraft.sln'

# Keep dotnet quiet, telemetry-free, and scoped to the repo drive (mirrors run-tests.ps1).
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$dotnetHome = Join-Path $RepoRoot 'tmp\dotnethome'
if (-not (Test-Path -LiteralPath $dotnetHome)) {
    New-Item -ItemType Directory -Path $dotnetHome -Force | Out-Null
}
$env:DOTNET_CLI_HOME = $dotnetHome

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "    OK: $Message" -ForegroundColor Green
}

function Write-Note {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "    $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "    FAILED: $Message" -ForegroundColor Red
}

# Run a native command and fail fast (exit) on a non-zero exit code.
function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string]$Exe,
        [string[]]$Arguments = @(),
        [string]$What
    )
    if (-not $What) { $What = "$Exe $($Arguments -join ' ')" }
    Write-Step $What
    & $Exe @Arguments
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        Write-Fail "$What (exit $code)"
        exit $code
    }
}

# Require a command on PATH; explain and exit non-zero if it is missing.
function Require-Command {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$InstallHint
    )
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Fail "'$Name' was not found on PATH."
        if ($InstallHint) { Write-Note $InstallHint }
        exit 127
    }
}

# Locate run-tests.ps1 (the canonical layered runner the test scripts delegate to).
function Get-RunTestsScript {
    $runTests = Join-Path $RepoRoot 'run-tests.ps1'
    if (-not (Test-Path -LiteralPath $runTests)) {
        Write-Fail "run-tests.ps1 not found at repo root ($runTests)."
        exit 1
    }
    return $runTests
}
