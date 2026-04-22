using Communication;
using WoWStateManager.Clients;
using WoWStateManager.Listeners;
using WoWStateManager.Logging;
using WoWStateManager.Repository;
using WoWStateManager.Settings;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using static WinProcessImports;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WoWStateManager
{
    public partial class StateManagerWorker
    {
        // Prevent repeated launches each loop iteration


        // Prevent repeated launches each loop iteration
        private bool _initialLaunchCompleted = false;
        // Optional basic backoff tracking (account -> last launch time)

        private static readonly TimeSpan MinRelaunchInterval = TimeSpan.FromMinutes(1);

        // 63c: Named-pipe log servers � one per foreground bot account


        /// <summary>
        /// Shared mutable timestamp used to communicate the real injection time
        /// from the injection code path to the monitoring task closure.
        /// Uses Interlocked on ticks since DateTime is a struct and cannot be volatile.
        /// </summary>
        private sealed class InjectionTimestampHolder(DateTime initial)
        {
            private long _ticks = initial.Ticks;
            public DateTime Value
            {
                get => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
                set => Interlocked.Exchange(ref _ticks, value.Ticks);
            }
        }


        public void StartBackgroundBotWorker(string accountName, string? characterClass = null, string? characterRace = null, string? characterGender = null, string? characterSpec = null, string? talentBuildName = null, int? characterNameAttemptOffset = null, bool useGmCommands = false, string? assignedActivity = null)
        {
            var tokenSource = new CancellationTokenSource();

            // Launch BackgroundBotRunner as a separate process so each bot owns its own
            // WoWSharpObjectManager/EventEmitter singletons (no cross-contamination).
            var baseDir = AppContext.BaseDirectory;
            var botExePath = Path.Combine(baseDir, "BackgroundBotRunner", "BackgroundBotRunner.exe");
            var botDllPath = Path.Combine(baseDir, "BackgroundBotRunner", "BackgroundBotRunner.dll");
            if (!File.Exists(botExePath) && !File.Exists(botDllPath))
            {
                botExePath = Path.Combine(baseDir, "BackgroundBotRunner.exe");
                botDllPath = Path.Combine(baseDir, "BackgroundBotRunner.dll");
            }

            var showWindows = Environment.GetEnvironmentVariable("WWOW_SHOW_WINDOWS") == "1";
            var psi = new ProcessStartInfo
            {
                FileName = File.Exists(botExePath) ? botExePath : "dotnet",
                Arguments = File.Exists(botExePath) ? string.Empty : $"\"{botDllPath}\"",
                WorkingDirectory = File.Exists(botExePath) ? Path.GetDirectoryName(botExePath)! : Path.GetDirectoryName(botDllPath)!,
                UseShellExecute = false,
                // When WWOW_SHOW_WINDOWS=1, don't redirect stdout/stderr so Serilog's Console
                // sink writes to the visible console window. Log file sink (WWoWLogs/bg_{account}.log)
                // still captures everything regardless.
                RedirectStandardOutput = !showWindows,
                RedirectStandardError = !showWindows,
                CreateNoWindow = !showWindows,
            };
            psi.Environment["WWOW_ACCOUNT_NAME"] = accountName;
            psi.Environment["WWOW_ACCOUNT_PASSWORD"] = "PASSWORD";
            psi.Environment["PathfindingService__IpAddress"] = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            psi.Environment["PathfindingService__Port"] = _configuration["PathfindingService:Port"] ?? "5001";
            psi.Environment["SceneDataService__IpAddress"] =
                _configuration["SceneDataService:IpAddress"]
                ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_IP")
                ?? "127.0.0.1";
            psi.Environment["SceneDataService__Port"] =
                _configuration["SceneDataService:Port"]
                ?? Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_PORT")
                ?? "5003";
            // Forward WWOW_DATA_DIR so the BG bot's local Navigation.dll can find
            // terrain/collision data (maps/, vmaps/, scenes/). Without this, the local
            // physics engine has no geometry and the bot falls through the world.
            var wwowDataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            if (!string.IsNullOrEmpty(wwowDataDir))
                psi.Environment["WWOW_DATA_DIR"] = wwowDataDir;

            psi.Environment["CharacterStateListener__IpAddress"] = _configuration["CharacterStateClient:IpAddress"] ?? "127.0.0.1";
            psi.Environment["CharacterStateListener__Port"] = _configuration["CharacterStateListener:Port"] ?? "5002";
            psi.Environment["RealmEndpoint__IpAddress"] = _configuration["RealmEndpoint:IpAddress"] ?? "127.0.0.1";
            if (!string.IsNullOrEmpty(characterClass))
                psi.Environment["WWOW_CHARACTER_CLASS"] = characterClass;
            if (!string.IsNullOrEmpty(characterRace))
                psi.Environment["WWOW_CHARACTER_RACE"] = characterRace;
            if (!string.IsNullOrEmpty(characterGender))
                psi.Environment["WWOW_CHARACTER_GENDER"] = characterGender;
            if (!string.IsNullOrEmpty(characterSpec))
                psi.Environment["WWOW_CHARACTER_SPEC"] = characterSpec;
            if (!string.IsNullOrEmpty(talentBuildName))
                psi.Environment["WWOW_TALENT_BUILD"] = talentBuildName;
            if (characterNameAttemptOffset.HasValue)
                psi.Environment["WWOW_CHARACTER_NAME_ATTEMPT_OFFSET"] = characterNameAttemptOffset.Value.ToString();
            psi.Environment["WWOW_USE_GM_COMMANDS"] = useGmCommands ? "1" : "0";
            if (!string.IsNullOrWhiteSpace(assignedActivity))
                psi.Environment["WWOW_ASSIGNED_ACTIVITY"] = assignedActivity;

            var process = Process.Start(psi);
            var pid = (uint?)process?.Id;

            // Forward stdout/stderr to our logger (only when redirecting, i.e. no visible window)
            if (process != null && !showWindows)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = await process.StandardOutput.ReadLineAsync();
                            if (line != null) _logger.LogInformation($"[{accountName}] {line}");
                        }
                    }
                    catch { }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            if (line != null) _logger.LogWarning($"[{accountName}:ERR] {line}");
                        }
                    }
                    catch { }
                });
            }

            _managedServices.TryAdd(accountName, (null, tokenSource, Task.CompletedTask, pid));
            _logger.LogInformation($"Started BackgroundBotRunner process for account {accountName} (PID: {pid})");
        }


        public void StartForegroundBotWorker(string accountName, int? targetProcessId = null, string? characterClass = null, string? characterRace = null, string? characterGender = null, string? characterSpec = null, string? talentBuildName = null, int? characterNameAttemptOffset = null, bool useGmCommands = false, string? assignedActivity = null)
        {
            // Backoff: prevent rapid re-launch loops if process dies immediately
            if (_lastLaunchTimes.TryGetValue(accountName, out var last) && DateTime.UtcNow - last < MinRelaunchInterval)
            {
                _logger.LogWarning($"Skipping launch for {accountName} - last attempt {DateTime.UtcNow - last:g} ago (< {MinRelaunchInterval}).");
                return;
            }
            _lastLaunchTimes[accountName] = DateTime.UtcNow;

            // Start WoW process and inject the bot worker service
            StartForegroundBotRunner(accountName, targetProcessId, characterClass, characterRace, characterGender, characterSpec, talentBuildName, characterNameAttemptOffset, useGmCommands, assignedActivity);
        }

        /// <summary>
        /// Gets the status of all managed bot processes
        /// </summary>


        /// <summary>
        /// Gets detailed status for a foreground (injected) bot by checking both process state and communication state
        /// </summary>
        private string GetForegroundBotStatus(string accountName, uint? processId)
        {
            // Check if we're receiving state updates from the bot
            var hasStateUpdates = _activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot);
            var hasRecentUpdate = hasStateUpdates && snapshot != null && !string.IsNullOrEmpty(snapshot.AccountName);

            // Check process state if we have a PID
            bool processRunning = false;
            if (processId.HasValue)
            {
                try
                {
                    var process = Process.GetProcessById((int)processId.Value);
                    processRunning = process != null && !process.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process no longer exists
                    processRunning = false;
                }
                catch (Exception)
                {
                    // Access denied or other error - assume still running if we can't check
                    processRunning = true;
                }
            }

            // Build status string based on what we know
            if (!processRunning && processId.HasValue)
            {
                return "Process Terminated";
            }

            if (hasRecentUpdate)
            {
                var playerInfo = snapshot?.Player?.Unit?.GameObject?.Base != null
                    ? $", GUID: {snapshot.Player.Unit.GameObject.Base.Guid}"
                    : "";
                return $"Running (PID: {processId}{playerInfo})";
            }

            if (processRunning)
            {
                return $"Running (PID: {processId}, No State Updates)";
            }

            return processId.HasValue ? $"Unknown (PID: {processId})" : "Not Started";
        }

        /// <summary>
        /// Gets detailed information about a specific managed bot
        /// </summary>


        /// <summary>
        /// Gets detailed information about a specific managed bot
        /// </summary>
        public string GetBotDetails(string accountName)
        {
            if (!_managedServices.TryGetValue(accountName, out var serviceTuple))
            {
                return $"Account '{accountName}' not found in managed services.";
            }

            var (Service, TokenSource, Task, ProcessId) = serviceTuple;

            if (Service != null)
            {
                return $"Account: {accountName}\nType: Hosted Background Service\nTask Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}";
            }
            else
            {
                var pidInfo = ProcessId.HasValue ? $"PID: {ProcessId}" : "PID: Unknown";
                var stateInfo = "";
                if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot) && snapshot?.Player != null)
                {
                    var guid = snapshot.Player.Unit?.GameObject?.Base?.Guid ?? 0;
                    stateInfo = $"\nPlayer GUID: {guid}\nTimestamp: {snapshot.Timestamp}";
                }
                return $"Account: {accountName}\nType: Injected WoW Process\n{pidInfo}\nMonitoring Task Status: {Task.Status}\nCancellation Requested: {TokenSource.Token.IsCancellationRequested}{stateInfo}";
            }
        }


        private void StartForegroundBotRunner(string accountName, int? targetProcessId = null, string? characterClass = null, string? characterRace = null, string? characterGender = null, string? characterSpec = null, string? talentBuildName = null, int? characterNameAttemptOffset = null, bool useGmCommands = false, string? assignedActivity = null)
        {
            // Set the path to ForegroundBotRunner.dll in an environment variable
            var foregroundBotDllPath = Path.Combine(AppContext.BaseDirectory, "ForegroundBotRunner.dll");
            Environment.SetEnvironmentVariable("FOREGROUNDBOT_DLL_PATH", foregroundBotDllPath);
            // Environment.SetEnvironmentVariable("WWOW_WAIT_DEBUG", "1"); // Uncomment to wait for debugger attach
            // Environment.SetEnvironmentVariable("LOADER_PAUSE_ON_EXCEPTION", "1"); // Enable pause on exception for debugging

            // Pass account credentials to the injected ForegroundBotRunner via environment variables (backup)
            // The password is always "PASSWORD" as set by MangosSOAPClient.CreateAccountAsync
            Environment.SetEnvironmentVariable("WWOW_ACCOUNT_NAME", accountName);
            Environment.SetEnvironmentVariable("WWOW_ACCOUNT_PASSWORD", "PASSWORD");
            if (!string.IsNullOrEmpty(characterClass))
                Environment.SetEnvironmentVariable("WWOW_CHARACTER_CLASS", characterClass);
            if (!string.IsNullOrEmpty(characterRace))
                Environment.SetEnvironmentVariable("WWOW_CHARACTER_RACE", characterRace);
            if (!string.IsNullOrEmpty(characterGender))
                Environment.SetEnvironmentVariable("WWOW_CHARACTER_GENDER", characterGender);
            if (!string.IsNullOrEmpty(characterSpec))
                Environment.SetEnvironmentVariable("WWOW_CHARACTER_SPEC", characterSpec);
            if (!string.IsNullOrEmpty(talentBuildName))
                Environment.SetEnvironmentVariable("WWOW_TALENT_BUILD", talentBuildName);
            Environment.SetEnvironmentVariable(
                "WWOW_CHARACTER_NAME_ATTEMPT_OFFSET",
                characterNameAttemptOffset?.ToString());
            Environment.SetEnvironmentVariable("WWOW_USE_GM_COMMANDS", useGmCommands ? "1" : "0");
            Environment.SetEnvironmentVariable(
                "WWOW_ASSIGNED_ACTIVITY",
                string.IsNullOrWhiteSpace(assignedActivity) ? null : assignedActivity);
            _logger.LogInformation($"Set credentials environment variables for ForegroundBotRunner: WWOW_ACCOUNT_NAME={accountName}");

            // Disable packet hooks for crash diagnostics: "Injection:DisablePacketHooks": "true"
            var disableHooksFlag = _configuration["Injection:DisablePacketHooks"];
            if (!string.IsNullOrEmpty(disableHooksFlag) && disableHooksFlag.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", "1");
                _logger.LogWarning("FG packet hooks DISABLED via Injection:DisablePacketHooks config");
            }
            else
            {
                Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", null);
            }

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

            // 63c: Start named-pipe log server BEFORE launching WoW so the bot can connect immediately
            if (!_botLogPipeServers.ContainsKey(accountName))
            {
                var pipeServer = new BotLogPipeServer(accountName, _loggerFactory);
                try
                {
                    pipeServer.Start();
                    _botLogPipeServers[accountName] = pipeServer;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to start BotLogPipeServer for {accountName}, disposing");
                    pipeServer.Dispose();
                    throw;
                }
            }

            var PATH_TO_GAME = Environment.GetEnvironmentVariable("WWOW_WOW_EXE_PATH")
                ?? _configuration["GameClient:ExecutablePath"];

            // Check if we're injecting into an existing process
            bool injectIntoExisting = targetProcessId.HasValue;
            if (injectIntoExisting)
            {
                _logger.LogInformation($"=== INJECTING INTO EXISTING PROCESS {targetProcessId.Value} ===");
            }

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

            // Enhanced DLL Path Diagnostics - Check environment variable first, then fall back to config
            var loaderPath = Environment.GetEnvironmentVariable("WWOW_LOADER_DLL_PATH");
            if (string.IsNullOrWhiteSpace(loaderPath))
            {
                loaderPath = _configuration["LoaderDllPath"];
            }
            else
            {
                _logger.LogInformation($"Using WWOW_LOADER_DLL_PATH environment variable: {loaderPath}");
            }

            if (string.IsNullOrWhiteSpace(loaderPath))
            {
                _logger.LogError("Loader DLL path is not configured. Please set 'WWOW_LOADER_DLL_PATH' environment variable or 'LoaderDllPath' in appsettings.json.");
                return;
            }

            // Resolve relative paths relative to the application base directory
            if (!Path.IsPathRooted(loaderPath))
            {
                var appBaseDir = AppContext.BaseDirectory;

                // First check if the file exists relative to the app base directory
                var localPath = Path.Combine(appBaseDir, loaderPath);
                if (File.Exists(localPath))
                {
                    loaderPath = localPath;
                    _logger.LogInformation($"Resolved relative path to app directory: {loaderPath}");
                }
                else
                {
                    // Walk up from bin/Debug/net8.0 to find the repo root
                    var repoRoot = appBaseDir;
                    for (int i = 0; i < 5; i++)
                    {
                        if (File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
                        {
                            break;
                        }
                        var parent = Directory.GetParent(repoRoot);
                        if (parent == null) break;
                        repoRoot = parent.FullName;
                    }

                    loaderPath = Path.Combine(repoRoot, loaderPath);
                    _logger.LogInformation($"Resolved relative path to repo root: {loaderPath}");
                }
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

            uint processId;
            IntPtr processHandle;

            if (injectIntoExisting)
            {
                // Inject into an existing WoW process
                try
                {
                    var process = Process.GetProcessById(targetProcessId.Value);
                    processId = (uint)process.Id;
                    processHandle = process.Handle;
                    _logger.LogInformation($"Found existing WoW process: {process.ProcessName} (PID: {processId})");
                }
                catch (ArgumentException)
                {
                    _logger.LogError($"[FAIL] Target process ID {targetProcessId.Value} not found");
                    return;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    _logger.LogError($"[FAIL] Cannot access target process {targetProcessId.Value}: {ex.Message}");
                    _logger.LogError("Try running StateManager as Administrator");
                    return;
                }
            }
            else
            {
                // run WoW.exe in a new process
                var createResult = CreateProcess(
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

                if (!createResult || processInfo.hProcess == IntPtr.Zero)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    _logger.LogError($"[FAIL] CreateProcess failed. Error Code: {lastError} (0x{lastError:X})");
                    _logger.LogError($"Error Description: {new System.ComponentModel.Win32Exception(lastError).Message}");
                    return;
                }

                processId = processInfo.dwProcessId;
                processHandle = processInfo.hProcess;  // Use the handle directly from CreateProcess

                // Close the primary thread handle immediately � we only need the process handle
                if (processInfo.hThread != IntPtr.Zero)
                    CloseHandle(processInfo.hThread);

                _logger.LogWarning($"WoW.exe started for account {accountName} (Process ID: {processId}, Handle: 0x{processHandle:X})");
            }

            // IMPORTANT: Add to managed services IMMEDIATELY to prevent duplicate launches
            var tokenSource = new CancellationTokenSource();
            var capturedProcessId = processId;
            // Use a holder object so the monitoring closure always reads the latest timestamp
            // (the injection code path updates this after actual injection completes).
            var injectionTimestampHolder = new InjectionTimestampHolder(DateTime.UtcNow);
            var monitoringTask = Task.Run(async () =>
            {
                bool processExited = false;
                try
                {
                    // Wait a bit before starting to monitor to avoid interfering with injection
                    await Task.Delay(10000, tokenSource.Token);

                    // 62c: Orphan reaper � track whether the bot has phoned home
                    bool snapshotReceived = false;

                    // Monitor the WoW process
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        // Check if process is still running
                        try
                        {
                            var proc = Process.GetProcessById((int)capturedProcessId);
                            if (proc == null || proc.HasExited)
                            {
                                processExited = true;
                                break;
                            }
                        }
                        catch (ArgumentException)
                        {
                            processExited = true;
                            break;
                        }
                        catch
                        {
                            // Ignore other errors (access denied etc), just keep monitoring
                        }

                        // 62c: Check for snapshot phone-home; kill orphans after 60s from actual injection
                        if (!snapshotReceived)
                        {
                            if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snap)
                                && snap != null
                                && !string.IsNullOrEmpty(snap.AccountName)
                                && snap.AccountName != "?")
                            {
                                snapshotReceived = true;
                                _logger.LogInformation($"Orphan reaper: snapshot received for {accountName} (PID {capturedProcessId})");
                            }
                            else if (DateTime.UtcNow > injectionTimestampHolder.Value.AddSeconds(60))
                            {
                                _logger.LogWarning($"Orphan reaper: no snapshot from {accountName} (PID {capturedProcessId}) within 60s of injection � killing process.");
                                try
                                {
                                    var orphan = Process.GetProcessById((int)capturedProcessId);
                                    orphan.Kill();
                                }
                                catch (Exception killEx)
                                {
                                    _logger.LogWarning(killEx, $"Orphan reaper: failed to kill PID {capturedProcessId}");
                                }
                                processExited = true;
                                break;
                            }
                        }

                        await Task.Delay(5000, tokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation - don't remove from tracking
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error monitoring foreground bot process for account {accountName} (will continue tracking)");
                }
                finally
                {
                    // Only remove from managed services if process actually exited
                    if (processExited)
                    {
                        _managedServices.TryRemove(accountName, out _);
                        // Reset the activity member slot so '?' assignment works on relaunch
                        if (_activityMemberSocketListener.CurrentActivityMemberList.ContainsKey(accountName))
                        {
                            _activityMemberSocketListener.CurrentActivityMemberList[accountName] = new();
                            _logger.LogWarning($"Reset activity member slot for account {accountName}");
                        }
                        _logger.LogWarning($"Foreground bot process for account {accountName} has exited and been removed from tracking");
                    }
                }
            });

            // Add to managed services IMMEDIATELY to prevent race condition with ApplyDesiredWorkerState
            _managedServices[accountName] = (null, tokenSource, monitoringTask, processId);
            _logger.LogWarning($"Added {accountName} to managed services with PID {processId} - preventing duplicate launches");

            // 62a: Poll for WoW window before injection.
            // A second foreground client in live tests can take longer than 15s to create
            // a visible top-level window, so keep this configurable and use a less fragile default.
            // Uses WaitForSingleObject + EnumWindows instead of Process.GetProcessById
            // to avoid "Access denied" when the test host isn't running elevated.
            {
                var windowPollTimeoutSeconds = 90;
                var configuredWindowTimeout =
                    Environment.GetEnvironmentVariable("WWOW_FOREGROUND_WINDOW_TIMEOUT_SECONDS")
                    ?? _configuration["Injection:WindowTimeoutSeconds"];
                if (int.TryParse(configuredWindowTimeout, out var parsedWindowTimeoutSeconds)
                    && parsedWindowTimeoutSeconds > 0)
                {
                    windowPollTimeoutSeconds = parsedWindowTimeoutSeconds;
                }

                var hiddenWindowGraceSeconds = 15;
                var configuredHiddenWindowGrace =
                    Environment.GetEnvironmentVariable("WWOW_FOREGROUND_HIDDEN_WINDOW_GRACE_SECONDS")
                    ?? _configuration["Injection:HiddenWindowGraceSeconds"];
                if (int.TryParse(configuredHiddenWindowGrace, out var parsedHiddenWindowGraceSeconds)
                    && parsedHiddenWindowGraceSeconds > 0)
                {
                    hiddenWindowGraceSeconds = parsedHiddenWindowGraceSeconds;
                }

                var windowPollTimeout = TimeSpan.FromSeconds(windowPollTimeoutSeconds);
                var hiddenWindowGrace = TimeSpan.FromSeconds(hiddenWindowGraceSeconds);
                var windowPollSw = Stopwatch.StartNew();
                bool windowReady = false;
                bool sawTopLevelWindow = false;
                bool sawVisibleWindow = false;

                while (windowPollSw.Elapsed < windowPollTimeout)
                {
                    // Check if process is still alive using the CreateProcess handle
                    var processWait = WaitForSingleObject(processHandle, 0);
                    if (processWait == WAIT_OBJECT_0)
                    {
                        _logger.LogError($"WoW process (PID {processId}) exited before window appeared. Aborting injection for {accountName}.");
                        RemoveManagedService(accountName, tokenSource);
                        CloseHandleSafe(processHandle);
                        return;
                    }

                    bool foundAnyWindow = false;
                    bool foundVisibleWindow = false;
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowPid);
                        if (windowPid != processId)
                            return true;

                        foundAnyWindow = true;
                        if (IsWindowVisible(hWnd))
                        {
                            foundVisibleWindow = true;
                            return false; // stop enumerating
                        }

                        return true; // continue scanning for visible windows owned by this PID
                    }, IntPtr.Zero);

                    if (foundAnyWindow)
                    {
                        sawTopLevelWindow = true;
                        if (foundVisibleWindow)
                        {
                            sawVisibleWindow = true;
                            windowReady = true;
                            _logger.LogWarning($"WoW visible window detected for PID {processId} after {windowPollSw.Elapsed.TotalSeconds:F1}s");
                            break;
                        }

                        if (windowPollSw.Elapsed >= hiddenWindowGrace)
                        {
                            windowReady = true;
                            _logger.LogWarning(
                                "WoW window handle detected for PID {ProcessId} after {Elapsed:F1}s but it is not visible yet; proceeding with injection.",
                                processId,
                                windowPollSw.Elapsed.TotalSeconds);
                            break;
                        }
                    }

                    Thread.Sleep(250);
                }

                if (!windowReady)
                {
                    var processWait = WaitForSingleObject(processHandle, 0);
                    if (processWait == WAIT_OBJECT_0)
                    {
                        _logger.LogError($"WoW process (PID {processId}) exited during window readiness wait. Aborting injection for {accountName}.");
                        RemoveManagedService(accountName, tokenSource);
                        CloseHandleSafe(processHandle);
                        return;
                    }

                    _logger.LogWarning(
                        "WoW window did not become ready within {Timeout}s for PID {ProcessId} (topLevelSeen={TopLevelSeen}, visibleSeen={VisibleSeen}). Proceeding with best-effort injection.",
                        windowPollTimeout.TotalSeconds,
                        processId,
                        sawTopLevelWindow,
                        sawVisibleWindow);
                    Thread.Sleep(1000);
                }
            }

            _logger.LogWarning($"ATTEMPTING DLL INJECTION: {loaderPath}");

            try
            {
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
                
                // Check if target process is still alive using the CreateProcess handle
                if (WaitForSingleObject(processHandle, 0) == WAIT_OBJECT_0)
                {
                    _logger.LogError($"[FAIL] Target WoW process (PID {processId}) has already exited");
                }
                
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

            _logger.LogWarning($"[OK] Remote thread created successfully (Handle: 0x{threadHandle:X})");
            _logger.LogWarning("Waiting for injection to complete...");

            // Wait for the injection thread to complete (with timeout)
            var waitResult = WaitForSingleObject(threadHandle, 30000); // 30 second timeout for injection

            if (waitResult == 0) // WAIT_OBJECT_0
            {
                // Get the thread exit code (LoadLibrary return value)
                if (GetExitCodeThread(threadHandle, out var exitCode))
                {
                    if (exitCode != 0)
                    {
                        _logger.LogWarning($"SUCCESS: DLL injection completed successfully!");
                        _logger.LogWarning($"[OK] LoadLibrary returned: 0x{exitCode:X} (Module handle)");
                        
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

            // Update the injection timestamp so the orphan reaper uses the real time
            injectionTimestampHolder.Value = DateTime.UtcNow;

            // 62b: Wait for bot phone-home after injection (poll CurrentActivityMemberList, 30s timeout)
            {
                var phoneHomeTimeout = TimeSpan.FromSeconds(30);
                var phoneHomeSw = Stopwatch.StartNew();
                bool phoneHomeReceived = false;

                while (phoneHomeSw.Elapsed < phoneHomeTimeout)
                {
                    if (_activityMemberSocketListener.CurrentActivityMemberList.TryGetValue(accountName, out var snapshot)
                        && snapshot != null
                        && !string.IsNullOrEmpty(snapshot.AccountName)
                        && snapshot.AccountName != "?")
                    {
                        phoneHomeReceived = true;
                        _logger.LogWarning($"Bot phone-home received for {accountName} after {phoneHomeSw.Elapsed.TotalSeconds:F1}s");
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (!phoneHomeReceived)
                {
                    _logger.LogWarning($"No phone-home snapshot from {accountName} within {phoneHomeTimeout.TotalSeconds}s of injection. Bot may be unresponsive (PID {processId}).");
                }
            }

            _logger.LogWarning($"Foreground Bot Runner setup completed for account {accountName} (Process ID: {processId})");
            _logger.LogWarning("=== DLL INJECTION DIAGNOSTICS END ===");
            }
            finally
            {
                // Close the process handle — we're done with low-level manipulation.
                // The monitoring task uses Process.GetProcessById which opens its own handle.
                CloseHandleSafe(processHandle);
            }
        }

        /// <summary>
        /// Removes an account from managed services and cancels its monitoring task.
        /// Used when injection aborts after the entry was already added.
        /// </summary>


        /// <summary>
        /// Removes an account from managed services and cancels its monitoring task.
        /// Used when injection aborts after the entry was already added.
        /// </summary>
        private void RemoveManagedService(string accountName, CancellationTokenSource tokenSource)
        {
            tokenSource.Cancel();
            _managedServices.TryRemove(accountName, out _);
            _lastLaunchTimes.TryRemove(accountName, out _);
            // Reset the activity member slot so '?' assignment works on relaunch
            if (_activityMemberSocketListener.CurrentActivityMemberList.ContainsKey(accountName))
            {
                _activityMemberSocketListener.CurrentActivityMemberList[accountName] = new();
            }
            _logger.LogInformation($"Removed {accountName} from managed services after injection abort");
        }

        /// <summary>
        /// Safely closes a Win32 handle, ignoring errors on IntPtr.Zero.
        /// </summary>


        /// <summary>
        /// Safely closes a Win32 handle, ignoring errors on IntPtr.Zero.
        /// </summary>
        private static void CloseHandleSafe(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                try { CloseHandle(handle); } catch { }
            }
        }

        /// <summary>
        /// Kill a managed process by PID. Used during shutdown to clean up
        /// WoW.exe (foreground bots) and dotnet BackgroundBotRunner processes.
        /// Only kills specific PIDs that StateManager launched — never blanket-kills.
        /// </summary>


        /// <summary>
        /// Kill a managed process by PID. Used during shutdown to clean up
        /// WoW.exe (foreground bots) and dotnet BackgroundBotRunner processes.
        /// Only kills specific PIDs that StateManager launched — never blanket-kills.
        /// </summary>
        private void KillManagedProcess(string accountName, uint? processId)
        {
            if (!processId.HasValue) return;

            try
            {
                var process = Process.GetProcessById((int)processId.Value);
                if (!process.HasExited)
                {
                    _logger.LogInformation($"Killing managed process for {accountName} (PID: {processId.Value})");
                    process.Kill();
                    process.WaitForExit(5000);
                    _logger.LogInformation($"Process {processId.Value} for {accountName} terminated");
                }
            }
            catch (ArgumentException)
            {
                // Process already exited
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to kill process {processId.Value} for {accountName}");
            }
        }


        public async Task StopManagedServiceAsync(string accountName)
        {
            var found = _managedServices.TryRemove(accountName, out var serviceTuple);
            _lastLaunchTimes.TryRemove(accountName, out _);

            if (found)
            {
                var (Service, TokenSource, Task, ProcessId) = serviceTuple;

                _logger.LogInformation($"Stopping managed service for account {accountName}");

                // Cancel the token to signal shutdown
                TokenSource.Cancel();

                // Await service stop with a timeout to ensure deterministic teardown
                if (Service != null)
                {
                    try
                    {
                        await Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning($"Timeout waiting for service stop for account {accountName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error stopping service for account {accountName}");
                    }
                }

                // Kill the managed process (WoW.exe for FG, dotnet BackgroundBotRunner for BG)
                KillManagedProcess(accountName, ProcessId);

                // Wait for monitoring task to complete (with timeout)
                try
                {
                    await Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning($"Timeout waiting for monitoring task to complete for account {accountName}");
                }

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


        /// <summary>
        /// Stops all managed services gracefully
        /// </summary>
        public async Task StopAllManagedServices()
        {
            _logger.LogInformation("Stopping all managed services...");

            var servicesToStop = _managedServices.ToArray();

            foreach (var kvp in servicesToStop)
            {
                var accountName = kvp.Key;
                var (Service, TokenSource, Task, ProcessId) = kvp.Value;

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

                    // Kill the managed process (WoW.exe for FG, dotnet BackgroundBotRunner for BG)
                    KillManagedProcess(accountName, ProcessId);

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

            // 63c: Dispose all named-pipe log servers
            foreach (var kvp in _botLogPipeServers)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            _botLogPipeServers.Clear();

            _logger.LogInformation("All managed services stopped");
        }
    }
}
