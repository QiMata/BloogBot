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
                
                // Check if this account is already being managed
                if (_managedServices.ContainsKey(accountName))
                {
                    _logger.LogDebug($"Account {accountName} is already being managed, skipping");
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

                // Start the foreground bot worker (with WoW injection)
                StartForegroundBotWorker(accountName);
                
                // Small delay to prevent overwhelming the system
                await Task.Delay(100, stoppingToken);
                
                // Longer delay to allow the process to fully initialize
                await Task.Delay(500);
            }

            return true;
        }

        private void StartForegroundBotRunner(string accountName)
        {
            const string PATH_TO_GAME = @"C:\Users\wowadmin\Desktop\Elysium Project Game Client\WoW.exe";

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

            // Enhanced Process Architecture Diagnostics
            try
            {
                _logger.LogInformation("TARGET PROCESS ARCHITECTURE ANALYSIS");
                
                // Check if target process is 64-bit or 32-bit
                bool isWow64Process = false;
                if (IsWow64Process(process.Handle, out isWow64Process))
                {
                    bool isCurrentProcess64Bit = Environment.Is64BitProcess;
                    bool isTargetProcess64Bit = Environment.Is64BitOperatingSystem && !isWow64Process;
                    
                    var currentProcessName = Process.GetCurrentProcess().ProcessName;
                    var currentProcessArch = isCurrentProcess64Bit ? "x64" : "x86";
                    var targetProcessArch = isTargetProcess64Bit ? "x64" : "x86";
                    
                    _logger.LogInformation($"Source Process ({currentProcessName}): {currentProcessArch}");
                    _logger.LogInformation($"Target Process ({process.ProcessName}): {targetProcessArch}");
                    
                    if (isCurrentProcess64Bit != isTargetProcess64Bit)
                    {
                        _logger.LogWarning($"WARNING: ARCHITECTURE MISMATCH DETECTED between {currentProcessName} ({currentProcessArch}) and {process.ProcessName} ({targetProcessArch})!");
                        _logger.LogWarning("This may cause DLL injection to fail.");
                        _logger.LogWarning("Ensure both processes have the same architecture (x64/x86).");
                    }
                    else
                    {
                        _logger.LogInformation($"[OK] Architecture match confirmed: both processes are {currentProcessArch}");
                    }
                }
                else
                {
                    _logger.LogWarning("Could not determine target process architecture");
                }

                // Display process information
                _logger.LogInformation($"Target Process Name: {process.ProcessName}");
                _logger.LogInformation($"Target Process ID: {process.Id}");
                _logger.LogInformation($"Target Process Handle: 0x{process.Handle:X}");
                
                if (process.MainModule != null)
                {
                    _logger.LogInformation($"Target Main Module: {process.MainModule.FileName}");
                    _logger.LogInformation($"Target Base Address: 0x{process.MainModule.BaseAddress:X}");
                    _logger.LogInformation($"Target Module Size: {process.MainModule.ModuleMemorySize} bytes");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during process architecture analysis");
            }

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // get a handle to the WoW process
            var processHandle = Process.GetProcessById((int)processInfo.dwProcessId).Handle;

            // Enhanced DLL Path Diagnostics
            var loaderPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0\Loader.dll");
            
            _logger.LogInformation("DLL FILE ANALYSIS");
            
            // Verify the DLL exists before attempting injection
            if (!File.Exists(loaderPath))
            {
                _logger.LogError($"[FAIL] Loader.dll not found at path: {loaderPath}");
                return;
            }
            _logger.LogInformation($"[OK] Loader.dll found at: {loaderPath}");

            // Get DLL file information
            try
            {
                var fileInfo = new FileInfo(loaderPath);
                _logger.LogInformation($"DLL Size: {fileInfo.Length} bytes");
                _logger.LogInformation($"DLL Last Modified: {fileInfo.LastWriteTime}");
                
                // Check DLL architecture
                var dllArchitecture = GetDllArchitecture(loaderPath);
                _logger.LogInformation($"DLL Architecture: {dllArchitecture}");
                
                // Check if DLL has dependencies
                CheckDllDependencies(loaderPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing DLL file");
            }

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
                return;
            }
            _logger.LogInformation($"[OK] Memory allocated at address: 0x{loaderPathPtr:X}");

            // this seems to help prevent timing issues
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

            // this seems to help prevent timing issues
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
            var waitResult = WaitForSingleObject(threadHandle, 10000); // 10 second timeout

            if (waitResult == 0) // WAIT_OBJECT_0
            {
                // Get the thread exit code (LoadLibrary return value)
                if (GetExitCodeThread(threadHandle, out var exitCode))
                {
                    if (exitCode != 0)
                    {
                        _logger.LogInformation($"SUCCESS: DLL injection completed successfully!");
                        _logger.LogInformation($"[OK] LoadLibrary returned: 0x{exitCode:X} (Module handle)");
                    }
                    else
                    {
                        _logger.LogError($"[FAIL] DLL injection failed. LoadLibrary returned 0 (failed to load)");
                        
                        // Enhanced error analysis for LoadLibrary failure
                        _logger.LogError("POSSIBLE CAUSES FOR LOADLIBRARY FAILURE:");
                        _logger.LogError("   - DLL architecture mismatch");
                        _logger.LogError("   - Missing dependencies (.NET runtime, Visual C++ redistributables)");
                        _logger.LogError("   - DLL file is corrupted or invalid");
                        _logger.LogError("   - Insufficient permissions");
                        _logger.LogError("   - DLL path contains invalid characters");
                        _logger.LogError("   - Target process doesn't support .NET CLR hosting");
                        _logger.LogError("   - WoWActivityMember.exe not found in same directory as Loader.dll");
                        
                        // Check for target executable
                        var activityMemberPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0\WoWActivityMember.exe");
                        if (!File.Exists(activityMemberPath))
                        {
                            _logger.LogError($"[FAIL] WoWActivityMember.exe not found at expected path: {activityMemberPath}");
                        }
                        else
                        {
                            _logger.LogInformation($"[OK] WoWActivityMember.exe found at: {activityMemberPath}");
                        }
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
                    _logger.LogWarning("Thread execution timed out after 10 seconds");
                }
                else if (waitResult == 0xFFFFFFFF) // WAIT_FAILED
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogWarning($"Wait failed with error: {lastError} (0x{lastError:X})");
                }
            }

            CloseHandle(threadHandle);

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // free the memory that was allocated by VirtualAllocEx
            VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);

            _logger.LogInformation($"Foreground Bot Runner setup completed for account {accountName} (Process ID: {processInfo.dwProcessId})");
            _logger.LogInformation("=== DLL INJECTION DIAGNOSTICS END ===");

            // Additional verification: Check if WoWActivityMember.exe is present
            var activityMemberVerifyPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0\WoWActivityMember.exe");
            if (!File.Exists(activityMemberVerifyPath))
            {
                _logger.LogWarning($"WARNING: WoWActivityMember.exe not found at expected path: {activityMemberVerifyPath}");
                _logger.LogWarning("This may cause the CLR hosting to fail even if DLL injection succeeds");
            }
            else
            {
                _logger.LogInformation($"[OK] WoWActivityMember.exe verified at: {activityMemberVerifyPath}");
            }
        }

        /// <summary>
        /// Gets the architecture of a DLL file (x86, x64, AnyCPU)
        /// </summary>
        private string GetDllArchitecture(string dllPath)
        {
            try
            {
                using (var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    // Read DOS header
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    var peHeaderOffset = br.ReadInt32();
                    
                    // Read PE header
                    fs.Seek(peHeaderOffset + 4, SeekOrigin.Begin);
                    var machine = br.ReadUInt16();
                    
                    return machine switch
                    {
                        0x014c => "x86 (32-bit)",
                        0x8664 => "x64 (64-bit)",
                        0x0200 => "Itanium (64-bit)",
                        _ => $"Unknown (0x{machine:X4})"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine DLL architecture");
                return "Unknown";
            }
        }

        /// <summary>
        /// Checks for common DLL dependencies
        /// </summary>
        private void CheckDllDependencies(string dllPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(dllPath);
                _logger.LogInformation("CHECKING DLL DEPENDENCIES:");
                
                // Check for common .NET dependencies
                var commonDependencies = new[]
                {
                    "WoWActivityMember.exe",
                    "WoWActivityMember.runtimeconfig.json",
                    "WoWActivityMember.deps.json"
                };
                
                foreach (var dep in commonDependencies)
                {
                    var depPath = Path.Combine(directory!, dep);
                    if (File.Exists(depPath))
                    {
                        _logger.LogInformation($"   [OK] {dep}");
                    }
                    else
                    {
                        _logger.LogWarning($"   [MISSING] {dep}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking DLL dependencies");
            }
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
                    await ApplyDesiredWorkerState(stoppingToken);
                    
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
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in StateManagerWorker main loop");
                    await Task.Delay(1000, stoppingToken); // Brief delay before retrying
                }
            }

            // Use the new method for proper cleanup
            await StopAllManagedServices();
            
            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }
    }
    
}
