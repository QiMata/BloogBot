#Requires -Version 7.0
# scripts/lint.ps1 - verify formatting/style without modifying files (read-only).
# Exits non-zero if formatting changes would be needed.
. "$PSScriptRoot/_common.ps1"

Require-Command -Name 'dotnet' -InstallHint 'Run scripts/bootstrap first (needs the .NET 8 SDK).'

Write-Step 'Linting WestworldOfWarcraft.sln (dotnet format whitespace --verify-no-changes)'
Write-Note 'Enforces the root .editorconfig whitespace/formatting baseline. Hard gate in scripts/check.'

& dotnet format whitespace $Solution --verify-no-changes
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Note "Formatting differences found (exit $code). Run 'scripts/format' to apply."
    exit $code
}
Write-Ok 'No formatting differences.'
