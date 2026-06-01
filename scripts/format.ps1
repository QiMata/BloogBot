#Requires -Version 7.0
# scripts/format.ps1 - apply code formatting across the solution. MUTATES source files.
. "$PSScriptRoot/_common.ps1"

Require-Command -Name 'dotnet' -InstallHint 'Run scripts/bootstrap first (needs the .NET 8 SDK).'

Write-Note 'Applies the root .editorconfig whitespace/formatting baseline (symmetric with scripts/lint).'
Write-Note 'Rewrites source files in place. To also apply style/analyzer fixes, run: dotnet format <sln>.'

Invoke-Checked -Exe 'dotnet' -Arguments @('format', 'whitespace', $Solution) -What 'Formatting WestworldOfWarcraft.sln (dotnet format whitespace)'

Write-Step 'Format complete.'
