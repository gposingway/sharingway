# Sharingway IPC Framework

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Languages](https://img.shields.io/badge/languages-C%2B%2B%20%7C%20C%23-orange)

A high-performance, cross-language Inter-Process Communication (IPC) framework for Windows applications using memory-mapped files, named synchronization objects, and a centralized registry system.

## 🚀 Features

- **Cross-Language Support**: Native C++ and .NET implementations
- **High Performance**: Memory-mapped files for zero-copy data sharing
- **Thread-Safe**: Built-in synchronization using named mutexes and events
- **Provider Discovery**: Automatic registry-based provider discovery
- **Real-Time Monitoring**: Comprehensive monitoring and statistics
- **Easy Integration**: Simple APIs for both providers and subscribers
- **Game Engine Ready**: Perfect for Dalamud plugins, ReShade addons, and other game modifications

## 📁 Project Structure

```
Sharingway/
├── Sharingway.native/          # Native C++ implementation
├── Sharingway.Net/             # .NET wrapper library
├── Demos/                      # Example applications
│   ├── nativeApp/             # C++ demo application
│   ├── dotNetApp/             # C# demo application
│   ├── monitorApp/            # Monitoring application
│   └── run_all_demos.bat     # Build and run script
└── README.md                  # This file
```

## 🔧 Getting Started

### Prerequisites

- **Windows 10/11** (x64)
- **Visual Studio 2022** with C++ and .NET workloads
- **.NET 9.0 SDK**
- **vcpkg** (for C++ dependencies, optional)

### Quick Start Options

#### Option 1: Use Pre-built Distribution (Recommended)

```cmd
# Clone and build distribution
git clone https://github.com/yourusername/sharingway.git
cd sharingway
build_dist.bat
```

The `dist/` folder will contain everything you need for integration.

#### Option 2: Build from Source

1. **Clone the repository**:
   ```cmd
   git clone https://github.com/yourusername/sharingway.git
   cd sharingway
   ```

2. **Build from Visual Studio**:
   - Open `Sharingway.sln` in Visual Studio 2022
   - Set to Debug/Release and x64 platform
   - Build Solution (Ctrl+Shift+B)

3. **Build from Command Line**:
   ```cmd
   msbuild Sharingway.sln /p:Configuration=Release /p:Platform=x64
   ```

### Running the Demos

Execute the demo script to see the framework in action:

```cmd
cd Demos
run_all_demos.bat
```

This will:
1. Build all components
2. Launch the Monitor application
3. Launch the C++ demo provider/subscriber
4. Launch the C# demo provider/subscriber

## 🔧 Integration Guide

### 📦 Using Pre-built Distribution (Recommended)

The easiest way to integrate Sharingway into your project:

#### 1. Build Distribution Package

```cmd
# From the root Sharingway directory
build_dist.bat
```

This creates a `dist/` folder with:
- `csharp/` - .NET library (Sharingway.Net.dll)
- `cpp/` - C++ headers and static library
- `examples/` - Demo applications and source code
- `docs/` - Documentation and integration guides

#### 2. Integration Instructions

See `dist/INTEGRATION.md` for quick start instructions, or follow the detailed steps below.

### Adding Sharingway to Your C# Project

#### Method 1: Using Distribution Package (Recommended)

```xml
<!-- In your .csproj file -->
<ItemGroup>
  <Reference Include="Sharingway.Net">
    <HintPath>path\to\dist\csharp\Sharingway.Net.dll</HintPath>
  </Reference>
</ItemGroup>
```

#### Method 2: Project Reference (Development)

Add the Sharingway.Net project to your solution:

```xml
<!-- In your .csproj file -->
<ItemGroup>
  <ProjectReference Include="path\to\Sharingway.Net\Sharingway.Net.csproj" />
</ItemGroup>
```

#### Method 3: NuGet Package (Future)

Once published to NuGet:

```xml
<PackageReference Include="Sharingway.Net" Version="1.0.0" />
```

**Basic C# Implementation:**

```csharp
using Sharingway.Net;
using System.Text.Json;

class Program 
{
    static void Main(string[] args) 
    {
        var capabilities = new List<string> { "sensor", "data" };
        using var provider = new Provider("MyProvider", "Sample provider", capabilities);
        
        if (!provider.Initialize()) 
        {
            Console.WriteLine("Failed to initialize provider");
            return;
        }
        
        // Publish data
        var data = new {
            temperature = 23.5,
            humidity = 65.2,
            timestamp = DateTimeOffset.Now
        };
        
        provider.PublishData(data);
    }
}
```

### Adding Sharingway to Your C++ Project

#### Method 1: Using Distribution Package (Recommended)

1. **Copy Files from Distribution:**
   ```
   From dist/cpp/:
   ├── include/
   │   ├── Sharingway.h          # Main header file
   │   └── json.hpp              # JSON library (nlohmann/json)
   └── lib/x64/
       └── Sharingway.native.lib # Static library
   ```

2. **Visual Studio Project Configuration:**

   - **Include Directories:**
     - Project Properties → Configuration Properties → C/C++ → General
     - Add `path\to\dist\cpp\include` to "Additional Include Directories"

   - **Library Directories:**
     - Project Properties → Configuration Properties → Linker → General
     - Add `path\to\dist\cpp\lib\x64` to "Additional Library Directories"

   - **Link Libraries:**
     - Project Properties → Configuration Properties → Linker → Input
     - Add `Sharingway.native.lib` to "Additional Dependencies"

3. **CMake Configuration:**

   ```cmake
   # CMakeLists.txt
   cmake_minimum_required(VERSION 3.20)
   project(MyProject)

   set(CMAKE_CXX_STANDARD 17)
   set(CMAKE_CXX_STANDARD_REQUIRED ON)

   # Add Sharingway from distribution
   set(SHARINGWAY_DIST_DIR "path/to/dist/cpp")
   include_directories("${SHARINGWAY_DIST_DIR}/include")

   # Add executable
   add_executable(MyProject main.cpp)

   # Link Sharingway library
   target_link_libraries(MyProject 
       "${SHARINGWAY_DIST_DIR}/lib/x64/Sharingway.native.lib"
   )
   ```

#### Method 2: Source Integration (Development)

Copy the following files to your project:

```
From Sharingway.native/:
├── Sharingway.h          # Main header file
├── json.hpp              # JSON library (nlohmann/json)
└── x64/Debug|Release/
    └── Sharingway.native.lib  # Static library
```

**Visual Studio Configuration:**

1. **Include Directories**:
   - Project Properties → Configuration Properties → C/C++ → General
   - Add the path containing `Sharingway.h` to "Additional Include Directories"

2. **Library Directories**:
   - Project Properties → Configuration Properties → Linker → General
   - Add the path containing `Sharingway.native.lib` to "Additional Library Directories"

3. **Link Libraries**:
   - Project Properties → Configuration Properties → Linker → Input
   - Add `Sharingway.native.lib` to "Additional Dependencies"

#### Method 3: vcpkg Integration (Alternative)

If using vcpkg for dependency management:

```bash
# Install nlohmann-json dependency
vcpkg install nlohmann-json:x64-windows

# In your CMakeLists.txt
find_package(nlohmann_json CONFIG REQUIRED)
target_link_libraries(MyProject PRIVATE nlohmann_json::nlohmann_json)
```

**Basic C++ Implementation:**

```cpp
#include "Sharingway.h"
#include <iostream>
#include <memory>

int main() {
    // Initialize the provider
    auto provider = std::make_unique<Provider>("MyProvider", "Sample provider", 
                                             std::vector<std::string>{"sensor", "data"});
    
    if (!provider->Initialize()) {
        std::cerr << "Failed to initialize provider" << std::endl;
        return 1;
    }
    
    // Publish data
    nlohmann::json data = {
        {"temperature", 23.5},
        {"humidity", 65.2},
        {"timestamp", std::time(nullptr)}
    };
    
    provider->PublishData(data);
    
    return 0;
}
```

## 💡 Basic Usage Examples

### Creating a Subscriber (C#)

```csharp
using Sharingway.Net;
using System.Text.Json;

class Program 
{
    static void Main(string[] args) 
    {
        using var subscriber = new Subscriber();
        
        if (!subscriber.Initialize()) 
        {
            Console.WriteLine("Failed to initialize subscriber");
            return;
        }

        // Set up data handler
        subscriber.SetDataUpdateHandler(OnDataReceived);
        
        // Subscribe to all available providers
        var providers = subscriber.GetAvailableProviders();
        foreach (var provider in providers)
        {
            subscriber.SubscribeTo(provider.Name);
            Console.WriteLine($"Subscribed to: {provider.Name}");
        }
        
        // Keep running
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
    
    static void OnDataReceived(string providerName, JsonElement data) 
    {
        Console.WriteLine($"Received data from {providerName}: {data}");
    }
}
```

### Creating a Subscriber (C++)

```cpp
#include "Sharingway.h"
#include <iostream>
#include <memory>

void OnDataReceived(const std::string& providerName, const nlohmann::json& data) {
    std::cout << "Received data from " << providerName << ": " << data.dump(2) << std::endl;
}

int main() {
    auto subscriber = std::make_unique<Subscriber>();
    
    if (!subscriber->Initialize()) {
        std::cerr << "Failed to initialize subscriber" << std::endl;
        return 1;
    }
    
    // Set up data handler
    subscriber->SetDataUpdateHandler(OnDataReceived);
    
    // Subscribe to all available providers
    auto providers = subscriber->GetAvailableProviders();
    for (const auto& provider : providers) {
        subscriber->SubscribeTo(provider.Name);
        std::cout << "Subscribed to: " << provider.Name << std::endl;
    }
    
    // Keep running
    std::cout << "Press Enter to exit..." << std::endl;
    std::cin.get();
    
    return 0;
}
```

## 📚 API Reference

### Provider Class

**C# API:**
```csharp
public class Provider : IDisposable
{
    // Constructor
    public Provider(string name, string description, List<string> capabilities)
    
    // Core methods
    public bool Initialize()                    // Initialize the provider
    public void PublishData(object data)       // Publish JSON-serializable data
    public void Dispose()                      // Clean up resources
}
```

**C++ API:**
```cpp
class Provider
{
public:
    // Constructor
    Provider(const std::string& name, const std::string& description, 
             const std::vector<std::string>& capabilities);
             
    // Core methods
    bool Initialize();                         // Initialize the provider
    void PublishData(const nlohmann::json& data); // Publish JSON data
    ~Provider();                              // Destructor - cleans up resources
};
```

### Subscriber Class

**C# API:**
```csharp
public class Subscriber : IDisposable
{
    // Core methods
    public bool Initialize()                                    // Initialize subscriber
    public List<ProviderInfo> GetAvailableProviders()         // Get all providers
    public void SubscribeTo(string providerName)              // Subscribe to provider
    public void UnsubscribeFrom(string providerName)          // Unsubscribe from provider
    
    // Event handlers
    public void SetDataUpdateHandler(Action<string, JsonElement> handler)
    public void SetProviderChangeHandler(Action<string, ProviderStatus> handler)
    
    public void Dispose()                                      // Clean up resources
}
```

**C++ API:**
```cpp
class Subscriber
{
public:
    // Core methods
    bool Initialize();                                         // Initialize subscriber
    std::vector<ProviderInfo> GetAvailableProviders();       // Get all providers
    void SubscribeTo(const std::string& providerName);       // Subscribe to provider
    void UnsubscribeFrom(const std::string& providerName);   // Unsubscribe from provider
    
    // Event handlers
    void SetDataUpdateHandler(std::function<void(const std::string&, const nlohmann::json&)> handler);
    void SetProviderChangeHandler(std::function<void(const std::string&, ProviderStatus)> handler);
    
    ~Subscriber();                                            // Destructor
};
```

### Data Structures

**ProviderInfo:**
```csharp
public class ProviderInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Capabilities { get; set; }
}
```

**ProviderStatus Enum:**
```csharp
public enum ProviderStatus
{
    Online,      // Provider is available
    Offline      // Provider is no longer available
}
```

## 🚀 Deployment Guide

### Creating Distribution Package

Create a ready-to-use distribution package:

```cmd
# From the root Sharingway directory
build_dist.bat
```

This creates a `dist/` folder containing:
- Pre-built libraries (C# and C++)
- Headers and dependencies
- Example applications
- Integration documentation

### Development Environment Setup

1. **Install Prerequisites:**
   ```cmd
   # Install Visual Studio 2022 with C++ and .NET workloads
   # Install .NET 9.0 SDK
   winget install Microsoft.VisualStudio.2022.Community
   winget install Microsoft.DotNet.SDK.9
   ```

2. **Clone and Build:**
   ```cmd
   git clone https://github.com/yourusername/sharingway.git
   cd sharingway
   
   # Build all projects
   msbuild Sharingway.sln /p:Configuration=Release /p:Platform=x64
   
   # Or create distribution package
   build_dist.bat
   ```

### Production Deployment

#### For C# Applications

**Option 1: Self-Contained Deployment**
```cmd
# Publish as self-contained (includes runtime)
dotnet publish YourApp.csproj -c Release -r win-x64 --self-contained true
```

**Option 2: Framework-Dependent Deployment**
```cmd
# Requires .NET 9.0 runtime on target machine
dotnet publish YourApp.csproj -c Release -r win-x64 --self-contained false
```

**Required Files for Distribution:**
```
YourApp/
├── YourApp.exe
├── YourApp.dll
├── Sharingway.Net.dll          # Required - from dist/csharp/
└── runtime dependencies...
```

#### For C++ Applications

**Static Linking (Recommended):**
- Link `Sharingway.native.lib` statically (from `dist/cpp/lib/x64/`)
- Include headers from `dist/cpp/include/`
- Distribute single executable

**Required Files for Distribution:**
```
YourApp/
├── YourApp.exe                 # Your application
└── (No additional DLLs needed with static linking)
```

### System Requirements

**Runtime Requirements:**
- Windows 10 version 1809 or later (x64)
- Visual C++ Redistributable 2022 (for C++ applications)
- .NET 9.0 Runtime (for C# applications, if not self-contained)

**Development Requirements:**
- Visual Studio 2022 with C++ and .NET workloads
- Windows 10 SDK (latest version)
- vcpkg (for C++ dependency management)

### Security Considerations

**Permissions:**
- Applications may require elevated privileges for creating named objects
- Memory-mapped files are created with appropriate security descriptors
- Consider running as limited user where possible

### Security Considerations

**Permissions:**
- Applications may require elevated privileges for creating named objects
- Memory-mapped files are created with appropriate security descriptors
- Consider running as limited user where possible

**Best Practices:**
- Validate all incoming data from providers
- Implement proper error handling for IPC failures
- Use appropriate timeouts for operations
- Monitor memory usage with large datasets

## 🎮 Game Development Integration

### Dalamud Plugin Integration (Final Fantasy XIV)

Perfect for creating data-sharing plugins for FFXIV:

**1. Add to your Dalamud plugin project:**
```xml
<!-- In your .csproj file -->
<ItemGroup>
  <ProjectReference Include="path\to\Sharingway.Net\Sharingway.Net.csproj" />
</ItemGroup>
```

**2. Plugin Implementation Example:**
```csharp
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Sharingway.Net;

public class MyFFXIVPlugin : IDalamudPlugin
{
    private Provider? _provider;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targetManager;
    
    public MyFFXIVPlugin(IClientState clientState, ITargetManager targetManager)
    {
        _clientState = clientState;
        _targetManager = targetManager;
        
        var capabilities = new List<string> { "player-data", "combat-data", "game-state" };
        _provider = new Provider("FFXIVPlugin", "FFXIV game data provider", capabilities);
        
        if (_provider.Initialize())
        {
            // Start publishing game data
            Task.Run(PublishGameData);
        }
    }
    
    private async Task PublishGameData()
    {
        while (_provider != null)
        {
            if (_clientState.IsLoggedIn && _clientState.LocalPlayer != null)
            {
                var gameData = new {
                    player = new {
                        name = _clientState.LocalPlayer.Name.ToString(),
                        level = _clientState.LocalPlayer.Level,
                        job = _clientState.LocalPlayer.ClassJob.GameData?.NameEnglish.ToString(),
                        hp = _clientState.LocalPlayer.CurrentHp,
                        mp = _clientState.LocalPlayer.CurrentMp,
                        position = new {
                            x = _clientState.LocalPlayer.Position.X,
                            y = _clientState.LocalPlayer.Position.Y,
                            z = _clientState.LocalPlayer.Position.Z
                        }
                    },
                    target = _targetManager.Target?.Name.ToString(),
                    zone = _clientState.TerritoryType,
                    timestamp = DateTimeOffset.Now
                };
                
                _provider.PublishData(gameData);
            }
            
            await Task.Delay(1000); // Update every second
        }
    }
    
    public void Dispose()
    {
        _provider?.Dispose();
        _provider = null;
    }
}
```

### ReShade Addon Integration

Ideal for graphics overlays and game enhancement tools:

**1. Project Setup:**
```cpp
// Include required headers
#include "Sharingway.h"
#include <reshade.hpp>
#include <memory>

static std::unique_ptr<Provider> g_sharingwayProvider;
```

**2. ReShade Addon Implementation:**
```cpp
extern "C" __declspec(dllexport) const char *NAME = "SharingwayGraphicsAddon";
extern "C" __declspec(dllexport) const char *DESCRIPTION = "ReShade Sharingway Integration";

BOOL APIENTRY DllMain(HMODULE hModule, DWORD fdwReason, LPVOID)
{
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        // Initialize Sharingway provider
        g_sharingwayProvider = std::make_unique<Provider>(
            "ReShadeGraphics", 
            "ReShade graphics and performance data", 
            std::vector<std::string>{"graphics", "fps", "performance"}
        );
        
        if (!g_sharingwayProvider->Initialize()) {
            // Handle initialization failure
            g_sharingwayProvider.reset();
        }
        break;
        
    case DLL_PROCESS_DETACH:
        g_sharingwayProvider.reset();
        break;
    }
    return TRUE;
}

// ReShade event handlers
void on_present(reshade::api::command_queue*, reshade::api::swapchain* swapchain)
{
    if (!g_sharingwayProvider) return;
    
    // Get frame timing and performance data
    static auto lastFrameTime = std::chrono::high_resolution_clock::now();
    auto currentTime = std::chrono::high_resolution_clock::now();
    auto frameDuration = std::chrono::duration_cast<std::chrono::microseconds>(
        currentTime - lastFrameTime).count();
    
    // Get display information
    reshade::api::resource backBuffer = {};
    swapchain->get_current_back_buffer(&backBuffer);
    
    auto desc = swapchain->get_desc();
    
    // Publish frame and performance data
    nlohmann::json frameData = {
        {"fps", frameDuration > 0 ? 1000000.0 / frameDuration : 0},
        {"frame_time_us", frameDuration},
        {"resolution", {desc.back_buffer_width, desc.back_buffer_height}},
        {"format", static_cast<int>(desc.back_buffer_format)},
        {"timestamp", std::time(nullptr)},
        {"process_name", GetCurrentProcessName()}
    };
    
    g_sharingwayProvider->PublishData(frameData);
    lastFrameTime = currentTime;
}

// Register ReShade callbacks
extern "C" __declspec(dllexport) bool ReShadeAddonInit(HMODULE hModule, 
    const reshade::api::addon_event* events, size_t numEvents)
{
    // Register for present events
    return reshade::register_addon(hModule);
}
```

### Unity Game Engine Integration

For Unity-based games and tools:

**1. Create Unity Plugin Structure:**
```
Assets/
└── Plugins/
    ├── Sharingway.Net.dll          # Copy from build output
    └── SharingwayUnityPlugin.cs    # Your integration script
```

**2. Unity Script Example:**
```csharp
using UnityEngine;
using Sharingway.Net;
using System.Collections.Generic;

public class SharingwayUnityPlugin : MonoBehaviour
{
    private Provider _provider;
    private Subscriber _subscriber;
    
    [Header("Sharingway Settings")]
    public string providerName = "UnityGame";
    public float updateRate = 1.0f;
    
    void Start()
    {
        // Initialize provider
        var capabilities = new List<string> { "game-data", "unity", "player-stats" };
        _provider = new Provider(providerName, "Unity game data provider", capabilities);
        
        if (_provider.Initialize())
        {
            Debug.Log("Sharingway provider initialized successfully");
            InvokeRepeating(nameof(PublishGameData), 1.0f, updateRate);
        }
        
        // Initialize subscriber (optional)
        _subscriber = new Subscriber();
        if (_subscriber.Initialize())
        {
            _subscriber.SetDataUpdateHandler(OnDataReceived);
            // Subscribe to other providers as needed
        }
    }
    
    void PublishGameData()
    {
        if (_provider == null) return;
        
        var gameData = new {
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            fps = (int)(1.0f / Time.deltaTime),
            player_count = FindObjectsOfType<Player>().Length,
            game_time = Time.timeSinceLevelLoad,
            camera_position = Camera.main?.transform.position,
            timestamp = System.DateTimeOffset.Now
        };
        
        _provider.PublishData(gameData);
    }
    
    void OnDataReceived(string providerName, System.Text.Json.JsonElement data)
    {
        Debug.Log($"Received data from {providerName}: {data}");
        // Process received data as needed
    }
    
    void OnDestroy()
    {
        _provider?.Dispose();
        _subscriber?.Dispose();
    }
}
```

### Unreal Engine Integration

For Unreal Engine C++ projects:

**1. Add to your .Build.cs file:**
```csharp
// YourProject.Build.cs
public class YourProject : ModuleRules
{
    public YourProject(ReadOnlyTargetRules Target) : base(Target)
    {
        // ... existing configuration ...
        
        // Add Sharingway include path
        PublicIncludePaths.Add("Path/To/Sharingway/Headers");
        
        // Link Sharingway library
        PublicAdditionalLibraries.Add("Path/To/Sharingway.native.lib");
    }
}
```

**2. Unreal Actor Implementation:**
```cpp
// SharingwayGameActor.h
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Sharingway.h"
#include "SharingwayGameActor.generated.h"

UCLASS()
class YOURGAME_API ASharingwayGameActor : public AActor
{
    GENERATED_BODY()
    
public:
    ASharingwayGameActor();
    
protected:
    virtual void BeginPlay() override;
    virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
    virtual void Tick(float DeltaTime) override;
    
private:
    std::unique_ptr<Provider> SharingwayProvider;
    FTimerHandle PublishTimer;
    
    UFUNCTION()
    void PublishGameData();
};

// SharingwayGameActor.cpp
#include "SharingwayGameActor.h"
#include "Engine/Engine.h"
#include "Engine/World.h"

ASharingwayGameActor::ASharingwayGameActor()
{
    PrimaryActorTick.bCanEverTick = false;
}

void ASharingwayGameActor::BeginPlay()
{
    Super::BeginPlay();
    
    // Initialize Sharingway provider
    std::vector<std::string> capabilities = {"unreal", "game-data", "performance"};
    SharingwayProvider = std::make_unique<Provider>("UnrealGame", 
        "Unreal Engine game data", capabilities);
    
    if (SharingwayProvider->Initialize())
    {
        UE_LOG(LogTemp, Log, TEXT("Sharingway provider initialized"));
        
        // Start publishing data every second
        GetWorld()->GetTimerManager().SetTimer(PublishTimer, this, 
            &ASharingwayGameActor::PublishGameData, 1.0f, true);
    }
}

void ASharingwayGameActor::PublishGameData()
{
    if (!SharingwayProvider) return;
    
    nlohmann::json gameData = {
        {"level_name", TCHAR_TO_UTF8(*GetWorld()->GetMapName())},
        {"fps", FApp::GetDeltaTime() > 0 ? 1.0f / FApp::GetDeltaTime() : 0},
        {"player_count", GetWorld()->GetNumPlayers()},
        {"game_time", GetWorld()->GetTimeSeconds()},
        {"timestamp", std::time(nullptr)}
    };
    
    SharingwayProvider->PublishData(gameData);
}

void ASharingwayGameActor::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
    GetWorld()->GetTimerManager().ClearTimer(PublishTimer);
    SharingwayProvider.reset();
    Super::EndPlay(EndPlayReason);
}
```

### Dalamud Plugin Integration

For **Final Fantasy XIV** Dalamud plugins:

1. **Add NuGet Reference**:
   ```xml
   <PackageReference Include="Sharingway.Net" Version="1.0.0" />
   ```

2. **Plugin Implementation**:
   ```csharp
   using Dalamud.Plugin;
   using Sharingway.Net;
   
   public class MyPlugin : IDalamudPlugin
   {
       private Provider? _provider;
       
       public void Initialize(DalamudPluginInterface pluginInterface)
       {
           var capabilities = new List<string> { "player-data", "game-state" };
           _provider = new Provider("FFXIVPlugin", "FFXIV data provider", capabilities);
           _provider.Initialize();
       }
       
       public void Update()
       {
           // Publish game data
           var gameData = new {
               player = GetPlayerInfo(),
               zone = GetCurrentZone(),
               timestamp = DateTimeOffset.Now
           };
           
           _provider?.PublishData(gameData);
       }
       
       public void Dispose()
       {
           _provider?.Dispose();
       }
   }
   ```

### ReShade Addon Integration

For **ReShade** addons using C++:

1. **Include Headers**:
   ```cpp
   #include "Sharingway.h"
   #include <reshade.hpp>
   ```

2. **Addon Implementation**:
   ```cpp
   static std::unique_ptr<Provider> g_provider;
   
   extern "C" __declspec(dllexport) const char *NAME = "SharingwayAddon";
   extern "C" __declspec(dllexport) const char *DESCRIPTION = "ReShade Sharingway Integration";
   
   BOOL APIENTRY DllMain(HMODULE hModule, DWORD fdwReason, LPVOID)
   {
       switch (fdwReason)
       {
       case DLL_PROCESS_ATTACH:
           // Initialize provider
           g_provider = std::make_unique<Provider>("ReShadeAddon", 
                                                 "ReShade graphics data", 
                                                 std::vector<std::string>{"graphics", "fps"});
           g_provider->Initialize();
           break;
           
       case DLL_PROCESS_DETACH:
           g_provider.reset();
           break;
       }
       return TRUE;
   }
   
   void on_present(reshade::api::command_queue*, reshade::api::swapchain*)
   {
       // Publish frame data
       nlohmann::json frameData = {
           {"fps", GetCurrentFPS()},
           {"resolution", {GetScreenWidth(), GetScreenHeight()}},
           {"timestamp", std::time(nullptr)}
       };
       
       g_provider->PublishData(frameData);
   }
   ```

## 🛠️ Troubleshooting & FAQ

### Common Integration Issues

**Q: "Failed to initialize provider" error**

A: This usually indicates permission or naming issues:
```csharp
// Solution 1: Check for naming conflicts
var uniqueName = $"MyProvider_{Process.GetCurrentProcess().Id}";
var provider = new Provider(uniqueName, "My Provider", capabilities);

// Solution 2: Run as Administrator (if required)
// Right-click your application → "Run as administrator"

// Solution 3: Check for special characters in names
// Avoid spaces, special chars in provider names
var safeName = "MyProvider_Safe_Name";
```

**Q: Providers not discovering each other**

A: Registry synchronization issues:
```csharp
// Add retry logic for provider discovery
var subscriber = new Subscriber();
subscriber.Initialize();

var maxRetries = 5;
for (int i = 0; i < maxRetries; i++)
{
    var providers = subscriber.GetAvailableProviders();
    if (providers.Count > 0) break;
    
    Thread.Sleep(1000); // Wait 1 second between retries
}
```

**Q: Memory-mapped file access denied**

A: Usually occurs with insufficient permissions:
```cpp
// C++ solution: Check return values
auto provider = std::make_unique<Provider>("MyProvider", "Description", capabilities);
if (!provider->Initialize()) {
    // Log the specific error
    std::cerr << "Provider initialization failed - check permissions" << std::endl;
    // Try running as administrator
}
```

**Q: Data not being received by subscribers**

A: Verify subscription and handler setup:
```csharp
// Ensure proper handler setup
subscriber.SetDataUpdateHandler((providerName, data) => {
    Console.WriteLine($"Received: {providerName} - {data}");
});

// Verify subscription
var providers = subscriber.GetAvailableProviders();
foreach (var provider in providers)
{
    subscriber.SubscribeTo(provider.Name);
    Console.WriteLine($"Subscribed to: {provider.Name}");
}
```

### Performance Optimization

**Large Data Payloads:**
```csharp
// Compress large JSON payloads
var largeData = GetLargeDataSet();
var compressedData = new {
    compressed = true,
    data = CompressJson(largeData),
    original_size = JsonSerializer.Serialize(largeData).Length
};
provider.PublishData(compressedData);
```

**High-Frequency Updates:**
```cpp
// Batch multiple updates
class BatchedProvider {
    std::vector<nlohmann::json> batch;
    std::chrono::steady_clock::time_point lastFlush;
    
public:
    void QueueData(const nlohmann::json& data) {
        batch.push_back(data);
        
        auto now = std::chrono::steady_clock::now();
        if (batch.size() >= 10 || 
            std::chrono::duration_cast<std::chrono::milliseconds>(now - lastFlush).count() > 100) {
            FlushBatch();
        }
    }
    
private:
    void FlushBatch() {
        if (batch.empty()) return;
        
        nlohmann::json batchedData = {
            {"batch", batch},
            {"count", batch.size()},
            {"timestamp", std::time(nullptr)}
        };
        
        provider->PublishData(batchedData);
        batch.clear();
        lastFlush = std::chrono::steady_clock::now();
    }
};
```

### Debugging Tips

**Enable Debug Logging:**
```csharp
// C# - Add at application startup
SharingwayUtils.DebugLogging = true;

// This will output detailed logs about:
// - Provider registration/unregistration
// - Memory-mapped file operations
// - Data publication events
// - Subscription activities
```

```cpp
// C++ - Define before including headers
#define SHARINGWAY_DEBUG_LOGGING
#include "Sharingway.h"
```

**Monitor Memory Usage:**
```csharp
// Check memory-mapped file sizes
var process = Process.GetCurrentProcess();
Console.WriteLine($"Working Set: {process.WorkingSet64 / 1024 / 1024} MB");
Console.WriteLine($"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024} MB");
```

**Validate JSON Data:**
```csharp
// Ensure your data is JSON-serializable
try 
{
    var json = JsonSerializer.Serialize(yourData);
    var parsed = JsonSerializer.Deserialize<object>(json);
    provider.PublishData(yourData); // Safe to publish
}
catch (JsonException ex)
{
    Console.WriteLine($"JSON serialization error: {ex.Message}");
    // Fix your data structure
}
```

### Platform-Specific Notes

**Windows Version Compatibility:**
- **Windows 10 1809+**: Full support
- **Windows 11**: Recommended platform
- **Server 2019+**: Supported

**Architecture Support:**
- **x64**: Fully supported and recommended
- **x86**: Not officially supported
- **ARM64**: Not currently supported

**Antivirus Considerations:**
```csharp
// If Windows Defender blocks named objects:
// 1. Add your application to exclusions
// 2. Or use specific security descriptors
var provider = new Provider("MyProvider", "Description", capabilities);
// The framework handles appropriate security descriptors automatically
```

### Best Practices Summary

**✅ Do:**
- Use meaningful provider names without special characters
- Implement proper error handling and retries
- Monitor memory usage with large datasets
- Validate all JSON data before publishing
- Use debug logging during development
- Test with multiple providers running simultaneously

**❌ Don't:**
- Use special characters or spaces in provider names
- Publish extremely large payloads (>10MB) without compression
- Ignore initialization failures
- Assume providers will always be available
- Forget to dispose of resources properly
- Block UI threads with IPC operations

**📊 Performance Guidelines:**
- **Small frequent updates**: < 1KB every 100ms ✅
- **Medium updates**: < 100KB every second ✅  
- **Large updates**: < 1MB every 5+ seconds ✅
- **Huge updates**: > 10MB - consider file sharing instead ⚠️

## 🔧 Advanced Configuration & Monitoring

### Memory Management

**Default Configuration:**
- **MMF Size**: 1MB per provider (automatically managed)
- **Auto-cleanup**: Resources cleaned up on process exit
- **Buffer Management**: Circular buffer for historical data

**Custom Memory Configuration:**
```cpp
// C++ - Custom memory size (future enhancement)
Provider provider("MyProvider", "Description", capabilities, 5 * 1024 * 1024); // 5MB
```

```csharp
// C# - Monitor memory usage
var provider = new Provider("MyProvider", "Description", capabilities);
// Memory size is automatically determined based on data patterns
```

### Synchronization Mechanisms

The framework uses multiple Windows synchronization primitives:

- **Named Mutexes**: Thread-safe data access across processes
- **Named Events**: Change notifications and signaling
- **Memory-Mapped Files**: Zero-copy data sharing
- **Registry Locks**: Coordinated provider discovery

### Debug Logging Configuration

**Detailed Logging Levels:**
```csharp
// C# - Enable different logging levels
SharingwayUtils.DebugLogging = true;

// Available log categories (automatically handled):
// - Provider lifecycle events
// - Data publication/subscription
// - Memory-mapped file operations
// - Registry management
// - Error conditions and recovery
```

**Log Output Examples:**
```
[DEBUG] Provider 'MyProvider' registered successfully
[DEBUG] MMF created: size=1048576, name=SharingwayMMF_MyProvider
[DEBUG] Data published: 247 bytes to MyProvider
[DEBUG] Subscriber connected to provider: MyProvider
[DEBUG] Provider 'MyProvider' went offline
```

## 📊 Monitoring and Statistics

The framework includes a powerful monitoring application that provides:

- **Real-time Statistics**: Message rates, provider status, data volumes
- **Provider Discovery**: Automatic detection of new providers
- **Performance Metrics**: Latency and throughput analysis
- **Export Capabilities**: Save statistics to JSON files

Launch the monitor:
```cmd
cd Demos
dotnet run --project monitorApp
```

## 🛠️ Troubleshooting

### Common Issues

1. **"Failed to initialize provider"**
   - Ensure running as Administrator (for named object creation)
   - Check Windows permissions for memory-mapped files

2. **"No providers found"**
   - Verify registry service is running
   - Check firewall settings for local communication

3. **Build Errors**
   - Ensure Visual Studio 2022 with C++ workload
   - Install .NET 9.0 SDK
   - Update vcpkg packages

### Performance Tips

- Use appropriate MMF sizes for your data
- Minimize JSON serialization overhead
- Consider data batching for high-frequency updates
- Monitor memory usage with large datasets

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Memory-mapped file implementation inspired by Windows SDK documentation
- JSON handling via [nlohmann/json](https://github.com/nlohmann/json) for C++
- .NET System.Text.Json for C# implementation

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/sharingway/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/sharingway/discussions)
- **Documentation**: [Wiki](https://github.com/yourusername/sharingway/wiki)

---

**Built with ❤️ for the Windows development community**
