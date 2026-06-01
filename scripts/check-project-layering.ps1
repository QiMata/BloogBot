#!/usr/bin/env pwsh
# check-project-layering.ps1
#
# Toolchain-free mirror of Tests/BotRunner.Tests/Spec/ProjectLayeringTests.cs.
# Parses every *.csproj for <ProjectReference> edges and enforces the documented
# dependency-direction (layering) rule from CLAUDE.md:
#
#     GameData.Core -> BotCommLayer -> BotRunner -> WoWSharpClient
#       -> Services -> UI
#
# Use this on machines without the .NET/native test toolchain (where
# `dotnet test` cannot build BotRunner.Tests). The C# test is canonical; this
# script exists only to validate the same invariants quickly. Keep the two in
# sync when an invariant changes.
#
# Exit code 0 = all invariants hold; 1 = at least one violation.

[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $RepoRoot 'WestworldOfWarcraft.sln'))) {
    Write-Error "WestworldOfWarcraft.sln not found under '$RepoRoot'. Pass -RepoRoot explicitly."
    exit 2
}

function Get-RepoRelative([string]$FullPath) {
    $full = [System.IO.Path]::GetFullPath($FullPath)
    if ($full.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $full = $full.Substring($RepoRoot.Length)
    }
    return ($full.TrimStart('\', '/') -replace '\\', '/')
}

function Get-FirstSegment([string]$RepoRelative) {
    $idx = $RepoRelative.IndexOf('/')
    if ($idx -lt 0) { return $RepoRelative }
    return $RepoRelative.Substring(0, $idx)
}

$edges = New-Object System.Collections.Generic.List[object]
$refPattern = '<ProjectReference\s+[^>]*Include\s*=\s*"([^"]+)"'

$csprojFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

foreach ($csproj in $csprojFiles) {
    $fromRel = Get-RepoRelative $csproj.FullName
    $fromSeg = Get-FirstSegment $fromRel
    $fromProject = [System.IO.Path]::GetFileNameWithoutExtension($csproj.FullName)
    $projDir = $csproj.DirectoryName
    $content = Get-Content -Raw -LiteralPath $csproj.FullName

    foreach ($m in [regex]::Matches($content, $refPattern)) {
        $include = $m.Groups[1].Value -replace '/', '\'
        $targetFull = [System.IO.Path]::GetFullPath((Join-Path $projDir $include))
        $toRel = Get-RepoRelative $targetFull
        $edges.Add([pscustomobject]@{
            FromRelative = $fromRel
            FromSegment  = $fromSeg
            FromProject  = $fromProject
            ToRelative   = $toRel
            ToSegment    = Get-FirstSegment $toRel
            ToProject    = [System.IO.Path]::GetFileNameWithoutExtension($targetFull)
        })
    }
}

Write-Host "Parsed $($edges.Count) ProjectReference edges from $($csprojFiles.Count) .csproj files."
if ($edges.Count -lt 30) {
    Write-Error "Expected >=30 edges; got $($edges.Count). Enumeration likely failed."
    exit 2
}

$violations = New-Object System.Collections.Generic.List[string]

# INV-1: GameData.Core has zero in-repo ProjectReferences.
$inv1 = $edges | Where-Object { $_.FromProject -eq 'GameData.Core' }
foreach ($e in $inv1) {
    $violations.Add("INV-1 GameData.Core -> $($e.ToProject) ($($e.ToSegment))")
}

# INV-2: No Exports/* references Services/, UI/, or Tests/.
$inv2 = $edges | Where-Object { $_.FromSegment -eq 'Exports' -and $_.ToSegment -in @('Services', 'UI', 'Tests') }
foreach ($e in $inv2) {
    $violations.Add("INV-2 $($e.FromRelative) -> $($e.ToRelative)")
}

# INV-3: No Services/* references UI/ or Tests/.
$inv3 = $edges | Where-Object { $_.FromSegment -eq 'Services' -and $_.ToSegment -in @('UI', 'Tests') }
foreach ($e in $inv3) {
    $violations.Add("INV-3 $($e.FromRelative) -> $($e.ToRelative)")
}

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "LAYERING VIOLATIONS:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "OK: INV-1 (GameData.Core dependency-free), INV-2 (Exports no upward ref), INV-3 (Services no UI/Tests ref) all hold." -ForegroundColor Green
exit 0
