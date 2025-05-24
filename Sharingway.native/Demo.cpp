#include "pch.h"
#include "Sharingway.h"
#include <iostream>
#include <thread>
#include <chrono>

using namespace Sharingway;

// Demo Provider function
void RunProvider() {
    std::cout << "[Provider] Starting provider..." << std::endl;
    
    Provider provider("test_provider", "Test Provider for Demo", {"data_publishing", "json_support"});
    
    if (!provider.Initialize()) {
        std::cout << "[Provider] Failed to initialize!" << std::endl;
        return;
    }

    std::cout << "[Provider] Provider initialized successfully" << std::endl;

    // Publish some test data
    for (int i = 0; i < 10; ++i) {
        json data = {
            {"timestamp", std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()},
            {"counter", i},
            {"message", "Hello from provider #" + std::to_string(i)},
            {"data", {
                {"temperature", 25.5 + i},
                {"humidity", 60 + i * 2},
                {"pressure", 1013.25 + i * 0.1}
            }}
        };

        if (provider.PublishData(data)) {
            std::cout << "[Provider] Published data #" << i << std::endl;
        } else {
            std::cout << "[Provider] Failed to publish data #" << i << std::endl;
        }

        std::this_thread::sleep_for(std::chrono::seconds(2));
    }

    std::cout << "[Provider] Shutting down..." << std::endl;
}

// Demo Subscriber function
void RunSubscriber() {
    std::cout << "[Subscriber] Starting subscriber..." << std::endl;
    
    Subscriber subscriber;
    
    if (!subscriber.Initialize()) {
        std::cout << "[Subscriber] Failed to initialize!" << std::endl;
        return;
    }

    // Set up event handlers
    subscriber.SetDataUpdateHandler([](const std::string& provider, const json& data) {
        std::cout << "[Subscriber] Data from " << provider << ": " << data.dump(2) << std::endl;
    });

    subscriber.SetProviderChangeHandler([](const std::string& provider, ProviderStatus status) {
        std::cout << "[Subscriber] Provider " << provider << " status changed to: " 
                  << ProviderStatusToString(status) << std::endl;
    });

    // Subscribe to test provider
    if (subscriber.SubscribeTo("test_provider")) {
        std::cout << "[Subscriber] Subscribed to test_provider" << std::endl;
    } else {
        std::cout << "[Subscriber] Failed to subscribe to test_provider" << std::endl;
    }

    // Wait and listen for data
    std::this_thread::sleep_for(std::chrono::seconds(25));

    std::cout << "[Subscriber] Shutting down..." << std::endl;
}

// Export functions for external use
extern "C" {
    __declspec(dllexport) void StartDemo() {
        std::cout << "=== Sharingway C++ Demo ===" << std::endl;
        
        // Start subscriber in background
        std::thread subscriberThread(RunSubscriber);
        
        // Wait a bit for subscriber to initialize
        std::this_thread::sleep_for(std::chrono::seconds(1));
        
        // Start provider
        RunProvider();
        
        // Wait for subscriber to finish
        subscriberThread.join();
        
        std::cout << "=== Demo Complete ===" << std::endl;
    }

    __declspec(dllexport) void TestRegistryManager() {
        std::cout << "=== Testing Registry Manager ===" << std::endl;
        
        RegistryManager registry;
        if (!registry.Initialize()) {
            std::cout << "Failed to initialize registry manager!" << std::endl;
            return;
        }

        // Register some test providers
        registry.RegisterProvider("provider1", "First test provider", {"test", "demo"});
        registry.RegisterProvider("provider2", "Second test provider", {"production", "data"});

        // Get and display registry
        auto providers = registry.GetRegistry();
        std::cout << "Registry contains " << providers.size() << " providers:" << std::endl;
        
        for (const auto& provider : providers) {
            std::cout << "  - " << provider.name << " (" << ProviderStatusToString(provider.status) << "): " 
                      << provider.description << std::endl;
            std::cout << "    Capabilities: ";
            for (const auto& cap : provider.capabilities) {
                std::cout << cap << " ";
            }
            std::cout << std::endl;
        }

        // Update status
        registry.UpdateStatus("provider1", ProviderStatus::Offline);
        
        // Remove provider
        registry.RemoveProvider("provider2");

        std::cout << "Registry test complete." << std::endl;
    }
}
