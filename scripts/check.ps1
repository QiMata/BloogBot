#Requires -Version 7.0
# scripts/check.ps1 - the standard validation sequence to run before opening a PR:
#   1. lint  (advisory - never fails the gate while no .editorconfig baseline exists)
#   2. build (hard gate)
#   3. fast tests (hard gate; build already ran, so the rebuild is skipped)
# Sub-scripts run as child pwsh processes so their fail-fast 'exit' calls don't abort
# this aggregator and we can collect each result.
. "$PSScriptRoot/_common.ps1"

$failed = $false

Write-Step '[1/3] Lint (advisory)'
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'lint.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Note "Lint reported differences (advisory) - not failing check. Run 'scripts/format' to address."
}

Write-Step '[2/3] Build'
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    Write-Fail 'Build failed.'
    $failed = $true
}

if (-not $failed) {
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
Write-Ok 'Pre-PR check PASSED (build + fast tests green; lint advisory).'
exit 0
