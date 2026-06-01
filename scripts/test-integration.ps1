#Requires -Version 7.0
# scripts/test-integration.ps1 - live integration tests (Layer 4, Category=Integration).
# Requires the MaNGOS stack to be up. Extra args are forwarded.
. "$PSScriptRoot/_common.ps1"

$runTests = Get-RunTestsScript

Write-Note 'Integration tests need the live MaNGOS stack + SOAP (http://127.0.0.1:7878/).'
Write-Note 'Bring the server up first; otherwise these tests will fail or skip.'
Write-Step 'Running integration tests (Layer 4)'
& $runTests -Layer 4 @args
exit $LASTEXITCODE
