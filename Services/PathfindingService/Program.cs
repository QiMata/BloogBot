using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

using System.IO;

namespace PathfindingService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // PathfindingService runs as x64 from Bot\Debug\x64\.
            // Map data (mmaps/vmaps/maps) lives in Bot\Debug\net8.0\ alongside the x86 bot.
            // Set WWOW_DATA_DIR so the native Navigation.dll finds the map files.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WWOW_DATA_DIR")))
            {
                var dataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "net8.0"));
                if (Directory.Exists(Path.Combine(dataDir, "mmaps")))
                {
                    Environment.SetEnvironmentVariable("WWOW_DATA_DIR", dataDir + Path.DirectorySeparatorChar);
                    Console.WriteLine($"[PathfindingService] WWOW_DATA_DIR set to: {dataDir}");
                }
            }

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.PathfindingService.json", optional: false, reloadOnChange: true);
                    builder.AddJsonFile($"appsettings.PathfindingService.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    builder.AddEnvironmentVariables();
                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    
                    // Register PathfindingSocketServer as a singleton
                    services.AddSingleton<PathfindingSocketServer>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<PathfindingSocketServer>>();
                        
                        var ipAddress = configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
                        var port = int.Parse(configuration["PathfindingService:Port"] ?? "5000");
                        
                        return new PathfindingSocketServer(ipAddress, port, logger);
                    });
                    
                    // Register the hosted service
                    services.AddHostedService<PathfindingServiceWorker>();
                });
    }
}
