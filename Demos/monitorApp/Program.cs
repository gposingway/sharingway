using System;
using System.Threading.Tasks;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
class Program
{
    static async Task Main(string[] args)
    {
        await Monitor.RunMonitor(args);
    }
}
