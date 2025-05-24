@echo off
echo Starting Sharingway .NET Demo (simplified)...
echo.
echo This will run the C# application as both provider and subscriber simultaneously.
echo.

REM Get path to the executable
set APP_PATH=.\dotNetApp\bin\Debug\net9.0\dotNetApp.exe

REM Check if executable exists
if not exist "%APP_PATH%" (
    echo ERROR: Cannot find %APP_PATH%
    echo Please build the solution first.
    pause
    exit /b 1
)

REM Run the application with a default provider name
"%APP_PATH%" SimplifiedDemo DotNetProviderDemo

pause
