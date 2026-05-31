#Requires -Version 7.0
# scripts/build.ps1 - build the .NET solution, and optionally the native C++ projects.
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Native,
    [switch]$NoRestore
)
. "$PSScriptRoot/_common.ps1"

Require-Command -Name 'dotnet' -InstallHint 'Run scripts/bootstrap first (needs the .NET 8 SDK).'

# WoW.exe locks the DLLs that ForegroundBotRunner injects from the build output, causing
# MSB3027 copy errors. Warn rather than kill (non-destructive default) - close it or kill
# the specific PID yourself if the copy step fails.
$wow = Get-Process -Name 'WoW' -ErrorAction SilentlyContinue
if ($wow) {
    Write-Note ("WoW.exe is running (PID(s): {0}); it can lock injected DLLs and cause MSB3027 copy errors." -f ($wow.Id -join ', '))
    Write-Note "If the build fails to copy outputs, close WoW.exe (or 'taskkill /F /PID <pid>' for that PID) and retry."
}

$buildArgs = @('build', $Solution, '--configuration', $Configuration)
if ($NoRestore) { $buildArgs += '--no-restore' }
Write-Step "Building WestworldOfWarcraft.sln ($Configuration)"
& dotnet @buildArgs
$buildCode = $LASTEXITCODE
if ($buildCode -ne 0) {
    Write-Fail "dotnet build failed (exit $buildCode)."
    Write-Note 'This repo has native C++ components. A full build needs the native DLLs'
    Write-Note '(FastCall.dll / Loader.dll / Navigation.dll) that ForegroundBotRunner copies,'
    Write-Note 'plus the pinned MSVC toolset. Build them with:  scripts/build -Native'
    Write-Note '(requires Visual Studio with the "Desktop development with C++" workload).'
    exit $buildCode
}

if ($Native) {
    Write-Step 'Building native C++ components'
    $pf86 = ${env:ProgramFiles(x86)}
    $vswhere = if ($pf86) { Join-Path $pf86 'Microsoft Visual Studio\Installer\vswhere.exe' } else { $null }
    $msbuild = $null
    if ($vswhere -and (Test-Path -LiteralPath $vswhere)) {
        $msbuild = (& $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1)
    }
    if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild)) {
        Write-Fail "MSBuild not found. Native C++ builds need Visual Studio with the 'Desktop development with C++' workload."
        Write-Note 'Install it (or build the native projects from the Visual Studio IDE). The .NET output above is already built.'
        exit 1
    }
    Write-Ok "Using MSBuild: $msbuild"

    $nativeProjects = @(
        @{ Path = 'Exports/Navigation/Navigation.vcxproj'; Platform = 'x64' },
        @{ Path = 'Exports/Loader/Loader.vcxproj'; Platform = 'x86' },
        @{ Path = 'Exports/FastCall/FastCall.vcxproj'; Platform = 'x86' }
    )
    foreach ($proj in $nativeProjects) {
        Invoke-Checked -Exe $msbuild -Arguments @(
            $proj.Path,
            "-p:Configuration=$Configuration",
            "-p:Platform=$($proj.Platform)",
            '-p:PlatformToolset=v145',
            '-v:minimal'
        ) -What "MSBuild $($proj.Path) ($Configuration|$($proj.Platform))"
    }
}

Write-Step "Build complete ($Configuration)."
