#Requires -Version 7.0
# scripts/format.ps1 - apply code formatting across the solution. MUTATES source files.
. "$PSScriptRoot/_common.ps1"

Require-Command -Name 'dotnet' -InstallHint 'Run scripts/bootstrap first (needs the .NET 8 SDK).'

Write-Note "No .editorconfig baseline exists yet, so 'dotnet format' applies default .NET rules."
Write-Note 'This rewrites source files in place and may produce a large diff. Review before committing.'

Invoke-Checked -Exe 'dotnet' -Arguments @('format', $Solution) -What 'Formatting WestworldOfWarcraft.sln (dotnet format)'

Write-Step 'Format complete.'
