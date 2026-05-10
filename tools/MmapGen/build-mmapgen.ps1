# Wrapper for configuring + building MmapGen.
# Usage:
#   .\tools\MmapGen\build-mmapgen.ps1                    # configure + build Release x64
#   .\tools\MmapGen\build-mmapgen.ps1 -Configuration Debug
#   .\tools\MmapGen\build-mmapgen.ps1 -Reconfigure       # nuke build/ first
#
# Produces:
#   tools\MmapGen\build\MmapGen.exe   (Ninja single-config layout)
#
# Generator choice: this machine has Visual Studio 18 (2026) Community with
# MSVC v145 (matches WWoW's `Westworld of Warcraft\CLAUDE.md` toolset rule).
# CMake 4.1 does not yet know the "Visual Studio 18 2026" generator string,
# so we use the Ninja generator under the VS 18 x64 dev-prompt environment.
# vcvarsall.bat puts cl/link/lib on PATH; cmake auto-detects MSVC from there.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [switch]$Reconfigure
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $here "build"

# --- Locate VS 18 (2026) Community vcvarsall.bat ----------------------------
$vsInstallerDir = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer"
$vswhere = Join-Path $vsInstallerDir "vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere"
}

# vcvarsall.bat calls `vswhere.exe` by bare name and expects it on PATH. If
# the VS Installer dir is not on PATH, the batch script writes a harmless
# stderr line which PowerShell ($ErrorActionPreference=Stop) treats as a
# terminating NativeCommandError. Pre-prepend the installer dir to PATH
# defensively for this session.
if (-not ($env:PATH -split ';' | Where-Object { $_ -ieq $vsInstallerDir })) {
    $env:PATH = "$vsInstallerDir;$env:PATH"
}

$vsInstall = (& $vswhere -latest -property installationPath) | Select-Object -First 1
if (-not $vsInstall) { throw "vswhere returned no Visual Studio installation" }
$vcvars = Join-Path $vsInstall "VC\Auxiliary\Build\vcvarsall.bat"
if (-not (Test-Path $vcvars)) { throw "vcvarsall.bat not found at $vcvars" }

Write-Host "Using Visual Studio at: $vsInstall"

# Map the cmake -A architecture name to the vcvarsall arch name.
$vcArch = if ($Architecture -eq "x86") { "x86" } else { "amd64" }

# --- Pull cl/link/lib onto PATH via vcvarsall ------------------------------
# Run vcvarsall in cmd, dump the resulting environment, and re-import it.
# PowerShell's cmd /c argument binding around quoted paths + && + redirects
# is fragile, so write a tiny shim .cmd and let cmd parse it natively.
$shimDir  = Join-Path $env:TEMP "mmapgen-vcvars"
New-Item -ItemType Directory -Force -Path $shimDir | Out-Null
$shimPath = Join-Path $shimDir "vcvars-and-dump.cmd"
$envDumpPath = Join-Path $shimDir "env-dump.txt"
Remove-Item -ErrorAction SilentlyContinue $envDumpPath
@"
@echo off
call "$vcvars" $vcArch
if errorlevel 1 exit /b %errorlevel%
set > "$envDumpPath"
"@ | Set-Content -LiteralPath $shimPath -Encoding ASCII
$prevEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& cmd /c "`"$shimPath`"" 2>&1 | Out-Null
$vcExit = $LASTEXITCODE
$ErrorActionPreference = $prevEAP
if ($vcExit -ne 0) { throw "vcvarsall.bat $vcArch failed (exit $vcExit)" }
if (-not (Test-Path $envDumpPath)) {
    throw "vcvarsall env dump missing at $envDumpPath"
}
foreach ($line in (Get-Content -LiteralPath $envDumpPath -Encoding Default)) {
    if ($line -match "^([^=]+)=(.*)$") {
        Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
    }
}

# Sanity: cl and ninja must be discoverable now.
$cl = (Get-Command cl.exe -ErrorAction SilentlyContinue)
if (-not $cl) { throw "cl.exe not on PATH after vcvarsall (arch $vcArch)" }
$ninja = (Get-Command ninja.exe -ErrorAction SilentlyContinue)
if (-not $ninja) { throw "ninja.exe not on PATH (install ninja or vcpkg)" }
Write-Host "cl.exe   : $($cl.Source)"
Write-Host "ninja.exe: $($ninja.Source)"

# --- Configure -------------------------------------------------------------
if ($Reconfigure -and (Test-Path $buildDir)) {
    Write-Host "Removing $buildDir"
    Remove-Item -Recurse -Force $buildDir
}

$needConfigure = -not (Test-Path (Join-Path $buildDir "build.ninja"))
if ($needConfigure) {
    Write-Host "Configuring MmapGen ($Architecture, $Configuration)"
    cmake -S $here -B $buildDir -G "Ninja" "-DCMAKE_BUILD_TYPE=$Configuration"
    if ($LASTEXITCODE -ne 0) { throw "cmake configure failed (exit $LASTEXITCODE)" }
}

# --- Build -----------------------------------------------------------------
Write-Host "Building MmapGen + SceneCacheBuilder ($Configuration)"
cmake --build $buildDir --target MmapGen SceneCacheBuilder
if ($LASTEXITCODE -ne 0) { throw "cmake build failed (exit $LASTEXITCODE)" }

$mmapExe = Join-Path $buildDir "MmapGen.exe"
if (Test-Path $mmapExe) {
    Write-Host "Built: $mmapExe" -ForegroundColor Green
}
$sceneExe = Join-Path $buildDir "SceneCacheBuilder.exe"
if (Test-Path $sceneExe) {
    Write-Host "Built: $sceneExe" -ForegroundColor Green
}

# --- NavMeshPhysicsValidator (.NET) ----------------------------------------
# Slice A of the physics-validated mmap pipeline. Drives the runtime physics
# classifier (ClassifyPathSegmentAffordance — same C export the BG runtime
# uses) over sample paths in a tile, emits a JSON report quantifying the
# bake-vs-runtime mismatch. Run separately via tools/scripts/validate-bake.ps1
# after MmapGen produces tiles.
$validatorProj = Join-Path $here "..\NavMeshPhysicsValidator\NavMeshPhysicsValidator.csproj"
if (Test-Path $validatorProj) {
    Write-Host "Building NavMeshPhysicsValidator (Release)"
    & dotnet build $validatorProj --configuration Release -v minimal --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet build NavMeshPhysicsValidator failed (exit $LASTEXITCODE)" }
    $validatorExe = Join-Path $here "..\..\Bot\Release\net8.0\NavMeshPhysicsValidator.exe"
    if (Test-Path $validatorExe) {
        Write-Host "Built: $validatorExe" -ForegroundColor Green
    }
}

Write-Host "MmapGen build complete." -ForegroundColor Green
