using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sharingway.Net;

namespace SharingwayDemo
{
    [SupportedOSPlatform("windows")]
    class Program
    {        static async Task Main(string[] args)
        {
            // Just delegate to our Demo class
            await Demo.RunDemo(args);
        }
    }
}
