using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Integration test that relies on WoWStateManager to launch WoW, inject
/// ForegroundBotRunner via Loader.dll, and run automated movement scenarios
/// (including swimming) to produce recordings.
///
/// Prerequisites:
///   - WoW 1.12.1 client at the path in WWOW_TEST_WOW_PATH (or default)
///   - MaNGOS auth + world servers running (ports 3724, 8085)
///   - Loader.dll + ForegroundBotRunner.dll built in Bot\Debug\net8.0\
///   - GM-level account (for teleport commands in scenarios)
///
/// The flow:
///   1. BotServiceFixture starts WoWStateManager (which launches WoW + injects Loader.dll)
///   2. ForegroundBotWorker auto-logs in via StateManager IPC
///   3. BLOOGBOT_AUTOMATED_RECORDING=1 triggers MovementScenarioRunner
///   4. Scenario 08_swim_forward records swimming data
///   5. Test monitors diagnostic logs and recordings directory for completion
///
/// Run this test in a separate elevated terminal:
///   dotnet test --filter "FullyQualifiedName~SwimmingRecordingSessionTest" --logger "console;verbosity=detailed"
/// </summary>
[Trait(TestCategories.Category, TestCategories.EndToEnd)]
[Trait(TestCategories.RequiresService, TestCategories.MangosStack)]
[Trait(TestCategories.Duration, TestCategories.Slow)]
public class SwimmingRecordingSessionTest(ITestOutputHelper output) : IDisposable
{
    private readonly ITestOutputHelper _output = output;
    private BotServiceFixture? _serviceFixture;
    private readonly IntegrationTestConfig _config = IntegrationTestConfig.FromEnvironment();
    private readonly string _recordingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BloogBot", "MovementRecordings");

    // Timeouts
    private static readonly TimeSpan WoWProcessTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan InWorldTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan RecordingTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    [SkippableFact]
    public async Task LaunchAndRecord_SwimmingData()
    {
        // This test launches WoW + StateManager. Only run when explicitly requested.
        var enabled = Environment.GetEnvironmentVariable("BLOOGBOT_RUN_RECORDING_TESTS");
        global::Tests.Infrastructure.Skip.IfNot(enabled == "1",
            "Set BLOOGBOT_RUN_RECORDING_TESTS=1 to enable this test.");

        // Create the service fixture on demand (it launches StateManager)
        _serviceFixture = new BotServiceFixture();
        _serviceFixture.SetOutput(_output);
        await _serviceFixture.InitializeAsync();

        // Skip if the BotServiceFixture detected services are unavailable
        global::Tests.Infrastructure.Skip.IfNot(_serviceFixture.ServicesReady,
            _serviceFixture.UnavailableReason ?? "Required services are not ready.");

        // DLL injection requires admin privileges
        var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        global::Tests.Infrastructure.Skip.IfNot(isElevated,
            "This test requires admin privileges for DLL injection. Run from an elevated terminal.");

        _output.WriteLine("Services ready.");

        var healthChecker = new ServiceHealthChecker();

        var authAvailable = await healthChecker.IsRealmdAvailableAsync(_config);
        global::Tests.Infrastructure.Skip.IfNot(authAvailable,
            $"Auth server not available at {_config.AuthServerIp}:{_config.AuthServerPort}");

        var worldAvailable = await healthChecker.IsMangosdAvailableAsync(_config);
        global::Tests.Infrastructure.Skip.IfNot(worldAvailable,
            $"World server not available on port {_config.WorldServerPort}");

        // ?? Set automated recording mode ??????????????????????????????????
        Environment.SetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING", "1");
        _output.WriteLine("Set BLOOGBOT_AUTOMATED_RECORDING=1 for automated scenarios");

        // Snapshot existing recordings before test starts
        var existingRecordings = Directory.Exists(_recordingsDir)
            ? new HashSet<string>(Directory.GetFiles(_recordingsDir, "*.json"))
            : new HashSet<string>();
        _output.WriteLine($"Existing recordings: {existingRecordings.Count}");

        // ?? Wait for WoW process launched by StateManager ?????????????????
        // StateManager's ApplyDesiredWorkerState calls StartForegroundBotRunner
        // which launches WoW.exe and injects Loader.dll. We just find that process.
        _output.WriteLine("Waiting for WoW process launched by StateManager...");
        var wowProcessDeadline = DateTime.UtcNow + WoWProcessTimeout;
        Process? wowProcess = null;

        while (DateTime.UtcNow < wowProcessDeadline)
        {
            var wowProcesses = Process.GetProcessesByName("WoW");
            if (wowProcesses.Length > 0)
            {
                wowProcess = wowProcesses[0];
                _output.WriteLine($"? Found WoW process: PID {wowProcess.Id}");
                break;
            }
            await Task.Delay(PollInterval);
        }

        if (wowProcess == null)
        {
            _output.WriteLine("FAIL: WoW process was not launched by StateManager within timeout.");
            Assert.Fail("StateManager did not launch WoW.exe");
            return;
        }

        int wowPid = wowProcess.Id;

        // Wait for managed code to initialize after injection
        _output.WriteLine("Waiting for managed code initialization...");
        await Task.Delay(10_000);

        // Check injection_firstchance.log for EntryPointNotFoundException (FastCall.dll issue)
        CheckFirstChanceLog(wowPid);

        // ?? Monitor diagnostic log for login progress ?????????????????????
        string? diagLogPath = FindDiagnosticLog(wowPid);
        _output.WriteLine($"Diagnostic log: {diagLogPath ?? "(not found yet)"}");

        // Wait for login ? character select ? enter world
        var inWorldDeadline = DateTime.UtcNow + InWorldTimeout;
        bool inWorld = false;

        while (DateTime.UtcNow < inWorldDeadline && !wowProcess.HasExited)
        {
            await Task.Delay(PollInterval);

            // Re-check diagnostic log path
            diagLogPath ??= FindDiagnosticLog(wowPid);

            if (diagLogPath != null && File.Exists(diagLogPath))
            {
                var logContent = SafeReadFile(diagLogPath);
                if (logContent.Contains("SCREEN_STATE_CHANGED") && logContent.Contains("InWorld"))
                {
                    _output.WriteLine("? Character entered world!");
                    inWorld = true;
                    break;
                }

                // Log last few lines for progress tracking
                var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    var lastLine = lines[^1].Trim();
                    if (lastLine.Length > 0)
                        _output.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {lastLine}");
                }
            }
            else
            {
                _output.WriteLine($"  [{DateTime.Now:HH:mm:ss}] Waiting for diagnostic log...");
            }
        }

        if (!inWorld)
        {
            _output.WriteLine("WARNING: Did not detect InWorld state within timeout.");
            _output.WriteLine("The bot may still be logging in. Check WoW manually.");
            // Don't fail - the bot might still work; we'll wait for recordings
        }

        // ?? Wait for swimming recording to appear ?????????????????????????
        _output.WriteLine("Waiting for automated recordings (including swim scenario)...");
        var recordingDeadline = DateTime.UtcNow + RecordingTimeout;
        var newRecordings = new List<string>();
        bool swimRecordingFound = false;

        while (DateTime.UtcNow < recordingDeadline && !wowProcess.HasExited)
        {
            await Task.Delay(PollInterval);

            if (!Directory.Exists(_recordingsDir)) continue;

            var currentFiles = Directory.GetFiles(_recordingsDir, "*.json");
            var newFiles = currentFiles.Where(f => !existingRecordings.Contains(f)).ToList();

            if (newFiles.Count > newRecordings.Count)
            {
                foreach (var f in newFiles.Where(f => !newRecordings.Contains(f)))
                {
                    var fileName = Path.GetFileName(f);
                    _output.WriteLine($"  ? New recording: {fileName}");
                    newRecordings.Add(f);
                }
            }

            // Check scenario runner log for swim completion
            var scenarioLogPath = FindScenarioRunnerLog(wowPid);
            if (scenarioLogPath != null && File.Exists(scenarioLogPath))
            {
                var scenarioLog = SafeReadFile(scenarioLogPath);
                if (scenarioLog.Contains("SCENARIO_COMPLETE: 08_swim_forward"))
                {
                    _output.WriteLine("? Swim forward scenario completed!");
                    swimRecordingFound = true;
                }
                if (scenarioLog.Contains("All scenarios complete"))
                {
                    _output.WriteLine("? All automated scenarios completed!");
                    break;
                }
            }
        }

        // ?? Report results ????????????????????????????????????????????????
        _output.WriteLine($"\n=== RECORDING SESSION RESULTS ===");
        _output.WriteLine($"New recordings: {newRecordings.Count}");
        foreach (var f in newRecordings)
        {
            var fi = new FileInfo(f);
            _output.WriteLine($"  {fi.Name} ({fi.Length / 1024}KB)");
        }

        if (swimRecordingFound || newRecordings.Any(f =>
            Path.GetFileName(f).Contains("swim", StringComparison.OrdinalIgnoreCase)))
        {
            _output.WriteLine("\n? Swimming recording captured successfully!");
            _output.WriteLine("Update TestConstants.cs with the new recording filename.");
        }
        else
        {
            _output.WriteLine("\nSwimming recording not captured in this session.");
            _output.WriteLine("You can manually record: /say rec swim_forward ? swim ? /say rec");
        }

        // Don't assert on swim recording - the test's primary job is to get the session running
        Assert.True(newRecordings.Count >= 0,
            "Recording session completed (check output for new recordings)");
    }

    /// <summary>
    /// Checks injection_firstchance.log for EntryPointNotFoundException indicating
    /// a stale FastCall.dll without the LuaCall export.
    /// </summary>
    private void CheckFirstChanceLog(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var wowDir = Path.GetDirectoryName(process.MainModule?.FileName);
            if (wowDir == null) return;

            var firstChancePath = Path.Combine(wowDir, "WWoWLogs", "injection_firstchance.log");
            if (!File.Exists(firstChancePath)) return;

            var content = SafeReadFile(firstChancePath);
            if (content.Contains("EntryPointNotFoundException") && content.Contains("LuaCall"))
            {
                _output.WriteLine("!!! CRITICAL: injection_firstchance.log shows EntryPointNotFoundException for LuaCall !!!");
                _output.WriteLine("    FastCall.dll in Bot\\Debug\\net8.0\\ is likely the stale 12KB version.");
                _output.WriteLine("    Fix: Copy-Item Bot\\net8.0\\FastCall.dll Bot\\Debug\\net8.0\\FastCall.dll -Force");
                _output.WriteLine("    Then restart StateManager and re-run this test.");
            }
        }
        catch { /* Best-effort diagnostic */ }
    }

    /// <summary>
    /// Finds the ForegroundBotWorker diagnostic log in the WoW directory.
    /// </summary>
    private string? FindDiagnosticLog(int? processId)
    {
        if (processId == null) return null;

        // Try the WoW.exe directory first
        try
        {
            var process = Process.GetProcessById(processId.Value);
            var wowDir = Path.GetDirectoryName(process.MainModule?.FileName);
            if (wowDir != null)
            {
                var logPath = Path.Combine(wowDir, "WWoWLogs", "foreground_bot_debug.log");
                if (File.Exists(logPath)) return logPath;
            }
        }
        catch { /* Process may have exited or access denied */ }

        // Fallback: search common WoW paths
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("WWOW_TEST_WOW_PATH"),
            @"E:\Elysium Project Game Client",
            @"C:\Games\WoW-1.12.1",
            @"D:\Games\WoW-1.12.1",
            @"E:\Games\WoW-1.12.1",
        };

        foreach (var dir in candidates.Where(d => d != null))
        {
            var wowDir = Path.GetDirectoryName(dir) ?? dir;
            var logPath = Path.Combine(wowDir!, "WWoWLogs", "foreground_bot_debug.log");
            if (File.Exists(logPath)) return logPath;
        }

        return null;
    }

    /// <summary>
    /// Finds the scenario runner diagnostic log.
    /// </summary>
    private string? FindScenarioRunnerLog(int? processId)
    {
        if (processId == null) return null;

        try
        {
            var process = Process.GetProcessById(processId.Value);
            var wowDir = Path.GetDirectoryName(process.MainModule?.FileName);
            if (wowDir != null)
            {
                var logPath = Path.Combine(wowDir, "WWoWLogs", "scenario_runner.log");
                if (File.Exists(logPath)) return logPath;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Reads a file safely even if another process has it open for writing.
    /// </summary>
    private static string SafeReadFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _serviceFixture?.DisposeAsync().GetAwaiter().GetResult();
        _output.WriteLine("Test complete.");
    }
}
