// Sharingway Static Library Entry Point
#include "pch.h"
#include "Sharingway.h"

// Static library doesn't need DllMain, but we'll keep the exports for C API

// Export the Sharingway API for external use
extern "C" {
    // Create and return a new Provider instance
    Sharingway::Provider* CreateProvider(const char* name, const char* description) {
        try {
            std::vector<std::string> capabilities = {"ipc", "json"};
            return new Sharingway::Provider(name, description, capabilities);
        }
        catch (...) {
            return nullptr;
        }
    }

    // Initialize a Provider
    bool InitializeProvider(Sharingway::Provider* provider) {
        if (!provider) return false;
        return provider->Initialize();
    }

    // Publish JSON data through a Provider
    bool PublishData(Sharingway::Provider* provider, const char* jsonData) {
        if (!provider || !jsonData) return false;
        try {
            nlohmann::json data = nlohmann::json::parse(jsonData);
            return provider->PublishData(data);
        }
        catch (...) {
            return false;
        }
    }

    // Destroy a Provider instance
    void DestroyProvider(Sharingway::Provider* provider) {
        delete provider;
    }

    // Create and return a new Subscriber instance
    Sharingway::Subscriber* CreateSubscriber() {
        try {
            return new Sharingway::Subscriber();
        }
        catch (...) {
            return nullptr;
        }
    }

    // Initialize a Subscriber
    bool InitializeSubscriber(Sharingway::Subscriber* subscriber) {
        if (!subscriber) return false;
        return subscriber->Initialize();
    }

    // Subscribe to a provider
    bool SubscribeTo(Sharingway::Subscriber* subscriber, const char* providerName) {
        if (!subscriber || !providerName) return false;
        return subscriber->SubscribeTo(providerName);
    }

    // Destroy a Subscriber instance
    void DestroySubscriber(Sharingway::Subscriber* subscriber) {
        delete subscriber;
    }
}

