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
        }

        public List<ProviderInfo> GetRegistry()
        {
            var providers = new List<ProviderInfo>();

            if (_registrySync?.Lock(5000) != true) return providers;

            try
            {
                var registry = GetRegistryInternal();

                foreach (var kvp in registry)
                {
                    var name = kvp.Key;
                    var info = kvp.Value as Dictionary<string, object>;
                    
                    if (info == null) continue;

                    var provider = new ProviderInfo
                    {
                        Name = name,
                        Status = SharingwayUtils.StringToProviderStatus(info.GetValueOrDefault("status")?.ToString() ?? "offline"),
                        Description = info.GetValueOrDefault("description")?.ToString() ?? "",
                        Capabilities = new List<string>()
                    };

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
                    }

                    providers.Add(provider);
                }
            }
            catch
            {
                // Error reading registry
            }

            _registrySync.Unlock();
            return providers;
        }

        public void SetRegistryChangeHandler(RegistryChangeHandler handler)
        {
            lock (_callbackLock)
            {
                _onRegistryChanged = handler;
            }
        }

        private Dictionary<string, object> GetRegistryInternal()
        {
            var registry = new Dictionary<string, object>();

            if (_registryMmf?.ReadJson(out var data) == true && data.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in data.EnumerateObject())
                {
                    registry[property.Name] = property.Value;
                }
            }

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
