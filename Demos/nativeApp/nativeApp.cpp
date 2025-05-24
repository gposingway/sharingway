#include <iostream>
#include <string>
#include <vector>
#include <thread>
#include <chrono>
#include <conio.h>
#include "../../Sharingway.native/Sharingway.h"

using namespace Sharingway;

// Enable debug logging for C++ demo
bool enableDebugLogging() {
    SharingwayUtils::DebugLogging = true;
    SharingwayUtils::DebugLog("C++ demo application starting with debug logging enabled", "NativeApp");
    return true;
}
static bool debugInitialized = enableDebugLogging();

// Forward declarations
void TestReadOperation(const std::string& providerName);

void ShowUsage()
{
    std::cout << "Sharingway Native Demo Application\n";
    std::cout << "==================================\n";
    std::cout << "Usage:\n";
    std::cout << "  nativeApp provider <name>   - Run as data provider\n";
    std::cout << "  nativeApp subscriber        - Run as data subscriber\n";
    std::cout << "  nativeApp interactive       - Interactive mode\n";
}

void RunProviderDemo(const std::string& providerName)
{
    std::cout << "Starting C++ Provider Demo: " << providerName << "\n";
    
    std::vector<std::string> capabilities = {"native_sensor", "cpp_data"};
    Provider provider(providerName, "C++ native sensor provider", capabilities);
    
    if (!provider.Initialize()) {
        std::cout << "Failed to initialize provider!\n";
        return;
    }
    
    std::cout << "Provider '" << providerName << "' started successfully!\n";
    std::cout << "Publishing data every 3 seconds...\n";
    std::cout << "Press 'q' to quit\n";
    
    int counter = 0;
    while (true) {
        // Check for quit command
        if (_kbhit()) {
            char key = _getch();
            if (key == 'q' || key == 'Q') {
                break;
            }
        }
        
        // Generate sample data
        nlohmann::json sensorData = {
            {"timestamp", std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count()},
            {"counter", counter++},
            {"cpu_usage", 45.5 + (rand() % 200) / 10.0}, // 45.5-65.5%
            {"memory_usage", 60.0 + (rand() % 300) / 10.0}, // 60.0-90.0%
            {"disk_io", rand() % 1000}, // 0-1000 MB/s
            {"source", "native_cpp"},
            {"provider_id", providerName}
        };
          if (provider.PublishData(sensorData)) {
            std::cout << "Published: Counter=" << counter-1 
                     << ", CPU=" << sensorData["cpu_usage"] << "%" 
                     << ", Memory=" << sensorData["memory_usage"] << "%\n";
                     
            // Test read operation - read back what we just published
            TestReadOperation(providerName);
        } else {
            std::cout << "Failed to publish data\n";
        }
        
        std::this_thread::sleep_for(std::chrono::seconds(3));
    }
    
    std::cout << "Shutting down provider...\n";
    provider.Shutdown();
}

void RunSubscriberDemo()
{
    std::cout << "Starting C++ Subscriber Demo...\n";
    
    Subscriber subscriber;
    
    if (!subscriber.Initialize()) {
        std::cout << "Failed to initialize subscriber!\n";
        return;
    }
    
    // Set up callbacks
    subscriber.SetDataUpdateHandler([](const std::string& provider, const nlohmann::json& data) {
        std::cout << "\n[DATA] From '" << provider << "':\n";
        std::cout << "  " << data.dump(2) << "\n\n";
    });
    
    subscriber.SetProviderChangeHandler([](const std::string& provider, ProviderStatus status) {
        std::cout << "[STATUS] Provider '" << provider << "' is now: " 
                  << ProviderStatusToString(status) << "\n";
    });
    
    // Get available providers
    auto providers = subscriber.GetAvailableProviders();
    std::cout << "Available providers (" << providers.size() << "):\n";
    for (const auto& info : providers) {
        std::cout << "  - " << info.name << ": " << info.description 
                  << " (Status: " << ProviderStatusToString(info.status) << ")\n";
          // Subscribe to all available providers
        if (subscriber.SubscribeTo(info.name)) {
            std::cout << "    Subscribed successfully\n";
            
            // Test direct read from this provider
            TestReadOperation(info.name);
        } else {
            std::cout << "    Failed to subscribe\n";
        }
    }
    
    if (providers.empty()) {
        std::cout << "No providers available. Start a provider first.\n";
    }
    
    std::cout << "\nListening for data updates... Press 'q' to quit\n";
    
    while (true) {
        if (_kbhit()) {
            char key = _getch();
            if (key == 'q' || key == 'Q') {
                break;
            }
        }
        
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
    
    std::cout << "Shutting down subscriber...\n";
    subscriber.Shutdown();
}

void RunInteractiveDemo()
{
    std::cout << "C++ Interactive Demo Mode\n";
    std::cout << "Commands:\n";
    std::cout << "  p <name> - Start provider with specified name\n";
    std::cout << "  s        - Start subscriber\n";
    std::cout << "  l        - List providers\n";
    std::cout << "  q        - Quit\n";
    
    Provider* provider = nullptr;
    Subscriber* subscriber = nullptr;
    
    std::string input;
    while (true) {
        std::cout << "\nCommand: ";
        std::getline(std::cin, input);
        
        if (input.empty()) continue;
        
        char command = input[0];
        switch (command) {
            case 'p': {
                if (provider != nullptr) {
                    std::cout << "Provider already running\n";
                    break;
                }
                
                std::string providerName = "CppProvider";
                if (input.length() > 2) {
                    providerName = input.substr(2); // Skip "p "
                }
                
                std::vector<std::string> capabilities = {"interactive", "cpp_native"};
                provider = new Provider(providerName, "Interactive C++ provider", capabilities);
                
                if (provider->Initialize()) {
                    std::cout << "Provider '" << providerName << "' started. Type data to publish:\n";
                    
                    // Start background publishing thread
                    std::thread([provider, providerName]() {
                        std::string data;
                        int counter = 0;
                        while (provider->IsOnline()) {
                            std::cout << "Data to publish (or 'stop'): ";
                            std::getline(std::cin, data);
                            
                            if (data == "stop") {
                                provider->Shutdown();
                                break;
                            }
                            
                            if (!data.empty()) {
                                nlohmann::json payload = {
                                    {"timestamp", std::chrono::duration_cast<std::chrono::milliseconds>(
                                        std::chrono::system_clock::now().time_since_epoch()).count()},
                                    {"message", data},
                                    {"counter", counter++},
                                    {"source", "interactive_cpp"},
                                    {"provider", providerName}
                                };
                                
                                if (provider->PublishData(payload)) {
                                    std::cout << "Published successfully\n";
                                } else {
                                    std::cout << "Failed to publish\n";
                                }
                            }
                        }
                    }).detach();
                } else {
                    std::cout << "Failed to start provider\n";
                    delete provider;
                    provider = nullptr;
                }
                break;
            }
            
            case 's':
                if (subscriber != nullptr) {
                    std::cout << "Subscriber already running\n";
                    break;
                }
                
                subscriber = new Subscriber();
                if (subscriber->Initialize()) {
                    std::cout << "Subscriber started\n";
                    
                    subscriber->SetDataUpdateHandler([](const std::string& provider, const nlohmann::json& data) {
                        std::cout << "\n[RECEIVED] From '" << provider << "': " << data.dump() << "\n";
                        std::cout << "Command: ";
                    });
                    
                    // Auto-subscribe to all available providers
                    auto providers = subscriber->GetAvailableProviders();
                    for (const auto& info : providers) {
                        if (subscriber->SubscribeTo(info.name)) {
                            std::cout << "Subscribed to: " << info.name << "\n";
                        }
                    }
                } else {
                    std::cout << "Failed to start subscriber\n";
                    delete subscriber;
                    subscriber = nullptr;
                }
                break;
                
            case 'l':
                if (subscriber != nullptr) {
                    auto providers = subscriber->GetAvailableProviders();
                    std::cout << "Available providers (" << providers.size() << "):\n";
                    for (const auto& info : providers) {
                        std::cout << "  - " << info.name << ": " << info.description 
                                  << " (Status: " << ProviderStatusToString(info.status) << ")\n";
                    }
                } else {
                    std::cout << "Start subscriber first to list providers\n";
                }
                break;
                
            case 'q':
                goto cleanup;
                
            default:
                std::cout << "Unknown command\n";
                break;
        }
    }
    
cleanup:
    if (provider) {
        provider->Shutdown();
        delete provider;
    }
    if (subscriber) {
        subscriber->Shutdown();
        delete subscriber;
    }
}

// Helper function to test read operations
void TestReadOperation(const std::string& providerName)
{
    try
    {
        // Create temporary MMF and sync objects to read from provider
        auto mmf = std::make_unique<MemoryMappedFile>(GetProviderMmfName(providerName), DEFAULT_MMF_SIZE);
        auto sync = std::make_unique<NamedSyncObjects>(providerName);
        
        if (mmf->IsValid() && sync->IsValid())
        {
            if (sync->Lock(1000))
            {
                nlohmann::json data;
                if (mmf->ReadJson(data))
                {
                    std::string dataStr = data.dump();
                    std::string preview = dataStr.length() > 100 ? dataStr.substr(0, 97) + "..." : dataStr;
                    std::cout << "  ✓ Read test successful: " << preview << "\n";
                }
                else
                {
                    std::cout << "  ⚠ Read test: No data available\n";
                }
                sync->Unlock();
            }
            else
            {
                std::cout << "  ❌ Read test: Failed to acquire lock\n";
            }
        }
        else
        {
            std::cout << "  ❌ Read test: Failed to access MMF or sync objects\n";
        }
    }
    catch (const std::exception& ex)
    {
        std::cout << "  ❌ Read test exception: " << ex.what() << "\n";
    }
}

int main(int argc, char* argv[])
{
    if (argc > 1) {
        std::string command = argv[1];
        
        if (command == "provider" && argc > 2) {
            std::string providerName = argv[2];
            RunProviderDemo(providerName);
        } else if (command == "subscriber") {
            RunSubscriberDemo();
        } else if (command == "interactive") {
            RunInteractiveDemo();
        } else {
            ShowUsage();
        }
    } else {
        RunInteractiveDemo();
    }
    
    return 0;
}
