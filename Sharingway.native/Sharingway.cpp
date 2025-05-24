#include "pch.h"
#include "Sharingway.h"
#include <sstream>
#include <iomanip>

namespace Sharingway {    // Debug logging system implementation
    bool SharingwayUtils::DebugLogging = false;

    void SharingwayUtils::DebugLog(const std::string& message, const std::string& component) {
        if (DebugLogging) {
            std::cout << "[" << component << "] [DEBUG] " << message << std::endl;
        }
    }

    bool SharingwayUtils::EnsureRegistryInitialized() {
        try {
            auto registryMmf = std::make_unique<MemoryMappedFile>(REGISTRY_NAME, DEFAULT_MMF_SIZE);
            auto registrySync = std::make_unique<NamedSyncObjects>("Registry");

            if (!registryMmf->IsValid() || !registrySync->IsValid()) {
                SharingwayUtils::DebugLog("Failed to create registry MMF or sync objects", "SharingwayUtils");
                return false;
            }

            if (!registrySync->Lock(5000)) {
                SharingwayUtils::DebugLog("Failed to acquire registry lock", "SharingwayUtils");
                return false;
            }

            try {
                // Check if registry has valid data
                json existingData;
                if (!registryMmf->ReadJson(existingData) || !existingData.is_object()) {
                    // Initialize with empty registry
                    json emptyRegistry = json::object();
                    bool result = registryMmf->WriteJson(emptyRegistry);
                    
                    if (result) {
                        registrySync->Signal();
                        SharingwayUtils::DebugLog("Initialized empty registry", "SharingwayUtils");
                    }
                    
                    registrySync->Unlock();
                    return result;
                }
                
                SharingwayUtils::DebugLog("Registry already initialized", "SharingwayUtils");
                registrySync->Unlock();
                return true;
            }
            catch (...) {
                registrySync->Unlock();
                SharingwayUtils::DebugLog("Exception during registry initialization", "SharingwayUtils");
                return false;
            }
        }
        catch (...) {
            SharingwayUtils::DebugLog("Exception in EnsureRegistryInitialized", "SharingwayUtils");
            return false;
        }
    }

    // Utility functions implementation
    std::string GetProviderMmfName(const std::string& provider) {
        return "Global\\Sharingway." + provider;
    }

    std::string GetProviderMutexName(const std::string& provider) {
        return "Global\\Sharingway." + provider + ".Lock";
    }

    std::string GetProviderEventName(const std::string& provider) {
        return "Global\\Sharingway." + provider + ".Signal";
    }

    std::string ProviderStatusToString(ProviderStatus status) {
        switch (status) {
        case ProviderStatus::Online: return "online";
        case ProviderStatus::Offline: return "offline";
        case ProviderStatus::Error: return "error";
        default: return "unknown";
        }
    }

    ProviderStatus StringToProviderStatus(const std::string& status) {
        if (status == "online") return ProviderStatus::Online;
        if (status == "offline") return ProviderStatus::Offline;
        if (status == "error") return ProviderStatus::Error;
        return ProviderStatus::Offline;
    }    // MemoryMappedFile implementation
    MemoryMappedFile::MemoryMappedFile(const std::string& name, size_t size)
        : hFile(INVALID_HANDLE_VALUE), hMapping(NULL), pView(nullptr), size(size), name(name) {
        
        SharingwayUtils::DebugLog("Creating memory-mapped file: " + name, "MMF");
        
        // Try to open existing MMF first
        hMapping = OpenFileMappingA(FILE_MAP_ALL_ACCESS, FALSE, name.c_str());
        
        if (hMapping == NULL) {
            DWORD lastError = GetLastError();
            SharingwayUtils::DebugLog("Failed to open existing MMF: " + name + ", error: " + std::to_string(lastError), "MMF");
            
            // Try to create new MMF
            hMapping = CreateFileMappingA(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, static_cast<DWORD>(size), name.c_str());
            
            if (hMapping == NULL) {
                lastError = GetLastError();
                SharingwayUtils::DebugLog("Failed to create new MMF: " + name + ", error: " + std::to_string(lastError), "MMF");
                
                // Try without Global prefix for local access (for cross-language compatibility)
                std::string localName = name;
                if (localName.substr(0, 7) == "Global\\") {
                    localName = localName.substr(7);
                    SharingwayUtils::DebugLog("Trying without Global\\ prefix: " + localName, "MMF");
                    
                    // Try to open existing MMF with local name
                    hMapping = OpenFileMappingA(FILE_MAP_ALL_ACCESS, FALSE, localName.c_str());
                    
                    if (hMapping == NULL) {
                        SharingwayUtils::DebugLog("Failed to open existing MMF with local name, trying to create", "MMF");
                        // Try to create new MMF with local name
                        hMapping = CreateFileMappingA(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, static_cast<DWORD>(size), localName.c_str());
                    }
                }
            }
        }        if (hMapping != NULL) {
            pView = MapViewOfFile(hMapping, FILE_MAP_ALL_ACCESS, 0, 0, size);
            
            if (pView == nullptr) {
                DWORD lastError = GetLastError();
                SharingwayUtils::DebugLog("Failed to map view of file, error: " + std::to_string(lastError), "MMF");
            } else {
                SharingwayUtils::DebugLog("Successfully mapped view of file: " + name, "MMF");
            }
        } else {
            SharingwayUtils::DebugLog("Failed to create or open memory-mapped file: " + name, "MMF");
        }
    }

    MemoryMappedFile::~MemoryMappedFile() {
        if (pView) {
            UnmapViewOfFile(pView);
            pView = nullptr;
        }
        if (hMapping) {
            CloseHandle(hMapping);
            hMapping = NULL;
        }
    }

    bool MemoryMappedFile::IsValid() const {
        return pView != nullptr;
    }

    void* MemoryMappedFile::GetView() const {
        return pView;
    }

    size_t MemoryMappedFile::GetSize() const {
        return size;
    }

    bool MemoryMappedFile::WriteJson(const json& data) {
        if (!IsValid()) return false;

        try {
            std::string jsonStr = data.dump();
            std::vector<uint8_t> utf8Data(jsonStr.begin(), jsonStr.end());
            
            if (utf8Data.size() + sizeof(int32_t) > size) {
                return false; // Data too large
            }

            // Write length first (4 bytes)
            int32_t length = static_cast<int32_t>(utf8Data.size());
            memcpy(pView, &length, sizeof(int32_t));
            
            // Write JSON data
            memcpy(static_cast<uint8_t*>(pView) + sizeof(int32_t), utf8Data.data(), utf8Data.size());
            
            return true;
        }
        catch (...) {
            return false;
        }
    }

    bool MemoryMappedFile::ReadJson(json& data) const {
        if (!IsValid()) return false;

        try {
            // Read length
            int32_t length;
            memcpy(&length, pView, sizeof(int32_t));
            
            if (length <= 0 || length > static_cast<int32_t>(size - sizeof(int32_t))) {
                return false; // Invalid length
            }

            // Read JSON data
            std::vector<uint8_t> utf8Data(length);
            memcpy(utf8Data.data(), static_cast<uint8_t*>(pView) + sizeof(int32_t), length);
            
            std::string jsonStr(utf8Data.begin(), utf8Data.end());
            data = json::parse(jsonStr);
            
            return true;
        }
        catch (...) {
            return false;
        }
    }    // NamedSyncObjects implementation
    NamedSyncObjects::NamedSyncObjects(const std::string& baseName)
        : hMutex(NULL), hEvent(NULL), baseName(baseName) {
        
        std::string mutexName = GetProviderMutexName(baseName);
        std::string eventName = GetProviderEventName(baseName);
        
        SharingwayUtils::DebugLog("Creating named sync objects for: " + baseName, "Sync");
        SharingwayUtils::DebugLog("Mutex name: " + mutexName, "Sync");
        SharingwayUtils::DebugLog("Event name: " + eventName, "Sync");

        // Try to open existing mutex first
        hMutex = OpenMutexA(MUTEX_ALL_ACCESS, FALSE, mutexName.c_str());
        if (hMutex == NULL) {
            DWORD lastError = GetLastError();
            SharingwayUtils::DebugLog("Failed to open existing mutex, error: " + std::to_string(lastError), "Sync");
            
            hMutex = CreateMutexA(NULL, FALSE, mutexName.c_str());
            
            if (hMutex == NULL) {
                lastError = GetLastError();
                SharingwayUtils::DebugLog("Failed to create mutex, error: " + std::to_string(lastError), "Sync");
                
                // Try without Global prefix
                std::string localMutexName = mutexName;
                if (localMutexName.substr(0, 7) == "Global\\") {
                    localMutexName = localMutexName.substr(7);
                    SharingwayUtils::DebugLog("Trying without Global\\ prefix: " + localMutexName, "Sync");
                    
                    hMutex = OpenMutexA(MUTEX_ALL_ACCESS, FALSE, localMutexName.c_str());
                    if (hMutex == NULL) {
                        hMutex = CreateMutexA(NULL, FALSE, localMutexName.c_str());
                    }
                }
            }
        }

        // Try to open existing event first
        hEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, eventName.c_str());
        if (hEvent == NULL) {
            DWORD lastError = GetLastError();
            SharingwayUtils::DebugLog("Failed to open existing event, error: " + std::to_string(lastError), "Sync");
            
            hEvent = CreateEventA(NULL, FALSE, FALSE, eventName.c_str());
            
            if (hEvent == NULL) {
                lastError = GetLastError();
                SharingwayUtils::DebugLog("Failed to create event, error: " + std::to_string(lastError), "Sync");
                
                // Try without Global prefix
                std::string localEventName = eventName;
                if (localEventName.substr(0, 7) == "Global\\") {
                    localEventName = localEventName.substr(7);
                    SharingwayUtils::DebugLog("Trying without Global\\ prefix: " + localEventName, "Sync");
                    
                    hEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, localEventName.c_str());
                    if (hEvent == NULL) {
                        hEvent = CreateEventA(NULL, FALSE, FALSE, localEventName.c_str());
                    }
                }
            }
        }
        
        if (hMutex == NULL || hEvent == NULL) {
            SharingwayUtils::DebugLog("Failed to create sync objects for: " + baseName, "Sync");
        } else {
            SharingwayUtils::DebugLog("Successfully created sync objects for: " + baseName, "Sync");
        }
    }

    NamedSyncObjects::~NamedSyncObjects() {
        if (hMutex) {
            CloseHandle(hMutex);
            hMutex = NULL;
        }
        if (hEvent) {
            CloseHandle(hEvent);
            hEvent = NULL;
        }
    }

    bool NamedSyncObjects::IsValid() const {
        return hMutex != NULL && hEvent != NULL;
    }

    bool NamedSyncObjects::Lock(DWORD timeout) {
        if (!IsValid()) return false;
        return WaitForSingleObject(hMutex, timeout) == WAIT_OBJECT_0;
    }

    void NamedSyncObjects::Unlock() {
        if (hMutex) {
            ReleaseMutex(hMutex);
        }
    }

    void NamedSyncObjects::Signal() {
        if (hEvent) {
            SetEvent(hEvent);
        }
    }

    bool NamedSyncObjects::WaitForSignal(DWORD timeout) {
        if (!IsValid()) return false;
        return WaitForSingleObject(hEvent, timeout) == WAIT_OBJECT_0;
    }

    // RegistryManager implementation
    RegistryManager::RegistryManager() : running(false) {}

    RegistryManager::~RegistryManager() {
        Shutdown();
    }    bool RegistryManager::Initialize() {
        SharingwayUtils::DebugLog("Initializing registry manager", "Registry");
        
        // First, ensure registry is properly initialized
        if (!SharingwayUtils::EnsureRegistryInitialized()) {
            SharingwayUtils::DebugLog("Registry initialization failed, but trying to continue", "Registry");
        }
        
        registryMmf = std::make_unique<MemoryMappedFile>(REGISTRY_NAME, DEFAULT_MMF_SIZE);
        
        if (!registryMmf->IsValid()) {
            SharingwayUtils::DebugLog("Failed to create registry MMF, trying alternate names", "Registry");
            
            // Try name without Global prefix
            std::string localName = REGISTRY_NAME;
            if (localName.substr(0, 7) == "Global\\") {
                localName = localName.substr(7);
                SharingwayUtils::DebugLog("Trying registry MMF with local name: " + localName, "Registry");
                registryMmf = std::make_unique<MemoryMappedFile>(localName, DEFAULT_MMF_SIZE);
            }
            
            if (!registryMmf->IsValid()) {
                SharingwayUtils::DebugLog("All attempts to access registry MMF failed", "Registry");
                return false;
            }
        }
        
        registrySync = std::make_unique<NamedSyncObjects>("Registry");
        if (!registrySync->IsValid()) {
            SharingwayUtils::DebugLog("Failed to create registry sync objects", "Registry");
            return false;
        }

        running = true;
        watchThread = std::thread(&RegistryManager::WatchRegistry, this);
        
        SharingwayUtils::DebugLog("Registry manager initialized successfully", "Registry");
        return true;
    }

    void RegistryManager::Shutdown() {
        running = false;
        if (watchThread.joinable()) {
            watchThread.join();
        }
        registryMmf.reset();
        registrySync.reset();
    }    bool RegistryManager::RegisterProvider(const std::string& name, const std::string& description, const std::vector<std::string>& capabilities) {
        SharingwayUtils::DebugLog("Registering provider: " + name, "Registry");
        
        if (!registrySync->Lock(5000)) {
            SharingwayUtils::DebugLog("Failed to acquire registry lock for provider registration", "Registry");
            return false;
        }

        try {
            json registry;
            registryMmf->ReadJson(registry);

            if (!registry.is_object()) {
                registry = json::object();
                SharingwayUtils::DebugLog("Initialized empty registry", "Registry");
            }

            auto now = std::chrono::system_clock::now();
            auto timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count();

            registry[name] = {
                {"status", "online"},
                {"description", description},
                {"capabilities", capabilities},
                {"lastUpdate", timestamp},
                {"lastHeartbeat", timestamp}
            };

            bool result = registryMmf->WriteJson(registry);
            registrySync->Unlock();
            
            if (result) {
                registrySync->Signal();
                SharingwayUtils::DebugLog("Provider registered successfully: " + name, "Registry");
            } else {
                SharingwayUtils::DebugLog("Failed to write registry data for provider: " + name, "Registry");
            }
            
            return result;
        }
        catch (...) {
            registrySync->Unlock();
            SharingwayUtils::DebugLog("Exception during provider registration: " + name, "Registry");
            return false;
        }
    }

    bool RegistryManager::UpdateStatus(const std::string& name, ProviderStatus status) {
        if (!registrySync->Lock(5000)) return false;

        try {
            json registry;
            registryMmf->ReadJson(registry);

            if (!registry.is_object() || !registry.contains(name)) {
                registrySync->Unlock();
                return false;
            }

            auto now = std::chrono::system_clock::now();
            auto timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count();

            registry[name]["status"] = ProviderStatusToString(status);
            registry[name]["lastUpdate"] = timestamp;

            bool result = registryMmf->WriteJson(registry);
            registrySync->Unlock();
            
            if (result) {
                registrySync->Signal();
            }
            
            return result;
        }
        catch (...) {
            registrySync->Unlock();
            return false;
        }
    }

    bool RegistryManager::RemoveProvider(const std::string& name) {
        if (!registrySync->Lock(5000)) return false;

        try {
            json registry;
            registryMmf->ReadJson(registry);

            if (!registry.is_object()) {
                registrySync->Unlock();
                return false;
            }

            registry.erase(name);

            bool result = registryMmf->WriteJson(registry);
            registrySync->Unlock();
            
            if (result) {
                registrySync->Signal();
            }
            
            return result;
        }
        catch (...) {
            registrySync->Unlock();
            return false;
        }
    }

    std::vector<ProviderInfo> RegistryManager::GetRegistry() {
        std::vector<ProviderInfo> providers;
        
        if (!registrySync->Lock(5000)) return providers;

        try {
            json registry;
            if (registryMmf->ReadJson(registry) && registry.is_object()) {
                for (auto& [name, info] : registry.items()) {
                    ProviderInfo provider;
                    provider.name = name;
                    provider.status = StringToProviderStatus(info.value("status", "offline"));
                    provider.description = info.value("description", "");
                    
                    if (info.contains("capabilities") && info["capabilities"].is_array()) {
                        for (auto& cap : info["capabilities"]) {
                            provider.capabilities.push_back(cap.get<std::string>());
                        }
                    }

                    auto lastUpdate = info.value("lastUpdate", 0LL);
                    auto lastHeartbeat = info.value("lastHeartbeat", 0LL);
                    
                    provider.lastUpdate = std::chrono::system_clock::from_time_t(lastUpdate / 1000);
                    provider.lastHeartbeat = std::chrono::system_clock::from_time_t(lastHeartbeat / 1000);

                    providers.push_back(provider);
                }
            }
        }
        catch (...) {
            // Error reading registry
        }

        registrySync->Unlock();
        return providers;
    }

    void RegistryManager::SetRegistryChangeHandler(RegistryChangeHandler handler) {
        std::lock_guard<std::mutex> lock(callbackMutex);
        onRegistryChanged = handler;
    }

    void RegistryManager::WatchRegistry() {
        while (running) {
            if (registrySync->WaitForSignal(1000)) {
                std::lock_guard<std::mutex> lock(callbackMutex);
                if (onRegistryChanged) {
                    onRegistryChanged();
                }
            }
        }
    }    // Provider implementation
    Provider::Provider(const std::string& name, const std::string& description, const std::vector<std::string>& capabilities)
        : providerName(name), isOnline(false) {
        
        SharingwayUtils::DebugLog("Creating provider: " + name, "Provider");
        
        // Ensure registry is available before doing anything
        if (!SharingwayUtils::EnsureRegistryInitialized()) {
            SharingwayUtils::DebugLog("Registry initialization failed, continuing without registry", "Provider");
        }
        
        try {
            registry = std::make_shared<RegistryManager>();
            if (registry->Initialize()) {
                SharingwayUtils::DebugLog("Registry manager initialized successfully", "Provider");
                if (registry->RegisterProvider(name, description, capabilities)) {
                    SharingwayUtils::DebugLog("Provider registered in registry", "Provider");
                } else {
                    SharingwayUtils::DebugLog("Failed to register provider in registry", "Provider");
                }
            } else {
                SharingwayUtils::DebugLog("Failed to initialize registry manager", "Provider");
                registry = nullptr;
            }
        }
        catch (...) {
            SharingwayUtils::DebugLog("Exception during registry initialization, continuing without registry", "Provider");
            registry = nullptr;
        }
    }

    Provider::~Provider() {
        Shutdown();
    }    bool Provider::Initialize(size_t mmfSize) {
        SharingwayUtils::DebugLog("Initializing provider: " + providerName, "Provider");
        
        dataMmf = std::make_unique<MemoryMappedFile>(GetProviderMmfName(providerName), mmfSize);
        dataSync = std::make_unique<NamedSyncObjects>(providerName);

        if (!dataMmf->IsValid() || !dataSync->IsValid()) {
            SharingwayUtils::DebugLog("Failed to initialize MMF or sync objects", "Provider");
            return false;
        }

        isOnline = true;
        SharingwayUtils::DebugLog("Provider MMF and sync objects created successfully", "Provider");
        
        if (registry) {
            if (registry->UpdateStatus(providerName, ProviderStatus::Online)) {
                SharingwayUtils::DebugLog("Updated provider status to online in registry", "Provider");
            } else {
                SharingwayUtils::DebugLog("Failed to update provider status in registry", "Provider");
            }
        }
        
        SharingwayUtils::DebugLog("Provider initialization completed successfully", "Provider");
        return true;
    }

    void Provider::Shutdown() {
        if (isOnline) {
            isOnline = false;
            
            // Clear the MMF data
            if (dataMmf && dataSync) {
                if (dataSync->Lock(1000)) {
                    json emptyData = json::object();
                    dataMmf->WriteJson(emptyData);
                    dataSync->Unlock();
                    dataSync->Signal();
                }
            }

            // Update registry status
            if (registry) {
                registry->UpdateStatus(providerName, ProviderStatus::Offline);
            }
        }

        dataMmf.reset();
        dataSync.reset();
    }

    bool Provider::PublishData(const json& data) {
        if (!isOnline || !dataMmf || !dataSync) {
            return false;
        }

        if (!dataSync->Lock(5000)) {
            return false;
        }

        bool result = dataMmf->WriteJson(data);
        dataSync->Unlock();

        if (result) {
            dataSync->Signal();
            // Update heartbeat
            registry->UpdateStatus(providerName, ProviderStatus::Online);
        }

        return result;
    }

    bool Provider::IsOnline() const {
        return isOnline;
    }

    std::string Provider::GetName() const {
        return providerName;
    }

    // Subscriber implementation
    Subscriber::Subscriber() : running(false) {}

    Subscriber::~Subscriber() {
        Shutdown();
    }    bool Subscriber::Initialize() {
        SharingwayUtils::DebugLog("Initializing subscriber", "Subscriber");
        
        // Ensure registry is available
        if (!SharingwayUtils::EnsureRegistryInitialized()) {
            SharingwayUtils::DebugLog("Registry initialization failed", "Subscriber");
            return false;
        }
        
        registry = std::make_shared<RegistryManager>();
        if (!registry->Initialize()) {
            SharingwayUtils::DebugLog("Failed to initialize registry manager", "Subscriber");
            return false;
        }

        registry->SetRegistryChangeHandler([this]() { OnRegistryChange(); });
        running = true;
        
        SharingwayUtils::DebugLog("Subscriber initialized successfully", "Subscriber");
        return true;
    }

    void Subscriber::Shutdown() {
        running = false;

        // Stop all watch threads
        {
            std::lock_guard<std::mutex> lock(subscriptionMutex);
            for (auto& [name, subscription] : subscriptions) {
                subscription->watching = false;
                if (subscription->watchThread.joinable()) {
                    subscription->watchThread.join();
                }
            }
            subscriptions.clear();
        }

        if (registry) {
            registry->Shutdown();
            registry.reset();
        }
    }

    bool Subscriber::SubscribeTo(const std::string& provider) {
        std::lock_guard<std::mutex> lock(subscriptionMutex);

        if (subscriptions.find(provider) != subscriptions.end()) {
            return true; // Already subscribed
        }

        auto subscription = std::make_unique<ProviderSubscription>();
        subscription->name = provider;
        subscription->mmf = std::make_unique<MemoryMappedFile>(GetProviderMmfName(provider), DEFAULT_MMF_SIZE);
        subscription->sync = std::make_unique<NamedSyncObjects>(provider);
        subscription->watching = true;

        if (!subscription->mmf->IsValid() || !subscription->sync->IsValid()) {
            return false;
        }

        subscription->watchThread = std::thread(&Subscriber::WatchProvider, this, subscription.get());
        subscriptions[provider] = std::move(subscription);

        return true;
    }

    bool Subscriber::Unsubscribe(const std::string& provider) {
        std::lock_guard<std::mutex> lock(subscriptionMutex);

        auto it = subscriptions.find(provider);
        if (it == subscriptions.end()) {
            return false;
        }

        it->second->watching = false;
        if (it->second->watchThread.joinable()) {
            it->second->watchThread.join();
        }

        subscriptions.erase(it);
        return true;
    }    std::vector<std::string> Subscriber::GetSubscriptions() {
        std::lock_guard<std::mutex> lock(subscriptionMutex);
        std::vector<std::string> result;
        
        for (const auto& [name, subscription] : subscriptions) {
            result.push_back(name);
        }
        
        return result;
    }

    std::vector<ProviderInfo> Subscriber::GetAvailableProviders() {
        try {
            if (registry) {
                return registry->GetRegistry();
            }
            else {
                // Try to create a temporary registry connection
                auto tempRegistry = std::make_shared<RegistryManager>();
                if (tempRegistry->Initialize()) {
                    return tempRegistry->GetRegistry();
                }
            }
        }
        catch (...) {
            // Ignore errors and return empty list
        }

        return std::vector<ProviderInfo>();
    }

    void Subscriber::SetDataUpdateHandler(DataUpdateHandler handler) {
        std::lock_guard<std::mutex> lock(callbackMutex);
        onDataUpdated = handler;
    }

    void Subscriber::SetProviderChangeHandler(ProviderChangeHandler handler) {
        std::lock_guard<std::mutex> lock(callbackMutex);
        onProviderChanged = handler;
    }

    void Subscriber::WatchProvider(ProviderSubscription* subscription) {
        while (subscription->watching && running) {
            if (subscription->sync->WaitForSignal(1000)) {
                if (subscription->sync->Lock(1000)) {
                    json data;
                    if (subscription->mmf->ReadJson(data)) {
                        std::lock_guard<std::mutex> lock(callbackMutex);
                        if (onDataUpdated) {
                            onDataUpdated(subscription->name, data);
                        }
                    }
                    subscription->sync->Unlock();
                }
            }
        }
    }

    void Subscriber::OnRegistryChange() {
        auto providers = registry->GetRegistry();
        
        std::lock_guard<std::mutex> lock(callbackMutex);
        if (onProviderChanged) {
            for (const auto& provider : providers) {
                onProviderChanged(provider.name, provider.status);
            }
        }
    }

} // namespace Sharingway
