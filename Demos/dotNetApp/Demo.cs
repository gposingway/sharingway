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
    class Demo
    {
        // Shared state flags and monitoring variables
        private static readonly CancellationTokenSource CancellationSource = new();
        private static readonly Random Random = new();
        private static readonly object ConsoleLock = new();
        private static bool _isRunning = true;
        private static int _receivedMessageCount = 0;

        public static async Task RunDemo(string[] args)
        {
            Console.WriteLine("Sharingway .NET Demo Application");
            Console.WriteLine("================================");
            
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
                        Console.WriteLine("Shutting down demo...");
                    }
                }
                await Task.Delay(100);
            }
            
            // Wait for tasks to complete
            try
            {
                await Task.WhenAll(providerTask, subscriberTask).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Graceful shutdown timed out, forcing exit.");
            }
            
            Console.WriteLine("Demo shut down complete.");
        }
        
        private static async Task RunProvider(string providerName, CancellationToken cancellationToken)
        {
            try
            {
                // Define provider capabilities
                var capabilities = new List<string> { "temperature", "humidity", "pressure" };
                
                // Create and initialize the provider
                using var provider = new Provider(providerName, "C# .NET data provider", capabilities);
                
                if (!provider.Initialize())
                {
                    Console.WriteLine("Failed to initialize provider!");
                    return;
                }
                
                Console.WriteLine($"Provider '{providerName}' initialized successfully");
                
                // Generate and publish data until cancelled
                int messageId = 1;
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Create sample data with random values
                    var data = new
                    {
                        id = messageId++,
                        timestamp = DateTimeOffset.Now,
                        readings = new List<Dictionary<string, object>>
                        {
                            new()
                            {
                                { "type", "temperature" },
                                { "value", Math.Round(Random.NextDouble() * 35 + 5, 1) },
                                { "unit", "celsius" }
                            },
                            new()
                            {
                                { "type", "humidity" },
                                { "value", Math.Round(Random.NextDouble() * 60 + 20, 1) },
                                { "unit", "percent" }
                            },
                            new() 
                            {
                                { "type", "pressure" },
                                { "value", Math.Round(Random.NextDouble() * 50 + 950, 1) },
                                { "unit", "hPa" }
                            }
                        }
                    };
                    
                    // Publish the data
                    provider.PublishData(data);
                    
                    lock (ConsoleLock)
                    {
                        Console.WriteLine($"[PROVIDER] Published message {messageId-1} from {providerName}");
                    }
                    
                    // Wait a bit before publishing next data
                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, do nothing
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROVIDER] Fatal error: {ex.Message}");
            }
        }
        
        private static async Task RunSubscriber(CancellationToken cancellationToken)
        {
            try
            {
                // Create subscriber and initialize it
                using var subscriber = new Subscriber();
                
                if (!subscriber.Initialize())
                {
                    Console.WriteLine("Failed to initialize subscriber!");
                    return;
                }
                
                // Set up the data received callback
                subscriber.SetDataUpdateHandler(OnDataReceived);
                
                // Set up provider change events
                subscriber.SetProviderChangeHandler(OnProviderChange);
                  // Get available providers
                var providers = subscriber.GetAvailableProviders();
                Console.WriteLine($"Found {providers.Count} active providers:");
                foreach (var provider in providers)
                {
                    Console.WriteLine($"  - {provider.Name}");
                    Console.WriteLine("    Attempting to subscribe...");
                    
                    if (subscriber.SubscribeTo(provider.Name))
                    {
                        Console.WriteLine("    Successfully subscribed.");
                    }
                    else
                    {
                        Console.WriteLine("    Failed to subscribe!");
                    }
                }
                
                // Keep running until cancelled
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, clean up handled by using statement
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SUBSCRIBER] Fatal error: {ex.Message}");
            }
        }
        
        private static void OnDataReceived(string providerName, JsonElement data)
        {
            Interlocked.Increment(ref _receivedMessageCount);
            
            lock (ConsoleLock) 
            {
                Console.WriteLine($"[SUBSCRIBER] Received data from {providerName}");
                Console.WriteLine($"[SUBSCRIBER] Total messages received: {_receivedMessageCount}");
                
                // Pretty-print the data
                var options = new JsonSerializerOptions { WriteIndented = true };
                string formattedJson = JsonSerializer.Serialize(data, options);
                Console.WriteLine(formattedJson);
            }
        }
          private static void OnProviderChange(string providerName, ProviderStatus status)
        {
            lock (ConsoleLock)
            {
                if (status == ProviderStatus.Online)
                {
                    Console.WriteLine($"[SUBSCRIBER] Provider '{providerName}' is now available");
                }
                else
                {
                    Console.WriteLine($"[SUBSCRIBER] Provider '{providerName}' is no longer available");
                }
            }
        }
    }
}
