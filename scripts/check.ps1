#Requires -Version 7.0
# scripts/check.ps1 - the standard validation sequence to run before opening a PR:
#   1. lint  (hard gate - enforces the root .editorconfig whitespace baseline)
#   2. build (hard gate)
#   3. fast tests (hard gate; build already ran, so the rebuild is skipped)
# Sub-scripts run as child pwsh processes so their fail-fast 'exit' calls don't abort
# this aggregator and we can collect each result.
. "$PSScriptRoot/_common.ps1"

$failed = $false

Write-Step '[1/3] Lint'
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'lint.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Lint found formatting differences. Run 'scripts/format' to fix."
    $failed = $true
}

Write-Step '[2/3] Build'
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'build.ps1')
$buildOk = ($LASTEXITCODE -eq 0)
if (-not $buildOk) {
    Write-Fail 'Build failed.'
    $failed = $true
}

# Run fast tests whenever the build is green — even if lint failed — so a
# formatting miss doesn't hide test signal. The summary still fails if any step did.
if ($buildOk) {
    Write-Step '[3/3] Fast tests'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'test-fast.ps1') -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        Write-Fail 'Fast tests failed.'
        $failed = $true
    }
}

Write-Step 'Check summary'
if ($failed) {
    Write-Fail 'Pre-PR check FAILED. Fix the issues above before opening a PR.'
    exit 1
}
Write-Ok 'Pre-PR check PASSED (lint + build + fast tests green).'
exit 0
