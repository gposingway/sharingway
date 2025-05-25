# Sharingway IPC Framework - Build Distribution (PowerShell)
# Creates a distribution package with libraries, headers, and examples

Write-Host "======================================" -ForegroundColor Green
Write-Host "Sharingway IPC Framework - Build Distribution" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host

# Set variables
$SolutionFile = "Sharingway.sln"
$DistDir = "dist"
$Config = "Release"
$Platform = "x64"

# Clean previous distribution
if (Test-Path $DistDir) {
    Write-Host "Cleaning previous distribution..." -ForegroundColor Yellow
    Remove-Item -Path $DistDir -Recurse -Force
}

# Create distribution directory structure
Write-Host "Creating distribution directories..." -ForegroundColor Blue
$Directories = @(
    $DistDir,
    "$DistDir\csharp",
    "$DistDir\cpp",
    "$DistDir\cpp\include",
    "$DistDir\cpp\lib",
    "$DistDir\cpp\lib\x64",
    "$DistDir\examples",
    "$DistDir\docs"
)

foreach ($Dir in $Directories) {
    New-Item -Path $Dir -ItemType Directory -Force | Out-Null
}

# Build the solution
Write-Host
Write-Host "Building solution in $Config configuration..." -ForegroundColor Blue
$BuildResult = & msbuild $SolutionFile /p:Configuration=$Config /p:Platform=$Platform /nologo /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host

# Copy C# (.NET) Library
Write-Host "Copying C# (.NET) library..." -ForegroundColor Blue
$CSharpSource = "Sharingway.Net\bin\$Config\net9.0"
$CSharpFiles = @("Sharingway.Net.dll", "Sharingway.Net.pdb")

foreach ($File in $CSharpFiles) {
    $SourcePath = Join-Path $CSharpSource $File
    if (Test-Path $SourcePath) {
        Copy-Item -Path $SourcePath -Destination "$DistDir\csharp\" -Force
    }
}

# Try to copy XML documentation if it exists
$XmlDoc = Join-Path $CSharpSource "Sharingway.Net.xml"
if (Test-Path $XmlDoc) {
    Copy-Item -Path $XmlDoc -Destination "$DistDir\csharp\" -Force
}

# Copy C++ Library and Headers
Write-Host "Copying C++ library and headers..." -ForegroundColor Blue
$CppHeaders = @("Sharingway.native\Sharingway.h", "Sharingway.native\json.hpp")
$CppLibs = @("Sharingway.native\x64\$Config\Sharingway.native.lib", "Sharingway.native\x64\$Config\Sharingway.native.pdb")

foreach ($Header in $CppHeaders) {
    if (Test-Path $Header) {
        Copy-Item -Path $Header -Destination "$DistDir\cpp\include\" -Force
    }
}

foreach ($Lib in $CppLibs) {
    if (Test-Path $Lib) {
        Copy-Item -Path $Lib -Destination "$DistDir\cpp\lib\x64\" -Force
    }
}

# Copy demo applications as examples (excluding build outputs)
Write-Host "Copying example applications..." -ForegroundColor Blue
$ExcludePatterns = @("bin", "obj", "x64", ".vs", "*.user")

# Copy Demos folder while excluding build artifacts
Get-ChildItem "Demos" -Recurse | Where-Object {
    $Item = $_
    $ShouldExclude = $false
    
    foreach ($Pattern in $ExcludePatterns) {
        if ($Item.Name -like $Pattern -or $Item.FullName -like "*\$Pattern\*") {
            $ShouldExclude = $true
            break
        }
    }
    
    return -not $ShouldExclude
} | ForEach-Object {
    $RelativePath = $_.FullName.Substring((Get-Item "Demos").FullName.Length + 1)
    $DestPath = Join-Path "$DistDir\examples" $RelativePath
    
    if ($_.PSIsContainer) {
        New-Item -Path $DestPath -ItemType Directory -Force | Out-Null
    } else {
        $DestDir = Split-Path $DestPath -Parent
        if (-not (Test-Path $DestDir)) {
            New-Item -Path $DestDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -Path $_.FullName -Destination $DestPath -Force
    }
}

# Copy documentation
Write-Host "Copying documentation..." -ForegroundColor Blue
$DocFiles = @(
    @{Source = "README.md"; Dest = "README.md"},
    @{Source = "Demos\README.md"; Dest = "DEMOS.md"}
)

foreach ($Doc in $DocFiles) {
    if (Test-Path $Doc.Source) {
        Copy-Item -Path $Doc.Source -Destination "$DistDir\docs\$($Doc.Dest)" -Force
    }
}

# Copy LICENSE if it exists
if (Test-Path "LICENSE") {
    Copy-Item -Path "LICENSE" -Destination "$DistDir\docs\LICENSE" -Force
}

# Create integration guide
Write-Host "Creating integration guide..." -ForegroundColor Blue
$IntegrationGuide = @"
# Sharingway IPC Framework - Integration Guide

This distribution contains pre-built libraries and headers for easy integration.

## Contents

- ``csharp/`` - .NET library (Sharingway.Net.dll)
- ``cpp/`` - C++ headers and static library
- ``examples/`` - Demo applications and source code
- ``docs/`` - Documentation and guides

## Quick Integration

### C# Projects

1. Copy ``csharp/Sharingway.Net.dll`` to your project
2. Add reference in your .csproj:

``````xml
<ItemGroup>
  <Reference Include="Sharingway.Net">
    <HintPath>path\to\Sharingway.Net.dll</HintPath>
  </Reference>
</ItemGroup>
``````

### C++ Projects

1. Copy ``cpp/include/`` headers to your include path
2. Copy ``cpp/lib/x64/Sharingway.native.lib`` to your library path
3. Link against Sharingway.native.lib

See docs/README.md for detailed integration instructions.
"@

Set-Content -Path "$DistDir\INTEGRATION.md" -Value $IntegrationGuide -Encoding UTF8

# Create version info
Write-Host "Creating version information..." -ForegroundColor Blue
$VersionInfo = @"
# Sharingway IPC Framework - Distribution

**Build Date:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Configuration:** $Config
**Platform:** $Platform

## Library Files

### C# (.NET)
- Sharingway.Net.dll - Main library
- Sharingway.Net.pdb - Debug symbols

### C++
- Sharingway.h - Main header file
- json.hpp - JSON library (nlohmann/json)
- Sharingway.native.lib - Static library (x64)
- Sharingway.native.pdb - Debug symbols

## System Requirements

- Windows 10 (1809+) or Windows 11
- Visual C++ Redistributable 2022 (for C++ apps)
- .NET 9.0 Runtime (for C# apps)

For development, see docs/README.md for full requirements.
"@

Set-Content -Path "$DistDir\VERSION.txt" -Value $VersionInfo -Encoding UTF8

# Display summary
Write-Host
Write-Host "======================================" -ForegroundColor Green
Write-Host "Distribution created successfully!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host

Write-Host "Location: " -NoNewline
Write-Host "$(Get-Location)\$DistDir" -ForegroundColor Cyan
Write-Host

Write-Host "Contents:" -ForegroundColor Yellow
Get-ChildItem $DistDir | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host

Write-Host "Integration files:" -ForegroundColor Yellow
Write-Host "  C#: " -NoNewline -ForegroundColor White
Write-Host "$DistDir\csharp\Sharingway.Net.dll" -ForegroundColor Cyan
Write-Host "  C++: " -NoNewline -ForegroundColor White
Write-Host "$DistDir\cpp\include\Sharingway.h" -ForegroundColor Cyan
Write-Host "  C++: " -NoNewline -ForegroundColor White
Write-Host "$DistDir\cpp\lib\x64\Sharingway.native.lib" -ForegroundColor Cyan
Write-Host

Write-Host "See " -NoNewline
Write-Host "$DistDir\INTEGRATION.md" -ForegroundColor Cyan -NoNewline
Write-Host " for quick start instructions."
Write-Host "See " -NoNewline
Write-Host "$DistDir\docs\README.md" -ForegroundColor Cyan -NoNewline
Write-Host " for detailed documentation."
Write-Host

Read-Host "Press Enter to exit"
