#pragma once
#include "pch.h"

namespace Sharingway {

    // Forward declarations
    class RegistryManager;
    class Provider;
    class Subscriber;

    // Constants
    constexpr size_t DEFAULT_MMF_SIZE = 1024 * 1024; // 1MB default
    constexpr const char* REGISTRY_NAME = "Global\\Sharingway.Registry";

    // Provider status enumeration
    enum class ProviderStatus {
        Online,
        Offline,
        Error
    };

    // Provider information structure
    struct ProviderInfo {
        std::string name;
        ProviderStatus status;
        std::string description;
        std::vector<std::string> capabilities;
        std::chrono::system_clock::time_point lastUpdate;
        std::chrono::system_clock::time_point lastHeartbeat;
    };

    // Event handler types
    using DataUpdateHandler = std::function<void(const std::string& provider, const json& data)>;
    using ProviderChangeHandler = std::function<void(const std::string& provider, ProviderStatus status)>;
    using RegistryChangeHandler = std::function<void()>;

    // Utility class for memory-mapped file operations
    class MemoryMappedFile {
    private:
        HANDLE hFile;
        HANDLE hMapping;
        void* pView;
        size_t size;
        std::string name;

    public:
        MemoryMappedFile(const std::string& name, size_t size);
        ~MemoryMappedFile();

        bool IsValid() const;
        void* GetView() const;
        size_t GetSize() const;
        
        // Write JSON data to MMF
        bool WriteJson(const json& data);
        
        // Read JSON data from MMF
        bool ReadJson(json& data) const;
    };

    // Utility class for named synchronization objects
    class NamedSyncObjects {
    private:
        HANDLE hMutex;
        HANDLE hEvent;
        std::string baseName;

    public:
        NamedSyncObjects(const std::string& baseName);
        ~NamedSyncObjects();

        bool IsValid() const;
        bool Lock(DWORD timeout = INFINITE);
        void Unlock();
        void Signal();
        bool WaitForSignal(DWORD timeout = INFINITE);
    };

    // Registry Manager - manages the global provider registry
    class RegistryManager {
    private:
        std::unique_ptr<MemoryMappedFile> registryMmf;
        std::unique_ptr<NamedSyncObjects> registrySync;
        std::atomic<bool> running;
        std::thread watchThread;
        std::mutex callbackMutex;
        RegistryChangeHandler onRegistryChanged;

        void WatchRegistry();

    public:
        RegistryManager();
        ~RegistryManager();

        bool Initialize();
        void Shutdown();

        bool RegisterProvider(const std::string& name, const std::string& description, const std::vector<std::string>& capabilities);
        bool UpdateStatus(const std::string& name, ProviderStatus status);
        bool RemoveProvider(const std::string& name);
        std::vector<ProviderInfo> GetRegistry();

        void SetRegistryChangeHandler(RegistryChangeHandler handler);
    };

    // Provider - publishes data to its own MMF
    class Provider {
    private:
        std::string providerName;
        std::unique_ptr<MemoryMappedFile> dataMmf;
        std::unique_ptr<NamedSyncObjects> dataSync;
        std::shared_ptr<RegistryManager> registry;
        std::atomic<bool> isOnline;

    public:
        Provider(const std::string& name, const std::string& description, const std::vector<std::string>& capabilities);
        ~Provider();

        bool Initialize(size_t mmfSize = DEFAULT_MMF_SIZE);
        void Shutdown();

        bool PublishData(const json& data);
        bool IsOnline() const;
        std::string GetName() const;
    };

    // Subscriber - reads from provider MMFs
    class Subscriber {
    private:
        struct ProviderSubscription {
            std::string name;
            std::unique_ptr<MemoryMappedFile> mmf;
            std::unique_ptr<NamedSyncObjects> sync;
            std::thread watchThread;
            std::atomic<bool> watching;
        };

        std::shared_ptr<RegistryManager> registry;
        std::map<std::string, std::unique_ptr<ProviderSubscription>> subscriptions;
        std::mutex subscriptionMutex;
        std::mutex callbackMutex;
        std::atomic<bool> running;

        DataUpdateHandler onDataUpdated;
        ProviderChangeHandler onProviderChanged;

        void WatchProvider(ProviderSubscription* subscription);
        void OnRegistryChange();

    public:
        Subscriber();
        ~Subscriber();

        bool Initialize();
        void Shutdown();        bool SubscribeTo(const std::string& provider);
        bool Unsubscribe(const std::string& provider);
        std::vector<std::string> GetSubscriptions();
        std::vector<ProviderInfo> GetAvailableProviders();

        void SetDataUpdateHandler(DataUpdateHandler handler);
        void SetProviderChangeHandler(ProviderChangeHandler handler);
    };

    // Utility functions
    std::string GetProviderMmfName(const std::string& provider);
    std::string GetProviderMutexName(const std::string& provider);
    std::string GetProviderEventName(const std::string& provider);
    std::string ProviderStatusToString(ProviderStatus status);
    ProviderStatus StringToProviderStatus(const std::string& status);

} // namespace Sharingway
