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
    class SimplifiedDemo
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
}