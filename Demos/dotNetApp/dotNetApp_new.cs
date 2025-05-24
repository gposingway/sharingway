using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sharingway.Net;

namespace SharingwayDemo
{
    [SupportedOSPlatform("windows")]
    class DotNetApp
    {
        // Shared state flags and monitoring variables
        private static readonly CancellationTokenSource CancellationSource = new();
        private static readonly Random Random = new();
        private static readonly object ConsoleLock = new();
        private static bool _isRunning = true;
        private static int _receivedMessageCount = 0;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Sharingway .NET Demo Application (Simplified)");
            Console.WriteLine("============================================");
            
            // Enable debug logging for better diagnostics
            SharingwayUtils.DebugLogging = true;
            SharingwayUtils.DebugLog("DotNetApp starting with debug logging enabled", "DotNetApp");
            
            // Get provider name from command line or use default
            string providerName = args.Length > 0 ? args[0] : "DotNetProvider";
            
            Console.WriteLine($"Starting with provider name: {providerName}");
            Console.WriteLine("This application runs as both provider and subscriber simultaneously");
            Console.WriteLine("Press 'q' to quit");
            Console.WriteLine();
            
            // Start the provider and subscriber tasks
            var providerTask = Task.Run(() => RunProvider(providerName, CancellationSource.Token));
            var subscriberTask = Task.Run(() => RunSubscriber(CancellationSource.Token));
            
            // Wait for quit signal
            while (_isRunning)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        _isRunning = false;
                        CancellationSource.Cancel();
                        break;
                    }
                }
                
                await Task.Delay(100);
            }
            
            try
            {
                await Task.WhenAll(providerTask, subscriberTask);
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling tasks
            }
            
            Console.WriteLine("Application terminated");
        }
        
        /// <summary>
        /// Provider task - publishes sensor data periodically
        /// </summary>
        static async Task RunProvider(string providerName, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"Starting provider: {providerName}...");
                
                // Create and initialize the provider
                var capabilities = new List<string> { "sensor_data", "dotnet_provider", "real_time" };
                using var provider = new Provider(providerName, "C# sensor data provider", capabilities);
                
                if (!provider.Initialize())
                {
                    Console.WriteLine("Failed to initialize provider!");
                    return;
                }
                
                Console.WriteLine($"Provider '{provider.Name}' successfully initialized");
                
                // Publishing loop
                int messageCounter = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Generate sample environmental sensor data
                        var sensorData = new
                        {
                            timestamp = DateTime.UtcNow.ToString("O"),
                            counter = messageCounter++,
                            temperature = Math.Round(20 + Random.NextDouble() * 15, 2), // 20-35°C
                            humidity = Math.Round(30 + Random.NextDouble() * 40, 2),    // 30-70%
                            pressure = Math.Round(1000 + Random.NextDouble() * 50, 2),  // 1000-1050 hPa
                            source = "dotnet_sensor",
                            provider = providerName
                        };
                        
                        // Publish to shared memory
                        lock (ConsoleLock)
                        {
                            Console.Write($"[{DateTime.Now:HH:mm:ss}] Publishing data... ");
                            
                            if (provider.PublishData(sensorData))
                            {
                                Console.WriteLine($"Success (T={sensorData.temperature}°C, H={sensorData.humidity}%)");
                            }
                            else
                            {
                                Console.WriteLine("Failed!");
                            }
                        }
                        
                        // Wait between publishes
                        await Task.Delay(2500, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in provider: {ex.Message}");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
                
                Console.WriteLine($"Shutting down provider: {providerName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Provider task exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Subscriber task - discovers and subscribes to all providers
        /// </summary>
        static async Task RunSubscriber(CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("Starting subscriber...");
                
                // Create and initialize the subscriber
                using var subscriber = new Subscriber();
                
                if (!subscriber.Initialize())
                {
                    Console.WriteLine("Failed to initialize subscriber!");
                    return;
                }
                
                // Set up callbacks for data updates and provider changes
                subscriber.SetDataUpdateHandler((provider, data) =>
                {
                    lock (ConsoleLock)
                    {
                        Interlocked.Increment(ref _receivedMessageCount);
                        Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] #{_receivedMessageCount} Received from '{provider}':");
                        
                        // Format and truncate data for display
                        string dataText = data.GetRawText();
                        Console.WriteLine($"  {(dataText.Length > 200 ? dataText.Substring(0, 197) + "..." : dataText)}");
                    }
                });
                
                subscriber.SetProviderChangeHandler((provider, status) =>
                {
                    lock (ConsoleLock)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Provider '{provider}' is now: {status}");
                    }
                });
                
                Console.WriteLine("Subscriber successfully initialized");
                
                // Provider discovery loop
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Get available providers from registry
                        var providers = subscriber.GetAvailableProviders();
                        
                        lock (ConsoleLock)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Found {providers.Count} providers");
                        }
                        
                        // Subscribe to each provider
                        foreach (var providerInfo in providers)
                        {
                            if (subscriber.SubscribeTo(providerInfo.Name))
                            {
                                lock (ConsoleLock)
                                {
                                    Console.WriteLine($"Subscribed to: {providerInfo.Name} ({providerInfo.Description})");
                                }
                            }
                        }
                        
                        // Check for new providers every 5 seconds
                        await Task.Delay(5000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in subscriber: {ex.Message}");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
                
                Console.WriteLine("Shutting down subscriber");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Subscriber task exception: {ex.Message}");
            }
        }
    }
    class CombinedDemo
    {
        private static bool running = true;
        private static int messagesSent = 0;
        private static int messagesReceived = 0;
        private static string lastReceivedData = "{}";
        private static string lastSentData = "{}";
        private static DateTime startTime = DateTime.Now;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Sharingway .NET Combined Demo Application");
            Console.WriteLine("========================================");
            
            // Enable debug logging for comprehensive testing
            SharingwayUtils.DebugLogging = true;
            SharingwayUtils.DebugLog("Debug logging enabled", "DotNetApp");

            string providerName = "DotNetProvider";
            
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "combined":
                        if (args.Length > 1)
                        {
                            providerName = args[1];
                        }
                        await RunCombinedDemo(providerName);
                        break;
                    case "provider":
                        await RunProviderDemo();
                        break;
                    case "subscriber":
                        await RunSubscriberDemo();
                        break;
                    case "interactive":
                        await RunInteractiveDemo();
                        break;
                    default:
                        ShowUsage();
                        break;
                }
            }
            else
            {
                await RunCombinedDemo(providerName);
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotNetApp                   - Run combined provider/subscriber demo (default)");
            Console.WriteLine("  dotNetApp combined [name]   - Run combined demo with custom provider name");
            Console.WriteLine("  dotNetApp provider          - Run as data provider only");
            Console.WriteLine("  dotNetApp subscriber        - Run as data subscriber only");
            Console.WriteLine("  dotNetApp interactive       - Run in interactive mode");
        }

        static async Task RunCombinedDemo(string providerName)
        {
            Console.WriteLine("Starting Combined Provider/Subscriber Demo...");
            Console.WriteLine($"Provider name: {providerName}");
            
            // Initialize statistics
            messagesSent = 0;
            messagesReceived = 0;
            startTime = DateTime.Now;
            
            // Configure provider
            var capabilities = new List<string> { "sensor_data", "real_time", "c#_combined" };
            
            // Initialize provider
            using var provider = new Provider(providerName, ".NET combined provider/subscriber", capabilities);
            
            Console.WriteLine("Initializing provider component...");
            if (!provider.Initialize())
            {
                Console.WriteLine("Failed to initialize provider component!");
                
                // Try to initialize registry manually for debugging
                Console.WriteLine("Testing registry initialization...");
                if (SharingwayUtils.EnsureRegistryInitialized())
                {
                    Console.WriteLine("Registry initialization: SUCCESS");
                }
                else
                {
                    Console.WriteLine("Registry initialization: FAILED");
                }
                
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine($"Provider '{provider.Name}' started successfully!");
            
            // Initialize subscriber
            Console.WriteLine("Initializing subscriber component...");
            using var subscriber = new Subscriber();
            
            if (!subscriber.Initialize())
            {
                Console.WriteLine("Failed to initialize subscriber component!");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Subscriber component initialized successfully!");
            
            // Set up subscriber callbacks
            subscriber.SetDataUpdateHandler((providerName, data) => 
            {
                Interlocked.Increment(ref messagesReceived);
                lastReceivedData = data.GetRawText();
                Console.WriteLine($"[RECEIVED] From: '{providerName}', Data: {lastReceivedData}");
            });
            
            subscriber.SetProviderChangeHandler((providerName, status) => 
            {
                Console.WriteLine($"[STATUS] Provider '{providerName}' is now {status}");
            });
            
            // Subscribe to available providers
            Console.WriteLine("\nDiscovering available providers...");
            var providers = subscriber.GetAvailableProviders();
            Console.WriteLine($"Found {providers.Count} providers:");
            
            int providerCount = 0;
            foreach (var providerInfo in providers)
            {
                // Don't subscribe to ourselves to avoid feedback loops
                if (providerInfo.Name != provider.Name)
                {
                    Console.WriteLine($"  {++providerCount}. {providerInfo.Name} - {providerInfo.Description} (Status: {providerInfo.Status})");
                    
                    if (subscriber.SubscribeTo(providerInfo.Name))
                    {
                        Console.WriteLine($"     ✓ Subscribed successfully");
                    }
                    else
                    {
                        Console.WriteLine($"     ✗ Failed to subscribe");
                    }
                }
                else
                {
                    Console.WriteLine($"  {++providerCount}. {providerInfo.Name} - {providerInfo.Description} (own provider, not subscribing)");
                }
            }

            // Start publisher thread
            var random = new Random();
            var publishTask = Task.Run(async () =>
            {
                while (running)
                {
                    try
                    {
                        // Generate sample data
                        var sensorData = new
                        {
                            timestamp = DateTime.UtcNow.ToString("O"),
                            temperature = Math.Round(20 + random.NextDouble() * 15, 2), // 20-35°C
                            humidity = Math.Round(30 + random.NextDouble() * 40, 2),    // 30-70%
                            pressure = Math.Round(1000 + random.NextDouble() * 50, 2),   // 1000-1050 hPa
                            location = "C# Lab",
                            sensor_id = "DOTNET_SENSOR",
                            messages_sent = messagesSent,
                            messages_received = messagesReceived,
                            uptime_seconds = (DateTime.Now - startTime).TotalSeconds
                        };
                        
                        lastSentData = JsonSerializer.Serialize(sensorData);
                        
                        if (provider.PublishData(sensorData))
                        {
                            Interlocked.Increment(ref messagesSent);
                        }
                        
                        await Task.Delay(2000); // Publish every 2 seconds
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in publisher thread: {ex.Message}");
                        await Task.Delay(5000); // Wait longer after error
                    }
                }
            });
            
            // Start stats display thread
            var statsTask = Task.Run(async () =>
            {
                while (running)
                {
                    try
                    {
                        await Task.Delay(5000); // Update stats every 5 seconds
                        
                        double uptime = (DateTime.Now - startTime).TotalSeconds;
                        double sendRate = messagesSent / Math.Max(1, uptime);
                        double receiveRate = messagesReceived / Math.Max(1, uptime);
                        
                        Console.WriteLine($"\n[STATS] Uptime: {uptime:F0}s | " + 
                                        $"Sent: {messagesSent} ({sendRate:F1}/sec) | " + 
                                        $"Received: {messagesReceived} ({receiveRate:F1}/sec)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in stats thread: {ex.Message}");
                    }
                }
            });
            
            Console.WriteLine("\nRunning in combined mode. Commands:");
            Console.WriteLine("  s - Show status");
            Console.WriteLine("  r - Refresh provider list");
            Console.WriteLine("  d - Send custom data");
            Console.WriteLine("  q - Quit");
            
            // Main command loop
            while (running)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    switch (key.KeyChar)
                    {
                        case 's':
                            double uptime = (DateTime.Now - startTime).TotalSeconds;
                            Console.WriteLine("\n=== Status ===");
                            Console.WriteLine($"Provider: {provider.Name} (Online: {provider.IsOnline})");
                            Console.WriteLine($"Messages Sent: {messagesSent}");
                            Console.WriteLine($"Messages Received: {messagesReceived}");
                            Console.WriteLine($"Uptime: {uptime:F1} seconds");
                            Console.WriteLine($"Last Sent: {lastSentData}");
                            Console.WriteLine($"Last Received: {lastReceivedData}");
                            Console.WriteLine("==============");
                            break;
                            
                        case 'r':
                            Console.WriteLine("\nRefreshing provider list...");
                            providers = subscriber.GetAvailableProviders();
                            Console.WriteLine($"Found {providers.Count} providers:");
                            
                            foreach (var providerInfo in providers)
                            {
                                if (providerInfo.Name != provider.Name)
                                {
                                    Console.WriteLine($"  • {providerInfo.Name} - {providerInfo.Description} (Status: {providerInfo.Status})");
                                    
                                    if (!subscriber.IsSubscribedTo(providerInfo.Name) && 
                                        providerInfo.Status == ProviderStatus.Online)
                                    {
                                        Console.WriteLine("     ▶ Subscribing...");
                                        if (subscriber.SubscribeTo(providerInfo.Name))
                                        {
                                            Console.WriteLine("     ✓ Subscribed successfully");
                                        }
                                        else
                                        {
                                            Console.WriteLine("     ✗ Failed to subscribe");
                                        }
                                    }
                                }
                            }
                            break;
                            
                        case 'd':
                            Console.Write("\nEnter custom data to send: ");
                            var customData = Console.ReadLine();
                            if (!string.IsNullOrEmpty(customData))
                            {
                                var payload = new
                                {
                                    timestamp = DateTime.UtcNow.ToString("O"),
                                    message = customData,
                                    source = "user_input",
                                    uptime_seconds = (DateTime.Now - startTime).TotalSeconds
                                };
                                
                                if (provider.PublishData(payload))
                                {
                                    Interlocked.Increment(ref messagesSent);
                                    Console.WriteLine("Custom data sent successfully");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to send custom data");
                                }
                            }
                            break;
                            
                        case 'q':
                            running = false;
                            break;
                    }
                }
                
                await Task.Delay(100);
            }
            
            Console.WriteLine("\nShutting down...");
            await Task.WhenAll(publishTask, statsTask);
        }

        static async Task RunProviderDemo()
        {
            Console.WriteLine("Starting Provider Demo...");
            
            var capabilities = new List<string> { "sensor_data", "real_time" };
            using var provider = new Provider("SensorProvider", "Temperature and humidity sensor", capabilities);
            
            Console.WriteLine("Attempting to initialize provider...");
            if (!provider.Initialize())
            {
                Console.WriteLine("Failed to initialize provider!");
                Console.WriteLine("This might be due to:");
                Console.WriteLine("1. Insufficient permissions to create global memory-mapped files");
                Console.WriteLine("2. Registry initialization issues");
                Console.WriteLine("3. Named synchronization object creation failures");
                Console.WriteLine("\nTrying to get more information...");
                
                // Try to initialize registry manually for debugging
                Console.WriteLine("Testing registry initialization...");
                if (SharingwayUtils.EnsureRegistryInitialized())
                {
                    Console.WriteLine("Registry initialization: SUCCESS");
                }
                else
                {
                    Console.WriteLine("Registry initialization: FAILED");
                }
                
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine($"Provider '{provider.Name}' started successfully!");
            Console.WriteLine("Publishing data every 2 seconds...");
            Console.WriteLine("Press 'q' to quit");
            
            var random = new Random();
            var startTime = DateTime.Now;
            
            while (true)
            {
                // Check for quit command
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                        break;
                }
                
                // Generate sample data
                var sensorData = new
                {
                    timestamp = DateTime.UtcNow.ToString("O"),
                    temperature = Math.Round(20 + random.NextDouble() * 15, 2), // 20-35°C
                    humidity = Math.Round(30 + random.NextDouble() * 40, 2),    // 30-70%
                    pressure = Math.Round(1000 + random.NextDouble() * 50, 2), // 1000-1050 hPa
                    location = "Lab Room A",
                    sensor_id = "TEMP_001"
                };
                
                if (provider.PublishData(sensorData))
                {
                    Console.WriteLine($"Published: T={sensorData.temperature}°C, H={sensorData.humidity}%, P={sensorData.pressure}hPa");
                    
                    // Test read operation - read back what we just published
                    await TestReadOperation(provider.Name);
                }
                else
                {
                    Console.WriteLine("Failed to publish data");
                }
                
                await Task.Delay(2000);
            }
            
            Console.WriteLine("Shutting down provider...");
        }

        static async Task RunSubscriberDemo()
        {
            Console.WriteLine("Starting Subscriber Demo...");
            
            using var subscriber = new Subscriber();
            
            if (!subscriber.Initialize())
            {
                Console.WriteLine("Failed to initialize subscriber!");
                return;
            }
            
            // Subscribe to specific provider
            Console.WriteLine("Available providers:");
            var providers = subscriber.GetAvailableProviders();
            for (int i = 0; i < providers.Count; i++)
            {
                var info = providers[i];
                Console.WriteLine($"  {i + 1}. {info.Name} - {info.Description} (Status: {info.Status})");
            }
            
            if (providers.Count == 0)
            {
                Console.WriteLine("No providers available. Start a provider first.");
                return;
            }
            
            // Subscribe to all available providers
            foreach (var providerInfo in providers)
            {
                if (subscriber.SubscribeTo(providerInfo.Name))
                {
                    Console.WriteLine($"Subscribed to: {providerInfo.Name}");
                    
                    // Test direct read from this provider
                    await TestReadOperation(providerInfo.Name);
                }
                else
                {
                    Console.WriteLine($"Failed to subscribe to: {providerInfo.Name}");
                }
            }
            
            Console.WriteLine("\nListening for data updates... Press 'q' to quit");
            
            // Set up data received callback
            subscriber.SetDataUpdateHandler((provider, data) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Data from '{provider}':");
                Console.WriteLine($"  {data}");
                Console.WriteLine();
            });
            
            // Set up provider status change callback
            subscriber.SetProviderChangeHandler((provider, status) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Provider '{provider}' status changed to: {status}");
            });
            
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                        break;
                }
                
                await Task.Delay(100);
            }
            
            Console.WriteLine("Shutting down subscriber...");
        }

        static async Task RunInteractiveDemo()
        {
            Console.WriteLine("Interactive Demo Mode");
            Console.WriteLine("Commands:");
            Console.WriteLine("  p - Start provider");
            Console.WriteLine("  s - Start subscriber");
            Console.WriteLine("  l - List providers");
            Console.WriteLine("  q - Quit");
            
            Provider? provider = null;
            Subscriber? subscriber = null;
            
            try
            {
                while (true)
                {
                    Console.Write("\nEnter command: ");
                    var input = Console.ReadLine()?.Trim().ToLower();
                    
                    switch (input)
                    {
                        case "p":
                            if (provider == null)
                            {
                                var capabilities = new List<string> { "interactive_data", "test" };
                                provider = new Provider("InteractiveProvider", "Interactive test provider", capabilities);
                                if (provider.Initialize())
                                {
                                    Console.WriteLine("Provider started. Type data to publish (or 'stop' to stop provider):");
                                    _ = Task.Run(async () =>
                                    {
                                        while (provider.IsOnline)
                                        {
                                            Console.Write("Data: ");
                                            var data = Console.ReadLine();
                                            if (data == "stop")
                                            {
                                                provider.Shutdown();
                                                break;
                                            }
                                            if (!string.IsNullOrEmpty(data))
                                            {
                                                var payload = new
                                                {
                                                    timestamp = DateTime.UtcNow.ToString("O"),
                                                    message = data,
                                                    source = "interactive"
                                                };
                                                if (provider.PublishData(payload))
                                                {
                                                    Console.WriteLine("Data published successfully");
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Failed to publish data");
                                                }
                                            }
                                        }
                                    });
                                }
                                else
                                {
                                    Console.WriteLine("Failed to start provider");
                                    provider?.Dispose();
                                    provider = null;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Provider already running");
                            }
                            break;
                            
                        case "s":
                            if (subscriber == null)
                            {
                                subscriber = new Subscriber();
                                if (subscriber.Initialize())
                                {
                                    Console.WriteLine("Subscriber started");
                                    
                                    subscriber.SetDataUpdateHandler((provider, data) =>
                                    {
                                        Console.WriteLine($"\n[RECEIVED] From '{provider}': {data}");
                                        Console.Write("Enter command: ");
                                    });
                                    
                                    subscriber.SetProviderChangeHandler((provider, status) =>
                                    {
                                        Console.WriteLine($"\n[STATUS] Provider '{provider}': {status}");
                                        Console.Write("Enter command: ");
                                    });
                                    
                                    // Auto-subscribe to all available providers
                                    var providers = subscriber.GetAvailableProviders();
                                    foreach (var providerInfo in providers)
                                    {
                                        if (subscriber.SubscribeTo(providerInfo.Name))
                                        {
                                            Console.WriteLine($"Subscribed to: {providerInfo.Name}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Failed to start subscriber");
                                    subscriber?.Dispose();
                                    subscriber = null;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Subscriber already running");
                            }
                            break;
                            
                        case "l":
                            if (subscriber != null)
                            {
                                var providers = subscriber.GetAvailableProviders();
                                Console.WriteLine($"Available providers ({providers.Count}):");
                                foreach (var info in providers)
                                {
                                    Console.WriteLine($"  - {info.Name}: {info.Description} (Status: {info.Status})");
                                    Console.WriteLine($"    Capabilities: {string.Join(", ", info.Capabilities)}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Start subscriber first to list providers");
                            }
                            break;
                            
                        case "q":
                            return;
                            
                        default:
                            Console.WriteLine("Unknown command");
                            break;
                    }
                }
            }
            finally
            {
                provider?.Dispose();
                subscriber?.Dispose();
            }
        }
        
        // Test method to read data directly from a provider's MMF
        static async Task TestReadOperation(string providerName)
        {
            try
            {
                // Create a temporary MMF helper to read from the provider's MMF
                using var mmfHelper = new MemoryMappedFileHelper(SharingwayUtils.GetProviderMmfName(providerName), SharingwayUtils.DefaultMmfSize);
                using var syncObjects = new NamedSyncObjects(providerName);
                
                if (mmfHelper.IsValid && syncObjects.IsValid)
                {
                    if (syncObjects.Lock(1000))
                    {
                        try
                        {
                            if (mmfHelper.ReadJson(out var data))
                            {
                                Console.WriteLine($"  ✓ Read test successful: {data.GetRawText().Substring(0, Math.Min(100, data.GetRawText().Length))}...");
                            }
                            else
                            {
                                Console.WriteLine("  ⚠ Read test: No data available");
                            }
                        }
                        finally
                        {
                            syncObjects.Unlock();
                        }
                    }
                    else
                    {
                        Console.WriteLine("  ❌ Read test: Failed to acquire lock");
                    }
                }
                else
                {
                    Console.WriteLine("  ❌ Read test: Failed to access MMF or sync objects");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Read test exception: {ex.Message}");
            }
        }
    }
}
