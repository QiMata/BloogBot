namespace WoWActivityMember
{
    public class Loader
    {
        private static Thread? thread;
        private static bool isInitialized = false;

        public static int Load(string args)
        {
            try
            {
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"\n=== CORRECT ENTRY POINT - Loader.Load() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                File.AppendAllText(logPath, $"Arguments: {args}\n");
                File.AppendAllText(logPath, $"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\n");
                File.AppendAllText(logPath, $"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}\n");

                Console.WriteLine("=== CORRECT ENTRY POINT - WoWActivityMember.Loader.Load() called ===");
                Console.WriteLine($"Arguments: {args}");
                Console.WriteLine($"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}");
                Console.WriteLine($"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}");

                // Prevent multiple initializations
                if (isInitialized)
                {
                    Console.WriteLine("Loader already initialized, skipping...");
                    File.AppendAllText(logPath, "Loader already initialized, skipping...\n");
                    return 1;
                }

                isInitialized = true;

                // Verify we're in the WoW process
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var isWoWProcess = currentProcess.ProcessName.ToLower().Contains("wow") || 
                                 currentProcess.MainModule?.FileName?.ToLower().Contains("wow") == true;

                if (isWoWProcess)
                {
                    Console.WriteLine($"*** INJECTION SUCCESS: Running in WoW Process (PID: {currentProcess.Id}) ***");
                    File.AppendAllText(logPath, $"SUCCESS: Running in WoW Process (PID: {currentProcess.Id})\n");
                }
                else
                {
                    Console.WriteLine($"*** WARNING: Not running in WoW Process (PID: {currentProcess.Id}, Name: {currentProcess.ProcessName}) ***");
                    File.AppendAllText(logPath, $"WARNING: Not in WoW Process (PID: {currentProcess.Id}, Name: {currentProcess.ProcessName})\n");
                }

                // Start the simplified bot in a background thread
                thread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("Starting ultra-simplified bot...");
                        File.AppendAllText(logPath, "Starting ultra-simplified bot...\n");
                        
                        InitializeUltraSimplifiedBot();
                        
                        Console.WriteLine("Ultra-simplified bot completed successfully!");
                        File.AppendAllText(logPath, "Ultra-simplified bot completed successfully!\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ultra-simplified bot: {ex.Message}");
                        File.AppendAllText(logPath, $"Error in ultra-simplified bot: {ex}\n");
                    }
                })
                {
                    IsBackground = true,
                    Name = "UltraSimplifiedBot"
                };

                thread.Start();

                Console.WriteLine("=== Loader.Load() completed successfully ===");
                File.AppendAllText(logPath, "=== Loader.Load() completed successfully ===\n");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Loader.Load(): {ex}");
                try 
                {
                    var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                    File.AppendAllText(logPath, $"Error in Loader.Load(): {ex}\n");
                }
                catch 
                {
                    // Ignore file write errors
                }
                return 0;
            }
        }

        private static void InitializeUltraSimplifiedBot()
        {
            try
            {
                Console.WriteLine("=== ULTRA-SIMPLIFIED BOT STARTING ===");
                
                // Absolute minimal initialization - no external dependencies
                var process = System.Diagnostics.Process.GetCurrentProcess();
                Console.WriteLine($"Bot running in process: {process.ProcessName} (PID: {process.Id})");
                
                // Ultra-simple heartbeat loop with minimal resource usage
                int counter = 0;
                while (true)
                {
                    Thread.Sleep(10000); // 10 second intervals to be very gentle
                    
                    counter++;
                    if (counter % 6 == 0) // Every minute (6 * 10 seconds)
                    {
                        Console.WriteLine($"[Ultra-Simple Bot] Heartbeat #{counter} - Running for {counter * 10} seconds");
                        
                        // Log to file occasionally
                        try
                        {
                            var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Heartbeat #{counter} - Bot still alive\n");
                        }
                        catch { /* Ignore file errors */ }
                    }
                    
                    // Safety check - if we've been running for more than 5 minutes, log it
                    if (counter >= 30) // 30 * 10 seconds = 5 minutes
                    {
                        Console.WriteLine($"[Ultra-Simple Bot] Successfully running for {counter * 10} seconds without crashing WoW!");
                        counter = 0; // Reset counter to avoid overflow
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ultra-simplified bot: {ex}");
                
                try
                {
                    var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                    File.AppendAllText(logPath, $"Error in ultra-simplified bot: {ex}\n");
                }
                catch { /* Ignore file errors */ }
            }
        }
    }
}