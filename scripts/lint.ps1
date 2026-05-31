#Requires -Version 7.0
# scripts/lint.ps1 - verify formatting/style without modifying files (read-only).
# Exits non-zero if formatting changes would be needed.
. "$PSScriptRoot/_common.ps1"

Require-Command -Name 'dotnet' -InstallHint 'Run scripts/bootstrap first (needs the .NET 8 SDK).'

Write-Step 'Linting WestworldOfWarcraft.sln (dotnet format --verify-no-changes)'
Write-Note 'No .editorconfig baseline yet: this reports default-rule deltas. Treated as advisory in scripts/check.'

& dotnet format $Solution --verify-no-changes --severity warn
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Note "Formatting differences found (exit $code). Run 'scripts/format' to apply, or adopt an .editorconfig baseline."
    exit $code
}
Write-Ok 'No formatting differences.'
