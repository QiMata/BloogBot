using PathfindingService.Repository;
using System.Diagnostics;

namespace PathfindingService
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
                    
                    // Register Navigation as a singleton since it loads the native DLL
                    services.AddSingleton<Navigation>();
                    
                    // Register PathfindingSocketServer as a singleton
                    services.AddSingleton<PathfindingSocketServer>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<PathfindingSocketServer>>();
                        var navigation = serviceProvider.GetRequiredService<Navigation>();
                        
                        var ipAddress = configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
                        var port = int.Parse(configuration["PathfindingService:Port"] ?? "5000");
                        
                        return new PathfindingSocketServer(ipAddress, port, logger, navigation);
                    });
                    
                    // Register the hosted service
                    services.AddHostedService<PathfindingServiceWorker>();
                });
    }
}
