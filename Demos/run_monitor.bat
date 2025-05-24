@echo off
echo Starting Sharingway Enhanced Monitor...
echo.
echo This will monitor all Sharingway IPC framework activity.
echo.

REM Get path to the executable
set APP_PATH=.\monitorApp\bin\Debug\net9.0\monitorApp.exe

REM Check if executable exists
if not exist "%APP_PATH%" (
    echo ERROR: Cannot find %APP_PATH%
    echo Please build the solution first.
    pause
    exit /b 1
)

REM Run the enhanced monitor application
"%APP_PATH%" EnhancedMonitor

pause
