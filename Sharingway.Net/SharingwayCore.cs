using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sharingway.Net
{
    /// <summary>
    /// Provider status enumeration
    /// </summary>
    public enum ProviderStatus
    {
        Online,
        Offline,
        Error
    }

    /// <summary>
    /// Provider information structure
    /// </summary>
    public class ProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public ProviderStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Capabilities { get; set; } = new();
        public DateTime LastUpdate { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }

    /// <summary>
    /// Event handler delegates
    /// </summary>
    public delegate void DataUpdateHandler(string provider, JsonElement data);
    public delegate void ProviderChangeHandler(string provider, ProviderStatus status);
    public delegate void RegistryChangeHandler();    /// <summary>
    /// Utility class for memory-mapped file operations
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class MemoryMappedFileHelper : IDisposable
    {
        private readonly MemoryMappedFile? _mmf;
        private readonly MemoryMappedViewAccessor? _accessor;
        private readonly string _name;
        private readonly long _size;
        private bool _disposed;        public MemoryMappedFileHelper(string name, long size)
        {
            _name = name;
            _size = size;

            try
            {
                // Try to open existing MMF first
                try
                {
                    _mmf = MemoryMappedFile.OpenExisting(name);
                }
                catch (FileNotFoundException)
                {
                    // Create new MMF if it doesn't exist
                    _mmf = MemoryMappedFile.CreateNew(name, size);
                }
                catch (UnauthorizedAccessException)
                {
                    // Try to create new MMF with different access
                    try
                    {
                        _mmf = MemoryMappedFile.CreateNew(name, size);
                    }
                    catch
                    {
                        // Try without Global prefix for local access
                        var localName = name.StartsWith("Global\\") ? name.Substring(7) : name;
                        try
                        {
                            _mmf = MemoryMappedFile.OpenExisting(localName);
                        }
                        catch (FileNotFoundException)
                        {
                            _mmf = MemoryMappedFile.CreateNew(localName, size);
                        }
                    }
                }

                if (_mmf != null)
                {
                    _accessor = _mmf.CreateViewAccessor(0, size);
                }            }
            catch
            {
                // If global access fails, try without Global prefix but maintain cross-process compatibility
                try
                {
                    var localName = name.StartsWith("Global\\") ? name.Substring(7) : name;
                    try
                    {
                        _mmf = MemoryMappedFile.OpenExisting(localName);
                    }
                    catch (FileNotFoundException)
                    {
                        _mmf = MemoryMappedFile.CreateNew(localName, size);
                    }
                    
                    if (_mmf != null)
                    {
                        _accessor = _mmf.CreateViewAccessor(0, size);
                    }
                }
                catch
                {
                    // Only use process-specific MMF as last resort for isolated operation
                    try
                    {
                        var processSpecificName = (name.StartsWith("Global\\") ? name.Substring(7) : name) + "_Proc_" + Environment.ProcessId;
                        _mmf = MemoryMappedFile.CreateNew(processSpecificName, size);
                        if (_mmf != null)
                        {
                            _accessor = _mmf.CreateViewAccessor(0, size);
                        }
                    }
                    catch
                    {
                        // Complete failure
                    }
                }
            }
        }

        public bool IsValid => _accessor != null && !_disposed;

        public bool WriteJson(JsonElement data)
        {
            if (!IsValid) return false;

            try
            {
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data);
                
                if (jsonBytes.Length + sizeof(int) > _size)
                    return false;

                // Write length first (4 bytes)
                _accessor!.Write(0, jsonBytes.Length);
                
                // Write JSON data
                _accessor.WriteArray(sizeof(int), jsonBytes, 0, jsonBytes.Length);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ReadJson(out JsonElement data)
        {
            data = default;
            if (!IsValid) return false;

            try
            {
                // Read length
                var length = _accessor!.ReadInt32(0);
                
                if (length <= 0 || length > _size - sizeof(int))
                    return false;

                // Read JSON data
                var jsonBytes = new byte[length];
                _accessor.ReadArray(sizeof(int), jsonBytes, 0, length);
                
                var jsonDocument = JsonDocument.Parse(jsonBytes);
                data = jsonDocument.RootElement;
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _accessor?.Dispose();
                _mmf?.Dispose();
                _disposed = true;
            }
        }
    }    /// <summary>
    /// Utility class for named synchronization objects
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class NamedSyncObjects : IDisposable
    {
        private readonly Mutex? _mutex;
        private readonly EventWaitHandle? _event;
        private readonly string _baseName;
        private bool _disposed;        public NamedSyncObjects(string baseName)
        {
            _baseName = baseName;
            var mutexName = GetProviderMutexName(baseName);
            var eventName = GetProviderEventName(baseName);

            try
            {
                _mutex = new Mutex(false, mutexName);
                _event = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            }
            catch (UnauthorizedAccessException)
            {
                // Try without Global prefix - this maintains cross-process compatibility
                var localMutexName = mutexName.StartsWith("Global\\") ? mutexName.Substring(7) : mutexName;
                var localEventName = eventName.StartsWith("Global\\") ? eventName.Substring(7) : eventName;
                
                try
                {
                    _mutex = new Mutex(false, localMutexName);
                    _event = new EventWaitHandle(false, EventResetMode.AutoReset, localEventName);
                }
                catch
                {
                    // Only use process-specific names as a last resort for isolated operation
                    try
                    {
                        var processMutexName = localMutexName + "_Proc_" + Environment.ProcessId;
                        var processEventName = localEventName + "_Proc_" + Environment.ProcessId;
                        _mutex = new Mutex(false, processMutexName);
                        _event = new EventWaitHandle(false, EventResetMode.AutoReset, processEventName);
                    }
                    catch
                    {
                        // Handle complete failure
                    }
                }
            }
            catch
            {
                // Handle other creation failures
            }
        }

        public bool IsValid => _mutex != null && _event != null && !_disposed;

        public bool Lock(int timeoutMs = Timeout.Infinite)
        {
            if (!IsValid) return false;
            try
            {
                return _mutex!.WaitOne(timeoutMs);
            }
            catch
            {
                return false;
            }
        }

        public void Unlock()
        {
            if (!IsValid) return;
            try
            {
                _mutex!.ReleaseMutex();
            }
            catch
            {
                // Ignore errors
            }
        }

        public void Signal()
        {
            if (!IsValid) return;
            try
            {
                _event!.Set();
            }
            catch
            {
                // Ignore errors
            }
        }

        public bool WaitForSignal(int timeoutMs = Timeout.Infinite)
        {
            if (!IsValid) return false;
            try
            {
                return _event!.WaitOne(timeoutMs);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _mutex?.Dispose();
                _event?.Dispose();
                _disposed = true;
            }
        }

        private static string GetProviderMutexName(string provider) => $"Global\\Sharingway.{provider}.Lock";
        private static string GetProviderEventName(string provider) => $"Global\\Sharingway.{provider}.Signal";
    }    /// <summary>
    /// Utility functions for Sharingway
    /// </summary>
    public static class SharingwayUtils
    {
        public const long DefaultMmfSize = 1024 * 1024; // 1MB
        public const string RegistryName = "Global\\Sharingway.Registry";

        public static string GetProviderMmfName(string provider) => $"Global\\Sharingway.{provider}";
        public static string GetProviderMutexName(string provider) => $"Global\\Sharingway.{provider}.Lock";
        public static string GetProviderEventName(string provider) => $"Global\\Sharingway.{provider}.Signal";

        public static string ProviderStatusToString(ProviderStatus status) => status switch
        {
            ProviderStatus.Online => "online",
            ProviderStatus.Offline => "offline",
            ProviderStatus.Error => "error",
            _ => "unknown"
        };

        public static ProviderStatus StringToProviderStatus(string status) => status?.ToLower() switch
        {
            "online" => ProviderStatus.Online,
            "offline" => ProviderStatus.Offline,
            "error" => ProviderStatus.Error,
            _ => ProviderStatus.Offline
        };

        public static long GetUnixTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static DateTime FromUnixTimestamp(long timestamp) => DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;        /// <summary>
        /// Ensures the global registry is initialized and available
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool EnsureRegistryInitialized()
        {
            try
            {
                using var registryMmf = new MemoryMappedFileHelper(RegistryName, DefaultMmfSize);
                using var registrySync = new NamedSyncObjects("Registry");

                if (!registryMmf.IsValid || !registrySync.IsValid)
                {
                    return false;
                }

                if (!registrySync.Lock(5000))
                {
                    return false;
                }

                try
                {
                    // Check if registry has valid data
                    if (!registryMmf.ReadJson(out var existingData) || existingData.ValueKind != JsonValueKind.Object)
                    {
                        // Initialize with empty registry
                        var emptyRegistry = new Dictionary<string, object>();
                        var jsonElement = JsonSerializer.SerializeToElement(emptyRegistry);
                        var result = registryMmf.WriteJson(jsonElement);
                        
                        if (result)
                        {
                            registrySync.Signal();
                        }
                        
                        return result;
                    }
                    
                    return true;
                }
                finally
                {
                    registrySync.Unlock();
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
