#Requires -Version 7.0
# scripts/test.ps1 - run the full layered test suite (delegates to run-tests.ps1, all layers).
# Extra args are forwarded, e.g.  scripts/test -SkipBuild -TestTimeoutMinutes 10
. "$PSScriptRoot/_common.ps1"

$runTests = Get-RunTestsScript

Write-Step 'Running full test suite (all layers via run-tests.ps1)'
& $runTests @args
exit $LASTEXITCODE
