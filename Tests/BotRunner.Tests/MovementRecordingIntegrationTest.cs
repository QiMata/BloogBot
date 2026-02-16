using BotRunner.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Integration test for movement recording.
    ///
    /// This test:
    /// 1. Configures StateManager to launch WoW and inject the bot
    /// 2. Bot auto-logs in and enters the world
    /// 3. User manually controls the character
    /// 4. Type /say rec to START recording movement (60 FPS)
    /// 5. Move around, jump, swim, fall, ride elevators
    /// 6. Type /say rec to STOP recording and save
    /// 7. Close WoW to end the test
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
    [Collection(InfrastructureTestCollection.Name)]
    public class MovementRecordingIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private readonly InfrastructureTestGuard _guard;
        private readonly StateManagerProcessHelper _helper;

        public MovementRecordingIntegrationTest(InfrastructureTestGuard guard, ITestOutputHelper output)
        {
            _guard = guard;
            _output = output;
            _helper = new StateManagerProcessHelper();

            // Ensure no lingering StateManager/WoW from a previous test
            _guard.EnsureCleanState(msg => _output.WriteLine(msg));
            _guard.RegisterHelper(_helper);

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
            _output.WriteLine("  - Type /say rec to START recording (60 FPS)");
            _output.WriteLine("  - Type /say rec <desc> to start with a description (e.g. /say rec elevator)");
            _output.WriteLine("  - Move around, jump, swim, fall, ride elevators");
            _output.WriteLine("  - Type /say rec to STOP recording and save");
            _output.WriteLine("  - Close WoW to end the test");
            _output.WriteLine("");

            var solutionRoot = StateManagerProcessHelper.FindSolutionRoot(Directory.GetCurrentDirectory());
            Assert.NotNull(solutionRoot);

            var stateManagerBuildPath = StateManagerProcessHelper.GetStateManagerBuildPath(solutionRoot);

            // Verify required files exist
            VerifyBuildArtifacts(stateManagerBuildPath);

            // Clean up stale breadcrumb files
            StateManagerProcessHelper.CleanupBreadcrumbFiles(stateManagerBuildPath, msg => _output.WriteLine(msg));

            // Snapshot the recordings directory so we can detect new files
            var recordingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BloogBot", "MovementRecordings");
            var existingRecordings = Directory.Exists(recordingsDir)
                ? new HashSet<string>(Directory.GetFiles(recordingsDir))
                : new HashSet<string>();

            // Step 1: Determine account name
            var accountName = Environment.GetEnvironmentVariable("WWOW_RECORDING_ACCOUNT")
                ?? Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME")
                ?? "ORWR1";

            _output.WriteLine($"Step 1: Using account '{accountName}'");
            _output.WriteLine("  (Set WWOW_RECORDING_ACCOUNT env var to change)");
            _output.WriteLine("");

            // Step 2: Configure and launch StateManager via helper
            _output.WriteLine("Step 2: Configuring StateManager to launch WoW...");

            var config = new StateManagerLaunchConfig
            {
                AccountName = accountName,
                TaskMode = BotTaskMode.ManualRecording,
                RunnerType = "Foreground"
            };

            _helper.WriteSettings(stateManagerBuildPath, config);
            _output.WriteLine("  Settings written (no TargetProcessId = StateManager launches WoW)");
            _output.WriteLine("");

            // Step 3: Start StateManager
            _output.WriteLine("Step 3: Starting StateManager...");

            _helper.OnOutputLine += line => _output.WriteLine($"[SM] {line}");
            _helper.OnErrorLine += line => _output.WriteLine($"[SM-ERR] {line}");

            Assert.True(_helper.Launch(stateManagerBuildPath, config), "Failed to start StateManager process");

            _output.WriteLine($"  StateManager PID: {_helper.Process!.Id}");
            _output.WriteLine("  StateManager will: launch WoW -> inject bot -> auto-login");
            _output.WriteLine("");

            // Step 4: Monitor for milestones and recording events
            _output.WriteLine("Step 4: Monitoring progress...");
            _output.WriteLine("");

            var milestones = new Milestones();
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
                if (!_helper.IsRunning)
                {
                    _output.WriteLine($"\n  StateManager exited (code: {_helper.Process?.ExitCode})");
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
                    foreach (var p in wowProcesses) p.Dispose();
                }

                // Get new log output since last check
                var logs = _helper.CapturedLogs;

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

                            if (screenState != milestones.LastScreenState)
                            {
                                milestones.LastScreenState = screenState;
                                _output.WriteLine($"  [+] Screen state: {screenState}");
                            }

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

                    // Detect recording events
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

                // Also check the recordings directory for new files
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

        public void Dispose()
        {
            _helper.Stop();
            _guard.UnregisterHelper();
        }
    }
}
