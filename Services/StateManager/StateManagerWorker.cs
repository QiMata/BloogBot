using BackgroundBotRunner;
using Communication;
using ForegroundBotRunner;
using StateManager.Clients;
using StateManager.Listeners;
using StateManager.Repository;
using StateManager.Settings;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using static WinProcessImports;

namespace StateManager
{
    public class StateManagerWorker : BackgroundService
    {
        private readonly ILogger<StateManagerWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        private readonly CharacterStateSocketListener _activityMemberSocketListener;
        private readonly StateManagerSocketListener _worldStateManagerSocketListener;

        private readonly MangosSOAPClient _mangosSOAPClient;

        private readonly Dictionary<string, (IHostedService Service, CancellationTokenSource TokenSource, Task asyncTask)> _managedServices = [];

        // Prevent repeated launches each loop iteration
        private bool _initialLaunchCompleted = false;
        // Optional basic backoff tracking (account -> last launch time)
        private readonly Dictionary<string, DateTime> _lastLaunchTimes = new();
        private static readonly TimeSpan MinRelaunchInterval = TimeSpan.FromMinutes(1);

        public StateManagerWorker(
            ILogger<StateManagerWorker> logger,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _configuration = configuration;

            _mangosSOAPClient = new MangosSOAPClient(configuration["MangosSOAP:IpAddress"]);

            _activityMemberSocketListener = new CharacterStateSocketListener(
                StateManagerSettings.Instance.CharacterDefinitions,
                configuration["CharacterStateListener:IpAddress"],
                int.Parse(configuration["CharacterStateListener:Port"]),
                _loggerFactory.CreateLogger<CharacterStateSocketListener>()
            );

            _logger.LogInformation($"Started ActivityMemberListener| {configuration["CharacterStateListener:IpAddress"]}:{configuration["CharacterStateListener:Port"]}");

            _worldStateManagerSocketListener = new StateManagerSocketListener(
                configuration["StateManagerListener:IpAddress"],
                int.Parse(configuration["StateManagerListener:Port"]),
                _loggerFactory.CreateLogger<StateManagerSocketListener>()
            );

            _logger.LogInformation($"Started StateManagerListener| {configuration["StateManagerListener:IpAddress"]}:{configuration["StateManagerListener:Port"]}");

            _worldStateManagerSocketListener.DataMessageSubject.Subscribe(OnWorldStateUpdate);
        }

        public void StartBackgroundBotWorker(string accountName)
        {
            var scope = _serviceProvider.CreateScope();
            var tokenSource = new CancellationTokenSource();
            var service = ActivatorUtilities.CreateInstance<BackgroundBotWorker>(
                scope.ServiceProvider,
                _loggerFactory,
                _configuration
            );

            _managedServices.Add(accountName, (service, tokenSource, Task.Run(async () => await service.StartAsync(tokenSource.Token))));
            _logger.LogInformation($"Started ActivityManagerService for account {accountName}");
        }

        public void StartForegroundBotWorker(string accountName)
        {
            // Backoff: prevent rapid re-launch loops if process dies immediately
            if (_lastLaunchTimes.TryGetValue(accountName, out var last) && DateTime.UtcNow - last < MinRelaunchInterval)
            {
                _logger.LogWarning($"Skipping launch for {accountName} - last attempt {DateTime.UtcNow - last:g} ago (< {MinRelaunchInterval}).");
                return;
            }
            _lastLaunchTimes[accountName] = DateTime.UtcNow;

            // Start WoW process and inject the bot worker service
            StartForegroundBotRunner(accountName);
        }

        /// <summary>
        /// Gets the status of all managed bot processes
        /// </summary>
        public Dictionary<string, string> GetManagedBotStatus()
        {
            var status = new Dictionary<string, string>();

            foreach (var kvp in _managedServices)
            {
                var accountName = kvp.Key;
                var (Service, TokenSource, Task) = kvp.Value;

                if (Service != null)
                {
                    // This is a hosted service (BackgroundBotWorker)
                    status[accountName] = Task.IsCompleted ? "Stopped" : "Running (Hosted Service)";
                }
                else
                {
                    // This is a WoW process with injected bot
                    try
                    {
                        // Try to get the process to check if it's still running
                        var process = System.Diagnostics.Process.GetProcessesByName("wow")
                            .FirstOrDefault(p => _managedServices.ContainsKey(accountName));

                        if (process != null && !process.HasExited)
                        {
                            status[accountName] = $"Running (WoW PID: {process.Id})";
                        }
                        else
                        {
                            status[accountName] = "Process Terminated";
                        }
                    }
                    catch
                    {
                        status[accountName] = "Status Unknown";
                    }
                }
            }

            return status;
        }

        /// <summary>
        /// Gets detailed information about a specific managed bot
        /// </summary>
        public string GetBotDetails(string accountName)
        {
            if (!_managedServices.TryGetValue(accountName, out var serviceTuple))
            {
                return $"Account '{accountName}' not found in managed services.";
            }

            var (Service, TokenSource, Task) = serviceTuple;

            if (Service != null)
            {
                return $"Account: {accountName}\nType: Hosted Background Service\nTask Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}";
            }
            else
            {
                return $"Account: {accountName}\nType: Injected WoW Process\nMonitoring Task Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}";
            }
        }

        private void OnWorldStateUpdate(AsyncRequest dataMessage)
        {
            StateChangeRequest stateChange = dataMessage.StateChange;

            if (stateChange != null)
            {

            }

            StateChangeResponse stateChangeResponse = new();
            _worldStateManagerSocketListener.SendMessageToClient(dataMessage.Id, stateChangeResponse);
        }

        private async Task<bool> ApplyDesiredWorkerState(CancellationToken stoppingToken)
        {
            for (int i = 0; i < StateManagerSettings.Instance.CharacterDefinitions.Count; i++)
            {
                var characterDef = StateManagerSettings.Instance.CharacterDefinitions[i];
                var accountName = characterDef.AccountName;

                // Already tracked? skip
                if (_managedServices.ContainsKey(accountName))
                {
                    _logger.LogDebug($"Account {accountName} already managed, skipping");
                    continue;
                }

                _logger.LogInformation($"Setting up new bot for account: {accountName}");

                // Ensure the account exists in the database
                if (!ReamldRepository.CheckIfAccountExists(accountName))
                {
                    _logger.LogInformation($"Creating new account: {accountName}");
                    await _mangosSOAPClient.CreateAccountAsync(accountName);
                    await Task.Delay(100);
                    await _mangosSOAPClient.SetGMLevelAsync(accountName, 3);
                }

                // Start the foreground bot worker (with WoW injection) regardless of SOAP status
                StartForegroundBotWorker(accountName);

                // Small delay to prevent overwhelming the system
                await Task.Delay(100, stoppingToken);

                // Longer delay to allow the process to fully initialize
                await Task.Delay(500, stoppingToken);
            }

            return true;
        }

        private void StartForegroundBotRunner(string accountName)
        {
            // Set the path to ForegroundBotRunner.dll in an environment variable
            var foregroundBotDllPath = Path.Combine(AppContext.BaseDirectory, "ForegroundBotRunner.dll");
            Environment.SetEnvironmentVariable("FOREGROUNDBOT_DLL_PATH", foregroundBotDllPath);
            Environment.SetEnvironmentVariable("LOADER_PAUSE_ON_EXCEPTION", "1"); // Enable pause on exception for debugging

            // Enable optional loader console + extra diagnostics (config flag or always on for now)
            // Add to appsettings.json if desired: "Injection:AllocateConsole": "true"
            var allocConsoleFlag = _configuration["Injection:AllocateConsole"];
            if (string.IsNullOrEmpty(allocConsoleFlag) || allocConsoleFlag.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("LOADER_ALLOC_CONSOLE", "1");
            }
            else
            {
                Environment.SetEnvironmentVariable("LOADER_ALLOC_CONSOLE", null); // ensure not set
            }

            var PATH_TO_GAME = _configuration["GameClient:ExecutablePath"];

            var startupInfo = new STARTUPINFO();

            // Pre-injection diagnostics
            _logger.LogInformation("=== DLL INJECTION DIAGNOSTICS START ===");

            // Check if WoW.exe exists
            if (!File.Exists(PATH_TO_GAME))
            {
                _logger.LogError($"WoW.exe not found at path: {PATH_TO_GAME}");
                return;
            }
            _logger.LogInformation($"[OK] WoW.exe found at: {PATH_TO_GAME}");

            // Enhanced DLL Path Diagnostics
            var loaderPath = _configuration["LoaderDllPath"];

            if (string.IsNullOrWhiteSpace(loaderPath))
            {
                _logger.LogError("Loader DLL path is not configured. Please set 'LoaderDllPath' in appsettings.json.");
                return;
            }

            // Resolve relative paths relative to the application base directory
            if (!Path.IsPathRooted(loaderPath))
            {
                var appBaseDir = AppContext.BaseDirectory;
                // Walk up from bin/Debug/net8.0 to find the repo root
                var repoRoot = appBaseDir;
                for (int i = 0; i < 5; i++)
                {
                    if (File.Exists(Path.Combine(repoRoot, "BloogBot.sln")))
                    {
                        break;
                    }
                    var parent = Directory.GetParent(repoRoot);
                    if (parent == null) break;
                    repoRoot = parent.FullName;
                }
                
                loaderPath = Path.Combine(repoRoot, loaderPath);
                _logger.LogInformation($"Resolved relative path to: {loaderPath}");
            }

            _logger.LogInformation("DLL FILE ANALYSIS");

            // Verify the DLL exists before attempting injection
            if (!File.Exists(loaderPath))
            {
                _logger.LogError($"[FAIL] Loader.dll not found at path: {loaderPath}");
                return;
            }
            _logger.LogInformation($"[OK] Loader.dll found at: {loaderPath}");

            // Check loader DLL architecture
            try
            {
                var loaderInfo = FileVersionInfo.GetVersionInfo(loaderPath);
                _logger.LogInformation($"[OK] Loader.dll version info: {loaderInfo.FileDescription ?? "N/A"}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[WARN] Could not read Loader.dll version info: {ex.Message}");
            }

            // Verify ForegroundBotRunner.dll exists
            var loaderDir = Path.GetDirectoryName(loaderPath);
            var foregroundBotPath = Path.Combine(loaderDir ?? "", "ForegroundBotRunner.dll");
            
            if (!File.Exists(foregroundBotPath))
            {
                _logger.LogError($"[FAIL] ForegroundBotRunner.dll not found at: {foregroundBotPath}");
                _logger.LogError("The C++ loader expects ForegroundBotRunner.dll to be in the same directory as Loader.dll");
                return;
            }
            _logger.LogInformation($"[OK] ForegroundBotRunner.dll found at: {foregroundBotPath}");

            // Check for runtime config
            var runtimeConfigPath = Path.Combine(loaderDir ?? "", "ForegroundBotRunner.runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
            {
                _logger.LogError($"[FAIL] ForegroundBotRunner.runtimeconfig.json not found at: {runtimeConfigPath}");
                _logger.LogError("The .NET loader requires a runtime config file for proper .NET 8 hosting");
                return;
            }
            _logger.LogInformation($"[OK] ForegroundBotRunner.runtimeconfig.json found at: {runtimeConfigPath}");

            // Verify .NET 8 runtime availability
            try
            {
                var dotnetInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--info",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var dotnetProcess = Process.Start(dotnetInfo);
                if (dotnetProcess != null)
                {
                    var output = dotnetProcess.StandardOutput.ReadToEnd();
                    dotnetProcess.WaitForExit();
                    
                    if (output.Contains(".NET 8.0"))
                    {
                        _logger.LogInformation("[OK] .NET 8.0 runtime detected on system");
                    }
                    else
                    {
                        _logger.LogWarning("[WARN] .NET 8.0 runtime not clearly detected in dotnet --info output");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[WARN] Could not verify .NET runtime: {ex.Message}");
            }

            // run WoW.exe in a new process
            CreateProcess(
                PATH_TO_GAME,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ProcessCreationFlag.CREATE_DEFAULT_ERROR_MODE,
                IntPtr.Zero,
                null,
                ref startupInfo,
                out PROCESS_INFORMATION processInfo);

            _logger.LogInformation($"WoW.exe started for account {accountName} (Process ID: {processInfo.dwProcessId})");

            // IMPORTANT: Add to managed services IMMEDIATELY to prevent duplicate launches
            var tokenSource = new CancellationTokenSource();
            var process = Process.GetProcessById((int)processInfo.dwProcessId);
            var monitoringTask = Task.Run(async () =>
            {
                try
                {
                    while (!process.HasExited && !tokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, tokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error monitoring foreground bot process for account {accountName}");
                }
                finally
                {
                    // Remove from managed services when process exits
                    _managedServices.Remove(accountName);
                    _logger.LogInformation($"Foreground bot process for account {accountName} has exited and been removed from tracking");
                }
            });

            // Add to managed services IMMEDIATELY to prevent race condition with ApplyDesiredWorkerState
            _managedServices.Add(accountName, (null!, tokenSource, monitoringTask));
            _logger.LogInformation($"Added {accountName} to managed services - preventing duplicate launches");

            // Give WoW.exe time to initialize before injection
            Thread.Sleep(2000);

            // get a handle to the WoW process
            var processHandle = Process.GetProcessById((int)processInfo.dwProcessId).Handle;

            _logger.LogInformation($"ATTEMPTING DLL INJECTION: {loaderPath}");

            // allocate enough memory to hold the full file path to Loader.dll within the WoW process
            var loaderPathPtr = VirtualAllocEx(
                processHandle,
                (IntPtr)0,
                loaderPath.Length * 2, // Unicode characters are 2 bytes each
                MemoryAllocationType.MEM_COMMIT,
                MemoryProtectionType.PAGE_EXECUTE_READWRITE);

            if (loaderPathPtr == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to allocate memory in target process. Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                
                // Check if target process is still running
                try
                {
                    if (process.HasExited)
                    {
                        _logger.LogError($"[FAIL] Target WoW process (PID {processInfo.dwProcessId}) has already exited");
                    }
                }
                catch { }
                
                return;
            }
            _logger.LogInformation($"[OK] Memory allocated at address: 0x{loaderPathPtr:X}");

            // Give some time for memory allocation to settle
            Thread.Sleep(500);

            // write the file path to Loader.dll to the WoW process's memory
            var bytes = Encoding.Unicode.GetBytes(loaderPath);
            var bytesWritten = 0;
            var writeResult = WriteProcessMemory(processHandle, loaderPathPtr, bytes, bytes.Length, ref bytesWritten);

            if (!writeResult || bytesWritten != bytes.Length)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to write DLL path to target process");
                _logger.LogError($"Bytes written: {bytesWritten}/{bytes.Length}");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }
            _logger.LogInformation($"[OK] DLL path written to target process: {bytesWritten} bytes");

            // search current process for the memory address of the LoadLibraryW function within the kernel32.dll module
            var moduleHandle = GetModuleHandle("kernel32.dll");
            var loaderDllPointer = GetProcAddress(moduleHandle, "LoadLibraryW");

            if (loaderDllPointer == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to get LoadLibraryW function address");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }
            _logger.LogInformation($"[OK] LoadLibraryW address: 0x{loaderDllPointer:X}");

            // Give some time before remote thread creation
            Thread.Sleep(500);

            _logger.LogInformation("Creating remote thread for DLL injection...");

            // create a new thread with the execution starting at the LoadLibraryW function, 
            // with the path to our Loader.dll passed as a parameter
            var threadHandle = CreateRemoteThread(processHandle, (IntPtr)null, (IntPtr)0, loaderDllPointer, loaderPathPtr, 0, (IntPtr)null);

            if (threadHandle == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                _logger.LogError($"[FAIL] Failed to create remote thread for DLL injection");
                _logger.LogError($"Error Code: {lastError} (0x{lastError:X})");
                _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");

                // Common reasons for remote thread creation failure
                if (lastError == 5) // ACCESS_DENIED
                {
                    _logger.LogError("ACCESS_DENIED - Target process may have higher privileges or be protected");
                    _logger.LogError("Try running StateManager as Administrator");
                }
                else if (lastError == 8) // NOT_ENOUGH_MEMORY
                {
                    _logger.LogError("NOT_ENOUGH_MEMORY - Insufficient memory in target process");
                }

                VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);
                return;
            }

            _logger.LogInformation($"[OK] Remote thread created successfully (Handle: 0x{threadHandle:X})");
            _logger.LogInformation("Waiting for injection to complete...");

            // Wait for the injection thread to complete (with timeout)
            var waitResult = WaitForSingleObject(threadHandle, 30000); // 30 second timeout for injection

            if (waitResult == 0) // WAIT_OBJECT_0
            {
                // Get the thread exit code (LoadLibrary return value)
                if (GetExitCodeThread(threadHandle, out var exitCode))
                {
                    if (exitCode != 0)
                    {
                        _logger.LogInformation($"SUCCESS: DLL injection completed successfully!");
                        _logger.LogInformation($"[OK] LoadLibrary returned: 0x{exitCode:X} (Module handle)");
                        
                        // Give the injected DLL time to initialize
                        Thread.Sleep(2000);
                        
                        // Check for loader breadcrumb files to verify execution
                        var baseDir = loaderDir;
                        var stdcallBreadcrumb = Path.Combine(baseDir ?? "", "testentry_stdcall.txt");
                        var cdeclBreadcrumb = Path.Combine(baseDir ?? "", "testentry_cdecl.txt");
                        
                        if (File.Exists(stdcallBreadcrumb))
                        {
                            _logger.LogInformation($"[OK] Managed code execution confirmed (stdcall breadcrumb found)");
                            try
                            {
                                var content = File.ReadAllText(stdcallBreadcrumb);
                                _logger.LogInformation($"[OK] Breadcrumb content: {content.Trim()}");
                            }
                            catch { }
                        }
                        else if (File.Exists(cdeclBreadcrumb))
                        {
                            _logger.LogInformation($"[OK] Managed code execution confirmed (cdecl breadcrumb found)");
                        }
                        else
                        {
                            _logger.LogWarning($"[WARN] No execution breadcrumbs found. Managed code may not have executed properly.");
                            _logger.LogWarning($"Expected files: {stdcallBreadcrumb} or {cdeclBreadcrumb}");
                        }
                    }
                    else
                    {
                        _logger.LogError($"[FAIL] DLL injection failed. LoadLibrary returned 0 (failed to load)");

                        // Enhanced error analysis for LoadLibrary failure
                        _logger.LogError("POSSIBLE CAUSES FOR LOADLIBRARY FAILURE:");
                        _logger.LogError("   - DLL architecture mismatch (32-bit vs 64-bit)");
                        _logger.LogError("   - Missing dependencies (.NET runtime, Visual C++ redistributables)");
                        _logger.LogError("   - DLL file is corrupted or invalid");
                        _logger.LogError("   - Insufficient permissions");
                        _logger.LogError("   - DLL path contains invalid characters");
                        _logger.LogError("   - Target process doesn't support .NET CLR hosting");
                        _logger.LogError("   - WoW.exe may have anti-debugging/injection protection");
                    }
                }
                else
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogWarning($"WARNING: Could not retrieve thread exit code");
                    _logger.LogWarning($"Error Code: {lastError} (0x{lastError:X})");
                    _logger.LogWarning($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                }
            }
            else
            {
                _logger.LogWarning($"WARNING: Thread wait timed out or failed. Wait result: {waitResult}");
                if (waitResult == 258) // WAIT_TIMEOUT
                {
                    _logger.LogWarning("Thread execution timed out after 30 seconds");
                    _logger.LogWarning("This may indicate the DLL is loading but taking a long time to initialize");
                }
                else if (waitResult == 0xFFFFFFFF) // WAIT_FAILED
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogWarning($"Wait failed with error: {lastError} (0x{lastError:X})");
                }
            }

            CloseHandle(threadHandle);

            // Give some time before cleanup
            Thread.Sleep(500);

            // free the memory that was allocated by VirtualAllocEx
            VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);

            _logger.LogInformation($"Foreground Bot Runner setup completed for account {accountName} (Process ID: {processInfo.dwProcessId})");
            _logger.LogInformation("=== DLL INJECTION DIAGNOSTICS END ===");
        }

        public void StopManagedService(string accountName)
        {
            if (_managedServices.TryGetValue(accountName, out var serviceTuple))
            {
                var (Service, TokenSource, Task) = serviceTuple;

                _logger.LogInformation($"Stopping managed service for account {accountName}");

                // Cancel the token to signal shutdown
                TokenSource.Cancel();

                // If it's a background service, stop it properly
                if (Service != null)
                {
                    Task.Factory.StartNew(async () => await Service.StopAsync(CancellationToken.None));
                }

                // Remove from tracking immediately to prevent issues
                _managedServices.Remove(accountName);
                _logger.LogInformation($"Stopped managed service for account {accountName}");
            }
            else
            {
                _logger.LogWarning($"Attempted to stop non-existent managed service for account {accountName}");
            }
        }

        /// <summary>
        /// Stops all managed services gracefully
        /// </summary>
        public async Task StopAllManagedServices()
        {
            _logger.LogInformation("Stopping all managed services...");

            var servicesToStop = _managedServices.ToList(); // Create a copy to avoid modification during iteration

            foreach (var kvp in servicesToStop)
            {
                var accountName = kvp.Key;
                var (Service, TokenSource, Task) = kvp.Value;

                try
                {
                    _logger.LogInformation($"Stopping service for account {accountName}");

                    // Cancel the token
                    TokenSource.Cancel();

                    // Stop hosted services properly
                    if (Service != null)
                    {
                        await Service.StopAsync(CancellationToken.None);
                    }

                    // Wait for monitoring task to complete (with timeout)
                    try
                    {
                        await Task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning($"Timeout waiting for monitoring task to complete for account {accountName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error stopping service for account {accountName}");
                }
            }

            // Clear all services
            _managedServices.Clear();
            _logger.LogInformation("All managed services stopped");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"StateManagerServiceWorker is running.");
            stoppingToken.Register(() => _logger.LogInformation($"StateManagerServiceWorker is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Only launch bots once at startup (prevents repeated WoW.exe spawning)
                    if (!_initialLaunchCompleted)
                    {
                        await ApplyDesiredWorkerState(stoppingToken);
                        _initialLaunchCompleted = true;
                        _logger.LogInformation("Initial bot launch completed. Further launches suppressed.");
                    }

                    // Log status of managed services periodically
                    if (_managedServices.Count > 0)
                    {
                        var statusReport = GetManagedBotStatus();
                        _logger.LogInformation($"Managed Services Status: {string.Join(", ", statusReport.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
                    }
                    else
                    {
                        _logger.LogDebug("No managed services currently running");
                    }

                    await Task.Delay(5000, stoppingToken); // Check every 5 seconds
                }
                catch (OperationCanceledException)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in StateManagerWorker main loop");
                    await Task.Delay(1000, stoppingToken); // Brief delay before retrying
                }
            }

            await StopAllManagedServices();
            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }
    }

}
