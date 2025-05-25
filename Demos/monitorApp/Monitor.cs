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
        SharingwayUtils.DebugLogging = true;
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
    
    // Display configuration
    private static bool _showDetailedJson = false;
    private static bool _showProviderList = true;
    private static bool _showMessageCount = true;
    private static int _refreshRate = 1000; // ms
    
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
        Console.WriteLine("Sharingway Monitor Application");
        Console.WriteLine("=============================");
        Console.WriteLine("Monitoring all IPC activity on this system");
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
        Console.WriteLine("Sharingway Monitor Help");
        Console.WriteLine("=====================");
        Console.WriteLine();
        Console.WriteLine("Key Commands:");
        Console.WriteLine("  q - Quit the monitor application");
        Console.WriteLine("  j - Toggle detailed JSON display");
        Console.WriteLine("  p - Toggle provider list display");
        Console.WriteLine("  m - Toggle message count display");
        Console.WriteLine("  + - Increase refresh rate (faster updates)");
        Console.WriteLine("  - - Decrease refresh rate (slower updates)");
        Console.WriteLine("  c - Clear the console");
        Console.WriteLine("  h - Show this help screen");
        Console.WriteLine("  s - Save current stats to file");
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
    }
      private static async Task StartMonitoring(CancellationToken cancellationToken)
    {
        try
        {
            // Create a subscriber that monitors all providers
            var subscriber = new Subscriber();
            
            if (!subscriber.Initialize())
            {
                Console.WriteLine("Failed to initialize subscriber for monitoring!");
                return;
            }
            
            // Set up data and provider change handlers
            subscriber.SetDataUpdateHandler(OnDataReceived);
            subscriber.SetProviderChangeHandler(OnProviderChange);
            
            // Stay alive until cancelled
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, clean up
                subscriber.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in monitoring: {ex.Message}");
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
    }
    
    private static void OnProviderChange(string providerName, ProviderStatus status)
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
                            $"Current Rate: {messagesPerSec}/sec  |  " +
                            $"Recent: {_messageCount}");
            
            // Reset message count for next interval
            _messageCount = 0;
        }
        
        Console.WriteLine(new string('-', Console.WindowWidth - 1));
        
        // List of active providers
        if (_showProviderList)
        {
            Console.WriteLine("Active Providers:");
            
            List<ProviderMonitor> activeProviders;
            lock (_monitorsLock)
            {
                activeProviders = _providerMonitors.Values
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToList();
            }
            
            if (activeProviders.Count == 0)
            {
                Console.WriteLine("  No active providers detected");
            }
            
            foreach (var provider in activeProviders)
            {
                // Get stats for this provider
                string rateInfo = "";
                lock (_statsLock)
                {
                    if (_providerStats.TryGetValue(provider.Name, out var stats))
                    {
                        rateInfo = $" | {stats.MessageCount} msgs ({stats.MessagesPerSecond:F1}/sec)";
                    }
                }
                
                // Show provider info with capabilities
                string capabilities = string.Join(", ", provider.Capabilities);
                if (string.IsNullOrEmpty(capabilities))
                {
                    capabilities = "(none)";
                }
                
                TimeSpan activeTime = DateTime.Now - provider.FirstSeen;
                
                Console.WriteLine($"  {provider.Name} - {provider.Description}{rateInfo}");
                Console.WriteLine($"    Capabilities: {capabilities}");
                Console.WriteLine($"    Active for: {activeTime.Hours:D2}:{activeTime.Minutes:D2}:{activeTime.Seconds:D2}");
                
                // Show recent data if detailed mode is enabled
                if (_showDetailedJson && provider.RecentData.Count > 0)
                {
                    var lastData = provider.RecentData.Last();
                    
                    try
                    {
                        // Try to format the JSON for better display
                        using JsonDocument doc = JsonDocument.Parse(lastData.Data);
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string formattedJson = JsonSerializer.Serialize(doc.RootElement, options);
                        
                        Console.WriteLine("    Latest data:");
                        foreach (string line in formattedJson.Split('\n'))
                        {
                            Console.WriteLine($"      {line}");
                        }
                    }
                    catch (JsonException)
                    {
                        // If not valid JSON, show raw data
                        string displayData = lastData.Data.Length > 60 ? lastData.Data.Substring(0, 57) + "..." : lastData.Data;
                        Console.WriteLine($"    Latest data: {displayData}");
                    }
                }
                
                Console.WriteLine();
            }
        }
        
        // Help reminder at the bottom
        Console.WriteLine(new string('-', Console.WindowWidth - 1));
        Console.WriteLine("Press 'q' to quit, 'h' for help");
    }
}
