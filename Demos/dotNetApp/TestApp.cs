using System;
using System.Runtime.Versioning;
using Sharingway.Net;

[SupportedOSPlatform("windows")]
class TestProgram
{
    public static void MainTest(string[] args)
    {
        Console.WriteLine("Sharingway Test Application");
        Console.WriteLine("===========================");
        
        Console.WriteLine("Testing registry initialization...");
        if (SharingwayUtils.EnsureRegistryInitialized())
        {
            Console.WriteLine("✓ Registry initialization: SUCCESS");
        }
        else
        {
            Console.WriteLine("✗ Registry initialization: FAILED");
        }
        
        Console.WriteLine("\nTesting MMF creation...");
        try
        {
            using var mmf = new MemoryMappedFileHelper("Global\\Test.MMF", 1024);
            if (mmf.IsValid)
            {
                Console.WriteLine("✓ Global MMF creation: SUCCESS");
            }
            else
            {
                Console.WriteLine("✗ Global MMF creation: FAILED");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Global MMF creation: EXCEPTION - {ex.Message}");
        }
        
        Console.WriteLine("\nTesting local MMF creation...");
        try
        {
            using var mmf = new MemoryMappedFileHelper("Test.MMF.Local", 1024);
            if (mmf.IsValid)
            {
                Console.WriteLine("✓ Local MMF creation: SUCCESS");
            }
            else
            {
                Console.WriteLine("✗ Local MMF creation: FAILED");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Local MMF creation: EXCEPTION - {ex.Message}");
        }
        
        Console.WriteLine("\nTesting NamedSyncObjects...");
        try
        {
            using var sync = new NamedSyncObjects("TestProvider");
            if (sync.IsValid)
            {
                Console.WriteLine("✓ NamedSyncObjects creation: SUCCESS");
            }
            else
            {
                Console.WriteLine("✗ NamedSyncObjects creation: FAILED");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ NamedSyncObjects creation: EXCEPTION - {ex.Message}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
