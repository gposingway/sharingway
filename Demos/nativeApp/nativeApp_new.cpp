#include <iostream>
#include <string>
#include <vector>
#include <thread>
#include <chrono>
#include <conio.h>
#include <atomic>
#include <mutex>
#include <random>
#include "../../Sharingway.native/Sharingway.h"

using namespace Sharingway;

// Global variables
std::atomic<bool> g_running = true;
std::mutex g_consoleMutex;
std::random_device g_rd;
std::mt19937 g_gen(g_rd());
std::uniform_real_distribution<> g_cpuDist(45.5, 65.5);
std::uniform_real_distribution<> g_memDist(60.0, 90.0);
std::uniform_int_distribution<> g_diskIODist(0, 1000);

// Enable debug logging for C++ demo
bool enableDebugLogging() {
    SharingwayUtils::DebugLogging = true;
    SharingwayUtils::DebugLog("C++ demo application starting with debug logging enabled", "NativeDemoApp");
    return true;
}
static bool debugInitialized = enableDebugLogging();

// Function to display received data with timestamp
void displayReceivedData(const std::string& provider, const nlohmann::json& data) {
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    auto now = std::chrono::system_clock::now();
    auto timeNow = std::chrono::system_clock::to_time_t(now);
    
    char timeBuffer[9];
    std::strftime(timeBuffer, sizeof(timeBuffer), "%H:%M:%S", std::localtime(&timeNow));

    std::cout << "\n[" << timeBuffer << "] Received from '" << provider << "':\n";
    std::cout << "  " << data.dump(2).substr(0, 200);
    if (data.dump().length() > 200) std::cout << "...";
    std::cout << std::endl;
}

// Function to display provider status changes
void displayProviderStatus(const std::string& provider, ProviderStatus status) {
    std::lock_guard<std::mutex> lock(g_consoleMutex);
    auto now = std::chrono::system_clock::now();
    auto timeNow = std::chrono::system_clock::to_time_t(now);
    
    char timeBuffer[9];
    std::strftime(timeBuffer, sizeof(timeBuffer), "%H:%M:%S", std::localtime(&timeNow));

    std::cout << "\n[" << timeBuffer << "] Provider '" << provider << "' status: " 
              << ProviderStatusToString(status) << "\n";
}

// Provider thread function - publishes sensor data periodically
void providerThread(const std::string& providerName) {
    try {
        std::cout << "Starting provider: " << providerName << "..." << std::endl;
        
        std::vector<std::string> capabilities = {"native_sensor", "cpp_data"};
        Provider provider(providerName, "C++ native sensor provider", capabilities);
        
        if (!provider.Initialize()) {
            std::cout << "Failed to initialize provider: " << providerName << std::endl;
            return;
        }
        
        std::cout << "Provider '" << providerName << "' successfully initialized." << std::endl;
        
        // Main publishing loop
        int counter = 0;
        while (g_running) {
            // Generate sample sensor data
            nlohmann::json sensorData = {
                {"timestamp", std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count()},
                {"counter", counter++},
                {"cpu_usage", g_cpuDist(g_gen)},
                {"memory_usage", g_memDist(g_gen)},
                {"disk_io", g_diskIODist(g_gen)},
                {"source", "native_cpp"},
                {"provider_id", providerName}
            };
            
            // Publish data
            {
                std::lock_guard<std::mutex> lock(g_consoleMutex);
                std::cout << "[" << providerName << "] Publishing data... ";
                
                if (provider.PublishData(sensorData)) {
                    std::cout << "Success (Counter=" << counter-1 
                        << ", CPU=" << sensorData["cpu_usage"] << "%)" << std::endl;
                } else {
                    std::cout << "Failed!" << std::endl;
                }
            }
            
            // Sleep between publishes
            std::this_thread::sleep_for(std::chrono::milliseconds(3000));
        }
        
        std::cout << "Shutting down provider: " << providerName << std::endl;
        provider.Shutdown();
    }
    catch (const std::exception& ex) {
        std::lock_guard<std::mutex> lock(g_consoleMutex);
        std::cout << "Exception in provider thread: " << ex.what() << std::endl;
    }
}

// Subscriber thread function - listens for data from all providers
void subscriberThread() {
    try {
        std::cout << "Starting subscriber..." << std::endl;
        
        Subscriber subscriber;
        
        if (!subscriber.Initialize()) {
            std::cout << "Failed to initialize subscriber!" << std::endl;
            return;
        }
        
        // Set up callbacks
        subscriber.SetDataUpdateHandler(displayReceivedData);
        subscriber.SetProviderChangeHandler(displayProviderStatus);
        
        std::cout << "Subscriber successfully initialized." << std::endl;
        std::cout << "Checking for available providers..." << std::endl;
        
        // Regular provider discovery loop
        while (g_running) {
            // Get available providers and subscribe to any new ones
            auto providers = subscriber.GetAvailableProviders();
            
            {
                std::lock_guard<std::mutex> lock(g_consoleMutex);
                std::cout << "Found " << providers.size() << " providers. Subscribing to all..." << std::endl;
            }
            
            for (const auto& info : providers) {
                if (subscriber.SubscribeTo(info.name)) {
                    std::lock_guard<std::mutex> lock(g_consoleMutex);
                    std::cout << "Subscribed to: " << info.name << std::endl;
                }
            }
            
            // Check providers every 5 seconds
            std::this_thread::sleep_for(std::chrono::seconds(5));
        }
        
        std::cout << "Shutting down subscriber..." << std::endl;
        subscriber.Shutdown();
    }
    catch (const std::exception& ex) {
        std::lock_guard<std::mutex> lock(g_consoleMutex);
        std::cout << "Exception in subscriber thread: " << ex.what() << std::endl;
    }
}

int main(int argc, char* argv[])
{
    std::string providerName = "CppProvider";
    
    // Allow custom provider name from command line
    if (argc > 1) {
        providerName = argv[1];
    }
    
    std::cout << "Sharingway C++ Demo Application" << std::endl;
    std::cout << "=============================" << std::endl;
    std::cout << "This application runs as both provider and subscriber simultaneously." << std::endl;
    std::cout << "Provider name: " << providerName << std::endl;
    std::cout << "Press 'q' to quit" << std::endl << std::endl;
    
    // Start provider and subscriber threads
    std::thread provider(providerThread, providerName);
    std::thread subscriber(subscriberThread);
    
    // Wait for quit command
    while (true) {
        if (_kbhit()) {
            char key = _getch();
            if (key == 'q' || key == 'Q') {
                g_running = false;
                break;
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
    
    std::cout << "\nShutting down application..." << std::endl;
    
    // Wait for threads to finish
    provider.join();
    subscriber.join();
    
    std::cout << "Application terminated successfully." << std::endl;
    return 0;
}
#include <atomic>
#include <mutex>
#include <iomanip>
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
void RunCombinedDemo(const std::string& providerName) {
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
    
    // Clean up
    provider.Shutdown();
    subscriber.Shutdown();
    
    // Show final stats
    PrintStats();
}

int main(int argc, char* argv[]) {
    std::string providerName = "CppApp";
    
    if (argc > 1) {
        providerName = argv[1];
    }
    
    RunCombinedDemo(providerName);
    
    return 0;
}
