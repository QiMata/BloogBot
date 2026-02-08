using System;
using System.IO;
using System.Threading;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Entry point class for the native .NET 8 host (Loader.dll).
    /// 
    /// The Load method is called by the native host after initializing the CLR.
    /// It must match the ComponentEntryPoint delegate signature expected by
    /// load_assembly_and_get_function_pointer when delegate_type_name is null:
    ///   public delegate int ComponentEntryPoint(IntPtr args, int sizeBytes);
    /// </summary>
    public class Loader
    {
        private static Thread? _mainThread;
        private static bool _isInitialized = false;
        private static int _firstChanceReentrancy = 0;
        private static int _firstChanceLogged = 0;
        private const int FirstChanceLogLimit = 50;
        private static readonly string LogDirectory = InitLogDirectory();
        private static string InjectionLog => Path.Combine(LogDirectory, "injection.log");
        private static string FirstChanceLog => Path.Combine(LogDirectory, "injection_firstchance.log");

        private static string InitLogDirectory()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("WWOW_INJECT_LOG_DIR");
                string baseDir = !string.IsNullOrWhiteSpace(env) ? env : (AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory);
                var dir = Path.Combine(baseDir, "WWoWLogs");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch { return Environment.CurrentDirectory; }
        }

        /// <summary>
        /// Writes a breadcrumb file to the base directory for injection verification.
        /// </summary>
        private static void WriteBreadcrumb(string fileName, string message)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
                var path = Path.Combine(baseDir, fileName);
                var fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(path, fullMsg);
            }
            catch { /* Ignore errors - breadcrumbs are diagnostic only */ }
        }

        static Loader()
        {
            try
            {
                AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                {
                    if (_firstChanceLogged >= FirstChanceLogLimit) return;
                    if (Interlocked.Exchange(ref _firstChanceReentrancy, 1) == 1) return;
                    try
                    {
                        _firstChanceLogged++;
                        File.AppendAllText(FirstChanceLog, $"[{DateTime.Now:HH:mm:ss}] FirstChance({_firstChanceLogged}): {e.Exception.GetType()}: {e.Exception.Message}\n");
                    }
                    catch { }
                    finally { Interlocked.Exchange(ref _firstChanceReentrancy, 0); }
                };
            }
            catch { }
        }

        /// <summary>
        /// Entry point called by the native .NET 8 host.
        /// </summary>
        /// <param name="args">Pointer to arguments (unused)</param>
        /// <param name="sizeBytes">Size of arguments in bytes (unused)</param>
        /// <returns>0 on success, non-zero on failure</returns>
        public static int Load(IntPtr args, int sizeBytes)
        {
            try
            {
                File.AppendAllText(InjectionLog, $"\n=== Loader.Load() at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nProcess: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\nThread ID: {Thread.CurrentThread.ManagedThreadId}\n");
                Console.WriteLine("[Loader] Managed entry point called");

                // Write breadcrumb file to confirm managed code entry (same as MinimalLoader for test compatibility)
                WriteBreadcrumb("testentry_stdcall.txt", $"Managed entry point called via Loader.Load()\nProcess: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\n");

                if (_isInitialized)
                {
                    File.AppendAllText(InjectionLog, "Loader already initialized, skipping.\n");
                    return 0;
                }
                _isInitialized = true;
                
                // Start the main application thread
                // We use STA apartment state for WPF/WinForms compatibility if needed
                _mainThread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("[Loader] Starting injected bot service...");
                        Program.StartInjected();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Loader] Exception in main thread: {ex}");
                        try { File.AppendAllText(InjectionLog, $"[Loader] Exception in main thread: {ex}\n"); } catch { }
                    }
                });
                
                _mainThread.SetApartmentState(ApartmentState.STA);
                _mainThread.IsBackground = false; // Keep process alive
                _mainThread.Name = "ForegroundBotRunner.Main";
                _mainThread.Start();
                
                Console.WriteLine("[Loader] Main thread started successfully");
                File.AppendAllText(InjectionLog, "Loader.Load completed successfully.\n");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Loader] Failed to start: {ex}");
                try { File.AppendAllText(InjectionLog, $"Error in Loader.Load(): {ex}\n"); } catch { }
                return 1; // Failure
            }
        }
    }
}