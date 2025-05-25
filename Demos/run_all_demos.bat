@echo off
echo Starting all Sharingway demo applications...
echo.
echo This process will:
echo  1. Build all projects
echo  2. Run the Monitor
echo  3. Run the C++ Demo Application
echo  4. Run the C# Demo Application
echo.
echo Please close each window when finished testing.
echo.

REM Check for Visual Studio installation and MSBuild
where /q msbuild
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: MSBuild not found in PATH.
    echo Please run this script from a Visual Studio Developer Command Prompt
    echo or ensure that MSBuild is in your PATH environment variable.
    pause
    exit /b 1
)

REM Kill any existing demo processes that might lock the executables
echo Terminating any running demo processes...
taskkill /f /im nativeApp.exe >nul 2>&1
taskkill /f /im dotNetApp.exe >nul 2>&1
taskkill /f /im monitorApp.exe >nul 2>&1
echo.

echo Checking for pre-built executables...
echo.

REM Navigate to the solution directory
cd ..

REM Build the C++ executable
echo Building nativeApp...
msbuild "Demos\nativeApp\nativeApp.vcxproj" /p:Configuration=Debug /p:Platform=x64

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: C++ build failed! Please check the error messages above.
    pause
    exit /b 1
)

REM Build the .NET components
echo.
echo Building .NET components...
where dotnet >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    REM Try to build the .NET projects using dotnet CLI
    dotnet build "Sharingway.Net\Sharingway.Net.csproj" -c Debug
    dotnet build "Demos\dotNetApp\dotNetApp.csproj" -c Debug
    dotnet build "Demos\monitorApp\monitorApp.csproj" -c Debug
) else (
    echo WARNING: dotnet CLI not found. Will attempt to use the compiled .NET executables if available.
    echo You may need to install the .NET 9.0 SDK to run the .NET components.
)

echo.
echo Build completed successfully.
echo.

REM Return to the Demos directory
cd Demos

echo Ready to start the demo applications.
pause

REM Check if required executables exist
set MONITOR_APP=.\monitorApp\bin\Debug\net9.0\monitorApp.exe
set CPP_APP=.\nativeApp\x64\Debug\nativeApp.exe
set DOTNET_APP=.\dotNetApp\bin\Debug\net9.0\dotNetApp.exe

echo Verifying executables...

REM Check for C++ executable, which is mandatory
if not exist "%CPP_APP%" (
    echo ERROR: C++ application not found at %CPP_APP%
    echo The build failed or the path is incorrect.
    pause
    exit /b 1
) else (
    echo C++ application: OK
    set CPP_MISSING=0
)

REM Check for .NET executables
if not exist "%MONITOR_APP%" (
    where dotnet >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo Attempting to run Monitor with dotnet...
        if exist ".\monitorApp\bin\Debug\net9.0\monitorApp.dll" (
            set MONITOR_APP=dotnet ".\monitorApp\bin\Debug\net9.0\monitorApp.dll"
            set MONITOR_MISSING=0
            echo Monitor application: OK (using dotnet CLI)
        ) else (
            echo WARNING: Monitor application not found. It will be skipped.
            set MONITOR_MISSING=1
        )
    ) else (
        echo WARNING: Monitor application not found. It will be skipped.
        set MONITOR_MISSING=1
    )
) else (
    set MONITOR_MISSING=0
    echo Monitor application: OK
)

REM Check for .NET Demo app
if not exist "%DOTNET_APP%" (
    where dotnet >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo Attempting to run .NET Demo with dotnet...
        if exist ".\dotNetApp\bin\Debug\net9.0\dotNetApp.dll" (
            set DOTNET_APP=dotnet ".\dotNetApp\bin\Debug\net9.0\dotNetApp.dll"
            set DOTNET_MISSING=0
            echo .NET Demo application: OK (using dotnet CLI)
        ) else (
            echo WARNING: .NET Demo application not found. It will be skipped.
            set DOTNET_MISSING=1
        )
    ) else (
        echo WARNING: .NET Demo application not found. It will be skipped.
        set DOTNET_MISSING=1
    )
) else (
    set DOTNET_MISSING=0
    echo .NET Demo application: OK
)

echo Starting available demo applications...
echo.

REM Start the monitor first (if available)
if %MONITOR_MISSING%==0 (
    echo Starting Monitor application...
    if "%MONITOR_APP:~0,6%"=="dotnet" (
        start "Sharingway Monitor" cmd /c %MONITOR_APP%
    ) else (
        start "Sharingway Monitor" cmd /c "%MONITOR_APP%"
    )
    echo Started Monitor application
    REM Wait a bit for the monitor to initialize
    timeout /t 2 > nul
) else (
    echo Skipping Monitor application (not available)
)

REM Start the C++ demo (always available as we checked earlier)
echo Starting C++ Demo application...
start "Sharingway C++ Demo" cmd /c "%CPP_APP%" CppDemoProvider
echo Started C++ Demo application

REM Start the C# demo (if available)
if %DOTNET_MISSING%==0 (
    echo Starting .NET Demo application...
    if "%DOTNET_APP:~0,6%"=="dotnet" (
        start "Sharingway C# Demo" cmd /c %DOTNET_APP% DotNetDemoProvider
    ) else (
        start "Sharingway C# Demo" cmd /c "%DOTNET_APP%" DotNetDemoProvider
    )
    echo Started .NET Demo application
) else (
    echo Skipping .NET Demo application (not available)
)

echo.
echo All available demos started. Check each window to see the applications in action.
echo.
pause
