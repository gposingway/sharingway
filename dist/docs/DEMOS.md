# Sharingway IPC Framework - Demo Applications

This folder contains demonstration applications showcasing the Sharingway IPC framework's cross-process, cross-language communication capabilities.

## 🚀 Quick Start

### Running All Demos

The easiest way to see the framework in action:

```cmd
# From the Demos directory
run_all_demos.bat
```

This script will:
1. Build all demo components automatically
2. Launch the Monitor application in its own window
3. Launch the C++ demo application
4. Launch the C# demo application

### Manual Execution

**1. Start the Monitor (Optional but Recommended):**
```cmd
cd monitorApp
dotnet run
```

**2. Start the C++ Demo:**
```cmd
cd nativeApp
nativeApp.exe [optional_provider_name]
```

**3. Start the C# Demo:**
```cmd
cd dotNetApp
dotnet run [optional_provider_name]
```

## 📱 Demo Applications Overview

### 🖥️ Monitor Application (`monitorApp/`)

A comprehensive monitoring tool that visualizes communication between providers.

**Features:**
- Real-time provider discovery and status tracking
- Message count and rate statistics
- Communication matrix showing data flow
- Export statistics to JSON files
- Interactive keyboard controls

**Key Controls:**
- `q` - Quit application
- `h` - Show help screen
- `x` - Toggle communication matrix view
- `j` - Toggle detailed JSON data display
- `m` - Toggle message count display
- `s` - Save current statistics to file

**What You'll See:**
- 🟢 Active providers broadcasting data
- 📡 Inter-demo communication detection
- 📊 Real-time message rates and counts
- 🔄 Provider connection status

### 🔧 C++ Demo Application (`nativeApp/`)

Native C++ application demonstrating high-performance IPC.

**What It Does:**
- **Publishes:** System metrics (CPU, memory, disk I/O) every 3 seconds
- **Subscribes:** Receives data from all other providers
- **Displays:** Clear state screens showing sent/received message counts
- **Features:** Simple "Message Sent #X" notifications

**Data Published:**
```json
{
  "cpu_usage": 45.2,
  "memory_usage": 67.8,
  "disk_io": 1024,
  "timestamp": "2025-05-24T10:30:45",
  "provider_info": "C++ System Monitor"
}
```

### 🎯 C# Demo Application (`dotNetApp/`)

.NET application showcasing managed IPC integration.

**What It Does:**
- **Publishes:** Environmental sensor data every 2.5 seconds
- **Subscribes:** Receives data from all other providers
- **Displays:** Clean state summaries with message tracking
- **Features:** Per-provider message counts and last received data

**Data Published:**
```json
{
  "temperature": 23.5,
  "humidity": 65.2,
  "pressure": 1013.25,
  "timestamp": "2025-05-24T10:30:45.123Z",
  "sensor_id": "ENV_001"
}
```

## 🔍 What to Observe

### Cross-Language Communication

When all demos are running, you'll see:

1. **Provider Discovery:**
   - Each app discovers the others automatically
   - Monitor shows all active providers

2. **Data Exchange:**
   - C++ app sends system metrics
   - C# app sends environmental data
   - Each app receives and displays data from others

3. **Real-Time Updates:**
   - Monitor shows live communication statistics
   - Demo apps display clear state summaries
   - Message counts increment as data flows

### Expected Behavior

**Monitor Application:**
```
Communication Status:
🟢 Multiple providers active - Communication possible!

     🟢 CppDemo --> Broadcasting data (0.3/sec)
     🟢 CSharpDemo --> Broadcasting data (0.4/sec)

📡 Inter-demo communication detected!
     2 providers are broadcasting data
     Each provider can receive data from others
```

**C++ Demo:**
```
=== C++ Sharingway Demo (CppDemo) ===
Status: Running | Messages Sent: 15 | Providers: 2

📤 Published: Message Sent #15

📥 Last Received: CSharpDemo (2 seconds ago)
Data: {
  "temperature": 23.5,
  "humidity": 65.2,
  "timestamp": "2025-05-24T10:30:45.123Z"
}

Messages Received by Provider:
• CSharpDemo: 18 messages
```

**C# Demo:**
```
=== C# Sharingway Demo (CSharpDemo) ===
Status: Running | Messages Sent: 18 | Providers: 2

📤 Published: Message Sent #18

📥 Last Received: CppDemo (1 second ago)
Data: {
  "cpu_usage": 45.2,
  "memory_usage": 67.8,
  "timestamp": "2025-05-24T10:30:45"
}

Messages Received by Provider:
• CppDemo: 15 messages
```

## 🔧 Customization

### Custom Provider Names

Run with custom names to distinguish multiple instances:

```cmd
# C++ Demo
nativeApp.exe MyCustomCppProvider

# C# Demo
dotnet run MyCustomCSharpProvider
```

### Debug Logging

Both demos have debug logging enabled by default. Look for:
- Provider discovery events
- Subscription confirmations
- Data transmission logs

### Message Frequency

Edit the source code to adjust publishing intervals:
- **C++ Demo:** Modify sleep duration in main loop (default: 3000ms)
- **C# Demo:** Modify Task.Delay in publish loop (default: 2500ms)

## 🛠️ Building from Source

### Prerequisites

- Visual Studio 2022 with C++ and .NET workloads
- .NET 9.0 SDK
- Windows 10 SDK

### Manual Build

```cmd
# Build C++ components
msbuild ..\Sharingway.native\Sharingway.native.vcxproj /p:Configuration=Release /p:Platform=x64
msbuild nativeApp\nativeApp.vcxproj /p:Configuration=Release /p:Platform=x64

# Build .NET components
dotnet build ..\Sharingway.Net\Sharingway.Net.csproj -c Release
dotnet build dotNetApp\dotNetApp.csproj -c Release
dotnet build monitorApp\monitorApp.csproj -c Release
```

## 🐛 Troubleshooting

### Common Issues

**"No providers found":**
- Ensure all applications are running as the same user
- Check Windows Defender/antivirus isn't blocking named objects
- Try running as Administrator

**Monitor shows 0 messages:**
- Verify demo applications are actually publishing data
- Check debug output for subscription confirmations
- Restart applications in order: Monitor → C++ → C#

**Build errors:**
- Ensure Visual Studio 2022 is installed with C++ workload
- Verify .NET 9.0 SDK is installed
- Check that all projects can find their dependencies

### Performance Notes

- Default refresh rates are set to reduce console spam
- Monitor updates every 2 seconds (configurable with +/- keys)
- Demo apps use conservative publishing intervals
- Memory usage is minimal (~1MB per provider)

## 📁 Demo Structure

```
Demos/
├── run_all_demos.bat           # Automated build and run script
├── README.md                   # This file
├── dotNetApp/                  # C# demonstration
│   ├── Demo.cs                 # Main demo logic
│   ├── Program.cs              # Entry point
│   └── dotNetApp.csproj        # Project file
├── monitorApp/                 # Monitoring application
│   ├── Monitor.cs              # Main monitor logic
│   ├── Program.cs              # Entry point
│   └── monitorApp.csproj       # Project file
└── nativeApp/                  # C++ demonstration
    ├── nativeApp.cpp           # Main demo logic
    └── nativeApp.vcxproj       # Visual Studio project
```

These demos provide a comprehensive showcase of the Sharingway framework's capabilities and serve as excellent starting points for your own implementations.
