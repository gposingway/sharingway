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
class Program
{
    private static readonly Dictionary<string, ProviderMonitor> _providerMonitors = new();
    private static readonly Dictionary<string, ProviderStats> _providerStats = new();
    private static readonly object _monitorsLock = new();
    private static readonly object _statsLock = new();    private static volatile bool _running = true;
    private static int _messageCount = 0;
    private static int _totalMessageCount = 0;
    private static DateTime _startTime = DateTime.Now;
    private static DateTime _lastSummaryTime = DateTime.Now;    
    private static readonly TimeSpan SummaryInterval = GetSummaryInterval(); // Configurable summary interval
    
    private static TimeSpan GetSummaryInterval()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--summary-interval" || args[i] == "-s")
            {
                if (int.TryParse(args[i + 1], out int seconds) && seconds > 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        return TimeSpan.FromSeconds(30); // Default 30 seconds
    }

    static async Task Main(string[] args)
    {
        // Check for help argument
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            DisplayHelp();
            return;
        }
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            _running = false;
        };        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("          Sharingway IPC Monitor Application");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"Monitoring all Sharingway provider activity...");
        Console.WriteLine($"Summary interval: {SummaryInterval.TotalSeconds} seconds");
        Console.WriteLine("Press Ctrl+C to exit or use --help for options");
        Console.WriteLine();

        // Ensure registry is initialized
        if (!SharingwayUtils.EnsureRegistryInitialized())
        {
            Console.WriteLine("⚠️  Warning: Could not initialize registry. Monitoring may be limited.");
        }        // Start the monitoring tasks
        var monitoringTask = Task.Run(MonitorProvidersLoop);
        var statsTask = Task.Run(DisplayStatsLoop);
        var summaryTask = Task.Run(DisplayPeriodicSummary);

        await Task.WhenAny(monitoringTask, statsTask, summaryTask);
        
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
    }    static async Task MonitorProvidersLoop()
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
                    {                        if (!_providerMonitors.ContainsKey(providerInfo.Name))
                        {
                            var monitor = new ProviderMonitor(providerInfo.Name);
                            monitor.OnDataReceived += OnDataReceived;
                            if (monitor.Initialize())
                            {
                                _providerMonitors[providerInfo.Name] = monitor;
                                Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] Started monitoring provider: {providerInfo.Name} ({providerInfo.Status})");
                                
                                // Track provider connection event
                                lock (_statsLock)
                                {
                                    if (!_providerStats.ContainsKey(providerInfo.Name))
                                    {
                                        _providerStats[providerInfo.Name] = new ProviderStats 
                                        { 
                                            Name = providerInfo.Name,
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
                                Console.WriteLine($"❌ Failed to monitor provider: {providerInfo.Name}");
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
                        Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] Stopped monitoring provider: {provider}");
                        
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
                Console.WriteLine($"❌ Error in monitoring loop: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }    static async Task DisplayStatsLoop()
    {
        while (_running)
        {
            await Task.Delay(5000); // Update basic stats every 5 seconds
            
            var uptime = DateTime.Now - _startTime;
            var messageRate = _totalMessageCount / Math.Max(1, uptime.TotalSeconds);
            
            Console.WriteLine($"📊 [{DateTime.Now:HH:mm:ss}] Stats: {_totalMessageCount} messages, {messageRate:F1} msg/sec, {_providerMonitors.Count} providers");
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
        var uptime = DateTime.Now - _startTime;
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"📈 DETAILED SUMMARY - Uptime: {uptime:hh\\:mm\\:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════");
        
        lock (_statsLock)
        {
            if (_providerStats.Count == 0)
            {
                Console.WriteLine("   No providers detected yet");
            }
            else
            {
                Console.WriteLine($"📊 Total Messages: {_totalMessageCount}");
                Console.WriteLine($"📊 Overall Rate: {_totalMessageCount / Math.Max(1, uptime.TotalSeconds):F2} msg/sec");
                Console.WriteLine($"📊 Active Providers: {_providerStats.Count(p => p.Value.CurrentStatus == ProviderStatus.Online)}");
                Console.WriteLine();
                
                foreach (var kvp in _providerStats.OrderBy(p => p.Key))
                {
                    var stats = kvp.Value;
                    var statusIcon = stats.CurrentStatus == ProviderStatus.Online ? "🟢" : "🔴";
                    var timeSinceLastMessage = DateTime.Now - stats.LastMessage;
                    
                    Console.WriteLine($"{statusIcon} Provider: {stats.Name}");
                    Console.WriteLine($"   📨 Messages: {stats.MessageCount} ({stats.GetMessageRate():F2}/sec)");
                    Console.WriteLine($"   📊 Data: {FormatBytes(stats.TotalDataBytes)}");
                    Console.WriteLine($"   🕒 Last Message: {timeSinceLastMessage.TotalSeconds:F0}s ago");
                    Console.WriteLine($"   📍 Status: {stats.CurrentStatus} (since {(DateTime.Now - stats.LastStatusChange).TotalSeconds:F0}s)");
                    
                    // Show recent events
                    var recentEvents = stats.RecentEvents.TakeLast(3).ToList();
                    if (recentEvents.Any())
                    {
                        Console.WriteLine($"   📋 Recent Events:");
                        foreach (var evt in recentEvents)
                        {
                            var eventAge = DateTime.Now - evt.Timestamp;
                            Console.WriteLine($"      • [{eventAge.TotalMinutes:F0}m ago] {evt.Type}: {evt.Details}");
                        }
                    }
                    Console.WriteLine();
                }
            }
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine();
    }
    
    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }    static void OnDataReceived(string providerName, JsonElement data)
    {
        Interlocked.Increment(ref _totalMessageCount);
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
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
        
        // Display the event
        Console.WriteLine($"📨 [{timestamp}] {providerName}: {dataPreview} ({FormatBytes(dataSize)})");
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

    static void DisplayHelp()
    {
        Console.WriteLine("Sharingway IPC Monitor Application");
        Console.WriteLine("===================================");
        Console.WriteLine();
        Console.WriteLine("Usage: monitorApp [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -s, --summary-interval SECONDS   Set summary display interval (default: 30)");
        Console.WriteLine("  -h, --help                        Show this help message");
        Console.WriteLine();
        Console.WriteLine("Features:");
        Console.WriteLine("  • Real-time monitoring of all Sharingway providers");
        Console.WriteLine("  • Live event logging with timestamps");
        Console.WriteLine("  • Periodic detailed summaries with statistics");
        Console.WriteLine("  • Per-provider message rates and data volumes");
        Console.WriteLine("  • Provider connection/disconnection tracking");
        Console.WriteLine("  • Event history for each provider");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  monitorApp                           # Default monitoring with 30s summaries");
        Console.WriteLine("  monitorApp -s 60                    # Show summaries every 60 seconds");
        Console.WriteLine("  monitorApp --summary-interval 10    # Show summaries every 10 seconds");
        Console.WriteLine();
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
    }    public bool Initialize()
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
    }    private void OnDataReceivedInternal(string providerName, JsonElement data)
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
    public int MessageCount { get; set; } = 0;
    public DateTime FirstSeen { get; set; } = DateTime.Now;
    public DateTime LastMessage { get; set; } = DateTime.Now;
    public DateTime LastStatusChange { get; set; } = DateTime.Now;
    public ProviderStatus CurrentStatus { get; set; } = ProviderStatus.Offline;
    public List<ProviderEvent> RecentEvents { get; set; } = new();
    public long TotalDataBytes { get; set; } = 0;
    
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
