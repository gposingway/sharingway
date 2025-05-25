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
#include <map>
#include <sstream>
#include "../../Sharingway.native/Sharingway.h"

using namespace Sharingway;

// Enable debug logging for C++ demo
bool enableDebugLogging() {
    SharingwayUtils::DebugLogging = true;  // Enable debug logging to see what's happening
    SharingwayUtils::DebugLog("C++ demo application starting with debug logging enabled", "NativeApp");
    return true;
}
static bool debugInitialized = enableDebugLogging();

// Shared state for provider/subscriber
static std::atomic<bool> running(true);
static std::mutex consoleMutex;
static int messagesReceived = 0;
static int messagesSent = 0;
static std::map<std::string, int> messagesReceivedByProvider;
static std::string lastReceivedData = "None";
static std::string lastReceivedFrom = "";
static std::string lastReceivedTime = "";

void ShowCurrentState() {
    std::lock_guard<std::mutex> lock(consoleMutex);
    
    // Clear screen (Windows specific)
    system("cls");
    
    std::cout << "Sharingway C++ Demo - Current State\n";
    std::cout << "===================================\n\n";
    
    // Show summary statistics
    std::cout << "SUMMARY:\n";
    std::cout << "  Messages Sent: " << messagesSent << "\n";
    std::cout << "  Messages Received: " << messagesReceived << "\n";
    
    if (!messagesReceivedByProvider.empty()) {
        std::cout << "  Received by Provider:\n";
        for (const auto& pair : messagesReceivedByProvider) {
            std::cout << "    " << pair.first << ": " << pair.second << " messages\n";
        }
    }
    
    std::cout << "\n" << std::string(50, '=') << "\n\n";
      // Show last received message details
    if (messagesReceived > 0) {
        std::cout << "LAST RECEIVED MESSAGE:\n";
        std::cout << "  From: " << lastReceivedFrom << "\n";
        std::cout << "  Time: " << lastReceivedTime << "\n";
        std::cout << "  Payload:\n\n";
        
        // Show the JSON data with proper indentation (add 4 spaces to each line)
        std::stringstream ss(lastReceivedData);
        std::string line;
        while (std::getline(ss, line)) {
            std::cout << "    " << line << "\n";
        }
    } else {
        std::cout << "NO MESSAGES RECEIVED YET\n";
        std::cout << "Waiting for data from other providers...\n";
    }
    
    std::cout << "\n" << std::string(50, '=') << "\n";
    std::cout << "Press 'q' to quit\n";
}

// Combined demo that runs both a provider and subscriber at the same time
void RunDemo(const std::string& providerName) {
    std::cout << "Sharingway C++ Demo Application\n";
    std::cout << "===============================\n";
    std::cout << "Provider: " << providerName << "\n";
    std::cout << "This application runs as both provider and subscriber simultaneously\n";
    std::cout << "Press 'q' to quit\n";
    std::cout << "Initializing...\n\n";
    
    // Initialize the provider
    std::cout << "Initializing provider...\n";
    std::vector<std::string> capabilities = {"native_sensor", "cpp_data"};
    Provider provider(providerName, "C++ dual-mode provider/subscriber", capabilities);
      if (!provider.Initialize()) {
        std::cout << "Failed to initialize provider! Exiting.\n";
        return;
    }
    std::cout << "Provider '" << providerName << "' initialized successfully\n";
    
    // Initialize the subscriber
    std::cout << "Initializing subscriber...\n";
    Subscriber subscriber;
    
    if (!subscriber.Initialize()) {
        std::cout << "Failed to initialize subscriber! Provider will still run.\n";
    } else {
        std::cout << "Subscriber initialized successfully.\n";
          // Set up callbacks
        subscriber.SetDataUpdateHandler([](const std::string& provider, const nlohmann::json& data) {
            messagesReceived++;
            messagesReceivedByProvider[provider]++;
            lastReceivedData = data.dump(2); // Pretty print with 2-space indent
            lastReceivedFrom = provider;
            
            // Get current time
            auto now = std::chrono::system_clock::now();
            auto time_t = std::chrono::system_clock::to_time_t(now);
            auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
            
            std::ostringstream oss;
            struct tm timeinfo;
            localtime_s(&timeinfo, &time_t);
            oss << std::put_time(&timeinfo, "%H:%M:%S");
            oss << '.' << std::setfill('0') << std::setw(3) << ms.count();
            lastReceivedTime = oss.str();
            
            // Clear screen and show current state
            ShowCurrentState();
        });        subscriber.SetProviderChangeHandler([&subscriber, &providerName](const std::string& provider, ProviderStatus status) {
            std::lock_guard<std::mutex> lock(consoleMutex);
            std::cout << "[DEBUG] Provider change event: " << provider << ", status: " << static_cast<int>(status) << std::endl;
            
            if (status == ProviderStatus::Online && provider != providerName) {
                // New provider came online - subscribe to it (but not to ourselves)
                std::cout << "New provider detected: " << provider << ", subscribing...\n";
                subscriber.SubscribeTo(provider);
            }
        });
          // Get and subscribe to available providers
        auto providers = subscriber.GetAvailableProviders();
        if (!providers.empty()) {
            std::cout << "Found " << providers.size() << " providers, subscribing to all...\n";
            for (const auto& info : providers) {
                if (info.name != providerName) {  // Don't subscribe to self
                    subscriber.SubscribeTo(info.name);
                }
            }        } else {
            std::cout << "No providers found yet, will auto-subscribe to new ones...\n";
        }
    }
    
    // Show initial state
    std::this_thread::sleep_for(std::chrono::milliseconds(500)); // Brief pause
    ShowCurrentState();
    
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
                };                if (provider.PublishData(sensorData)) {
                    messagesSent++;
                    // Only show "Message Sent" if we haven't received any messages yet
                    // Once we start receiving messages, the display is controlled by ShowCurrentState()
                    if (messagesReceived == 0) {
                        std::lock_guard<std::mutex> lock(consoleMutex);
                        std::cout << "Message Sent #" << messagesSent << "\n";
                    }
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
