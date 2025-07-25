namespace WoWActivityMember // Changed from ForegroundBotRunner
{
    public class Loader // Made public
    {
        private static Thread thread;   

        public static int Load(string args) // Made public and static
        {
            // Add immediate console output for debugging
            try
            {
                var logPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0", "injection.log");
                File.AppendAllText(logPath, $"\n=== WoWActivityMember.Loader.Load() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                File.AppendAllText(logPath, $"Arguments: {args}\n");
                File.AppendAllText(logPath, $"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\n");
                File.AppendAllText(logPath, $"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}\n");
                
                Console.WriteLine("=== WoWActivityMember.Loader.Load() called ===");
                Console.WriteLine($"Arguments: {args}");
                Console.WriteLine($"Current Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}");
                Console.WriteLine($"Current Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                
                thread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("Starting Program.Main in new thread...");
                        File.AppendAllText(logPath, "Starting Program.Main in new thread...\n");
                        ForegroundBotRunner.Program.Main(args.Split(" "));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in Program.Main: {ex}");
                        File.AppendAllText(logPath, $"Error in Program.Main: {ex}\n");
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
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
    }
}
