# Build-ArchitectureCompatibleComponents.ps1
# Builds components with proper architecture matching for WoW.exe

param(
    [string]$WoWExePath = "C:\Users\WowAdmin\source\repos\sethrhod\BloogBot\Elysium Project Game Client\WoW.exe",
    [string]$Configuration = "Release"
)

Write-Host "=== Architecture-Compatible Component Builder ===" -ForegroundColor Green

$ErrorActionPreference = "Stop"

# Detect WoW.exe architecture
Write-Host "Detecting WoW.exe architecture..." -ForegroundColor Cyan

if (-not (Test-Path $WoWExePath)) {
    Write-Host "ERROR: WoW.exe not found at: $WoWExePath" -ForegroundColor Red
    exit 1
}

# Check if WoW is 32-bit or 64-bit
$wowFileInfo = Get-ItemProperty $WoWExePath
$wowSize = $wowFileInfo.Length

# Use a simple heuristic - files under 10MB are likely 32-bit
$is32BitWoW = $wowSize -lt 10MB

Write-Host "WoW.exe file size: $([math]::Round($wowSize / 1MB, 2)) MB" -ForegroundColor Yellow
Write-Host "Detected architecture: $(if($is32BitWoW) {'32-bit'} else {'64-bit'})" -ForegroundColor Yellow

# Determine build architecture
$buildArch = if($is32BitWoW) { "x86" } else { "x64" }
$runtimeId = if($is32BitWoW) { "win-x86" } else { "win-x64" }

Write-Host "Building for: $buildArch" -ForegroundColor Cyan
Write-Host "Runtime ID: $runtimeId" -ForegroundColor Cyan

$solutionRoot = $PSScriptRoot
$outputDir = Join-Path $solutionRoot "Exports\Bot\$Configuration\net8.0-$buildArch"

# Ensure output directory exists
New-Item -Path $outputDir -ItemType Directory -Force | Out-Null

try {
    # Build ForegroundBotRunner for correct architecture
    Write-Host "Building ForegroundBotRunner for $buildArch..." -ForegroundColor Cyan
    $foregroundBotProject = Join-Path $solutionRoot "Services\ForegroundBotRunner\ForegroundBotRunner.csproj"
    
    & dotnet build $foregroundBotProject -c $Configuration -r $runtimeId --self-contained false --output $outputDir
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build ForegroundBotRunner for $buildArch"
    }
    Write-Host "? ForegroundBotRunner built for $buildArch" -ForegroundColor Green

    # Build C++ Loader for correct architecture
    Write-Host "Building Loader.dll for $buildArch..." -ForegroundColor Cyan
    $loaderProject = Join-Path $solutionRoot "Exports\Loader\Loader.vcxproj"
    
    # Use msbuild with explicit platform
    & msbuild $loaderProject /p:Configuration=$Configuration /p:Platform=$buildArch /p:OutDir="$outputDir\"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "C++ build failed, trying alternative approach..." -ForegroundColor Yellow
        
        # Try with Visual Studio Developer Command Prompt
        $vcvarsPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
        if (Test-Path $vcvarsPath) {
            Write-Host "Using VsDevCmd.bat with $buildArch platform..." -ForegroundColor Yellow
            & cmd /c "`"$vcvarsPath`" -arch=$buildArch && msbuild `"$loaderProject`" /p:Configuration=$Configuration /p:Platform=$buildArch /p:OutDir=`"$outputDir\`""
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build Loader.dll for $buildArch after multiple attempts"
        }
    }
    Write-Host "? Loader.dll built for $buildArch" -ForegroundColor Green

    # Verify all files exist
    Write-Host "Verifying architecture-compatible components..." -ForegroundColor Cyan
    
    $requiredFiles = @(
        "Loader.dll",
        "ForegroundBotRunner.dll", 
        "ForegroundBotRunner.runtimeconfig.json"
    )
    
    $allFilesExist = $true
    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $outputDir $file
        if (Test-Path $filePath) {
            $fileInfo = Get-Item $filePath
            Write-Host "? $file ($($fileInfo.Length) bytes)" -ForegroundColor Green
        } else {
            Write-Host "? $file (missing)" -ForegroundColor Red
            $allFilesExist = $false
        }
    }
    
    if (-not $allFilesExist) {
        throw "Not all required files were built successfully"
    }
    
    # Update test configuration to use architecture-specific path
    $testConfigPath = Join-Path $solutionRoot "Tests\BotRunner.Tests\appsettings.test.json"
    if (Test-Path $testConfigPath) {
        Write-Host "Updating test configuration..." -ForegroundColor Cyan
        $config = Get-Content $testConfigPath | ConvertFrom-Json
        $config.LoaderDllPath = "Exports\Bot\$Configuration\net8.0-$buildArch\Loader.dll"
        $config | ConvertTo-Json -Depth 10 | Set-Content $testConfigPath
        Write-Host "? Updated LoaderDllPath to: $($config.LoaderDllPath)" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "=== ARCHITECTURE-COMPATIBLE BUILD COMPLETE ===" -ForegroundColor Green
    Write-Host "Components built for: $buildArch (matching WoW.exe)" -ForegroundColor Yellow
    Write-Host "Output directory: $outputDir" -ForegroundColor Yellow
    Write-Host "Test configuration updated automatically" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You can now run the integration test with architecture compatibility!" -ForegroundColor Yellow
    
} catch {
    Write-Host ""
    Write-Host "=== ARCHITECTURE BUILD FAILED ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Manual Steps:" -ForegroundColor Yellow
    Write-Host "1. Install $buildArch development tools" -ForegroundColor White
    Write-Host "2. Build manually: dotnet build -r $runtimeId" -ForegroundColor White
    Write-Host "3. Build C++: msbuild /p:Platform=$buildArch" -ForegroundColor White
    exit 1
}