namespace ForegroundBotRunner
{
    public class Loader
    {
        private static Thread? thread;
        private static bool isInitialized = false;

        static Loader()
        {
            try
            {
                AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                {
                    try
                    {
                        var p = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection_firstchance.log");
                        File.AppendAllText(p, $"[{DateTime.Now:HH:mm:ss}] FirstChance: {e.Exception.GetType()}: {e.Exception.Message}\n");
                    }
                    catch { }
                };
            }
            catch { }
        }

        // Correct signature: two parameters (args pointer, size)
        [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "LoadUnmanaged")]
        public static int LoadUnmanaged(System.IntPtr argsPtr, int size)
        {
            try
            {
                string args = "NONE";
                if (argsPtr != System.IntPtr.Zero)
                {
                    try { args = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(argsPtr) ?? "NONE"; } catch { }
                }
            }
            catch { }
            return Load("NONE");
        }

        public static int Load(string args)
        {
            try
            {
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"\n=== CORRECT ENTRY POINT - Loader.Load() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                File.AppendAllText(logPath, $"Arguments: {args}\n");
                File.AppendAllText(logPath, $"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\n");
                File.AppendAllText(logPath, $"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}\n");

                Console.WriteLine("=== CORRECT ENTRY POINT - Loader.Load() called ===");
                Console.WriteLine($"Arguments: {args}");
                Console.WriteLine($"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}");
                Console.WriteLine($"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}");

                if (isInitialized)
                {
                    Console.WriteLine("Loader already initialized, skipping...");
                    File.AppendAllText(logPath, "Loader already initialized, skipping...\n");
                    return 1;
                }
                isInitialized = true;

                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var isWoWProcess = currentProcess.ProcessName.Contains("wow", StringComparison.OrdinalIgnoreCase) ||
                                   currentProcess.MainModule?.FileName?.Contains("wow", StringComparison.OrdinalIgnoreCase) == true;

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

                thread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("Starting ultra-simplified bot...");
                        File.AppendAllText(logPath, "Starting ultra-simplified bot...\n");
                        InitializeUltraSimplifiedBot();
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
                catch { }
                return 0;
            }
        }

        private static void InitializeUltraSimplifiedBot()
        {
            try
            {
                Console.WriteLine("=== ULTRA-SIMPLIFIED BOT STARTING ===");
                var process = System.Diagnostics.Process.GetCurrentProcess();
                Console.WriteLine($"Bot running in process: {process.ProcessName} (PID: {process.Id})");
                int counter = 0;
                while (true)
                {
                    Thread.Sleep(10000);
                    counter++;
                    if (counter % 6 == 0)
                    {
                        Console.WriteLine($"[Ultra-Simple Bot] Heartbeat #{counter} - Running for {counter * 10} seconds");
                        try
                        {
                            var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Heartbeat #{counter} - Bot still alive\n");
                        }
                        catch { }
                    }
                    if (counter >= 30)
                    {
                        Console.WriteLine($"[Ultra-Simple Bot] Successfully running for {counter * 10} seconds without crashing WoW!");
                        counter = 0;
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
                catch { }
            }
        }
    }
}