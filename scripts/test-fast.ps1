#Requires -Version 7.0
# scripts/test-fast.ps1 - fast feedback: unit tests only (Layer 3, no live infrastructure).
# Extra args are forwarded, e.g.  scripts/test-fast -SkipBuild
. "$PSScriptRoot/_common.ps1"

$runTests = Get-RunTestsScript

Write-Step 'Running fast unit tests (Layer 3: no MaNGOS/WoW server required)'
& $runTests -Layer 3 @args
exit $LASTEXITCODE
