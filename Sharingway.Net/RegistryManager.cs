using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sharingway.Net
{
    /// <summary>
    /// Registry Manager - manages the global provider registry
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class RegistryManager : IDisposable
    {
        private MemoryMappedFileHelper? _registryMmf;
        private NamedSyncObjects? _registrySync;
        private readonly CancellationTokenSource _cancellationSource = new();
        private Task? _watchTask;
        private readonly object _callbackLock = new();
        private RegistryChangeHandler? _onRegistryChanged;
        private bool _disposed;

        public RegistryManager()
        {
        }        public bool Initialize()
        {
            try
            {
                _registryMmf = new MemoryMappedFileHelper(SharingwayUtils.RegistryName, SharingwayUtils.DefaultMmfSize);
                _registrySync = new NamedSyncObjects("Registry");

                if (!_registryMmf.IsValid || !_registrySync.IsValid)
                {
                    return false;
                }

                // Initialize registry with empty data if it's new
                if (!InitializeRegistryData())
                {
                    return false;
                }

                _watchTask = Task.Run(WatchRegistryAsync, _cancellationSource.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializeRegistryData()
        {
            if (_registrySync?.Lock(5000) != true) return false;

            try
            {
                // Check if registry has valid data
                if (_registryMmf?.ReadJson(out var existingData) != true || existingData.ValueKind != JsonValueKind.Object)
                {
                    // Initialize with empty registry
                    var emptyRegistry = new Dictionary<string, object>();
                    var jsonElement = JsonSerializer.SerializeToElement(emptyRegistry);
                    var result = _registryMmf?.WriteJson(jsonElement) == true;
                    
                    if (result)
                    {
                        _registrySync.Signal();
                    }
                    
                    return result;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _registrySync?.Unlock();
            }
        }

        public void Shutdown()
        {
            if (_disposed) return;

            _cancellationSource.Cancel();
            
            try
            {
                _watchTask?.Wait(5000);
            }
            catch
            {
                // Ignore timeout
            }

            _registryMmf?.Dispose();
            _registrySync?.Dispose();
        }

        public bool RegisterProvider(string name, string description, List<string> capabilities)
        {
            if (_registrySync?.Lock(5000) != true) return false;

            try
            {
                var registry = GetRegistryInternal();
                var timestamp = SharingwayUtils.GetUnixTimestamp();

                var providerData = new Dictionary<string, object>
                {
                    ["status"] = "online",
                    ["description"] = description,
                    ["capabilities"] = capabilities,
                    ["lastUpdate"] = timestamp,
                    ["lastHeartbeat"] = timestamp
                };

                registry[name] = providerData;

                var jsonElement = JsonSerializer.SerializeToElement(registry);
                var result = _registryMmf?.WriteJson(jsonElement) == true;
                
                _registrySync.Unlock();

                if (result)
                {
                    _registrySync.Signal();
                }

                return result;
            }
            catch
            {
                _registrySync?.Unlock();
                return false;
            }
        }

        public bool UpdateStatus(string name, ProviderStatus status)
        {
            if (_registrySync?.Lock(5000) != true) return false;

            try
            {
                var registry = GetRegistryInternal();

                if (!registry.ContainsKey(name))
                {
                    _registrySync.Unlock();
                    return false;
                }

                var providerData = registry[name] as Dictionary<string, object> ?? new Dictionary<string, object>();
                var timestamp = SharingwayUtils.GetUnixTimestamp();

                providerData["status"] = SharingwayUtils.ProviderStatusToString(status);
                providerData["lastUpdate"] = timestamp;

                registry[name] = providerData;

                var jsonElement = JsonSerializer.SerializeToElement(registry);
                var result = _registryMmf?.WriteJson(jsonElement) == true;
                
                _registrySync.Unlock();

                if (result)
                {
                    _registrySync.Signal();
                }

                return result;
            }
            catch
            {
                _registrySync?.Unlock();
                return false;
            }
        }

        public bool RemoveProvider(string name)
        {
            if (_registrySync?.Lock(5000) != true) return false;

            try
            {
                var registry = GetRegistryInternal();
                registry.Remove(name);

                var jsonElement = JsonSerializer.SerializeToElement(registry);
                var result = _registryMmf?.WriteJson(jsonElement) == true;
                
                _registrySync.Unlock();

                if (result)
                {
                    _registrySync.Signal();
                }

                return result;
            }
            catch
            {
                _registrySync?.Unlock();
                return false;
            }
        }        public List<ProviderInfo> GetRegistry()
        {
            SharingwayUtils.DebugLog("RegistryManager.GetRegistry() called", "Registry");
            var providers = new List<ProviderInfo>();

            if (_registrySync?.Lock(5000) != true) 
            {
                SharingwayUtils.DebugLog("Failed to acquire registry lock", "Registry");
                return providers;
            }

            try
            {
                SharingwayUtils.DebugLog("Registry lock acquired, getting internal registry", "Registry");
                var registry = GetRegistryInternal();
                SharingwayUtils.DebugLog($"Internal registry returned {registry.Count} entries", "Registry");                foreach (var kvp in registry)
                {
                    var name = kvp.Key;
                    SharingwayUtils.DebugLog($"Processing provider entry: {name}", "Registry");
                    SharingwayUtils.DebugLog($"Raw value type: {kvp.Value?.GetType()?.Name ?? "null"}", "Registry");
                    SharingwayUtils.DebugLog($"Raw value: {kvp.Value}", "Registry");
                    
                    Dictionary<string, object>? info = null;
                    
                    // Handle both JsonElement and Dictionary<string, object> types
                    if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        SharingwayUtils.DebugLog($"Converting JsonElement to Dictionary", "Registry");
                        info = new Dictionary<string, object>();
                        foreach (var property in jsonElement.EnumerateObject())
                        {
                            info[property.Name] = property.Value;
                        }
                    }
                    else if (kvp.Value is Dictionary<string, object> dictValue)
                    {
                        SharingwayUtils.DebugLog($"Using existing Dictionary", "Registry");
                        info = dictValue;
                    }
                    
                    if (info == null) 
                    {
                        SharingwayUtils.DebugLog($"Provider {name} has null info, skipping", "Registry");
                        continue;
                    }

                    var provider = new ProviderInfo
                    {
                        Name = name,
                        Status = SharingwayUtils.StringToProviderStatus(info.GetValueOrDefault("status")?.ToString() ?? "offline"),
                        Description = info.GetValueOrDefault("description")?.ToString() ?? "",
                        Capabilities = new List<string>()
                    };
                    
                    SharingwayUtils.DebugLog($"Provider {name} status: {provider.Status}", "Registry");

                    if (info.GetValueOrDefault("capabilities") is JsonElement capsElement && capsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cap in capsElement.EnumerateArray())
                        {
                            if (cap.ValueKind == JsonValueKind.String)
                            {
                                provider.Capabilities.Add(cap.GetString() ?? "");
                            }
                        }
                    }

                    var lastUpdate = info.GetValueOrDefault("lastUpdate");
                    var lastHeartbeat = info.GetValueOrDefault("lastHeartbeat");

                    if (lastUpdate is JsonElement luElement && luElement.ValueKind == JsonValueKind.Number)
                    {
                        provider.LastUpdate = SharingwayUtils.FromUnixTimestamp(luElement.GetInt64());
                    }

                    if (lastHeartbeat is JsonElement lhElement && lhElement.ValueKind == JsonValueKind.Number)
                    {
                        provider.LastHeartbeat = SharingwayUtils.FromUnixTimestamp(lhElement.GetInt64());
                    }                    providers.Add(provider);
                    SharingwayUtils.DebugLog($"Added provider to list: {provider.Name}", "Registry");
                }
            }
            catch (Exception ex)
            {
                SharingwayUtils.DebugLog($"Exception in GetRegistry: {ex.Message}", "Registry");
                // Error reading registry
            }

            _registrySync.Unlock();
            SharingwayUtils.DebugLog($"GetRegistry returning {providers.Count} providers", "Registry");
            return providers;
        }

        public void SetRegistryChangeHandler(RegistryChangeHandler handler)
        {
            lock (_callbackLock)
            {
                _onRegistryChanged = handler;
            }
        }        private Dictionary<string, object> GetRegistryInternal()
        {
            SharingwayUtils.DebugLog("GetRegistryInternal() called", "Registry");
            var registry = new Dictionary<string, object>();

            if (_registryMmf?.ReadJson(out var data) == true && data.ValueKind == JsonValueKind.Object)
            {
                SharingwayUtils.DebugLog("Registry MMF read successful, parsing JSON", "Registry");
                foreach (var property in data.EnumerateObject())
                {
                    registry[property.Name] = property.Value;
                    SharingwayUtils.DebugLog($"Found registry entry: {property.Name}", "Registry");
                }
            }
            else
            {
                SharingwayUtils.DebugLog("Registry MMF read failed or data is not JSON object", "Registry");
            }

            SharingwayUtils.DebugLog($"GetRegistryInternal returning {registry.Count} entries", "Registry");
            return registry;
        }

        private async Task WatchRegistryAsync()
        {
            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_registrySync?.WaitForSignal(1000) == true)
                    {
                        lock (_callbackLock)
                        {
                            _onRegistryChanged?.Invoke();
                        }
                    }
                }
                catch
                {
                    // Continue on error
                }

                await Task.Delay(100, _cancellationSource.Token);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _cancellationSource.Dispose();
                _disposed = true;
            }
        }
    }
}
