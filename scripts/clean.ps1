#Requires -Version 7.0
# scripts/clean.ps1 - remove build artifacts. DESTRUCTIVE by design, but only build
# outputs: dotnet clean, Bot/<config>, the CMake build/ dir, test scratch, and every
# per-project bin/ and obj/. Never touches source, .git, or the dotnet CLI cache.
. "$PSScriptRoot/_common.ps1"

Write-Step "Cleaning build artifacts under $RepoRoot"

# 1. Let dotnet clean the solution (best-effort; do not abort the script if it errors).
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    & dotnet clean $Solution --verbosity quiet
    if ($LASTEXITCODE -eq 0) { Write-Ok 'dotnet clean' }
    else { Write-Note "dotnet clean returned $LASTEXITCODE (continuing)." }
}

# 2. Whole directories to remove.
$targets = @(
    'Bot/Debug',
    'Bot/Release',
    'build',            # CMake out-of-source build dir
    'tmp/test-runtime'  # xUnit result/temp scratch (kept off C: by run-tests.ps1)
)
foreach ($rel in $targets) {
    $path = Join-Path $RepoRoot $rel
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok "removed $rel"
    }
}

# 3. Per-project bin/ and obj/ (skip .git and the scripts dir; remove deepest first).
Write-Step 'Removing per-project bin/ and obj/ directories'
$removed = 0
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Where-Object { $_.FullName -notmatch '[\\/](\.git|scripts)[\\/]' } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removed++
    }
$noun = if ($removed -eq 1) { 'directory' } else { 'directories' }
Write-Ok "removed $removed bin/obj $noun"

Write-Step 'Clean complete.'
Write-Note "Kept tmp/dotnethome (dotnet CLI cache). Run 'scripts/bootstrap' then 'scripts/build' to rebuild."
