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

        /// <summary>
        /// Launches the PathfindingService as a separate process.
        /// Used by StateManager when the service isn't already running.
        /// </summary>
        public static void LaunchServiceFromCommandLine()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var exePath = Path.Combine(baseDir, "PathfindingService.exe");
                var dllPath = Path.Combine(baseDir, "PathfindingService.dll");

                ProcessStartInfo psi;
                if (File.Exists(exePath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = baseDir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (File.Exists(dllPath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"\"{dllPath}\"",
                        WorkingDirectory = baseDir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else
                {
                    Console.WriteLine($"PathfindingService not found at {exePath} or {dllPath}");
                    return;
                }

                Process.Start(psi);
                Console.WriteLine("PathfindingService launched as separate process.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService: {ex.Message}");
            }
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
