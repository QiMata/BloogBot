using System;
using System.IO;
using System.Threading;

namespace ForegroundBotRunner
{
    public class Loader
    {
        private static Thread? thread;
        private static bool isInitialized = false;
        private static int firstChanceReentrancy = 0;
        private static int firstChanceLogged = 0;
        private const int FirstChanceLogLimit = 50;
        private static readonly string LogDirectory = InitLogDirectory();
        private static string InjectionLog => Path.Combine(LogDirectory, "injection.log");
        private static string FirstChanceLog => Path.Combine(LogDirectory, "injection_firstchance.log");

        private static string InitLogDirectory()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("BLOOGBOT_INJECT_LOG_DIR");
                string baseDir = !string.IsNullOrWhiteSpace(env) ? env : (AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory);
                var dir = Path.Combine(baseDir, "BloogBotLogs");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch { return Environment.CurrentDirectory; }
        }

        static Loader()
        {
            try
            {
                AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                {
                    if (firstChanceLogged >= FirstChanceLogLimit) return;
                    if (Interlocked.Exchange(ref firstChanceReentrancy, 1) == 1) return;
                    try
                    {
                        firstChanceLogged++;
                        File.AppendAllText(FirstChanceLog, $"[{DateTime.Now:HH:mm:ss}] FirstChance({firstChanceLogged}): {e.Exception.GetType()}: {e.Exception.Message}\n");
                    }
                    catch { }
                    finally { Interlocked.Exchange(ref firstChanceReentrancy, 0); }
                };
            }
            catch { }
        }

#if NET8_0_OR_GREATER
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
#else
        public static int LoadUnmanaged(System.IntPtr argsPtr, int size) => Load("NONE");
#endif

        public static int Load(string args)
        {
            try
            {
                File.AppendAllText(InjectionLog, $"\n=== Loader.Load() at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nArguments: {args}\nProcess: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\nThread ID: {Thread.CurrentThread.ManagedThreadId}\n");
                Console.WriteLine($"Loader.Load invoked. LogDir={LogDirectory}");

                if (isInitialized)
                {
                    File.AppendAllText(InjectionLog, "Loader already initialized, skipping.\n");
                    return 1;
                }
                isInitialized = true;

#if NETFRAMEWORK && !NET8_0_OR_GREATER
                // Start the injected bot host (net48 minimal loop)
                InjectedBotHost.Start();
                File.AppendAllText(InjectionLog, "InjectedBotHost.Start() invoked.\n");
#endif

                // Keep a small heartbeat thread for visibility
                thread = new Thread(() => HeartbeatLoop()) { IsBackground = true, Name = "ForegroundHeartbeat" };
                thread.Start();

                File.AppendAllText(InjectionLog, "Loader.Load completed successfully.\n");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Loader.Load(): {ex}");
                try { File.AppendAllText(InjectionLog, $"Error in Loader.Load(): {ex}\n"); } catch { }
                return 0;
            }
        }

        private static void HeartbeatLoop()
        {
            int counter = 0;
            while (true)
            {
                Thread.Sleep(10000);
                counter++;
                if (counter % 6 == 0)
                {
                    try { File.AppendAllText(InjectionLog, $"[{DateTime.Now:HH:mm:ss}] Loader heartbeat #{counter}\n"); } catch { }
                }
            }
        }
    }
}