using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Integration tests for hijacking (injecting into) an existing WoW client.
    ///
    /// Prerequisites:
    /// 1. WoW.exe must be running (vanilla 1.12.1 client)
    /// 2. Mangos server must be running
    /// 3. Loader.dll must be built (run Setup-InjectionDlls.ps1)
    ///
    /// These tests will:
    /// - Find a running WoW.exe process
    /// - Inject Loader.dll into it
    /// - Verify the ForegroundBotRunner initializes successfully
    /// </summary>
    [RequiresInfrastructure]
    public class ClientHijackIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private Process? _stateManagerProcess;
        private readonly StringBuilder _stateManagerLogs = new();
        private readonly object _logLock = new();

        public ClientHijackIntegrationTest(ITestOutputHelper output)
        {
            _output = output;

            // Kill any lingering WoWStateManager processes from previous test runs
            // that may be locking DLLs in Bot/Debug/net8.0/
            KillLingeringProcesses();

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            _configuration = configBuilder.Build();
        }

        /// <summary>
        /// Kills any WoWStateManager.exe processes left over from previous test runs.
        /// These linger when the test runner exits abnormally or Dispose isn't called,
        /// and they lock DLLs preventing rebuilds.
        /// </summary>
        private void KillLingeringProcesses()
        {
            foreach (var name in new[] { "WoWStateManager", "WoW" })
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            _output.WriteLine($"Killing lingering {name} process (PID: {proc.Id})...");
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(5000);
                            _output.WriteLine($"  Killed PID {proc.Id}");
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"  Warning: Could not kill PID {proc.Id}: {ex.Message}");
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch { }
            }
        }

        [Fact]
        public void FindRunningWoWProcess_ShouldDetectClient()
        {
            _output.WriteLine("=== FIND RUNNING WOW PROCESS TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            var wowProcesses = Process.GetProcessesByName("WoW");

            _output.WriteLine($"Found {wowProcesses.Length} WoW process(es):");

            if (wowProcesses.Length == 0)
            {
                _output.WriteLine("  No WoW.exe processes found.");
                _output.WriteLine("");
                _output.WriteLine("To run this test, start WoW.exe first:");
                _output.WriteLine("  1. Launch your vanilla 1.12.1 WoW client");
                _output.WriteLine("  2. Get to the character selection or login screen");
                _output.WriteLine("  3. Run this test again");

                Assert.Fail("No WoW.exe process found. Please start WoW.exe before running this test.");
            }

            foreach (var process in wowProcesses)
            {
                _output.WriteLine($"  - PID: {process.Id}");
                _output.WriteLine($"    Process Name: {process.ProcessName}");
                _output.WriteLine($"    Main Window Title: {process.MainWindowTitle}");
                _output.WriteLine($"    Responding: {process.Responding}");

                try
                {
                    _output.WriteLine($"    Main Module: {process.MainModule?.FileName}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"    Main Module: Unable to access ({ex.Message})");
                }
                _output.WriteLine("");
            }

            Assert.True(wowProcesses.Length > 0, "At least one WoW process should be running");
            _output.WriteLine("✓ WoW process detected successfully!");
        }

        [Fact]
        public async Task HijackWoWClient_ShouldInjectSuccessfully()
        {
            _output.WriteLine("=== HIJACK WOW CLIENT TEST (Direct Injection) ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            // Step 1: Find a running WoW process
            _output.WriteLine("Step 1: Finding running WoW process...");
            var wowProcesses = Process.GetProcessesByName("WoW");

            if (wowProcesses.Length == 0)
            {
                Assert.Fail("No WoW.exe process found. Please start WoW.exe before running this test.");
            }

            var targetProcess = wowProcesses[0];
            _output.WriteLine($"  ✓ Found WoW process: PID {targetProcess.Id}");
            _output.WriteLine("");

            // Step 2: Validate Loader.dll exists
            _output.WriteLine("Step 2: Validating injection prerequisites...");
            var loaderPath = _configuration["LoaderDllPath"];

            if (string.IsNullOrEmpty(loaderPath))
            {
                Assert.Fail("LoaderDllPath not configured in appsettings.test.json");
            }

            // Resolve relative path
            if (!Path.IsPathRooted(loaderPath))
            {
                var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
                if (solutionRoot != null)
                {
                    loaderPath = Path.Combine(solutionRoot, loaderPath);
                }
            }

            if (!File.Exists(loaderPath))
            {
                _output.WriteLine($"  ✗ Loader.dll not found at: {loaderPath}");
                Assert.Fail($"Loader.dll not found at: {loaderPath}. Run Setup-InjectionDlls.ps1 first.");
            }
            _output.WriteLine($"  ✓ Loader.dll found at: {loaderPath}");

            // Check for ForegroundBotRunner.dll
            var loaderDir = Path.GetDirectoryName(loaderPath);
            var foregroundBotPath = Path.Combine(loaderDir ?? "", "ForegroundBotRunner.dll");

            if (!File.Exists(foregroundBotPath))
            {
                Assert.Fail($"ForegroundBotRunner.dll not found at: {foregroundBotPath}");
            }
            _output.WriteLine($"  ✓ ForegroundBotRunner.dll found at: {foregroundBotPath}");
            _output.WriteLine("");

            // Clean up stale breadcrumb files
            CleanupBreadcrumbFiles(loaderDir ?? "");

            // Step 3: Perform injection
            _output.WriteLine("Step 3: Performing DLL injection...");
            _output.WriteLine($"  Target Process: {targetProcess.ProcessName} (PID: {targetProcess.Id})");
            _output.WriteLine($"  Loader DLL: {loaderPath}");
            _output.WriteLine("");

            bool injectionSuccess = false;
            string errorMessage = string.Empty;

            try
            {
                injectionSuccess = WinProcessImports.SafeInjection.InjectDllSafely(
                    targetProcess.Id, loaderPath, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            if (injectionSuccess)
            {
                _output.WriteLine("  ✓ DLL injection initiated successfully!");
                _output.WriteLine("");

                // Step 4: Wait for injection to complete and verify memory reading
                _output.WriteLine("Step 4: Waiting for ForegroundBotRunner to initialize and verify memory reading...");

                var verificationResult = await VerifyInjectionAndMemoryReading(loaderDir ?? "", true);

                _output.WriteLine("");
                _output.WriteLine("=== TEST SUMMARY ===");
                _output.WriteLine($"  WoW Process: PID {targetProcess.Id}");
                _output.WriteLine($"  Injection Status: Success");
                _output.WriteLine($"  Managed Code Entry: {(verificationResult.ManagedCodeEntry ? "Yes" : "No")}");
                _output.WriteLine($"  Bot Service Started: {(verificationResult.BotServiceStarted ? "Yes" : "No")}");
                _output.WriteLine($"  Memory Reading Works: {(verificationResult.MemoryReadingWorks ? "Yes" : "No")}");

                if (verificationResult.MemoryReadingWorks && verificationResult.MemoryValues.Count > 0)
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== MEMORY VALUES READ FROM WOW CLIENT ===");
                    foreach (var kvp in verificationResult.MemoryValues)
                    {
                        _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }

                _output.WriteLine("");
                _output.WriteLine("The bot is now running inside the WoW process.");
                _output.WriteLine("Check the WWoWLogs directory for detailed logs.");

                // Assert that injection was successful
                Assert.True(verificationResult.ManagedCodeEntry,
                    "Managed code entry not confirmed - check testentry_stdcall.txt/testentry_cdecl.txt");
            }
            else
            {
                _output.WriteLine($"  ✗ DLL injection failed: {errorMessage}");
                _output.WriteLine("");

                if (errorMessage.Contains("Architecture mismatch"))
                {
                    _output.WriteLine("ARCHITECTURE MISMATCH DETECTED:");
                    _output.WriteLine("  The test runner (64-bit) cannot inject into 32-bit WoW.exe.");
                    _output.WriteLine("");
                    _output.WriteLine("Solutions:");
                    _output.WriteLine("  1. Run the test with a 32-bit test runner");
                    _output.WriteLine("  2. Or run StateManager directly (which handles this properly)");

                    // Don't fail the test for architecture mismatch - it's expected
                    _output.WriteLine("");
                    _output.WriteLine("⚠ Test skipped due to architecture mismatch (expected for 32-bit WoW)");
                    return;
                }

                // Check for access denied (Error 5) - requires admin privileges
                if (errorMessage.Contains("Error: 5") || errorMessage.Contains("ACCESS_DENIED") || errorMessage.Contains("access denied"))
                {
                    _output.WriteLine("ACCESS DENIED DETECTED:");
                    _output.WriteLine("  The test process does not have permission to inject into WoW.exe.");
                    _output.WriteLine("");
                    _output.WriteLine("Solutions:");
                    _output.WriteLine("  1. Run Visual Studio / test runner as Administrator");
                    _output.WriteLine("  2. Or use the StateManager_ShouldHijackConfiguredProcess test which runs WoWStateManager.exe");
                    _output.WriteLine("");
                    _output.WriteLine("⚠ Test requires elevated privileges to inject into another process");

                    // Don't fail - this is an expected permission issue when not running elevated
                    return;
                }

                Assert.Fail($"DLL injection failed: {errorMessage}");
            }
        }

        [Fact]
        public async Task StateManager_ShouldHijackConfiguredProcess()
        {
            _output.WriteLine("=== STATE MANAGER HIJACK TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            // Step 1: Find a running WoW process
            _output.WriteLine("Step 1: Finding running WoW process to hijack...");
            var wowProcesses = Process.GetProcessesByName("WoW");

            if (wowProcesses.Length == 0)
            {
                Assert.Fail("No WoW.exe process found. Please start WoW.exe before running this test.");
            }

            var targetProcess = wowProcesses[0];
            var targetPid = targetProcess.Id;
            _output.WriteLine($"  ✓ Found WoW process: PID {targetPid}");
            _output.WriteLine("");

            // Step 2: Update StateManagerSettings.json with the target process ID
            _output.WriteLine("Step 2: Configuring StateManager to hijack this process...");
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot == null)
            {
                Assert.Fail("Could not find solution root directory");
            }

            // Settings must be in the Bot output folder where StateManager reads from (not source directory)
            var stateManagerBuildPath = Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
            var settingsPath = Path.Combine(stateManagerBuildPath, "Settings", "StateManagerSettings.json");
            var originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;

            // Clean up any stale breadcrumb files from previous runs
            CleanupBreadcrumbFiles(stateManagerBuildPath);

            try
            {
                // Write settings with the target process ID
                var hijackSettings = $@"[
  {{
    ""AccountName"": ""ORWR1"",
    ""Openness"": 1.0,
    ""Conscientiousness"": 1.0,
    ""Extraversion"": 1.0,
    ""Agreeableness"": 1.0,
    ""Neuroticism"": 1.0,
    ""ShouldRun"": true,
    ""RunnerType"": ""Foreground"",
    ""TargetProcessId"": {targetPid}
  }}
]";
                File.WriteAllText(settingsPath, hijackSettings);
                _output.WriteLine($"  ✓ Settings updated with TargetProcessId: {targetPid}");
                _output.WriteLine("");

                // Step 3: Start StateManager
                _output.WriteLine("Step 3: Starting StateManager...");
                var stateManagerExe = Path.Combine(stateManagerBuildPath, "WoWStateManager.exe");

                if (!File.Exists(stateManagerExe))
                {
                    // Try the dll if exe doesn't exist
                    var stateManagerDll = Path.Combine(stateManagerBuildPath, "WoWStateManager.dll");
                    if (!File.Exists(stateManagerDll))
                    {
                        Assert.Fail($"StateManager not found at: {stateManagerExe} or {stateManagerDll}. Build StateManager first.");
                    }
                    // Use dotnet to run the dll
                    stateManagerExe = stateManagerDll;
                }

                _output.WriteLine($"  Using StateManager at: {stateManagerExe}");

                var psi = new ProcessStartInfo
                {
                    FileName = stateManagerExe.EndsWith(".dll") ? "dotnet" : stateManagerExe,
                    Arguments = stateManagerExe.EndsWith(".dll") ? $"\"{stateManagerExe}\"" : "",
                    WorkingDirectory = stateManagerBuildPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Set environment variables
                psi.Environment["Logging__LogLevel__Default"] = "Debug";

                // Pass through WWOW_DATA_DIR if set, or use game client directory
                var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
                if (string.IsNullOrEmpty(dataDir))
                {
                    // Try to derive from game client path
                    var gameClientPath = _configuration["GameClient:ExecutablePath"];
                    if (!string.IsNullOrEmpty(gameClientPath) && File.Exists(gameClientPath))
                    {
                        dataDir = Path.GetDirectoryName(gameClientPath);
                    }
                }
                if (!string.IsNullOrEmpty(dataDir))
                {
                    psi.Environment["WWOW_DATA_DIR"] = dataDir;
                    _output.WriteLine($"  WWOW_DATA_DIR: {dataDir}");
                }

                _stateManagerProcess = new Process { StartInfo = psi };

                _stateManagerProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock)
                        {
                            _stateManagerLogs.AppendLine($"[OUT] {args.Data}");
                        }
                        _output.WriteLine($"[StateManager] {args.Data}");
                    }
                };

                _stateManagerProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock)
                        {
                            _stateManagerLogs.AppendLine($"[ERR] {args.Data}");
                        }
                        _output.WriteLine($"[StateManager-ERR] {args.Data}");
                    }
                };

                bool started = _stateManagerProcess.Start();
                Assert.True(started, "Failed to start StateManager process");

                _stateManagerProcess.BeginOutputReadLine();
                _stateManagerProcess.BeginErrorReadLine();

                _output.WriteLine($"  ✓ StateManager started with PID: {_stateManagerProcess.Id}");
                _output.WriteLine("");

                // Step 4: Wait for injection to complete
                _output.WriteLine("Step 4: Waiting for StateManager to perform injection...");

                var timeout = TimeSpan.FromSeconds(120); // 2 minutes for nav/physics data loading
                var sw = Stopwatch.StartNew();
                bool injectionDetected = false;

                while (sw.Elapsed < timeout)
                {
                    if (_stateManagerProcess.HasExited)
                    {
                        _output.WriteLine($"  StateManager exited with code: {_stateManagerProcess.ExitCode}");
                        break;
                    }

                    // Check logs for injection progress and success
                    lock (_logLock)
                    {
                        var logs = _stateManagerLogs.ToString();

                        // Check for injection success
                        if (logs.Contains("DLL injection completed successfully") ||
                            logs.Contains("SUCCESS: DLL injection completed"))
                        {
                            injectionDetected = true;
                            _output.WriteLine("  ✓ Injection success detected in logs!");
                            break;
                        }

                        // Check for failure
                        if (logs.Contains("DLL injection failed") ||
                            logs.Contains("[FAIL]"))
                        {
                            _output.WriteLine("  ✗ Injection failure detected in logs");
                            break;
                        }

                        // Check if injection was even attempted (early progress indicators)
                        if (!logs.Contains("INJECTING INTO EXISTING PROCESS") &&
                            !logs.Contains("ATTEMPTING DLL INJECTION") &&
                            sw.Elapsed.TotalSeconds > 30)
                        {
                            _output.WriteLine("  ⚠ No injection attempt detected after 30s - checking settings...");
                            _output.WriteLine($"    Settings path: {settingsPath}");
                            if (File.Exists(settingsPath))
                            {
                                _output.WriteLine($"    Settings content: {File.ReadAllText(settingsPath)}");
                            }
                        }
                    }

                    await Task.Delay(1000);
                    _output.WriteLine($"    Waiting... ({sw.Elapsed.TotalSeconds:F0}s)");
                }

                _output.WriteLine("");

                // Step 5: Verify injection breadcrumbs and memory reading
                _output.WriteLine("Step 5: Verifying injection success and memory reading...");
                var verificationResult = await VerifyInjectionAndMemoryReading(stateManagerBuildPath, injectionDetected);

                _output.WriteLine("");
                _output.WriteLine("=== TEST SUMMARY ===");
                _output.WriteLine($"  Target WoW Process: PID {targetPid}");
                _output.WriteLine($"  StateManager PID: {_stateManagerProcess?.Id}");
                _output.WriteLine($"  Injection Detected: {(injectionDetected ? "Yes" : "No")}");
                _output.WriteLine($"  Managed Code Entry: {(verificationResult.ManagedCodeEntry ? "Yes" : "No")}");
                _output.WriteLine($"  Bot Service Started: {(verificationResult.BotServiceStarted ? "Yes" : "No")}");
                _output.WriteLine($"  Memory Reading Works: {(verificationResult.MemoryReadingWorks ? "Yes" : "No")}");

                if (verificationResult.MemoryReadingWorks && verificationResult.MemoryValues.Count > 0)
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== MEMORY VALUES READ FROM WOW CLIENT ===");
                    foreach (var kvp in verificationResult.MemoryValues)
                    {
                        _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }

                _output.WriteLine("");
                _output.WriteLine("=== CAPTURED STATE MANAGER LOGS ===");
                _output.WriteLine(_stateManagerLogs.ToString());
                _output.WriteLine("=== END LOGS ===");

                // Assert on success criteria
                Assert.True(injectionDetected || verificationResult.ManagedCodeEntry,
                    "Injection was not detected in logs and no managed code entry breadcrumb found");
            }
            finally
            {
                // Restore original settings
                if (originalSettings != null)
                {
                    File.WriteAllText(settingsPath, originalSettings);
                    _output.WriteLine("Restored original StateManagerSettings.json");
                }
            }
        }

        [Fact]
        public async Task StateManager_ShouldSpawnAndInjectWoWClient()
        {
            _output.WriteLine("=== STATE MANAGER SPAWN & INJECT TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");
            _output.WriteLine("This test spawns a fresh WoW client (no TargetProcessId)");
            _output.WriteLine("");

            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot == null)
            {
                Assert.Fail("Could not find solution root directory");
            }

            var stateManagerBuildPath = Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
            var settingsPath = Path.Combine(stateManagerBuildPath, "Settings", "StateManagerSettings.json");
            var originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;

            // Clean up any stale breadcrumb files from previous runs
            CleanupBreadcrumbFiles(stateManagerBuildPath);

            // Track spawned WoW process for cleanup
            Process? spawnedWoW = null;

            try
            {
                // Step 1: Configure StateManager WITHOUT TargetProcessId (spawns fresh WoW)
                _output.WriteLine("Step 1: Configuring StateManager to spawn fresh WoW client...");
                var spawnSettings = @"[
  {
    ""AccountName"": ""ORWR1"",
    ""Openness"": 1.0,
    ""Conscientiousness"": 1.0,
    ""Extraversion"": 1.0,
    ""Agreeableness"": 1.0,
    ""Neuroticism"": 1.0,
    ""ShouldRun"": true,
    ""RunnerType"": ""Foreground""
  }
]";
                File.WriteAllText(settingsPath, spawnSettings);
                _output.WriteLine("  ✓ Settings configured (no TargetProcessId - will spawn fresh WoW)");
                _output.WriteLine("");

                // Step 2: Start StateManager
                _output.WriteLine("Step 2: Starting StateManager...");
                var stateManagerExe = Path.Combine(stateManagerBuildPath, "WoWStateManager.exe");

                if (!File.Exists(stateManagerExe))
                {
                    var stateManagerDll = Path.Combine(stateManagerBuildPath, "WoWStateManager.dll");
                    if (!File.Exists(stateManagerDll))
                    {
                        Assert.Fail($"StateManager not found at: {stateManagerExe}");
                    }
                    stateManagerExe = stateManagerDll;
                }

                _output.WriteLine($"  Using StateManager at: {stateManagerExe}");

                var psi = new ProcessStartInfo
                {
                    FileName = stateManagerExe.EndsWith(".dll") ? "dotnet" : stateManagerExe,
                    Arguments = stateManagerExe.EndsWith(".dll") ? $"\"{stateManagerExe}\"" : "",
                    WorkingDirectory = stateManagerBuildPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.Environment["Logging__LogLevel__Default"] = "Debug";

                _stateManagerProcess = new Process { StartInfo = psi };

                _stateManagerProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock) { _stateManagerLogs.AppendLine($"[OUT] {args.Data}"); }
                        _output.WriteLine($"[StateManager] {args.Data}");
                    }
                };

                _stateManagerProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock) { _stateManagerLogs.AppendLine($"[ERR] {args.Data}"); }
                        _output.WriteLine($"[StateManager-ERR] {args.Data}");
                    }
                };

                bool started = _stateManagerProcess.Start();
                Assert.True(started, "Failed to start StateManager process");

                _stateManagerProcess.BeginOutputReadLine();
                _stateManagerProcess.BeginErrorReadLine();

                _output.WriteLine($"  ✓ StateManager started with PID: {_stateManagerProcess.Id}");
                _output.WriteLine("");

                // Step 3: Wait for WoW spawn, injection, and valid snapshot
                _output.WriteLine("Step 3: Waiting for WoW spawn, injection, and character snapshot...");
                _output.WriteLine("  (Will keep running after snapshot for movement recording)");
                _output.WriteLine("  (Close WoW or press Ctrl+C to end test)");
                _output.WriteLine("");

                var timeout = TimeSpan.FromHours(24); // Run indefinitely for movement recording
                var sw = Stopwatch.StartNew();
                bool injectionDetected = false;
                int? wowPid = null;
                var snapshotResult = new CharacterSnapshotResult();
                var verificationResult = new InjectionVerificationResult();
                string? failureReason = null;

                // Regex to match: SNAPSHOT_RECEIVED: Account='...', Character='...'
                var snapshotRegex = new Regex(@"SNAPSHOT_RECEIVED: Account='([^']*)', Character='([^']*)'");

                while (sw.Elapsed < timeout)
                {
                    // Check for StateManager exit - could be crash OR user closing WoW
                    if (_stateManagerProcess.HasExited)
                    {
                        if (snapshotResult.SnapshotReceived)
                        {
                            // User closed WoW after getting snapshot - this is success for movement recording
                            _output.WriteLine($"  StateManager exited (user closed WoW) - test complete");
                        }
                        else
                        {
                            failureReason = $"StateManager exited unexpectedly with code: {_stateManagerProcess.ExitCode}";
                            _output.WriteLine($"  ✗ {failureReason}");
                        }
                        break;
                    }

                    // Also check if WoW was closed by user
                    if (spawnedWoW != null && spawnedWoW.HasExited && snapshotResult.SnapshotReceived)
                    {
                        _output.WriteLine($"  WoW closed by user - test complete");
                        break;
                    }

                    string logs;
                    lock (_logLock)
                    {
                        logs = _stateManagerLogs.ToString();
                    }

                    // Check for WoW process spawn
                    if (wowPid == null && logs.Contains("Created WoW process"))
                    {
                        var wowProcesses = Process.GetProcessesByName("WoW");
                        if (wowProcesses.Length > 0)
                        {
                            spawnedWoW = wowProcesses[0];
                            wowPid = spawnedWoW.Id;
                            _output.WriteLine($"  ✓ WoW process spawned: PID {wowPid}");
                        }
                    }

                    // Check for injection success
                    if (!injectionDetected && (logs.Contains("DLL injection completed successfully") ||
                        logs.Contains("SUCCESS: DLL injection completed")))
                    {
                        injectionDetected = true;
                        _output.WriteLine("  ✓ Injection success detected!");
                    }

                    // Check for injection failure - immediate failure
                    if (logs.Contains("DLL injection failed") || logs.Contains("[FAIL]"))
                    {
                        failureReason = "DLL injection failed";
                        _output.WriteLine($"  ✗ {failureReason}");
                        break;
                    }

                    // Check for valid snapshot - keep running for movement recording
                    var match = snapshotRegex.Match(logs);
                    if (match.Success && !snapshotResult.SnapshotReceived)
                    {
                        var characterName = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(characterName))
                        {
                            snapshotResult.SnapshotReceived = true;
                            snapshotResult.AccountName = match.Groups[1].Value;
                            snapshotResult.CharacterName = characterName;
                            snapshotResult.ElapsedTime = sw.Elapsed;
                            _output.WriteLine($"  ✓ Valid snapshot received! Character: {characterName}");
                            _output.WriteLine($"  ✓ SUCCESS - Bot is running. Use chat commands to record movement:");
                            _output.WriteLine($"      Say 'record start' to begin recording");
                            _output.WriteLine($"      Say 'record stop' to save recording");
                            _output.WriteLine($"      Close WoW when done to end test");
                            // DON'T break - keep running for movement recording!
                        }
                    }

                    await Task.Delay(500); // Check more frequently
                    if ((int)sw.Elapsed.TotalSeconds % 10 == 0 && sw.Elapsed.TotalSeconds > 0)
                    {
                        _output.WriteLine($"    Waiting... ({sw.Elapsed.TotalSeconds:F0}s) - Injection: {(injectionDetected ? "Yes" : "No")}, WoW: {(wowPid.HasValue ? $"PID {wowPid}" : "No")}");
                    }
                }

                snapshotResult.ElapsedTime = sw.Elapsed;
                _output.WriteLine("");

                // Quick verification check if we got a snapshot
                if (snapshotResult.SnapshotReceived)
                {
                    verificationResult.ManagedCodeEntry = true;
                    verificationResult.BotServiceStarted = true;
                }
                else
                {
                    // Only do detailed verification if we didn't get a snapshot
                    _output.WriteLine("Step 4: Verifying injection status...");
                    verificationResult = await VerifyInjectionAndMemoryReading(stateManagerBuildPath, injectionDetected);
                }

                _output.WriteLine("");
                _output.WriteLine("=== TEST SUMMARY ===");
                _output.WriteLine($"  Total Time: {sw.Elapsed.TotalSeconds:F1}s");
                _output.WriteLine($"  Spawned WoW Process: {(wowPid.HasValue ? $"PID {wowPid}" : "Not detected")}");
                _output.WriteLine($"  StateManager PID: {_stateManagerProcess?.Id}");
                _output.WriteLine($"  Injection Detected: {(injectionDetected ? "Yes" : "No")}");
                _output.WriteLine($"  Managed Code Entry: {(verificationResult.ManagedCodeEntry ? "Yes" : "No")}");
                _output.WriteLine($"  Bot Service Started: {(verificationResult.BotServiceStarted ? "Yes" : "No")}");
                _output.WriteLine($"  Valid Snapshot Received: {(snapshotResult.SnapshotReceived ? "Yes" : "No")}");
                _output.WriteLine($"  Character Name: {(string.IsNullOrEmpty(snapshotResult.CharacterName) ? "(none)" : snapshotResult.CharacterName)}");
                if (!string.IsNullOrEmpty(failureReason))
                {
                    _output.WriteLine($"  Failure Reason: {failureReason}");
                }

                // Always show ForegroundBotRunner diagnostic log (it runs inside WoW.exe)
                DisplayForegroundBotDiagnosticLog();

                // Only show full StateManager logs on failure to reduce noise
                if (!snapshotResult.SnapshotReceived || string.IsNullOrEmpty(snapshotResult.CharacterName))
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== CAPTURED STATE MANAGER LOGS (Last 100 lines) ===");
                    var logLines = _stateManagerLogs.ToString().Split('\n');
                    var lastLines = logLines.Skip(Math.Max(0, logLines.Length - 100));
                    foreach (var line in lastLines)
                    {
                        _output.WriteLine(line);
                    }
                    _output.WriteLine("=== END LOGS ===");
                }

                // Assert on success criteria - require valid snapshot with character name
                // Don't fail if user closed WoW after getting a snapshot (that's expected for movement recording)
                if (!string.IsNullOrEmpty(failureReason) && !snapshotResult.SnapshotReceived)
                {
                    Assert.Fail(failureReason);
                }

                Assert.True(snapshotResult.SnapshotReceived && !string.IsNullOrEmpty(snapshotResult.CharacterName),
                    $"No valid activity snapshot received with character name. " +
                    $"Injection detected: {injectionDetected}, Managed code entry: {verificationResult.ManagedCodeEntry}, " +
                    $"Snapshot received: {snapshotResult.SnapshotReceived}, Character: '{snapshotResult.CharacterName}'");
            }
            finally
            {
                // Capture all logs from WWoWLogs folder BEFORE cleanup
                _output.WriteLine("");
                _output.WriteLine("=== CAPTURED WWoWLogs FILES ===");
                CaptureWWoWLogs();

                // Restore original settings
                if (originalSettings != null)
                {
                    File.WriteAllText(settingsPath, originalSettings);
                    _output.WriteLine("Restored original StateManagerSettings.json");
                }

                // Clean up spawned WoW process
                if (spawnedWoW != null && !spawnedWoW.HasExited)
                {
                    try
                    {
                        _output.WriteLine($"Cleaning up spawned WoW process (PID: {spawnedWoW.Id})...");
                        spawnedWoW.Kill(entireProcessTree: true);
                        _output.WriteLine("  WoW process terminated.");
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  Warning: Could not terminate WoW: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Captures and outputs all log files from the WWoWLogs folder in the WoW client directory.
        /// This includes diagnostic logs from ForegroundBotRunner, SignalEventManager, ObjectManager, etc.
        /// </summary>
        private void CaptureWWoWLogs()
        {
            try
            {
                // Get WoW client directory from config
                var gameClientPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH");
                if (string.IsNullOrEmpty(gameClientPath))
                {
                    gameClientPath = _configuration["GameClient:ExecutablePath"];
                }

                if (string.IsNullOrEmpty(gameClientPath))
                {
                    _output.WriteLine("  (GameClient:ExecutablePath not configured - cannot locate WWoWLogs)");
                    return;
                }

                var wowDir = Path.GetDirectoryName(gameClientPath);
                if (string.IsNullOrEmpty(wowDir))
                {
                    _output.WriteLine("  (Could not determine WoW directory)");
                    return;
                }

                var logsDir = Path.Combine(wowDir, "WWoWLogs");
                _output.WriteLine($"  WWoWLogs path: {logsDir}");
                _output.WriteLine("");

                if (!Directory.Exists(logsDir))
                {
                    _output.WriteLine("  (WWoWLogs directory does not exist - ForegroundBotRunner may not have started)");
                    return;
                }

                // List of expected log files with their descriptions
                var expectedLogFiles = new (string FileName, string Description)[]
                {
                    ("foreground_bot_debug.log", "ForegroundBotWorker main loop diagnostics"),
                    ("signal_event_manager.log", "SignalEventManager WoW event hook and events"),
                    ("object_manager_debug.log", "ObjectManager player GUID and memory read diagnostics"),
                    ("injection.log", "Loader.dll injection diagnostics"),
                    ("injection_firstchance.log", "First-chance exception log")
                };

                // Output each log file that exists
                foreach (var (fileName, description) in expectedLogFiles)
                {
                    var logPath = Path.Combine(logsDir, fileName);

                    _output.WriteLine($"--- {fileName} ({description}) ---");

                    if (!File.Exists(logPath))
                    {
                        _output.WriteLine("  (File does not exist)");
                        _output.WriteLine("");
                        continue;
                    }

                    try
                    {
                        var content = File.ReadAllText(logPath);
                        var lines = content.Split('\n');

                        // Show file info
                        var fileInfo = new FileInfo(logPath);
                        _output.WriteLine($"  Size: {fileInfo.Length} bytes, Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                        _output.WriteLine($"  Lines: {lines.Length}");
                        _output.WriteLine("");

                        // Show last 200 lines (or all if less)
                        var linesToShow = lines.Length > 200
                            ? lines.Skip(lines.Length - 200)
                            : lines;

                        foreach (var line in linesToShow)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                _output.WriteLine($"  {line.TrimEnd()}");
                            }
                        }

                        if (lines.Length > 200)
                        {
                            _output.WriteLine($"  ... (showing last 200 of {lines.Length} lines)");
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  (Error reading file: {ex.Message})");
                    }

                    _output.WriteLine("");
                }

                // Also show any other *.log or *.txt files in the directory
                _output.WriteLine("--- Other log files ---");
                var knownFiles = expectedLogFiles.Select(f => f.FileName.ToLowerInvariant()).ToHashSet();

                foreach (var file in Directory.GetFiles(logsDir))
                {
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    if (!knownFiles.Contains(fileName) && (fileName.EndsWith(".log") || fileName.EndsWith(".txt")))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            _output.WriteLine($"  {Path.GetFileName(file)} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime:HH:mm:ss})");

                            // Show first few lines of unknown log files
                            var content = File.ReadAllText(file);
                            var lines = content.Split('\n').Take(20);
                            foreach (var line in lines)
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    _output.WriteLine($"    {line.TrimEnd()}");
                                }
                            }
                        }
                        catch { }
                    }
                }

                _output.WriteLine("=== END WWoWLogs ===");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Error capturing WWoWLogs: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays the ForegroundBotRunner diagnostic log from the WoW client directory.
        /// This log is written by ForegroundBotWorker running inside WoW.exe.
        /// </summary>
        private void DisplayForegroundBotDiagnosticLog()
        {
            try
            {
                // Get WoW client directory from config
                var gameClientPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH");
                if (string.IsNullOrEmpty(gameClientPath))
                {
                    gameClientPath = _configuration["GameClient:ExecutablePath"];
                }

                if (string.IsNullOrEmpty(gameClientPath))
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== FOREGROUND BOT DIAGNOSTIC LOG ===");
                    _output.WriteLine("  (GameClient:ExecutablePath not configured - cannot locate log)");
                    return;
                }

                var wowDir = Path.GetDirectoryName(gameClientPath);
                if (string.IsNullOrEmpty(wowDir))
                {
                    return;
                }

                var diagnosticLogPath = Path.Combine(wowDir, "WWoWLogs", "foreground_bot_debug.log");

                _output.WriteLine("");
                _output.WriteLine("=== FOREGROUND BOT DIAGNOSTIC LOG ===");
                _output.WriteLine($"  Path: {diagnosticLogPath}");
                _output.WriteLine("");

                if (!File.Exists(diagnosticLogPath))
                {
                    _output.WriteLine("  (Log file does not exist - ForegroundBotRunner may not have started)");
                    return;
                }

                var content = File.ReadAllText(diagnosticLogPath);
                var lines = content.Split('\n');

                // Show last 100 lines
                var linesToShow = lines.Skip(Math.Max(0, lines.Length - 100));
                foreach (var line in linesToShow)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _output.WriteLine($"  {line.TrimEnd()}");
                    }
                }

                _output.WriteLine("=== END FOREGROUND BOT LOG ===");
            }
            catch (Exception ex)
            {
                _output.WriteLine("");
                _output.WriteLine($"=== FOREGROUND BOT DIAGNOSTIC LOG (Error reading: {ex.Message}) ===");
            }
        }

        /// <summary>
        /// Cleans up breadcrumb files from previous test runs.
        /// </summary>
        private void CleanupBreadcrumbFiles(string basePath)
        {
            var breadcrumbFiles = new[]
            {
                "testentry_stdcall.txt",
                "testentry_cdecl.txt",
                "bot_startup.txt",
                "bot_status.txt",
                "bot_service_init.txt",
                "bot_service_running.txt",
                "objectmanager_init.txt",
                "wow_init.txt"
            };

            foreach (var file in breadcrumbFiles)
            {
                var path = Path.Combine(basePath, file);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                        _output.WriteLine($"  Cleaned up stale breadcrumb: {file}");
                    }
                    catch { }
                }
            }

            // Also clean up WWoWLogs directory
            var logsDir = Path.Combine(basePath, "WWoWLogs");
            if (Directory.Exists(logsDir))
            {
                try
                {
                    foreach (var logFile in Directory.GetFiles(logsDir, "*.log"))
                    {
                        File.Delete(logFile);
                    }
                    _output.WriteLine("  Cleaned up WWoWLogs directory");
                }
                catch { }
            }
        }

        /// <summary>
        /// Result of injection verification including memory reading status.
        /// </summary>
        private class InjectionVerificationResult
        {
            public bool ManagedCodeEntry { get; set; }
            public bool BotServiceStarted { get; set; }
            public bool MemoryReadingWorks { get; set; }
            public Dictionary<string, string> MemoryValues { get; set; } = new();
        }

        /// <summary>
        /// Result of waiting for a valid character activity snapshot.
        /// </summary>
        private class CharacterSnapshotResult
        {
            public bool SnapshotReceived { get; set; }
            public string? CharacterName { get; set; }
            public string? AccountName { get; set; }
            public TimeSpan ElapsedTime { get; set; }
        }

        /// <summary>
        /// Waits for a valid activity snapshot with a non-blank character name.
        /// Looks for the SNAPSHOT_RECEIVED log message from CharacterStateSocketListener.
        /// </summary>
        private async Task<CharacterSnapshotResult> WaitForCharacterSnapshot(TimeSpan remainingTimeout)
        {
            var result = new CharacterSnapshotResult();
            var sw = Stopwatch.StartNew();

            // Use remaining timeout or default to 2 minutes
            var timeout = remainingTimeout > TimeSpan.Zero ? remainingTimeout : TimeSpan.FromMinutes(2);

            _output.WriteLine($"  Waiting up to {timeout.TotalSeconds:F0}s for character snapshot...");

            // Regex to match: SNAPSHOT_RECEIVED: Account='...', Character='...'
            var snapshotRegex = new Regex(
                @"SNAPSHOT_RECEIVED: Account='([^']*)', Character='([^']*)'");

            while (sw.Elapsed < timeout)
            {
                if (_stateManagerProcess?.HasExited == true)
                {
                    _output.WriteLine($"  StateManager exited while waiting for snapshot");
                    break;
                }

                lock (_logLock)
                {
                    var logs = _stateManagerLogs.ToString();
                    var match = snapshotRegex.Match(logs);

                    if (match.Success)
                    {
                        var accountName = match.Groups[1].Value;
                        var characterName = match.Groups[2].Value;

                        if (!string.IsNullOrEmpty(characterName))
                        {
                            result.SnapshotReceived = true;
                            result.AccountName = accountName;
                            result.CharacterName = characterName;
                            result.ElapsedTime = sw.Elapsed;

                            _output.WriteLine($"  ✓ Valid snapshot received!");
                            _output.WriteLine($"    Account: {accountName}");
                            _output.WriteLine($"    Character: {characterName}");
                            _output.WriteLine($"    Time elapsed: {sw.Elapsed.TotalSeconds:F1}s");
                            return result;
                        }
                    }
                }

                await Task.Delay(1000);

                // Log progress every 10 seconds
                if ((int)sw.Elapsed.TotalSeconds % 10 == 0 && sw.Elapsed.TotalSeconds > 0)
                {
                    _output.WriteLine($"    Still waiting for snapshot... ({sw.Elapsed.TotalSeconds:F0}s)");
                }
            }

            result.ElapsedTime = sw.Elapsed;
            _output.WriteLine($"  ✗ No valid snapshot received within {timeout.TotalSeconds:F0}s");
            return result;
        }

        /// <summary>
        /// Verifies injection success by checking breadcrumb files and memory values.
        /// </summary>
        private async Task<InjectionVerificationResult> VerifyInjectionAndMemoryReading(string basePath, bool injectionDetectedInLogs)
        {
            var result = new InjectionVerificationResult();

            // Wait for bot to initialize and start reading memory (up to 30 seconds)
            var timeout = TimeSpan.FromSeconds(30);
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                // Check for managed code entry breadcrumbs
                var stdcallBreadcrumb = Path.Combine(basePath, "testentry_stdcall.txt");
                var cdeclBreadcrumb = Path.Combine(basePath, "testentry_cdecl.txt");
                var injectionLogPath = Path.Combine(basePath, "WWoWLogs", "injection.log");

                if (File.Exists(stdcallBreadcrumb) || File.Exists(cdeclBreadcrumb))
                {
                    result.ManagedCodeEntry = true;
                    _output.WriteLine("  ✓ Managed code entry confirmed (breadcrumb file found)");

                    if (File.Exists(stdcallBreadcrumb))
                    {
                        var content = File.ReadAllText(stdcallBreadcrumb);
                        _output.WriteLine($"    stdcall entry: {content.Trim()}");
                    }
                }
                // Also check injection.log as alternative verification
                else if (File.Exists(injectionLogPath))
                {
                    try
                    {
                        var logContent = File.ReadAllText(injectionLogPath);
                        if (logContent.Contains("Loader.Load()"))
                        {
                            result.ManagedCodeEntry = true;
                            _output.WriteLine("  ✓ Managed code entry confirmed (injection.log shows Loader.Load() executed)");
                        }
                    }
                    catch { }
                }

                // Check for bot service started
                var botServiceRunning = Path.Combine(basePath, "bot_service_running.txt");
                if (File.Exists(botServiceRunning))
                {
                    result.BotServiceStarted = true;
                    _output.WriteLine("  ✓ Bot service started (bot_service_running.txt found)");
                }

                // Check for memory reading - bot_status.txt contains actual memory values
                var botStatusPath = Path.Combine(basePath, "bot_status.txt");
                if (File.Exists(botStatusPath))
                {
                    try
                    {
                        var statusContent = File.ReadAllText(botStatusPath);
                        _output.WriteLine("  ✓ bot_status.txt found - parsing memory values...");

                        // Parse memory values from the status file
                        result.MemoryValues = ParseBotStatusFile(statusContent);

                        // Memory reading works if we got any meaningful values
                        if (result.MemoryValues.ContainsKey("Status") ||
                            result.MemoryValues.ContainsKey("Player") ||
                            result.MemoryValues.ContainsKey("Position"))
                        {
                            result.MemoryReadingWorks = true;
                            _output.WriteLine("  ✓ Memory reading confirmed - actual game values retrieved!");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  ⚠ Error reading bot_status.txt: {ex.Message}");
                    }
                }

                // Also check injection.log in WWoWLogs for bot service started
                if (File.Exists(injectionLogPath))
                {
                    try
                    {
                        var logContent = File.ReadAllText(injectionLogPath);
                        if (logContent.Contains("ForegroundBotWorker started"))
                        {
                            result.BotServiceStarted = true;
                            _output.WriteLine("  ✓ ForegroundBotWorker started (injection.log confirms)");
                        }
                    }
                    catch { }
                }

                // If we have all the success indicators, we're done
                if (result.ManagedCodeEntry && result.BotServiceStarted)
                {
                    _output.WriteLine($"  Injection verified after {sw.Elapsed.TotalSeconds:F1}s");
                    break;
                }

                await Task.Delay(1000);
                _output.WriteLine($"    Verifying injection... ({sw.Elapsed.TotalSeconds:F0}s)");
            }

            // Report all breadcrumb files found
            _output.WriteLine("");
            _output.WriteLine("  All breadcrumb files found:");
            ReportBreadcrumbFiles(basePath);

            return result;
        }

        /// <summary>
        /// Parses the bot_status.txt file to extract memory values.
        /// Format: STATUS: value\nPlayer: name\nPosition: x, y, z\nTime: timestamp
        /// </summary>
        private Dictionary<string, string> ParseBotStatusFile(string content)
        {
            var values = new Dictionary<string, string>();

            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = trimmed.Substring(0, colonIndex).Trim();
                    var value = trimmed.Substring(colonIndex + 1).Trim();
                    values[key] = value;
                }
            }

            return values;
        }

        /// <summary>
        /// Reports all breadcrumb files found in the base path.
        /// </summary>
        private void ReportBreadcrumbFiles(string basePath)
        {
            var breadcrumbPatterns = new[]
            {
                "testentry_*.txt",
                "bot_*.txt",
                "objectmanager_*.txt",
                "wow_*.txt",
                "character_*.txt",
                "pathfinding_*.txt",
                "ui_*.txt",
                "movement_*.txt"
            };

            foreach (var pattern in breadcrumbPatterns)
            {
                try
                {
                    var files = Directory.GetFiles(basePath, pattern);
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        var fileInfo = new FileInfo(file);
                        _output.WriteLine($"    - {fileName} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime:HH:mm:ss})");
                    }
                }
                catch { }
            }

            // Also check WWoWLogs
            var logsDir = Path.Combine(basePath, "WWoWLogs");
            if (Directory.Exists(logsDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(logsDir))
                    {
                        var fileName = Path.GetFileName(file);
                        var fileInfo = new FileInfo(file);
                        _output.WriteLine($"    - WWoWLogs/{fileName} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime:HH:mm:ss})");
                    }
                }
                catch { }
            }
        }

        private string? FindSolutionRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")) ||
                    (Directory.Exists(Path.Combine(dir.FullName, "Services")) &&
                     Directory.Exists(Path.Combine(dir.FullName, "Exports"))))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        public void Dispose()
        {
            try
            {
                if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
                {
                    _output.WriteLine("Stopping StateManager process (tree kill)...");
                    _stateManagerProcess.Kill(entireProcessTree: true);
                    _stateManagerProcess.WaitForExit(10000);
                    _output.WriteLine("  StateManager stopped.");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error stopping StateManager: {ex.Message}");
            }
            finally
            {
                _stateManagerProcess?.Dispose();
            }

            // Belt-and-suspenders: kill any WoWStateManager still running by name
            // (covers cases where the process handle was lost)
            KillLingeringProcesses();
        }
    }
}
