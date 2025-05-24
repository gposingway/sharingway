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
class EnhancedMonitor
{
    // Enable debug logging for monitor application
    static EnhancedMonitor()
    {
        SharingwayUtils.DebugLogging = true;
        SharingwayUtils.DebugLog("Enhanced Monitor application starting with debug logging enabled", "EnhancedMonitor");
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
    private static DateTime _lastSummaryTime = DateTime.Now;
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(10); // More frequent summary
    
    static async Task Main(string[] args)
    {
        // Process command line args if needed
        ProcessArgs(args);
        
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            _running = false;
        };
        
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("       ENHANCED SHARINGWAY IPC MONITOR");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"Monitoring all Sharingway provider activity...");
        Console.WriteLine($"Summary interval: {SummaryInterval.TotalSeconds} seconds");
        Console.WriteLine("Press Ctrl+C to exit");
        Console.WriteLine();

        // Ensure registry is initialized
        if (!SharingwayUtils.EnsureRegistryInitialized())
        {
            Console.WriteLine("⚠️ Warning: Could not initialize registry. Creating a new one.");
            
            // Try to force registry initialization
            using (var subscriber = new Subscriber())
            {
                if (!subscriber.Initialize())
                {
                    Console.WriteLine("❌ Critical error: Failed to initialize monitoring services.");
                    return;
                }
            }
        }
        
        // Start the monitoring tasks
        var monitoringTask = Task.Run(MonitorProvidersLoop);
        var statsTask = Task.Run(DisplayStatsLoop);
        var summaryTask = Task.Run(DisplayPeriodicSummary);
        var frameCounterTask = Task.Run(DisplayFrameCounter);

        await Task.WhenAny(new[] { monitoringTask, statsTask, summaryTask, frameCounterTask }
            .Concat(args.Contains("--interactive") ? new[] { InteractiveConsoleLoop() } : Array.Empty<Task>()));
        
        Console.WriteLine("\n🛑 Shutting down monitor...");
        
        // Clean up all monitors
        lock (_monitorsLock)
        {
            foreach (var monitor in _providerMonitors.Values)
            {
                monitor.Dispose();
            }
            _providerMonitors.Clear();
        }
        
        Console.WriteLine("✅ Monitor shutdown complete.");
    }
    
    static void ProcessArgs(string[] args)
    {
        // Process any special command line arguments here
        // Currently no special arguments needed
    }
    
    static async Task InteractiveConsoleLoop()
    {
        Console.WriteLine("Interactive mode enabled. Type commands or press Enter for immediate summary.");
        
        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.Enter)
                {
                    // Force display of summary on Enter key
                    DisplayDetailedSummary();
                }
                else if (key.Key == ConsoleKey.H)
                {
                    // Show help
                    Console.WriteLine("\nCommands:");
                    Console.WriteLine("  Enter - Show detailed summary");
                    Console.WriteLine("  H     - Show this help");
                    Console.WriteLine("  Ctrl+C - Exit monitor");
                }
            }
            
            await Task.Delay(100);
        }
    }
    
    static async Task DisplayFrameCounter()
    {
        // Display a small indicator that shows the app is alive
        string[] frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int frameIndex = 0;
        
        while (_running)
        {
            Console.Write($"\r{frames[frameIndex]} Monitoring... ({_providerMonitors.Count} providers, {_totalMessageCount} messages)");
            
            frameIndex = (frameIndex + 1) % frames.Length;
            await Task.Delay(100);
        }
    }

    static async Task MonitorProvidersLoop()
    {
        using var subscriber = new Subscriber();
        
        if (!subscriber.Initialize())
        {
            Console.WriteLine("❌ Failed to initialize subscriber for monitoring");
            return;
        }
        
        while (_running)
        {
            try
            {
                // Get current providers from registry
                var currentProviders = subscriber.GetAvailableProviders();
                
                lock (_monitorsLock)
                {
                    // Add monitors for new providers
                    foreach (var providerInfo in currentProviders)
                    {
                        if (!_providerMonitors.ContainsKey(providerInfo.Name))
                        {
                            var monitor = new ProviderMonitor(providerInfo.Name);
                            monitor.OnDataReceived += OnDataReceived;
                            if (monitor.Initialize())
                            {
                                _providerMonitors[providerInfo.Name] = monitor;
                                
                                // Track provider connection event
                                lock (_statsLock)
                                {
                                    if (!_providerStats.ContainsKey(providerInfo.Name))
                                    {
                                        _providerStats[providerInfo.Name] = new ProviderStats 
                                        { 
                                            Name = providerInfo.Name,
                                            Description = providerInfo.Description,
                                            Capabilities = providerInfo.Capabilities.ToList(),
                                            CurrentStatus = providerInfo.Status,
                                            FirstSeen = DateTime.Now,
                                            LastStatusChange = DateTime.Now
                                        };
                                    }
                                    
                                    var stats = _providerStats[providerInfo.Name];
                                    stats.RecentEvents.Add(new ProviderEvent
                                    {
                                        Timestamp = DateTime.Now,
                                        Type = "CONNECT",
                                        Details = $"Provider connected with status: {providerInfo.Status}"
                                    });
                                    
                                    // Keep only recent events (last 50)
                                    if (stats.RecentEvents.Count > 50)
                                    {
                                        stats.RecentEvents.RemoveRange(0, stats.RecentEvents.Count - 50);
                                    }
                                }
                            }
                            else
                            {
                                monitor.Dispose();
                                Console.WriteLine($"\n❌ Failed to monitor provider: {providerInfo.Name}");
                            }
                        }
                    }
                      
                    // Remove monitors for providers that no longer exist
                    var activeProviderNames = currentProviders.Select(p => p.Name).ToHashSet();
                    var providersToRemove = _providerMonitors.Keys.Where(p => !activeProviderNames.Contains(p)).ToList();
                    foreach (var provider in providersToRemove)
                    {
                        _providerMonitors[provider].Dispose();
                        _providerMonitors.Remove(provider);
                        
                        // Track provider disconnection event
                        lock (_statsLock)
                        {
                            if (_providerStats.ContainsKey(provider))
                            {
                                var stats = _providerStats[provider];
                                stats.CurrentStatus = ProviderStatus.Offline;
                                stats.LastStatusChange = DateTime.Now;
                                stats.RecentEvents.Add(new ProviderEvent
                                {
                                    Timestamp = DateTime.Now,
                                    Type = "DISCONNECT",
                                    Details = "Provider disconnected"
                                });
                                
                                // Keep only recent events (last 50)
                                if (stats.RecentEvents.Count > 50)
                                {
                                    stats.RecentEvents.RemoveRange(0, stats.RecentEvents.Count - 50);
                                }
                            }
                        }
                    }
                }
                
                await Task.Delay(2000); // Check for new providers every 2 seconds
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error in monitoring loop: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }
    
    static async Task DisplayStatsLoop()
    {
        while (_running)
        {
            await Task.Delay(5000); // Update basic stats every 5 seconds
            
            var uptime = DateTime.Now - _startTime;
            var messageRate = _totalMessageCount / Math.Max(1, uptime.TotalSeconds);
            
            // Don't output anything here, save it for the summary display
        }
    }
    
    static async Task DisplayPeriodicSummary()
    {
        while (_running)
        {
            await Task.Delay(1000); // Check every second
            
            if (DateTime.Now - _lastSummaryTime >= SummaryInterval)
            {
                DisplayDetailedSummary();
                _lastSummaryTime = DateTime.Now;
            }
        }
    }
    
    static void DisplayDetailedSummary()
    {
        // Clear a large portion of the console
        Console.WriteLine("\n\n\n");
        
        var uptime = DateTime.Now - _startTime;
        
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"📈 SHARINGWAY FRAMEWORK STATISTICS - Uptime: {uptime:hh\\:mm\\:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════");
        
        lock (_statsLock)
        {
            if (_providerStats.Count == 0)
            {
                Console.WriteLine("   No providers detected yet");
            }
            else
            {
                // Framework summary
                var onlineProviders = _providerStats.Count(p => p.Value.CurrentStatus == ProviderStatus.Online);
                var totalRate = _totalMessageCount / Math.Max(1, uptime.TotalSeconds);
                var totalData = _providerStats.Sum(p => p.Value.TotalDataBytes);
                
                Console.WriteLine($"📊 Framework Overview:");
                Console.WriteLine($"   • Providers: {onlineProviders} online, {_providerStats.Count - onlineProviders} offline");
                Console.WriteLine($"   • Total Messages: {_totalMessageCount} ({totalRate:F2}/sec)");
                Console.WriteLine($"   • Total Data: {FormatBytes(totalData)}");
                Console.WriteLine($"   • Registry Initialized: Yes");
                Console.WriteLine();
                
                // Provider details
                Console.WriteLine($"📊 Provider Details:");
                foreach (var kvp in _providerStats.OrderByDescending(p => p.Value.CurrentStatus == ProviderStatus.Online)
                                                  .ThenBy(p => p.Key))
                {
                    var stats = kvp.Value;
                    var statusIcon = stats.CurrentStatus == ProviderStatus.Online ? "🟢" : "🔴";
                    var timeSinceLastMessage = DateTime.Now - stats.LastMessage;
                    var lastDataPreview = stats.LastDataReceived.Length > 0 
                        ? (stats.LastDataReceived.Length > 50 ? stats.LastDataReceived.Substring(0, 47) + "..." : stats.LastDataReceived) 
                        : "(no data)";
                    
                    Console.WriteLine($"{statusIcon} {stats.Name}");
                    Console.WriteLine($"   • Description: {stats.Description}");
                    Console.WriteLine($"   • Capabilities: {string.Join(", ", stats.Capabilities)}");
                    Console.WriteLine($"   • Messages: {stats.MessageCount} ({stats.GetMessageRate():F2}/sec)");
                    Console.WriteLine($"   • Data Volume: {FormatBytes(stats.TotalDataBytes)}");
                    Console.WriteLine($"   • Last Message: {(timeSinceLastMessage.TotalSeconds >= 1 ? $"{timeSinceLastMessage.TotalSeconds:F0}s ago" : "Just now")}");
                    Console.WriteLine($"   • Last Data: {lastDataPreview}");
                    
                    // Show only one recent event per provider to save space
                    var latestEvent = stats.RecentEvents.OrderByDescending(e => e.Timestamp).FirstOrDefault();
                    if (latestEvent != null)
                    {
                        var eventAge = DateTime.Now - latestEvent.Timestamp;
                        Console.WriteLine($"   • Last Event: {latestEvent.Type} ({eventAge.TotalSeconds:F0}s ago) - {latestEvent.Details}");
                    }
                    
                    Console.WriteLine();
                }
            }
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════");
    }
    
    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
    
    static void OnDataReceived(string providerName, JsonElement data)
    {
        Interlocked.Increment(ref _totalMessageCount);
        
        var dataPreview = GetDataPreview(data);
        var dataSize = data.GetRawText().Length;
        
        // Update provider statistics
        lock (_statsLock)
        {
            if (!_providerStats.ContainsKey(providerName))
            {
                _providerStats[providerName] = new ProviderStats 
                { 
                    Name = providerName,
                    FirstSeen = DateTime.Now,
                    CurrentStatus = ProviderStatus.Online
                };
            }
            
            var stats = _providerStats[providerName];
            stats.MessageCount++;
            stats.LastMessage = DateTime.Now;
            stats.TotalDataBytes += dataSize;
            stats.LastDataReceived = dataPreview;
            
            // Add data event
            stats.RecentEvents.Add(new ProviderEvent
            {
                Timestamp = DateTime.Now,
                Type = "DATA",
                Details = dataPreview.Length > 50 ? dataPreview.Substring(0, 47) + "..." : dataPreview,
                DataSize = dataSize
            });
            
            // Keep only recent events (last 50)
            if (stats.RecentEvents.Count > 50)
            {
                stats.RecentEvents.RemoveRange(0, stats.RecentEvents.Count - 50);
            }
        }
        
        // We don't need to display each message anymore, the summary will show stats
    }

    static string GetDataPreview(JsonElement data)
    {
        try
        {
            var jsonString = data.GetRawText();
            
            // Limit preview length
            if (jsonString.Length > 200)
            {
                return jsonString.Substring(0, 197) + "...";
            }
            
            return jsonString;
        }
        catch
        {
            return "[invalid JSON]";
        }
    }
}

[SupportedOSPlatform("windows")]
public class ProviderMonitor : IDisposable
{
    private readonly string _providerName;
    private Subscriber? _subscriber;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private bool _disposed;

    public ProviderMonitor(string providerName)
    {
        _providerName = providerName;
    }
    
    public bool Initialize()
    {
        try
        {
            _subscriber = new Subscriber();
            _cancellationTokenSource = new CancellationTokenSource();
            
            if (!_subscriber.Initialize())
            {
                return false;
            }
            
            // Set up event handler and subscribe to the specific provider
            _subscriber.SetDataUpdateHandler(OnDataReceivedInternal);
            if (_subscriber.SubscribeTo(_providerName))
            {
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    private void OnDataReceivedInternal(string providerName, JsonElement data)
    {
        if (providerName == _providerName)
        {
            OnDataReceived?.Invoke(providerName, data);
        }
    }

    public event Action<string, JsonElement>? OnDataReceived;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            _cancellationTokenSource?.Cancel();
            _subscriber?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}

// Data structures for tracking provider statistics and events
public class ProviderStats
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Capabilities { get; set; } = new();
    public int MessageCount { get; set; } = 0;
    public DateTime FirstSeen { get; set; } = DateTime.Now;
    public DateTime LastMessage { get; set; } = DateTime.Now;
    public DateTime LastStatusChange { get; set; } = DateTime.Now;
    public ProviderStatus CurrentStatus { get; set; } = ProviderStatus.Offline;
    public List<ProviderEvent> RecentEvents { get; set; } = new();
    public long TotalDataBytes { get; set; } = 0;
    public string LastDataReceived { get; set; } = "";
    
    public double GetMessageRate()
    {
        var duration = (DateTime.Now - FirstSeen).TotalSeconds;
        return duration > 0 ? MessageCount / duration : 0;
    }
}

public class ProviderEvent
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = ""; // "DATA", "STATUS_CHANGE", "CONNECT", "DISCONNECT"
    public string Details { get; set; } = "";
    public int DataSize { get; set; } = 0;
}
