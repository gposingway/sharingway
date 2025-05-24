using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sharingway.Net
{
    /// <summary>
    /// Subscriber - reads from provider MMFs
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class Subscriber : IDisposable
    {
        private class ProviderSubscription : IDisposable
        {
            public string Name { get; set; } = string.Empty;
            public MemoryMappedFileHelper? Mmf { get; set; }
            public NamedSyncObjects? Sync { get; set; }
            public Task? WatchTask { get; set; }
            public CancellationTokenSource? CancellationSource { get; set; }

            public void Dispose()
            {
                CancellationSource?.Cancel();
                
                try
                {
                    WatchTask?.Wait(1000);
                }
                catch
                {
                    // Ignore timeout
                }

                Mmf?.Dispose();
                Sync?.Dispose();
                CancellationSource?.Dispose();
            }
        }

        private RegistryManager? _registry;
        private readonly ConcurrentDictionary<string, ProviderSubscription> _subscriptions = new();
        private readonly object _callbackLock = new();
        private volatile bool _running;
        private bool _disposed;

        private DataUpdateHandler? _onDataUpdated;
        private ProviderChangeHandler? _onProviderChanged;

        public Subscriber()
        {
        }        public bool Initialize()
        {
            try
            {
                // Ensure registry is available
                if (!SharingwayUtils.EnsureRegistryInitialized())
                {
                    // Try to continue even if registry init fails
                }

                _registry = new RegistryManager();
                if (!_registry.Initialize())
                {
                    // Continue without registry if it fails
                    _registry?.Dispose();
                    _registry = null;
                }
                else
                {
                    _registry.SetRegistryChangeHandler(OnRegistryChange);
                }

                _running = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Shutdown()
        {
            if (!_running) return;

            _running = false;

            // Stop all subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();

            _registry?.Shutdown();
            _registry?.Dispose();
            _registry = null;
        }

        public bool SubscribeTo(string provider)
        {
            if (_subscriptions.ContainsKey(provider))
            {
                return true; // Already subscribed
            }

            try
            {
                var subscription = new ProviderSubscription
                {
                    Name = provider,
                    Mmf = new MemoryMappedFileHelper(SharingwayUtils.GetProviderMmfName(provider), SharingwayUtils.DefaultMmfSize),
                    Sync = new NamedSyncObjects(provider),
                    CancellationSource = new CancellationTokenSource()
                };

                if (!subscription.Mmf.IsValid || !subscription.Sync.IsValid)
                {
                    subscription.Dispose();
                    return false;
                }

                subscription.WatchTask = Task.Run(() => WatchProviderAsync(subscription), subscription.CancellationSource.Token);
                
                return _subscriptions.TryAdd(provider, subscription);
            }
            catch
            {
                return false;
            }
        }

        public bool Unsubscribe(string provider)
        {
            if (_subscriptions.TryRemove(provider, out var subscription))
            {
                subscription.Dispose();
                return true;
            }
            return false;
        }        public List<string> GetSubscriptions()
        {
            return _subscriptions.Keys.ToList();
        }        public List<ProviderInfo> GetAvailableProviders()
        {
            SharingwayUtils.DebugLog($"Subscriber.GetAvailableProviders() called, _registry is {(_registry != null ? "not null" : "null")}", "Subscriber");
            try
            {
                if (_registry != null)
                {
                    SharingwayUtils.DebugLog("Using existing registry to get providers", "Subscriber");
                    var providers = _registry.GetRegistry();
                    SharingwayUtils.DebugLog($"Registry returned {providers.Count} providers", "Subscriber");
                    return providers;
                }
                else
                {
                    SharingwayUtils.DebugLog("Creating temporary registry connection", "Subscriber");
                    // Try to create a temporary registry connection
                    using var tempRegistry = new RegistryManager();
                    if (tempRegistry.Initialize())
                    {
                        SharingwayUtils.DebugLog("Temporary registry initialized successfully", "Subscriber");
                        var providers = tempRegistry.GetRegistry();
                        SharingwayUtils.DebugLog($"Temporary registry returned {providers.Count} providers", "Subscriber");
                        return providers;
                    }
                    else
                    {
                        SharingwayUtils.DebugLog("Failed to initialize temporary registry", "Subscriber");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in GetAvailableProviders: {ex.Message}");
                // Ignore errors and return empty list
            }

            return new List<ProviderInfo>();
        }

        public void SetDataUpdateHandler(DataUpdateHandler handler)
        {
            lock (_callbackLock)
            {
                _onDataUpdated = handler;
            }
        }

        public void SetProviderChangeHandler(ProviderChangeHandler handler)
        {
            lock (_callbackLock)
            {
                _onProviderChanged = handler;
            }
        }

        private async Task WatchProviderAsync(ProviderSubscription subscription)
        {
            while (!subscription.CancellationSource!.Token.IsCancellationRequested && _running)
            {
                try
                {
                    if (subscription.Sync?.WaitForSignal(1000) == true)
                    {
                        if (subscription.Sync.Lock(1000))
                        {
                            try
                            {
                                if (subscription.Mmf?.ReadJson(out var data) == true)
                                {
                                    lock (_callbackLock)
                                    {
                                        _onDataUpdated?.Invoke(subscription.Name, data);
                                    }
                                }
                            }
                            finally
                            {
                                subscription.Sync.Unlock();
                            }
                        }
                    }
                }
                catch
                {
                    // Continue on error
                }

                await Task.Delay(100, subscription.CancellationSource.Token);
            }
        }

        private void OnRegistryChange()
        {
            try
            {
                var providers = _registry?.GetRegistry() ?? new List<ProviderInfo>();

                lock (_callbackLock)
                {
                    if (_onProviderChanged != null)
                    {
                        foreach (var provider in providers)
                        {
                            _onProviderChanged(provider.Name, provider.Status);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _disposed = true;
            }
        }
    }
}
