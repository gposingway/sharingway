#include <iostream>
#include <string>
#include <vector>
#include <thread>
#include <chrono>
#include <conio.h>
#include <atomic>
#include <mutex>
#include <iomanip>
#include <random>
#include "../../Sharingway.native/Sharingway.h"

using namespace Sharingway;

// Enable debug logging for C++ demo
bool enableDebugLogging() {
    SharingwayUtils::DebugLogging = true;
    SharingwayUtils::DebugLog("C++ demo application starting with debug logging enabled", "NativeApp");
    return true;
}
static bool debugInitialized = enableDebugLogging();

// Shared state for provider/subscriber
static std::atomic<bool> running(true);
static std::mutex consoleMutex;
static int messagesReceived = 0;
static int messagesSent = 0;
static std::string lastReceivedData = "None";

void ShowHeader() {
    std::cout << "Sharingway C++ Demo Application (Provider + Subscriber)\n";
    std::cout << "====================================================\n";
}

void PrintStats() {
    std::lock_guard<std::mutex> lock(consoleMutex);
    std::cout << "\n--- STATS ---\n";
    std::cout << "Messages sent: " << messagesSent << "\n";
    std::cout << "Messages received: " << messagesReceived << "\n";
    std::cout << "Last received: " << (lastReceivedData.length() > 50 ? lastReceivedData.substr(0, 47) + "..." : lastReceivedData) << "\n";
    std::cout << "-------------\n";
}

// Combined demo that runs both a provider and subscriber at the same time
void RunDemo(const std::string& providerName) {
    ShowHeader();
    std::cout << "Starting as both provider and subscriber. Provider name: " << providerName << "\n";
    std::cout << "Press 'q' to quit, 's' to show stats\n\n";
    
    // Initialize the provider
    std::cout << "Initializing provider...\n";
    std::vector<std::string> capabilities = {"native_sensor", "cpp_data"};
    Provider provider(providerName, "C++ dual-mode provider/subscriber", capabilities);
    
    if (!provider.Initialize()) {
        std::cout << "Failed to initialize provider! Exiting.\n";
        return;
    }
    std::cout << "Provider initialized successfully.\n";
    
    // Initialize the subscriber
    std::cout << "Initializing subscriber...\n";
    Subscriber subscriber;
    
    if (!subscriber.Initialize()) {
        std::cout << "Failed to initialize subscriber! Provider will still run.\n";
    } else {
        std::cout << "Subscriber initialized successfully.\n";
        
        // Set up callbacks
        subscriber.SetDataUpdateHandler([](const std::string& provider, const nlohmann::json& data) {
            std::lock_guard<std::mutex> lock(consoleMutex);
            messagesReceived++;
            lastReceivedData = data.dump();
            std::cout << "[RECEIVED] From: '" << provider << "', Data: " 
                     << (lastReceivedData.length() > 80 ? lastReceivedData.substr(0, 77) + "..." : lastReceivedData) << "\n";
        });
        
        subscriber.SetProviderChangeHandler([](const std::string& provider, ProviderStatus status) {
            std::lock_guard<std::mutex> lock(consoleMutex);
            std::cout << "[STATUS] Provider: '" << provider << "' is now " 
                     << ProviderStatusToString(status) << "\n";
        });
        
        // Get and subscribe to available providers
        auto providers = subscriber.GetAvailableProviders();
        std::cout << "Found " << providers.size() << " providers\n";
        for (const auto& info : providers) {
            std::cout << "  - " << info.name << ": " << info.description << "\n";
            if (info.name != providerName) {  // Don't subscribe to self
                if (subscriber.SubscribeTo(info.name)) {
                    std::cout << "    Subscribed to: " << info.name << "\n";
                }
            }
        }
    }
    
    // Background thread for publishing data
    std::thread publishThread([&provider, providerName]() {
        int counter = 0;
        while (running) {
            try {
                // Generate sample data
                nlohmann::json sensorData = {
                    {"timestamp", std::chrono::duration_cast<std::chrono::milliseconds>(
                        std::chrono::system_clock::now().time_since_epoch()).count()},
                    {"counter", counter++},
                    {"cpu_usage", 45.5 + (rand() % 200) / 10.0},  // 45.5-65.5%
                    {"memory_usage", 60.0 + (rand() % 300) / 10.0},  // 60.0-90.0%
                    {"source", "cpp_app"},
                    {"provider", providerName}
                };
                
                if (provider.PublishData(sensorData)) {
                    std::lock_guard<std::mutex> lock(consoleMutex);
                    messagesSent++;
                    std::cout << "[PUBLISHED] Counter=" << counter - 1 
                             << ", CPU=" << sensorData["cpu_usage"] << "%, "
                             << "Memory=" << sensorData["memory_usage"] << "%\n";
                }
            }
            catch (const std::exception& ex) {
                std::lock_guard<std::mutex> lock(consoleMutex);
                std::cout << "[ERROR] Exception while publishing: " << ex.what() << "\n";
            }
            
            // Sleep for 2 seconds between publishes
            std::this_thread::sleep_for(std::chrono::seconds(2));
        }
    });
    
    // Main loop for handling keyboard input
    while (running) {
        if (_kbhit()) {
            char key = _getch();
            if (key == 'q' || key == 'Q') {
                running = false;
                break;
            } else if (key == 's' || key == 'S') {
                PrintStats();
            }
        }
        
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
    
    std::cout << "Shutting down...\n";
    running = false;
    if (publishThread.joinable()) {
        publishThread.join();
    }
}

int main(int argc, char* argv[])
{
    // Get provider name from command line if provided, or use default
    std::string providerName = "CppProvider";
    if (argc > 1) {
        providerName = argv[1];
    }
    
    // Set a random seed based on current time
    srand(static_cast<unsigned int>(time(nullptr)));
    
    // Run the demo
    RunDemo(providerName);
    
    return 0;
}
