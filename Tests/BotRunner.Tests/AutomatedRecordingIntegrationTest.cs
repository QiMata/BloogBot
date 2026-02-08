using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Automated movement recording test.
    ///
    /// This test:
    /// 1. Launches WoW via StateManager with BLOOGBOT_AUTOMATED_RECORDING=1
    /// 2. Bot auto-logs in, enters the world
    /// 3. MovementScenarioRunner automatically runs 10 controlled scenarios:
    ///    - Flat run forward/backward
    ///    - Standing/running jumps
    ///    - Fall from height
    ///    - Strafe (diagonal + pure)
    ///    - Swim
    ///    - Uphill/downhill slopes
    /// 4. Each scenario saves a JSON + protobuf recording
    /// 5. Test verifies recordings were created
    ///
    /// Recordings saved to: Documents/BloogBot/MovementRecordings/
    ///
    /// Prerequisites:
    /// 1. Build the solution: dotnet build
    /// 2. Mangos server running
    /// 3. GM level 3 account (for .go xyz teleport)
    /// </summary>
    [RequiresInfrastructure]
    public class AutomatedRecordingIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private Process? _stateManagerProcess;
        private readonly StringBuilder _stateManagerLogs = new();
        private readonly object _logLock = new();

        public AutomatedRecordingIntegrationTest(ITestOutputHelper output)
        {
            _output = output;

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            _configuration = configBuilder.Build();
        }

        [Fact]
        public async Task AutomatedRecording_RunsAllScenariosAndSavesRecordings()
        {
            _output.WriteLine("=== AUTOMATED MOVEMENT RECORDING TEST ===");
            _output.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");
            _output.WriteLine("This test launches WoW, injects the bot with AUTOMATED_RECORDING mode,");
            _output.WriteLine("and runs 10 controlled movement scenarios automatically.");
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

            // Snapshot the recordings directory
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

                _output.WriteLine($"Step 1: Using account '{accountName}' (needs GM level 3)");
                _output.WriteLine("");

                // Step 2: Configure StateManager settings
                _output.WriteLine("Step 2: Configuring StateManager...");
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
                _output.WriteLine("");

                // Step 3: Start StateManager with automated recording env var
                _output.WriteLine("Step 3: Starting StateManager with BLOOGBOT_AUTOMATED_RECORDING=1...");
                var stateManagerExe = Path.Combine(stateManagerBuildPath, "WoWStateManager.exe");

                if (!File.Exists(stateManagerExe))
                {
                    var stateManagerDll = Path.Combine(stateManagerBuildPath, "WoWStateManager.dll");
                    Assert.True(File.Exists(stateManagerDll),
                        $"WoWStateManager not found at: {stateManagerExe} or {stateManagerDll}");
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
                psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = "1";

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
                _output.WriteLine("");

                // Step 4: Monitor for scenario completion
                _output.WriteLine("Step 4: Monitoring automated scenarios...");
                _output.WriteLine("");

                var scenarioCompleteRegex = new Regex(@"SCENARIO_COMPLETE: (\S+)");
                var allScenariosCompleteRegex = new Regex(@"All scenarios complete");
                var scenarioStartRegex = new Regex(@"SCENARIO_START: (\S+)");

                var completedScenarios = new HashSet<string>();
                var startedScenarios = new HashSet<string>();
                var scenariosComplete = false;
                var inWorld = false;

                // WoW log files - the bot writes diagnostics here
                var wowExePath = _configuration["GameClient:ExecutablePath"] ?? @"E:\Elysium Project Game Client\WoW.exe";
                var wowDir = Path.GetDirectoryName(wowExePath)!;
                var wowLogsDir = Path.Combine(wowDir, "WWoWLogs");
                var botDebugLog = Path.Combine(wowLogsDir, "foreground_bot_debug.log");
                var scenarioLog = Path.Combine(wowLogsDir, "scenario_runner.log");
                _output.WriteLine($"  WoW logs: {wowLogsDir}");

                // 10 minutes max: ~2 min login + ~8 min for 10 scenarios
                var testTimeout = TimeSpan.FromMinutes(10);
                var sw = Stopwatch.StartNew();
                var lastSmLogSnapshot = "";
                var lastBotLogSnapshot = "";
                var lastScenarioLogSnapshot = "";

                while (sw.Elapsed < testTimeout)
                {
                    if (_stateManagerProcess.HasExited)
                    {
                        _output.WriteLine($"\n  StateManager exited (code: {_stateManagerProcess.ExitCode})");
                        break;
                    }

                    // Read StateManager stdout logs
                    string smLogs;
                    lock (_logLock) { smLogs = _stateManagerLogs.ToString(); }

                    // Read WoW bot debug log file
                    string botLogs = "";
                    try { if (File.Exists(botDebugLog)) botLogs = File.ReadAllText(botDebugLog); } catch { }

                    // Read scenario runner log file
                    string scenarioLogs = "";
                    try { if (File.Exists(scenarioLog)) scenarioLogs = File.ReadAllText(scenarioLog); } catch { }

                    // Combine all log sources
                    var allLogs = smLogs + "\n" + botLogs + "\n" + scenarioLogs;

                    if (smLogs != lastSmLogSnapshot || botLogs != lastBotLogSnapshot || scenarioLogs != lastScenarioLogSnapshot)
                    {
                        lastSmLogSnapshot = smLogs;
                        lastBotLogSnapshot = botLogs;
                        lastScenarioLogSnapshot = scenarioLogs;

                        // Detect world entry (from bot debug log)
                        if (!inWorld && (allLogs.Contains("ENTERED_WORLD") || allLogs.Contains("OnPlayerEnteredWorld")))
                        {
                            inWorld = true;
                            _output.WriteLine("  [+] Character entered world");
                        }

                        // Detect scenario starts (from scenario runner log)
                        foreach (Match match in scenarioStartRegex.Matches(allLogs))
                        {
                            var name = match.Groups[1].Value;
                            if (startedScenarios.Add(name))
                                _output.WriteLine($"  [>] Running: {name}");
                        }

                        // Detect scenario completions (from scenario runner log)
                        foreach (Match match in scenarioCompleteRegex.Matches(allLogs))
                        {
                            var name = match.Groups[1].Value;
                            if (completedScenarios.Add(name))
                                _output.WriteLine($"  [+] Completed: {name} ({completedScenarios.Count}/10)");
                        }

                        // Detect all scenarios complete
                        if (allScenariosCompleteRegex.IsMatch(allLogs))
                        {
                            scenariosComplete = true;
                            _output.WriteLine("\n  All automated scenarios completed!");
                            break;
                        }
                    }

                    // Check for new recording files
                    if (Directory.Exists(recordingsDir))
                    {
                        var currentFiles = Directory.GetFiles(recordingsDir);
                        var newFiles = currentFiles.Where(f => !existingRecordings.Contains(f)).ToArray();
                        foreach (var newFile in newFiles)
                        {
                            existingRecordings.Add(newFile);
                            var fi = new FileInfo(newFile);
                            _output.WriteLine($"  [FILE] {fi.Name} ({fi.Length} bytes)");
                        }
                    }

                    await Task.Delay(1000);
                }

                // Summary
                _output.WriteLine("");
                _output.WriteLine("=== TEST SUMMARY ===");
                _output.WriteLine($"  Entered World: {inWorld}");
                _output.WriteLine($"  Scenarios Complete: {completedScenarios.Count}/10");
                _output.WriteLine($"  All Done: {scenariosComplete}");
                _output.WriteLine("");

                // List new recordings
                var newRecordings = new List<string>();
                if (Directory.Exists(recordingsDir))
                {
                    newRecordings = Directory.GetFiles(recordingsDir, "*.json")
                        .Where(f => new FileInfo(f).LastWriteTime > DateTime.Now.AddMinutes(-15))
                        .OrderBy(f => f)
                        .ToList();

                    _output.WriteLine($"  Recent recordings ({newRecordings.Count}):");
                    foreach (var file in newRecordings)
                    {
                        var fi = new FileInfo(file);
                        _output.WriteLine($"    {fi.Name} ({fi.Length} bytes)");
                    }
                }

                _output.WriteLine("=== END ===");

                // Assertions
                Assert.True(inWorld, "Character did not enter the world");
                Assert.True(completedScenarios.Count > 0,
                    "No scenarios completed. Check StateManager logs above for errors.");

                if (scenariosComplete)
                {
                    Assert.True(newRecordings.Count >= 10,
                        $"Expected at least 10 JSON recordings, found {newRecordings.Count}");
                }
            }
            finally
            {
                // Restore original settings
                if (originalSettings != null)
                {
                    File.WriteAllText(settingsPath, originalSettings);
                }
            }
        }

        private void VerifyBuildArtifacts(string buildPath)
        {
            var requiredFiles = new[] { "Loader.dll", "ForegroundBotRunner.dll", "ForegroundBotRunner.runtimeconfig.json" };
            _output.WriteLine("Verifying build artifacts...");
            foreach (var file in requiredFiles)
            {
                var path = Path.Combine(buildPath, file);
                var exists = File.Exists(path);
                _output.WriteLine($"  {(exists ? "[OK]" : "[MISSING]")} {file}");
                Assert.True(exists, $"Required file not found: {path}");
            }
            _output.WriteLine("");
        }

        private string? FindSolutionRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")) ||
                    (Directory.Exists(Path.Combine(dir.FullName, "Services")) &&
                     Directory.Exists(Path.Combine(dir.FullName, "Exports"))))
                    return dir.FullName;
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
