@echo off
echo === Building C++ Components for Bot Injection ===
echo.

REM Set Visual Studio environment (modify path as needed)
set VS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat
if exist "%VS_PATH%" (
    echo Setting up Visual Studio environment...
    call "%VS_PATH%"
) else (
    echo Warning: Visual Studio environment not found at %VS_PATH%
    echo Please ensure Visual Studio Build Tools are installed
)

echo.
echo Building Loader.dll...
echo Current directory: %CD%

REM Check if we're in the correct directory
if not exist "Exports\Loader\Loader.vcxproj" (
    echo Error: Loader.vcxproj not found. Please run this script from the repository root.
    pause
    exit /b 1
)

REM Build Loader project
msbuild Exports\Loader\Loader.vcxproj /p:Configuration=Debug /p:Platform=x64
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build Loader.dll (x64)
    pause
    exit /b 1
)

msbuild Exports\Loader\Loader.vcxproj /p:Configuration=Debug /p:Platform=x86
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build Loader.dll (x86)
    pause
    exit /b 1
)

echo.
echo Building ForegroundBotRunner...
dotnet build Services\ForegroundBotRunner\ForegroundBotRunner.csproj -c Debug --framework net8.0
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build ForegroundBotRunner.dll
    pause
    exit /b 1
)

echo.
echo === Copy DLLs to deployment location ===

REM Create deployment directory if it doesn't exist
if not exist "bin" mkdir bin

REM Determine which architecture to use based on what built successfully
set LOADER_X64=Exports\Loader\x64\Debug\Loader.dll
set LOADER_X86=Exports\Loader\Debug\Loader.dll

if exist "%LOADER_X64%" (
    echo Copying x64 Loader.dll...
    copy "%LOADER_X64%" "bin\Loader.dll"
    set ARCH=x64
) else if exist "%LOADER_X86%" (
    echo Copying x86 Loader.dll...
    copy "%LOADER_X86%" "bin\Loader.dll"
    set ARCH=x86
) else (
    echo Error: No Loader.dll found in expected locations
    echo Checked: %LOADER_X64%
    echo Checked: %LOADER_X86%
    pause
    exit /b 1
)

REM Copy ForegroundBotRunner components
set FOREGROUND_SOURCE=Services\ForegroundBotRunner\bin\Debug\net8.0
if exist "%FOREGROUND_SOURCE%\ForegroundBotRunner.dll" (
    echo Copying ForegroundBotRunner.dll...
    copy "%FOREGROUND_SOURCE%\ForegroundBotRunner.dll" "bin\"
    copy "%FOREGROUND_SOURCE%\ForegroundBotRunner.runtimeconfig.json" "bin\"
    copy "%FOREGROUND_SOURCE%\ForegroundBotRunner.deps.json" "bin\"
) else (
    echo Error: ForegroundBotRunner.dll not found at %FOREGROUND_SOURCE%
    pause
    exit /b 1
)

echo.
echo === Build Summary ===
echo Architecture: %ARCH%
echo Loader.dll: bin\Loader.dll
echo ForegroundBotRunner.dll: bin\ForegroundBotRunner.dll
echo.

echo === Updating configuration files ===
set CONFIG_PATH=%CD%\bin
echo LoaderDllPath should be set to: %CONFIG_PATH%\Loader.dll
echo FOREGROUNDBOT_DLL_PATH will be set to: %CONFIG_PATH%\ForegroundBotRunner.dll

echo.
echo Build completed successfully!
echo.
echo To use these DLLs:
echo 1. Update appsettings.json LoaderDllPath to: %CONFIG_PATH%\Loader.dll
echo 2. Ensure WoW.exe path is correct in GameClient:ExecutablePath
echo 3. Run StateManager service
echo.
pause