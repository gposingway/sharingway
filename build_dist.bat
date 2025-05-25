@echo off
echo ======================================
echo Sharingway IPC Framework - Build Distribution
echo ======================================
echo.

:: Set variables
set SOLUTION_FILE=Sharingway.sln
set DIST_DIR=dist
set CONFIG=Release
set PLATFORM=x64

:: Clean previous distribution
if exist "%DIST_DIR%" (
    echo Cleaning previous distribution...
    rmdir /s /q "%DIST_DIR%"
)

:: Create distribution directory structure
echo Creating distribution directories...
mkdir "%DIST_DIR%"
mkdir "%DIST_DIR%\csharp"
mkdir "%DIST_DIR%\cpp"
mkdir "%DIST_DIR%\cpp\include"
mkdir "%DIST_DIR%\cpp\lib"
mkdir "%DIST_DIR%\cpp\lib\x64"
mkdir "%DIST_DIR%\docs"

:: Build only the core libraries (not demos)
echo.
echo Building core libraries in %CONFIG% configuration...
echo Building Sharingway.native...
msbuild "Sharingway.native\Sharingway.native.vcxproj" /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /nologo /verbosity:minimal

if %ERRORLEVEL% neq 0 (
    echo ERROR: C++ library build failed!
    pause
    exit /b 1
)

echo Building Sharingway.Net...
dotnet build "Sharingway.Net\Sharingway.Net.csproj" -c %CONFIG% --nologo --verbosity minimal

if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET library build failed!
    pause
    exit /b 1
)

echo.
echo Core libraries built successfully!
echo.

:: Copy C# (.NET) Library
echo Copying C# (.NET) library...
copy "Sharingway.Net\bin\x64\%CONFIG%\net9.0\Sharingway.Net.dll" "%DIST_DIR%\csharp\" > nul
copy "Sharingway.Net\bin\x64\%CONFIG%\net9.0\Sharingway.Net.pdb" "%DIST_DIR%\csharp\" > nul
copy "Sharingway.Net\bin\x64\%CONFIG%\net9.0\Sharingway.Net.xml" "%DIST_DIR%\csharp\" > nul 2>nul

:: Copy C++ Library and Headers
echo Copying C++ library and headers...
copy "Sharingway.native\Sharingway.h" "%DIST_DIR%\cpp\include\" > nul
copy "Sharingway.native\json.hpp" "%DIST_DIR%\cpp\include\" > nul
copy "Sharingway.native\x64\%CONFIG%\Sharingway.native.lib" "%DIST_DIR%\cpp\lib\x64\" > nul
copy "Sharingway.native\x64\%CONFIG%\Sharingway.native.pdb" "%DIST_DIR%\cpp\lib\x64\" > nul

:: Skip demo applications - only distribute core libraries

:: Copy documentation
echo Copying documentation...
copy "README.md" "%DIST_DIR%\docs\README.md" > nul
copy "Demos\README.md" "%DIST_DIR%\docs\DEMOS.md" > nul
copy "LICENSE" "%DIST_DIR%\docs\LICENSE" > nul 2>nul

:: Create integration guide
echo Creating integration guide...
(
echo # Sharingway IPC Framework - Integration Guide
echo.
echo This distribution contains pre-built libraries and headers for easy integration.
echo.
echo ## Contents
echo.
echo - `csharp/` - .NET library ^(Sharingway.Net.dll^)
echo - `cpp/` - C++ headers and static library
echo - `docs/` - Documentation and guides
echo.
echo ## Quick Integration
echo.
echo ### C# Projects
echo.
echo 1. Copy `csharp/Sharingway.Net.dll` to your project
echo 2. Add reference in your .csproj:
echo.
echo ```xml
echo ^<ItemGroup^>
echo   ^<Reference Include="Sharingway.Net"^>
echo     ^<HintPath^>path\to\Sharingway.Net.dll^</HintPath^>
echo   ^</Reference^>
echo ^</ItemGroup^>
echo ```
echo.
echo ### C++ Projects
echo.
echo 1. Copy `cpp/include/` headers to your include path
echo 2. Copy `cpp/lib/x64/Sharingway.native.lib` to your library path
echo 3. Link against Sharingway.native.lib
echo.
echo See docs/README.md for detailed integration instructions.
) > "%DIST_DIR%\INTEGRATION.md"

:: Create version info
echo Creating version information...
(
echo # Sharingway IPC Framework - Distribution
echo.
echo **Build Date:** %DATE% %TIME%
echo **Configuration:** %CONFIG%
echo **Platform:** %PLATFORM%
echo.
echo ## Library Files
echo.
echo ### C# ^(.NET^)
echo - Sharingway.Net.dll - Main library
echo - Sharingway.Net.pdb - Debug symbols
echo.
echo ### C++
echo - Sharingway.h - Main header file
echo - json.hpp - JSON library ^(nlohmann/json^)
echo - Sharingway.native.lib - Static library ^(x64^)
echo - Sharingway.native.pdb - Debug symbols
echo.
echo ## System Requirements
echo.
echo - Windows 10 ^(1809+^) or Windows 11
echo - Visual C++ Redistributable 2022 ^(for C++ apps^)
echo - .NET 9.0 Runtime ^(for C# apps^)
echo.
echo For development, see docs/README.md for full requirements.
) > "%DIST_DIR%\VERSION.txt"

:: Display summary
echo.
echo ======================================
echo Distribution created successfully!
echo ======================================
echo.
echo Location: %CD%\%DIST_DIR%
echo.
echo Contents:
dir "%DIST_DIR%" /b
echo.
echo Integration files:
echo   C#: %DIST_DIR%\csharp\Sharingway.Net.dll
echo   C++: %DIST_DIR%\cpp\include\Sharingway.h
echo   C++: %DIST_DIR%\cpp\lib\x64\Sharingway.native.lib
echo.
echo See %DIST_DIR%\INTEGRATION.md for quick start instructions.
echo See %DIST_DIR%\docs\README.md for detailed documentation.
echo.
pause
