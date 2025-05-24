@echo off
echo Starting all Sharingway demo applications...
echo.
echo This process will:
echo  1. Build all projects
echo  2. Run the Enhanced Monitor
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

echo Building all projects...
echo.

REM Navigate to the solution directory
cd ..

REM Build the solution
echo Building Sharingway solution...
msbuild Sharingway.sln /p:Configuration=Debug /p:Platform=x64 /m

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed! Please check the error messages above.
    pause
    exit /b 1
)

echo.
echo Build completed successfully.
echo.

REM Return to the Demos directory
cd Demos

echo Ready to start the demo applications.
pause

REM Start the monitor first
start "Sharingway Monitor" cmd /c "run_monitor.bat"

REM Wait a bit for the monitor to initialize
timeout /t 2 > nul

REM Start the C++ demo
start "Sharingway C++ Demo" cmd /c "run_cpp_demo.bat"

REM Start the C# demo
start "Sharingway C# Demo" cmd /c "run_cs_demo.bat"

echo.
echo All demos started. Check each window to see the applications in action.
echo.
pause
