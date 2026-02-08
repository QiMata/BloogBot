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
                Console.WriteLine("ERROR: Program.Main() is running in WoW process! Use StartInjected() instead.");
                File.AppendAllText(logPath, "ERROR: Program.Main() running in WoW process - use StartInjected() entry point!\n");
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

    /// <summary>
    /// Entry point for injected execution inside WoW.exe.
    /// Called by Loader.Load() instead of Main() when running injected.
    /// </summary>
    public static void StartInjected()
    {
        string logPath = "";
        try
        {
            // Get log path early - use WoW directory via AppDomain
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
            var logDir = Path.Combine(baseDir, "WWoWLogs");
            Directory.CreateDirectory(logDir);
            logPath = Path.Combine(logDir, "startinjected.log");

            File.AppendAllText(logPath, $"\n=== StartInjected() at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            File.AppendAllText(logPath, $"BaseDir: {baseDir}\n");

            Console.WriteLine("=== ForegroundBotRunner StartInjected() - Running inside WoW ===");

            // Skip DisplayProcessInfo - it can crash with Access Denied
            File.AppendAllText(logPath, "STEP 1: Skipping DisplayProcessInfo\n");

            // Build and run the host - this will block until shutdown
            File.AppendAllText(logPath, "STEP 2: About to call CreateHostBuilder().Build().Run()\n");
            CreateHostBuilder([]).Build().Run();
            File.AppendAllText(logPath, "STEP 3: Host exited normally\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in StartInjected(): {ex}");
            try { if (!string.IsNullOrEmpty(logPath)) File.AppendAllText(logPath, $"EXCEPTION: {ex}\n"); } catch { }
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                var configDict = new Dictionary<string, string?>
                {
                    ["PathfindingService:IpAddress"] = "127.0.0.1",
                    ["PathfindingService:Port"] = "5001",
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
