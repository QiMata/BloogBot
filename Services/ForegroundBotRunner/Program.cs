#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== ForegroundBotRunner.exe Main() called directly ===");

            var logPath = Path.Combine(AppContext.BaseDirectory, "BloogBotLogs", "injection.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"\n=== DIRECT EXECUTION - Program.Main() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            if (currentProcess.ProcessName.Contains("wow", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("ERROR: Program.Main() is running in WoW process! Wrong entry point used.");
                File.AppendAllText(logPath, "ERROR: Program.Main() running in WoW process - CLR calling wrong entry point!\n");
                return;
            }

            Console.WriteLine("Running ForegroundBotRunner in standalone mode...");
            DisplayProcessInfo();

            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in ForegroundBotRunner Program.Main(): {ex}");
            var logPath = Path.Combine(AppContext.BaseDirectory, "BloogBotLogs", "injection.log");
            File.AppendAllText(logPath, $"FATAL ERROR in Program.Main(): {ex}\n");
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                var configDict = new Dictionary<string, string?>
                {
                    ["PathfindingService:IpAddress"] = "127.0.0.1",
                    ["PathfindingService:Port"] = "5000",
                    ["CharacterStateListener:IpAddress"] = "127.0.0.1",
                    ["CharacterStateListener:Port"] = "5002",
                    ["LoginServer:IpAddress"] = "127.0.0.1"
                };
                builder.AddInMemoryCollection(configDict);
                builder.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<ForegroundBotWorker>();
            })
            .ConfigureLogging((context, builder) =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

    private static void DisplayProcessInfo()
    {
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            Console.WriteLine("=== PROCESS INFORMATION ===");
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Process Name: {currentProcess.ProcessName}");
            Console.WriteLine($"Process ID: {currentProcess.Id}");
            Console.WriteLine($"Main Module: {currentProcess.MainModule?.FileName ?? "N/A"}");
            Console.WriteLine($"Working Set (MB): {currentProcess.WorkingSet64 / 1024 / 1024:N2}");
            Console.WriteLine($"Thread Count: {currentProcess.Threads.Count}");
            Console.WriteLine("==============================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error displaying process info: {ex.Message}");
        }
    }
}
#endif
