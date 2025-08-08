using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // This Main method should only run when WoWActivityMember.exe is executed directly
                // When called through CLR injection, only the Loader.Load() method should be called
                
                Console.WriteLine("=== WoWActivityMember.exe Main() called directly ===");
                Console.WriteLine("This should NOT run during CLR injection!");
                Console.WriteLine("If you see this during injection, there's a problem with the CLR execution.");
                
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"\n=== DIRECT EXECUTION - Program.Main() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                File.AppendAllText(logPath, "This should NOT happen during CLR injection!\n");
                
                // Check if this is being run in the context of WoW (which would be wrong)
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var isWoWProcess = currentProcess.ProcessName.ToLower().Contains("wow");
                
                if (isWoWProcess)
                {
                    Console.WriteLine("ERROR: Program.Main() is running in WoW process!");
                    Console.WriteLine("This means the CLR injection is calling the wrong entry point!");
                    File.AppendAllText(logPath, "ERROR: Program.Main() running in WoW process - CLR calling wrong entry point!\n");
                    
                    // Exit immediately to prevent WoW crash
                    Console.WriteLine("Exiting immediately to prevent WoW crash...");
                    return;
                }
                
                // If not in WoW, this is normal direct execution
                Console.WriteLine("Running ForegroundBotRunner in standalone mode...");
                DisplayProcessInfo();

                // Use the Worker Service pattern for standalone execution
                CreateHostBuilder(args)
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in ForegroundBotRunner Program.Main(): {ex}");
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"FATAL ERROR in Program.Main(): {ex}\n");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    // Simple configuration using hard-coded values for injection context
                    var configDict = new Dictionary<string, string?>
                    {
                        ["PathfindingService:IpAddress"] = "127.0.0.1",
                        ["PathfindingService:Port"] = "5000",
                        ["CharacterStateListener:IpAddress"] = "127.0.0.1",
                        ["CharacterStateListener:Port"] = "5002"
                    };

                    builder.AddInMemoryCollection(configDict);
                    builder.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Register the ForegroundBotWorker as the main hosted service
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
}
