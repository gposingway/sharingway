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
- **vcpkg** (for C++ dependencies)

### Building the Framework

1. **Clone the repository**:
   ```bash
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

## 💡 Basic Usage

### Creating a Provider (C++)

```cpp
#include "Sharingway.h"

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

### Creating a Provider (C#)

```csharp
using Sharingway.Net;

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

### Creating a Subscriber (C#)

```csharp
using Sharingway.Net;

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
        
        // Subscribe to a provider
        subscriber.SubscribeTo("MyProvider");
        
        // Keep running
        Console.ReadKey();
    }
    
    static void OnDataReceived(string providerName, JsonElement data) 
    {
        Console.WriteLine($"Received data from {providerName}: {data}");
    }
}
```

## 🎮 Game Integration

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

## 🔧 Advanced Configuration

### Memory Management

- **Default MMF Size**: 1MB per provider
- **Custom Size**: Specify during provider initialization
- **Auto-cleanup**: Automatic resource cleanup on process exit

### Synchronization

- **Named Mutexes**: For thread-safe data access
- **Named Events**: For change notifications
- **Registry Locks**: For provider discovery coordination

### Debug Logging

Enable detailed logging for troubleshooting:

```csharp
// C#
SharingwayUtils.DebugLogging = true;
```

```cpp
// C++ (via preprocessor)
#define SHARINGWAY_DEBUG_LOGGING
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
