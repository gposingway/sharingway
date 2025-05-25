using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Sharingway.Net;

[SupportedOSPlatform("windows")]
class Monitor
{
    // Enable debug logging for monitor application
    static Monitor()
    {
        SharingwayUtils.DebugLogging = false;
        SharingwayUtils.DebugLog("Monitor application starting with debug logging enabled", "Monitor");
    }

    // Provider monitoring data and stats
    private static readonly Dictionary<string, ProviderMonitor> _providerMonitors = new();
    private static readonly Dictionary<string, ProviderStats> _providerStats = new();
    private static readonly object _monitorsLock = new();
    private static readonly object _statsLock = new();
      // Runtime control
    private static volatile bool _running = true;
    private static int _messageCount = 0;
    private static int _totalMessageCount = 0;
    private static DateTime _startTime = DateTime.Now;
    private static Subscriber? _globalSubscriber = null;  // Store subscriber reference for auto-subscription
      // Display configuration
    private static bool _showDetailedJson = false;
    private static bool _showProviderList = true;
    private static bool _showMessageCount = true;
    private static bool _showCommunicationMatrix = true;
    private static int _refreshRate = 2000; // ms - slower to reduce spam
    
    // Store received data values for each provider
    private class ProviderMonitor
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Capabilities { get; set; } = new();
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public List<DataPoint> RecentData { get; set; } = new();
        public bool IsActive { get; set; } = true;
        
        public class DataPoint
        {
            public string Data { get; set; } = "";
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }
        
        public void AddDataPoint(string data) 
        {
            RecentData.Add(new DataPoint { Data = data, Timestamp = DateTime.Now });
            LastSeen = DateTime.Now;
            // Keep only the 10 most recent data points
            if (RecentData.Count > 10) 
            {
                RecentData.RemoveAt(0);
            }
        }
    }
    
    // Statistics for each provider
    private class ProviderStats
    {
        public string Name { get; set; } = "";
        public int MessageCount { get; set; } = 0;
        public DateTime FirstMessage { get; set; } = DateTime.Now;
        public DateTime LastMessage { get; set; } = DateTime.Now;
        public double MessagesPerSecond { get; set; } = 0;
    }
      public static async Task RunMonitor(string[] args)
    {
        Console.WriteLine("Sharingway Demo Communication Monitor");
        Console.WriteLine("====================================");
        Console.WriteLine("Monitoring communication between demo applications");
        Console.WriteLine();
        Console.WriteLine("Press 'q' to quit, 'h' for help");
        Console.WriteLine();
        
        // Start the monitoring process
        var cancelSource = new CancellationTokenSource();
        var monitorTask = Task.Run(() => StartMonitoring(cancelSource.Token));
        
        // Start the display update task
        var displayTask = Task.Run(() => UpdateDisplay(cancelSource.Token));
        
        // Wait for quit command
        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                HandleKeyPress(key.Key);
            }
            
            await Task.Delay(100);
        }
        
        // Clean up and exit
        cancelSource.Cancel();
        Console.Clear();
        Console.WriteLine("Shutting down monitor application...");
        
        try
        {
            await Task.WhenAll(monitorTask, displayTask);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, do nothing
        }
        
        Console.WriteLine("Monitor terminated.");
    }
    
    private static void HandleKeyPress(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.Q:
                _running = false;
                break;
            
            case ConsoleKey.J:
                _showDetailedJson = !_showDetailedJson;
                break;
                
            case ConsoleKey.P:
                _showProviderList = !_showProviderList;
                break;
                  case ConsoleKey.M:
                _showMessageCount = !_showMessageCount;
                break;
                
            case ConsoleKey.X:
                _showCommunicationMatrix = !_showCommunicationMatrix;
                break;
                
            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                _refreshRate = Math.Max(250, _refreshRate - 250);
                break;
                
            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                _refreshRate = Math.Min(5000, _refreshRate + 250);
                break;
                
            case ConsoleKey.C:
                Console.Clear();
                break;
                
            case ConsoleKey.H:
                ShowHelp();
                break;
                
            case ConsoleKey.S:
                SaveStats();
                break;
        }
    }
      private static void ShowHelp()
    {
        Console.Clear();
        Console.WriteLine("Sharingway Demo Communication Monitor Help");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Key Commands:");
        Console.WriteLine("  q - Quit the monitor application");
        Console.WriteLine("  j - Toggle detailed JSON display (off by default)");
        Console.WriteLine("  p - Toggle provider list display");
        Console.WriteLine("  m - Toggle message count display");
        Console.WriteLine("  x - Toggle communication matrix view");
        Console.WriteLine("  + - Increase refresh rate (faster updates)");
        Console.WriteLine("  - - Decrease refresh rate (slower updates)");
        Console.WriteLine("  c - Clear the console");
        Console.WriteLine("  h - Show this help screen");
        Console.WriteLine("  s - Save current stats to file");
        Console.WriteLine();
        Console.WriteLine("Communication Matrix shows data flow between providers.");
        Console.WriteLine("Look for arrows (-->) to see which demos are talking to each other.");
        Console.WriteLine();
        Console.WriteLine("Press any key to return to monitoring...");
        Console.ReadKey(true);
    }
    
    private static void SaveStats()
    {
        try
        {
            lock (_statsLock)
            {
                if (_providerStats.Count == 0)
                {
                    Console.WriteLine("No stats to save yet!");
                    Thread.Sleep(1000);
                    return;
                }
                
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"sharingway_stats_{timestamp}.json";
                
                var statsData = new
                {
                    GeneratedAt = DateTime.Now.ToString("o"),
                    RunDuration = (DateTime.Now - _startTime).ToString(),
                    TotalMessages = _totalMessageCount,
                    Providers = _providerStats.Values.ToList()
                };
                
                string json = JsonSerializer.Serialize(statsData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fileName, json);
                
                Console.WriteLine($"Stats saved to {fileName}");
                Thread.Sleep(1500);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving stats: {ex.Message}");
            Thread.Sleep(2000);
        }
    }    private static async Task StartMonitoring(CancellationToken cancellationToken)
    {
        try
        {
            // Create a subscriber that monitors all providers
            var subscriber = new Subscriber();
            _globalSubscriber = subscriber;  // Store for provider change handler
            
            if (!subscriber.Initialize())
            {
                Console.WriteLine("Failed to initialize subscriber for monitoring!");
                return;
            }
            
            // Set up data and provider change handlers
            subscriber.SetDataUpdateHandler(OnDataReceived);
            subscriber.SetProviderChangeHandler(OnProviderChange);
            
            // Subscribe to all available providers
            var providers = subscriber.GetAvailableProviders();
            if (providers.Count > 0)
            {
                Console.WriteLine($"Monitor found {providers.Count} providers, subscribing to all...");
                foreach (var provider in providers)
                {
                    subscriber.SubscribeTo(provider.Name);
                    Console.WriteLine($"Monitor subscribed to: {provider.Name}");
                }
            }
            else
            {
                Console.WriteLine("Monitor: No providers found yet, will auto-subscribe to new ones...");
            }
            
            // Stay alive until cancelled
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, clean up
                _globalSubscriber = null;
                subscriber.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in monitoring: {ex.Message}");
            _globalSubscriber = null;
        }
    }
    
    private static void OnProviderRegistered(string providerName, string description, List<string> capabilities)
    {
        lock (_monitorsLock)
        {
            if (!_providerMonitors.ContainsKey(providerName))
            {
                _providerMonitors[providerName] = new ProviderMonitor
                {
                    Name = providerName,
                    Description = description,
                    Capabilities = new List<string>(capabilities),
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    IsActive = true
                };
            }
            else
            {
                // Provider exists but might have been marked inactive
                _providerMonitors[providerName].IsActive = true;
                _providerMonitors[providerName].LastSeen = DateTime.Now;
                // Update description and capabilities in case they changed
                _providerMonitors[providerName].Description = description;
                _providerMonitors[providerName].Capabilities = new List<string>(capabilities);
            }
        }
    }
    
    private static void OnProviderUnregistered(string providerName)
    {
        lock (_monitorsLock)
        {
            if (_providerMonitors.ContainsKey(providerName))
            {
                _providerMonitors[providerName].IsActive = false;
            }
        }
    }
      private static void OnDataReceived(string providerName, JsonElement data)
    {
        _messageCount++;
        _totalMessageCount++;
        
        // Convert JsonElement to string for compatibility
        var dataString = data.ToString();
        
        // Update provider monitor
        lock (_monitorsLock)
        {
            if (!_providerMonitors.ContainsKey(providerName))
            {
                _providerMonitors[providerName] = new ProviderMonitor
                {
                    Name = providerName,
                    Description = "Unknown (detected from data)",
                    FirstSeen = DateTime.Now,
                    IsActive = true
                };
            }
            
            _providerMonitors[providerName].AddDataPoint(dataString);
            _providerMonitors[providerName].LastSeen = DateTime.Now;
        }
        
        // Update provider stats
        lock (_statsLock)
        {
            if (!_providerStats.ContainsKey(providerName))
            {
                _providerStats[providerName] = new ProviderStats
                {
                    Name = providerName,
                    FirstMessage = DateTime.Now,
                    MessageCount = 1,
                    LastMessage = DateTime.Now
                };
            }
            else
            {
                _providerStats[providerName].MessageCount++;
                _providerStats[providerName].LastMessage = DateTime.Now;
                
                // Calculate messages per second
                var timeSpan = _providerStats[providerName].LastMessage - _providerStats[providerName].FirstMessage;
                if (timeSpan.TotalSeconds > 0)
                {
                    _providerStats[providerName].MessagesPerSecond = 
                        _providerStats[providerName].MessageCount / timeSpan.TotalSeconds;
                }
            }
        }
    }    private static void OnProviderChange(string providerName, ProviderStatus status)
    {
        lock (_monitorsLock)
        {
            if (status == ProviderStatus.Online)
            {
                if (!_providerMonitors.ContainsKey(providerName))
                {
                    _providerMonitors[providerName] = new ProviderMonitor
                    {
                        Name = providerName,
                        Description = "Detected via registry",
                        FirstSeen = DateTime.Now,
                        IsActive = true
                    };
                    
                    Console.WriteLine($"Monitor: New provider detected: {providerName}, auto-subscribing...");
                    // Auto-subscribe to new provider
                    if (_globalSubscriber != null)
                    {
                        _globalSubscriber.SubscribeTo(providerName);
                        Console.WriteLine($"Monitor: Successfully subscribed to {providerName}");
                    }
                }
                _providerMonitors[providerName].IsActive = true;
            }
            else
            {
                if (_providerMonitors.ContainsKey(providerName))
                {
                    _providerMonitors[providerName].IsActive = false;
                }
            }
        }
    }
    
    private static async Task UpdateDisplay(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    RenderDisplay();
                    await Task.Delay(_refreshRate, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Display error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }
      private static void RenderDisplay()
    {
        // Reset position and prepare for rendering
        Console.SetCursorPosition(0, 4);  // Position after the header
        
        // Overall statistics
        var runTime = DateTime.Now - _startTime;
        int messagesPerSec = 0;
        
        if (runTime.TotalSeconds > 0)
        {
            messagesPerSec = (int)(_totalMessageCount / runTime.TotalSeconds);
        }
        
        if (_showMessageCount)
        {
            Console.WriteLine($"Runtime: {runTime.Hours:D2}:{runTime.Minutes:D2}:{runTime.Seconds:D2}  |  " +
                            $"Total Messages: {_totalMessageCount}  |  " +
                            $"Rate: {messagesPerSec}/sec");
            
            // Reset message count for next interval
            _messageCount = 0;
        }
        
        Console.WriteLine(new string('═', Math.Min(Console.WindowWidth - 1, 60)));
        
        // Get active providers
        List<ProviderMonitor> activeProviders;
        List<ProviderStats> activeStats;
        lock (_monitorsLock)
        {
            activeProviders = _providerMonitors.Values
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();
        }
        lock (_statsLock)
        {
            activeStats = _providerStats.Values.ToList();
        }
        
        // Communication Matrix - Simple overview of data flow
        if (_showCommunicationMatrix)
        {
            Console.WriteLine("Communication Status:");
            Console.WriteLine();
            
            if (activeProviders.Count == 0)
            {
                Console.WriteLine("  🔴 No providers active - Start the demo applications!");
            }
            else if (activeProviders.Count == 1)
            {
                var provider = activeProviders[0];
                var stats = activeStats.FirstOrDefault(s => s.Name == provider.Name);
                var msgInfo = stats != null ? $" ({stats.MessageCount} messages)" : "";
                Console.WriteLine($"  🟡 Only 1 provider active: {provider.Name}{msgInfo}");
                Console.WriteLine("     Start additional demo applications to see communication");
            }
            else
            {
                Console.WriteLine("  🟢 Multiple providers active - Communication possible!");
                Console.WriteLine();
                
                // Show each provider and their message activity
                foreach (var provider in activeProviders)
                {
                    var stats = activeStats.FirstOrDefault(s => s.Name == provider.Name);
                    if (stats != null && stats.MessageCount > 0)
                    {
                        var timeSinceLastMsg = DateTime.Now - stats.LastMessage;
                        var statusIcon = timeSinceLastMsg.TotalSeconds < 5 ? "🟢" : "🟡";
                        var rateText = stats.MessagesPerSecond > 0 ? $"{stats.MessagesPerSecond:F1}/sec" : "idle";
                        
                        Console.WriteLine($"     {statusIcon} {provider.Name} --> Broadcasting data ({rateText})");
                    }
                    else
                    {
                        Console.WriteLine($"     ⚪ {provider.Name} --> No data yet");
                    }
                }
                
                Console.WriteLine();
                
                // Show cross-communication summary
                var sendingProviders = activeProviders.Where(p => 
                    activeStats.Any(s => s.Name == p.Name && s.MessageCount > 0)).ToList();
                    
                if (sendingProviders.Count >= 2)
                {
                    Console.WriteLine("  📡 Inter-demo communication detected!");
                    Console.WriteLine($"     {sendingProviders.Count} providers are broadcasting data");
                    Console.WriteLine("     Each provider can receive data from others");
                }
                else if (sendingProviders.Count == 1)
                {
                    Console.WriteLine("  📤 One-way communication:");
                    Console.WriteLine($"     {sendingProviders[0].Name} is sending data");
                    Console.WriteLine("     Other providers should be receiving it");
                }
            }
        }
        
        Console.WriteLine();
        Console.WriteLine(new string('─', Math.Min(Console.WindowWidth - 1, 60)));
        
        // Detailed provider list (simplified)
        if (_showProviderList && activeProviders.Count > 0)
        {
            Console.WriteLine("Provider Details:");
            
            foreach (var provider in activeProviders)
            {
                var stats = activeStats.FirstOrDefault(s => s.Name == provider.Name);
                var msgCount = stats?.MessageCount ?? 0;
                var lastSeen = provider.LastSeen;
                var timeSinceLastSeen = DateTime.Now - lastSeen;
                
                // Simple one-line summary per provider
                var statusIcon = timeSinceLastSeen.TotalSeconds < 3 ? "🔥" : 
                               timeSinceLastSeen.TotalSeconds < 10 ? "✅" : "💤";
                               
                Console.WriteLine($"  {statusIcon} {provider.Name}: {msgCount} messages sent");
                
                // Show recent data only if detailed mode is on and there's recent data
                if (_showDetailedJson && provider.RecentData.Count > 0)
                {
                    var lastData = provider.RecentData.Last();
                    
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(lastData.Data);
                        
                        // Just show a compact summary instead of full JSON
                        var summary = "";
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            var props = doc.RootElement.EnumerateObject().Take(3).ToList();
                            summary = string.Join(", ", props.Select(p => $"{p.Name}:{GetValueSummary(p.Value)}"));
                            if (doc.RootElement.EnumerateObject().Count() > 3)
                                summary += "...";
                        }
                        else
                        {
                            summary = GetValueSummary(doc.RootElement);
                        }
                        
                        Console.WriteLine($"     Latest: {summary}");
                    }
                    catch (JsonException)
                    {
                        string displayData = lastData.Data.Length > 40 ? 
                            lastData.Data.Substring(0, 37) + "..." : lastData.Data;
                        Console.WriteLine($"     Latest: {displayData}");
                    }
                }
            }
        }
        
        // Clear any remaining lines from previous renders
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine(new string(' ', Math.Min(Console.WindowWidth - 1, 80)));
        }
        
        // Help reminder at the bottom
        Console.WriteLine();
        Console.WriteLine("Press 'h' for help | 'x' to toggle communication matrix | 'j' for detailed data");
    }
    
    private static string GetValueSummary(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => $"[{element.GetArrayLength()} items]",
            JsonValueKind.Object => "{...}",
            _ => element.ToString()
        };
    }
}
