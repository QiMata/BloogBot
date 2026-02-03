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
    internal class Loader
    {
        private static Thread? _mainThread;

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
                Console.WriteLine("[Loader] Managed entry point called");
                
                // Start the main application thread
                // We use STA apartment state for WPF/WinForms compatibility if needed
                _mainThread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("[Loader] Starting main application thread...");
                        Program.Main([]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Loader] Exception in main thread: {ex}");
                    }
                });
                
                _mainThread.SetApartmentState(ApartmentState.STA);
                _mainThread.IsBackground = false; // Keep process alive
                _mainThread.Name = "ForegroundBotRunner.Main";
                _mainThread.Start();
                
                Console.WriteLine("[Loader] Main thread started successfully");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Loader] Failed to start: {ex}");
                return 1; // Failure
            }
        }
    }
}
