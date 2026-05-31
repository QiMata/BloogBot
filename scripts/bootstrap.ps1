#Requires -Version 7.0
# scripts/bootstrap.ps1 - one-time (and re-runnable) setup: verify the toolchain and
# restore NuGet packages so the rest of the scripts can run.
. "$PSScriptRoot/_common.ps1"

Write-Step 'Bootstrapping the WestworldOfWarcraft developer environment'

# The .NET 8 SDK is required by every other script.
Require-Command -Name 'dotnet' -InstallHint 'Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0'

$sdkVersion = (& dotnet --version).Trim()
$major = 0
[void][int]::TryParse((($sdkVersion -split '\.')[0]), [ref]$major)
if ($major -lt 8) {
    Write-Fail ".NET SDK $sdkVersion found, but >= 8.0 is required."
    Write-Note 'Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0'
    exit 1
}
Write-Ok ".NET SDK $sdkVersion"

Invoke-Checked -Exe 'dotnet' -Arguments @('restore', $Solution) `
    -What 'Restoring NuGet packages for WestworldOfWarcraft.sln'

# Native C++ components (Exports/{Navigation,Loader,FastCall}) need Visual Studio + MSBuild.
# This is advisory only: pure .NET work does not need it, so never fail bootstrap here.
$pf86 = ${env:ProgramFiles(x86)}
$vswhere = if ($pf86) { Join-Path $pf86 'Microsoft Visual Studio\Installer\vswhere.exe' } else { $null }
if ($vswhere -and (Test-Path -LiteralPath $vswhere)) {
    $vsPath = (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath) 2>$null
    if ($vsPath) {
        Write-Ok "Visual Studio / MSBuild detected - native C++ builds available via 'scripts/build -Native'."
    }
    else {
        Write-Note "Visual Studio MSBuild not detected. .NET builds work; 'scripts/build -Native' (C++) needs the VS C++ workload."
    }
}
else {
    Write-Note 'vswhere not found. .NET builds work; native C++ builds require Visual Studio with the C++ workload.'
}

Write-Step 'Bootstrap complete.'
Write-Ok 'Next: scripts/build  then  scripts/test-fast'
