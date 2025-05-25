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
    {        // Shared state flags and monitoring variables
        private static readonly CancellationTokenSource CancellationSource = new();
        private static readonly Random Random = new();
        private static readonly object ConsoleLock = new();
        private static bool _isRunning = true;
          // Tracking state
        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        private static readonly Dictionary<string, int> _messagesReceivedByProvider = new();
        private static string _lastReceivedData = "";
        private static string _lastReceivedFrom = "";
        private static DateTime _lastReceivedTime = DateTime.MinValue;
        private static string _currentProviderName = "";

        public static async Task RunDemo(string[] args)
        {
            Console.WriteLine("Sharingway .NET Demo Application");
            Console.WriteLine("================================");
              // Enable debug logging for better diagnostics
            SharingwayUtils.DebugLogging = true;  // Enable debug logging
            SharingwayUtils.DebugLog("DotNetApp starting with debug logging enabled", "DotNetApp");
              // Get provider name from command line or use default
            string providerName = args.Length > 0 ? args[0] : "DotNetProvider";
            _currentProviderName = providerName;
            
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
                        _messagesSent++;
                        Console.WriteLine($"Message Sent #{_messagesSent}");
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
                
                // Get available providers and subscribe (but not to ourselves)
                var providers = subscriber.GetAvailableProviders();
                if (providers.Count > 0)
                {
                    Console.WriteLine($"Found {providers.Count} providers, subscribing to others...");
                    foreach (var provider in providers)
                    {                        // Don't subscribe to our own provider
                        if (provider.Name != _currentProviderName)
                        {
                            subscriber.SubscribeTo(provider.Name);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No providers found yet, will auto-subscribe to new ones...");
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
        }        private static void OnDataReceived(string providerName, JsonElement data)
        {
            lock (ConsoleLock)
            {
                _messagesReceived++;
                if (!_messagesReceivedByProvider.ContainsKey(providerName))
                    _messagesReceivedByProvider[providerName] = 0;
                _messagesReceivedByProvider[providerName]++;
                
                _lastReceivedData = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                _lastReceivedFrom = providerName;
                _lastReceivedTime = DateTime.Now;
                
                // Clear screen and show current state
                Console.Clear();
                ShowCurrentState();
            }
        }

        private static void ShowCurrentState()
        {
            Console.WriteLine("Sharingway .NET Demo - Current State");
            Console.WriteLine("===================================");
            Console.WriteLine();
            
            // Show summary statistics
            Console.WriteLine("SUMMARY:");
            Console.WriteLine($"  Messages Sent: {_messagesSent}");
            Console.WriteLine($"  Messages Received: {_messagesReceived}");
            
            if (_messagesReceivedByProvider.Count > 0)
            {
                Console.WriteLine("  Received by Provider:");
                foreach (var kvp in _messagesReceivedByProvider)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value} messages");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine(new string('=', 50));
            Console.WriteLine();
            
            // Show last received message details
            if (_messagesReceived > 0)
            {
                Console.WriteLine($"LAST RECEIVED MESSAGE:");
                Console.WriteLine($"  From: {_lastReceivedFrom}");
                Console.WriteLine($"  Time: {_lastReceivedTime:HH:mm:ss.fff}");
                Console.WriteLine($"  Payload:");
                Console.WriteLine();
                
                // Indent the JSON payload
                var lines = _lastReceivedData.Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        Console.WriteLine($"    {line}");
                }
            }
            else
            {
                Console.WriteLine("NO MESSAGES RECEIVED YET");
                Console.WriteLine("Waiting for data from other providers...");
            }
            
            Console.WriteLine();
            Console.WriteLine(new string('=', 50));
            Console.WriteLine("Press 'q' to quit");
        }        private static void OnProviderChange(string providerName, ProviderStatus status)
        {
            lock (ConsoleLock)
            {
                Console.WriteLine($"[DEBUG] Provider change event: {providerName}, status: {status}");
                
                if (status == ProviderStatus.Online && providerName != _currentProviderName)
                {
                    Console.WriteLine($"New provider detected: {providerName}, should auto-subscribe");
                    // TODO: Add auto-subscription logic here if needed
                }
            }
        }
    }
}
