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
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Sharingway .NET Demo Application");
            Console.WriteLine("================================");
            
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "provider":
                        await RunProviderDemo();
                        break;
                    case "subscriber":
                        await RunSubscriberDemo();
                        break;                    case "interactive":
                        await RunInteractiveDemo();
                        break;
                    case "test":
                        TestProgram.MainTest(args);
                        break;
                    default:
                        ShowUsage();
                        break;
                }
            }
            else
            {
                await RunInteractiveDemo();
            }
        }        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotNetApp provider     - Run as data provider");
            Console.WriteLine("  dotNetApp subscriber   - Run as data subscriber");
            Console.WriteLine("  dotNetApp interactive  - Interactive mode (default)");
            Console.WriteLine("  dotNetApp test         - Run diagnostic tests");
        }static async Task RunProviderDemo()
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
                                subscriber = new Subscriber();                                if (subscriber.Initialize())
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
