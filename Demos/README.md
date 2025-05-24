# Sharingway IPC Framework - Simplified Demo Applications

This folder contains simplified demo applications for the Sharingway IPC framework.

## Overview

The Sharingway IPC framework enables cross-process, cross-language communication using memory-mapped files, named events, and mutexes. These demo applications showcase the framework's capabilities by running as both providers and subscribers simultaneously.

## Demo Applications

### nativeApp_new.cpp (C++ Demo)
A simplified C++ application that runs as both a provider and subscriber simultaneously.

**Features:**
- Publishes sensor data (CPU, memory, disk IO metrics) every 3 seconds
- Discovers and subscribes to all available providers
- Displays received data in real-time
- Cleanly handles application shutdown

**Usage:**
```
nativeApp_new.exe [provider_name]
```

### SimplifiedDemo.cs (C# Demo)
A simplified C# application that runs as both a provider and subscriber simultaneously.

**Features:**
- Publishes environmental sensor data (temperature, humidity, pressure) every 2.5 seconds
- Discovers and subscribes to all available providers
- Displays received data with message counters
- Uses separate tasks for provider and subscriber functionality

**Usage:**
```
dotNetApp SimplifiedDemo [provider_name]
```

### EnhancedMonitor.cs (Monitor Application)
An enhanced monitoring application that provides detailed statistics about the Sharingway framework.

**Features:**
- Displays comprehensive framework statistics
- Shows provider details (capabilities, message rates, data volumes)
- Tracks provider connections/disconnections
- Provides real-time message monitoring
- Shows summary data at regular intervals

**Usage:**
```
monitorApp
```

## Cross-Language Communication Testing

To test cross-language communication:
1. Start the `EnhancedMonitor` application
2. Start the `nativeApp_new` C++ application
3. Start the `SimplifiedDemo` C# application

You should see all providers discovering each other, subscribing to each other's data, and displaying received messages accordingly. The monitor application will show detailed statistics about the communication.

## Framework Features Demonstrated

- Cross-process communication via memory-mapped files
- Cross-language compatibility between C++ and C# implementations
- Provider registration and discovery
- Real-time data publishing and subscribing
- Provider status monitoring
- Framework statistics tracking

## Notes

- All applications enable debug logging for more detailed diagnostic information
- Each application can be terminated cleanly with 'q' key (or Ctrl+C for the monitor)
- Custom provider names can be specified via command line arguments
