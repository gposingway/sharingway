using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;

namespace Sharingway.Net
{
    /// <summary>
    /// Provider - publishes data to its own MMF
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class Provider : IDisposable
    {
        private readonly string _providerName;
        private MemoryMappedFileHelper? _dataMmf;
        private NamedSyncObjects? _dataSync;
        private RegistryManager? _registry;
        private volatile bool _isOnline;
        private bool _disposed;        public Provider(string name, string description, List<string> capabilities)
        {
            _providerName = name;
            
            // Ensure registry is available before doing anything
            if (!SharingwayUtils.EnsureRegistryInitialized())
            {
                // If registry can't be initialized, try to continue without it
                // This allows the provider to work in isolation
            }
            
            try
            {
                _registry = new RegistryManager();
                if (_registry.Initialize())
                {
                    _registry.RegisterProvider(name, description, capabilities);
                }
            }
            catch
            {
                // Continue without registry if it fails
                _registry = null;
            }
        }

        public bool Initialize(long mmfSize = SharingwayUtils.DefaultMmfSize)
        {
            try
            {
                _dataMmf = new MemoryMappedFileHelper(SharingwayUtils.GetProviderMmfName(_providerName), mmfSize);
                _dataSync = new NamedSyncObjects(_providerName);

                if (!_dataMmf.IsValid || !_dataSync.IsValid)
                {
                    return false;
                }

                _isOnline = true;
                _registry?.UpdateStatus(_providerName, ProviderStatus.Online);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Shutdown()
        {
            if (!_isOnline) return;

            _isOnline = false;

            // Clear the MMF data
            if (_dataMmf != null && _dataSync != null)
            {
                if (_dataSync.Lock(1000))
                {
                    try
                    {
                        var emptyData = JsonSerializer.SerializeToElement(new { });
                        _dataMmf.WriteJson(emptyData);
                    }
                    catch
                    {
                        // Ignore errors
                    }
                    finally
                    {
                        _dataSync.Unlock();
                        _dataSync.Signal();
                    }
                }
            }

            // Update registry status
            _registry?.UpdateStatus(_providerName, ProviderStatus.Offline);
        }

        public bool PublishData(JsonElement data)
        {
            if (!_isOnline || _dataMmf == null || _dataSync == null)
            {
                return false;
            }

            if (!_dataSync.Lock(5000))
            {
                return false;
            }

            try
            {
                var result = _dataMmf.WriteJson(data);
                _dataSync.Unlock();

                if (result)
                {
                    _dataSync.Signal();
                    // Update heartbeat
                    _registry?.UpdateStatus(_providerName, ProviderStatus.Online);
                }

                return result;
            }
            catch
            {
                _dataSync.Unlock();
                return false;
            }
        }

        public bool PublishData(object data)
        {
            try
            {
                var jsonElement = JsonSerializer.SerializeToElement(data);
                return PublishData(jsonElement);
            }
            catch
            {
                return false;
            }
        }

        public bool IsOnline => _isOnline;
        public string Name => _providerName;

        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _dataMmf?.Dispose();
                _dataSync?.Dispose();
                _registry?.Dispose();
                _disposed = true;
            }
        }
    }
}
