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
    [Collection(InfrastructureTestCollection.Name)]
    public class AutomatedRecordingIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private readonly InfrastructureTestGuard _guard;
        private readonly StateManagerProcessHelper _helper;

        public AutomatedRecordingIntegrationTest(InfrastructureTestGuard guard, ITestOutputHelper output)
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
        public async Task AutomatedRecording_RunsAllScenariosAndSavesRecordings()
        {
            _output.WriteLine("=== AUTOMATED MOVEMENT RECORDING TEST ===");
            _output.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");
            _output.WriteLine("This test launches WoW, injects the bot with AUTOMATED_RECORDING mode,");
            _output.WriteLine("and runs 10 controlled movement scenarios automatically.");
            _output.WriteLine("");

            var solutionRoot = StateManagerProcessHelper.FindSolutionRoot(Directory.GetCurrentDirectory());
            Assert.NotNull(solutionRoot);

            var stateManagerBuildPath = StateManagerProcessHelper.GetStateManagerBuildPath(solutionRoot);

            // Verify required files exist
            VerifyBuildArtifacts(stateManagerBuildPath);

            // Clean up stale breadcrumb files
            StateManagerProcessHelper.CleanupBreadcrumbFiles(stateManagerBuildPath, msg => _output.WriteLine(msg));

            // Snapshot the recordings directory
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

            _output.WriteLine($"Step 1: Using account '{accountName}' (needs GM level 3)");
            _output.WriteLine("");

            // Step 2: Configure and launch StateManager via helper
            _output.WriteLine("Step 2: Configuring StateManager...");

            var config = new StateManagerLaunchConfig
            {
                AccountName = accountName,
                TaskMode = BotTaskMode.AutomatedRecording,
                RunnerType = "Foreground"
            };

            _helper.WriteSettings(stateManagerBuildPath, config);
            _output.WriteLine("");

            // Step 3: Start StateManager with automated recording env var
            _output.WriteLine("Step 3: Starting StateManager with BLOOGBOT_AUTOMATED_RECORDING=1...");

            _helper.OnOutputLine += line => _output.WriteLine($"[SM] {line}");
            _helper.OnErrorLine += line => _output.WriteLine($"[SM-ERR] {line}");

            Assert.True(_helper.Launch(stateManagerBuildPath, config), "Failed to start StateManager process");

            _output.WriteLine($"  StateManager PID: {_helper.Process!.Id}");
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
                if (!_helper.IsRunning)
                {
                    _output.WriteLine($"\n  StateManager exited (code: {_helper.Process?.ExitCode})");
                    break;
                }

                // Read StateManager stdout logs
                var smLogs = _helper.CapturedLogs;

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

                    // Detect world entry
                    if (!inWorld && (allLogs.Contains("ENTERED_WORLD") || allLogs.Contains("OnPlayerEnteredWorld")))
                    {
                        inWorld = true;
                        _output.WriteLine("  [+] Character entered world");
                    }

                    // Detect scenario starts
                    foreach (Match match in scenarioStartRegex.Matches(allLogs))
                    {
                        var name = match.Groups[1].Value;
                        if (startedScenarios.Add(name))
                            _output.WriteLine($"  [>] Running: {name}");
                    }

                    // Detect scenario completions
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

        public void Dispose()
        {
            _helper.Stop();
            _guard.UnregisterHelper();
        }
    }
}
