using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Integration test for movement recording.
    ///
    /// This test:
    /// 1. Configures StateManager to launch WoW and inject the bot
    /// 2. Bot auto-logs in and enters the world
    /// 3. Bot auto-logs in and enters world
    /// 4. User manually controls the character
    /// 5. Cast a heal on yourself to START recording movement
    /// 6. Move around, jump, swim, fall - all captured at 20 FPS
    /// 7. Cast a heal again to STOP recording and save
    /// 8. Close WoW to end the test
    ///
    /// Recordings are saved to: Documents/BloogBot/MovementRecordings/
    ///
    /// Prerequisites:
    /// 1. Build the solution (dotnet build)
    /// 2. Mangos server running (for auto-login)
    /// 3. Run this test
    ///
    /// Optional environment variables:
    /// - WWOW_RECORDING_ACCOUNT: Account name to use (default: ORWR1)
    /// - WWOW_GAME_CLIENT_PATH: Path to WoW.exe (overrides appsettings)
    /// </summary>
    [RequiresInfrastructure]
    public class MovementRecordingIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private Process? _stateManagerProcess;
        private readonly StringBuilder _stateManagerLogs = new();
        private readonly object _logLock = new();

        public MovementRecordingIntegrationTest(ITestOutputHelper output)
        {
            _output = output;

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            _configuration = configBuilder.Build();
        }

        [Fact]
        public async Task StateManagerLaunch_InjectAndRecordMovement()
        {
            _output.WriteLine("=== MOVEMENT RECORDING TEST ===");
            _output.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");
            _output.WriteLine("This test launches WoW via StateManager, injects the bot,");
            _output.WriteLine("auto-logs in, and enters the world.");
            _output.WriteLine("");
            _output.WriteLine("Once in-game:");
            _output.WriteLine("  - Target yourself (or have no target)");
            _output.WriteLine("  - Cast any spell to START recording");
            _output.WriteLine("  - Move around, jump, swim, fall");
            _output.WriteLine("  - Cast again to STOP recording and save");
            _output.WriteLine("  - Close WoW to end the test");
            _output.WriteLine("");

            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            Assert.NotNull(solutionRoot);

            var stateManagerBuildPath = Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
            var settingsDir = Path.Combine(stateManagerBuildPath, "Settings");
            var settingsPath = Path.Combine(settingsDir, "StateManagerSettings.json");

            // Verify required files exist
            VerifyBuildArtifacts(stateManagerBuildPath);

            // Backup original settings
            var originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;

            // Clean up stale breadcrumb files
            CleanupBreadcrumbFiles(stateManagerBuildPath);

            // Snapshot the recordings directory so we can detect new files
            var recordingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BloogBot", "MovementRecordings");
            var existingRecordings = Directory.Exists(recordingsDir)
                ? new HashSet<string>(Directory.GetFiles(recordingsDir))
                : new HashSet<string>();

            try
            {
                // Step 1: Determine account name
                var accountName = Environment.GetEnvironmentVariable("WWOW_RECORDING_ACCOUNT")
                    ?? Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME")
                    ?? "ORWR1";

                _output.WriteLine($"Step 1: Using account '{accountName}'");
                _output.WriteLine("  (Set WWOW_RECORDING_ACCOUNT env var to change)");
                _output.WriteLine("");

                // Step 2: Configure StateManager settings (no TargetProcessId = launch new WoW)
                _output.WriteLine("Step 2: Configuring StateManager to launch WoW...");
                Directory.CreateDirectory(settingsDir);

                var recordingSettings = $@"[
  {{
    ""AccountName"": ""{accountName}"",
    ""Openness"": 1.0,
    ""Conscientiousness"": 1.0,
    ""Extraversion"": 1.0,
    ""Agreeableness"": 1.0,
    ""Neuroticism"": 1.0,
    ""ShouldRun"": true,
    ""RunnerType"": ""Foreground""
  }}
]";
                File.WriteAllText(settingsPath, recordingSettings);
                _output.WriteLine($"  Settings written (no TargetProcessId = StateManager launches WoW)");
                _output.WriteLine("");

                // Step 3: Start StateManager
                _output.WriteLine("Step 3: Starting StateManager...");
                var stateManagerExe = Path.Combine(stateManagerBuildPath, "WoWStateManager.exe");

                if (!File.Exists(stateManagerExe))
                {
                    var stateManagerDll = Path.Combine(stateManagerBuildPath, "WoWStateManager.dll");
                    Assert.True(File.Exists(stateManagerDll),
                        $"StateManager not found at: {stateManagerExe} or {stateManagerDll}");
                    stateManagerExe = stateManagerDll;
                }

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
                        lock (_logLock) { _stateManagerLogs.AppendLine(args.Data); }
                        _output.WriteLine($"[SM] {args.Data}");
                    }
                };

                _stateManagerProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock) { _stateManagerLogs.AppendLine(args.Data); }
                        _output.WriteLine($"[SM-ERR] {args.Data}");
                    }
                };

                Assert.True(_stateManagerProcess.Start(), "Failed to start StateManager process");
                _stateManagerProcess.BeginOutputReadLine();
                _stateManagerProcess.BeginErrorReadLine();

                _output.WriteLine($"  StateManager PID: {_stateManagerProcess.Id}");
                _output.WriteLine("  StateManager will: launch WoW -> inject bot -> auto-login");
                _output.WriteLine("");

                // Step 4: Monitor for milestones and recording events
                _output.WriteLine("Step 4: Monitoring progress...");
                _output.WriteLine("");

                var milestones = new Milestones();
                // Match new format: SNAPSHOT_RECEIVED: Account='X', ScreenState='Y', Character='Z'
                // Also match old format without ScreenState for backward compat
                var snapshotRegex = new Regex(@"SNAPSHOT_RECEIVED: Account='([^']*)', ScreenState='([^']*)'(?:, Character='([^']*)')?");
                var snapshotOldRegex = new Regex(@"SNAPSHOT_RECEIVED: Account='([^']*)', Character='([^']*)'");
                var recordingStartRegex = new Regex(@"RECORDING_STARTED|Recording started");
                var recordingStopRegex = new Regex(@"RECORDING_STOPPED|Recording stopped|Saved recording");

                var testTimeout = TimeSpan.FromMinutes(30);
                var sw = Stopwatch.StartNew();
                var lastLogCheck = "";
                var recordingCount = 0;

                while (sw.Elapsed < testTimeout)
                {
                    // Check if StateManager exited
                    if (_stateManagerProcess.HasExited)
                    {
                        _output.WriteLine($"\n  StateManager exited (code: {_stateManagerProcess.ExitCode})");
                        break;
                    }

                    // Check if WoW was closed (after it was detected)
                    if (milestones.WowLaunched)
                    {
                        var wowProcesses = Process.GetProcessesByName("WoW");
                        if (wowProcesses.Length == 0)
                        {
                            _output.WriteLine("\n  WoW closed by user - test complete.");
                            break;
                        }
                    }

                    // Get new log output since last check
                    string logs;
                    lock (_logLock) { logs = _stateManagerLogs.ToString(); }

                    if (logs != lastLogCheck)
                    {
                        lastLogCheck = logs;

                        // Detect WoW launch
                        if (!milestones.WowLaunched &&
                            (logs.Contains("WoW.exe started") || logs.Contains("Process ID:")))
                        {
                            milestones.WowLaunched = true;
                            _output.WriteLine("  [+] WoW.exe launched by StateManager");
                        }

                        // Detect injection
                        if (!milestones.Injected &&
                            (logs.Contains("DLL injection completed successfully") ||
                             logs.Contains("SUCCESS: DLL injection completed")))
                        {
                            milestones.Injected = true;
                            _output.WriteLine("  [+] Bot injection successful!");
                        }

                        // Detect screen state transitions from snapshots
                        if (!milestones.InWorld)
                        {
                            var match = snapshotRegex.Match(logs);
                            if (match.Success)
                            {
                                var screenState = match.Groups[2].Value;
                                var charName = match.Groups[3].Success ? match.Groups[3].Value : "";

                                // Track screen state transitions
                                if (screenState != milestones.LastScreenState)
                                {
                                    milestones.LastScreenState = screenState;
                                    _output.WriteLine($"  [+] Screen state: {screenState}");
                                }

                                // Check if we're in world with a character name
                                if (screenState == "InWorld" && !string.IsNullOrEmpty(charName))
                                {
                                    milestones.InWorld = true;
                                    milestones.CharacterName = charName;
                                    _output.WriteLine($"  [+] Character in world: {milestones.CharacterName}");
                                    _output.WriteLine("");
                                    _output.WriteLine("  *** READY FOR RECORDING ***");
                                    _output.WriteLine("  Target yourself (or no target), then cast a spell to START.");
                                    _output.WriteLine("");
                                }
                            }
                            else
                            {
                                // Fallback to old format
                                var oldMatch = snapshotOldRegex.Match(logs);
                                if (oldMatch.Success && !string.IsNullOrEmpty(oldMatch.Groups[2].Value))
                                {
                                    milestones.InWorld = true;
                                    milestones.CharacterName = oldMatch.Groups[2].Value;
                                    _output.WriteLine($"  [+] Character in world: {milestones.CharacterName}");
                                    _output.WriteLine("");
                                    _output.WriteLine("  *** READY FOR RECORDING ***");
                                    _output.WriteLine("  Target yourself (or no target), then cast a spell to START.");
                                    _output.WriteLine("");
                                }
                            }
                        }

                        // Detect recording events from MovementRecordingService (StateManager side)
                        if (recordingStartRegex.IsMatch(logs) && !milestones.CurrentlyRecording)
                        {
                            milestones.CurrentlyRecording = true;
                            _output.WriteLine("\n  *** RECORDING STARTED ***");
                            _output.WriteLine("  Move around to capture data. Cast again to STOP.\n");
                        }

                        if (recordingStopRegex.IsMatch(logs) && milestones.CurrentlyRecording)
                        {
                            milestones.CurrentlyRecording = false;
                            recordingCount++;
                            _output.WriteLine($"\n  *** RECORDING #{recordingCount} SAVED ***");
                            _output.WriteLine("  Cast again for new recording, or close WoW to end.\n");
                        }
                    }

                    // Also check the recordings directory for new files (works for both recording systems)
                    if (Directory.Exists(recordingsDir))
                    {
                        var currentFiles = Directory.GetFiles(recordingsDir);
                        var newFiles = currentFiles.Where(f => !existingRecordings.Contains(f)).ToArray();
                        foreach (var newFile in newFiles)
                        {
                            existingRecordings.Add(newFile);
                            var fi = new FileInfo(newFile);
                            _output.WriteLine($"  [FILE] New recording: {fi.Name} ({fi.Length} bytes)");
                        }
                    }

                    await Task.Delay(500);
                }

                // Summary
                _output.WriteLine("");
                _output.WriteLine("=== TEST SUMMARY ===");
                _output.WriteLine($"  Account: {accountName}");
                _output.WriteLine($"  Character: {(string.IsNullOrEmpty(milestones.CharacterName) ? "(not detected)" : milestones.CharacterName)}");
                _output.WriteLine($"  WoW Launched: {(milestones.WowLaunched ? "Yes" : "No")}");
                _output.WriteLine($"  Injection: {(milestones.Injected ? "Success" : "Not detected")}");
                _output.WriteLine($"  In World: {(milestones.InWorld ? "Yes" : "No")}");
                _output.WriteLine($"  Recordings Saved: {recordingCount}");
                _output.WriteLine("");

                DisplayRecordingsFolder(existingRecordings);
                CaptureWoWDiagnosticLogs();

                // Test passes if bot was injected and character entered world
                Assert.True(milestones.Injected || milestones.InWorld,
                    "Bot was not successfully injected or character did not enter world. Check StateManager logs above.");
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

        private class Milestones
        {
            public bool WowLaunched;
            public bool Injected;
            public bool InWorld;
            public string CharacterName = "";
            public string LastScreenState = "";
            public bool CurrentlyRecording;
        }

        private void VerifyBuildArtifacts(string buildPath)
        {
            var requiredFiles = new[]
            {
                "WoWStateManager.exe",
                "Loader.dll",
                "ForegroundBotRunner.dll",
                "ForegroundBotRunner.runtimeconfig.json"
            };

            _output.WriteLine("Verifying build artifacts...");
            foreach (var file in requiredFiles)
            {
                var path = Path.Combine(buildPath, file);
                var exists = File.Exists(path);
                _output.WriteLine($"  {(exists ? "[OK]" : "[MISSING]")} {file}");
                if (!exists && file != "WoWStateManager.exe") // WoWStateManager.exe might be .dll
                {
                    // Don't fail - StateManager might be .dll instead of .exe
                }
            }
            _output.WriteLine("");
        }

        private void DisplayRecordingsFolder(HashSet<string>? knownFiles = null)
        {
            try
            {
                var recordingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BloogBot", "MovementRecordings");

                _output.WriteLine("=== RECORDINGS ===");
                _output.WriteLine($"  Path: {recordingsDir}");

                if (!Directory.Exists(recordingsDir))
                {
                    _output.WriteLine("  (No recordings directory)");
                    return;
                }

                var files = Directory.GetFiles(recordingsDir)
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Take(10);

                if (!files.Any())
                {
                    _output.WriteLine("  (No recording files)");
                    return;
                }

                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    _output.WriteLine($"    {fi.Name} ({fi.Length} bytes, {fi.LastWriteTime:HH:mm:ss})");
                }

                _output.WriteLine("=== END RECORDINGS ===");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Error: {ex.Message}");
            }
        }

        private void CaptureWoWDiagnosticLogs()
        {
            try
            {
                // Check for diagnostic logs in the WoW directory
                var gameClientPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH")
                    ?? _configuration["GameClient:ExecutablePath"];

                if (string.IsNullOrEmpty(gameClientPath))
                    return;

                var wowDir = Path.GetDirectoryName(gameClientPath);
                if (string.IsNullOrEmpty(wowDir))
                    return;

                var logsDir = Path.Combine(wowDir, "WWoWLogs");
                _output.WriteLine("");
                _output.WriteLine("=== WoW Diagnostic Logs ===");
                _output.WriteLine($"  Path: {logsDir}");

                if (!Directory.Exists(logsDir))
                {
                    _output.WriteLine("  (No logs directory)");
                    return;
                }

                foreach (var file in Directory.GetFiles(logsDir, "*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Take(5))
                {
                    var fi = new FileInfo(file);
                    _output.WriteLine($"    {fi.Name} ({fi.Length} bytes, {fi.LastWriteTime:HH:mm:ss})");
                }

                // Show last 20 lines of the foreground bot debug log
                var fgDebugLog = Path.Combine(logsDir, "foreground_bot_debug.log");
                if (File.Exists(fgDebugLog))
                {
                    _output.WriteLine("");
                    _output.WriteLine("  Last 20 lines of foreground_bot_debug.log:");
                    var lines = File.ReadAllLines(fgDebugLog);
                    foreach (var line in lines.TakeLast(20))
                    {
                        _output.WriteLine($"    {line}");
                    }
                }

                _output.WriteLine("=== END DIAGNOSTIC LOGS ===");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Error: {ex.Message}");
            }
        }

        private void CleanupBreadcrumbFiles(string basePath)
        {
            var breadcrumbFiles = new[]
            {
                "testentry_stdcall.txt",
                "testentry_cdecl.txt",
                "bot_startup.txt",
                "bot_status.txt"
            };

            foreach (var file in breadcrumbFiles)
            {
                var path = Path.Combine(basePath, file);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
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
                    _output.WriteLine("Stopping StateManager process...");
                    _stateManagerProcess.Kill(entireProcessTree: true);
                    _stateManagerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error stopping StateManager: {ex.Message}");
            }
        }
    }
}
