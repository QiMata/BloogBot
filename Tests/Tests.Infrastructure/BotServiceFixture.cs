using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that checks whether WoWStateManager and supporting
/// services (MaNGOS auth/world/MySQL) are available for integration tests.
///
/// Composes <see cref="MangosServerFixture"/> for auth/world/MySQL checks,
/// then adds StateManager auto-start and FastCall.dll verification on top.
///
/// Safety:
///   - Named mutex prevents concurrent test processes from racing
///   - Stale processes (StateManager, WoW.exe, BackgroundBotRunner) are killed before launch
///   - Port 8088 is verified free before starting a new StateManager
///   - MaNGOS session cleanup delay after killing WoW.exe
///
/// Usage:
///   public class MyTest : IClassFixture&lt;BotServiceFixture&gt;
///   {
///       public MyTest(BotServiceFixture fixture, ITestOutputHelper output)
///       {
///           fixture.SetOutput(output);
///           Skip.IfNot(fixture.ServicesReady, fixture.UnavailableReason ?? "Services not available");
///       }
///   }
/// </summary>
public class BotServiceFixture : IAsyncLifetime
{
    #region Fields and Properties — Core State

    private const string RepoMarker = "Westworld of Warcraft";
    private readonly MangosServerFixture _mangosFixture = new();
    private ITestOutputHelper? _output;
    private Process? _stateManagerProcess;
    private readonly List<int> _managedWoWPids = [];
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _capturedOutput = [];

    /// <summary>Get all captured StateManager output lines for test assertions.</summary>
    public IReadOnlyCollection<string> GetCapturedOutput() => _capturedOutput;

    /// <summary>
    /// Per-service log manager. Created when a test name is set via <see cref="SetTestName"/>.
    /// Writes per-service log files to TestResults/ServiceLogs/{TestName}/.
    /// </summary>
    public ServiceLogManager? ServiceLogs { get; private set; }

    /// <summary>
    /// Optional path to a custom StateManagerSettings.json file.
    /// When set, StateManager will load bot configuration from this file instead of the default.
    /// Set this before calling <see cref="InitializeAsync"/>.
    /// </summary>
    public string? CustomSettingsPath { get; set; }

    /// <summary>
    /// The normalized settings path that StateManager was last successfully started with.
    /// Used by <see cref="EnsureSettingsAsync"/> to skip redundant restarts.
    /// null = default settings (no custom path).
    /// </summary>
    private string? _activeSettingsPath;

    /// <summary>
    /// Snapshot of coordinator env var at last init, so we detect when
    /// WWOW_TEST_DISABLE_COORDINATOR changes between tests.
    /// </summary>
    private string? _activeCoordinatorFlag;
    private static readonly System.Text.RegularExpressions.Regex WoWPidRegex =
        new(@"WoW\.exe started.*Process ID: (\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Machine-wide mutex to prevent multiple test processes from concurrently
    /// launching StateManagers. Only one BotServiceFixture can be initializing at a time.
    /// </summary>
    private static Mutex? _globalMutex;

    /// <summary>
    /// Whether a managed client (WoW.exe or StateManager) has crashed during this session.
    /// When true, tests should fail fast instead of timing out.
    /// </summary>
    public bool ClientCrashed { get; private set; }

    /// <summary>Descriptive message about which client crashed and when.</summary>
    public string? CrashMessage { get; private set; }

    private CancellationTokenSource? _crashMonitorCts;
    private bool _mutexHeld;

    /// <summary>
    /// Whether all required services (MaNGOS + StateManager) are healthy and ready.
    /// </summary>
    public bool ServicesReady { get; private set; }

    /// <summary>
    /// Whether PathfindingService is listening on its configured port (default 5001).
    /// Tests that require pathfinding should check this in addition to <see cref="ServicesReady"/>.
    /// </summary>
    public bool PathfindingServiceReady { get; private set; }

    /// <summary>
    /// Whether SceneDataService is listening on its configured port (default 5003).
    /// Tests that require scene data should check this in addition to <see cref="ServicesReady"/>.
    /// </summary>
    public bool SceneDataServiceReady { get; private set; }

    /// <summary>
    /// Reason for service unavailability, if any.
    /// </summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>
    /// The underlying MaNGOS fixture for callers that need individual service status.
    /// </summary>
    public MangosServerFixture MangosFixture => _mangosFixture;

    /// <summary>
    /// Resolves the Bot\$(Configuration)\net8.0\ output directory relative to the solution root.
    /// </summary>
    public static string BotOutputDirectory
    {
        get
        {
            var envDir = Environment.GetEnvironmentVariable("WWOW_BOT_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
                return envDir;

            // Walk from test output (e.g. Tests\Foo\bin\Debug\net8.0\) up to solution root
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Bot");
                if (Directory.Exists(candidate))
                {
                    // Determine configuration from our own output path
                    var config = AppContext.BaseDirectory.Contains("Release",
                        StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
                    return Path.Combine(candidate, config, "net8.0");
                }
                dir = dir.Parent;
            }

            // Absolute fallback
            return @"E:\repos\BloogBot\Bot\Debug\net8.0";
        }
    }

    #endregion

    #region Test Output and Logging Configuration

    /// <summary>
    /// Sets the test output helper for logging. Call from your test constructor.
    /// </summary>
    public void SetOutput(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Initialize per-service logging for a specific test. Call before InitializeAsync.
    /// Log files are written to TestResults/ServiceLogs/{testName}/ and overwritten each run.
    /// </summary>
    public void SetTestName(string testName)
    {
        ServiceLogs?.Dispose();
        ServiceLogs = new ServiceLogManager(testName);
    }

    #endregion

    #region Initialization and Teardown — IAsyncLifetime

    public async Task InitializeAsync()
    {
        Log("BotServiceFixture initializing...");

        // Acquire machine-wide mutex — prevents concurrent test processes from racing.
        // Skip if we already hold it (e.g., during RestartWithSettingsAsync).
        if (!_mutexHeld)
        {
            const string mutexName = "Global\\WWoW_BotServiceFixture_Mutex";
            try
            {
                _globalMutex = new Mutex(false, mutexName);
                Log("  [Mutex] Acquiring machine-wide lock (prevents concurrent StateManager launches)...");
                if (!_globalMutex.WaitOne(TimeSpan.FromMinutes(2)))
                {
                    UnavailableReason = "Another test process is already running BotServiceFixture. Wait for it to finish.";
                    Log($"SKIP: {UnavailableReason}");
                    return;
                }
                Log("  [Mutex] Lock acquired.");
                _mutexHeld = true;
            }
            catch (AbandonedMutexException)
            {
                // Previous holder crashed — we now own the mutex, which is fine.
                Log("  [Mutex] Acquired (previous holder crashed).");
                _mutexHeld = true;
            }
        }
        else
        {
            Log("  [Mutex] Already held (restart in progress).");
        }

        // Kill stale StateManager / WoW.exe from previous test runs so we get a clean slate.
        await KillStaleProcessesAsync();

        // Verify FastCall.dll in the Bot output directory has the LuaCall export.
        VerifyFastCallDll();

        // Delegate auth/world/MySQL checks to MangosServerFixture
        await _mangosFixture.InitializeAsync();

        if (!_mangosFixture.IsAvailable)
        {
            UnavailableReason = _mangosFixture.UnavailableReason;
            Log($"SKIP: {UnavailableReason}");
            return;
        }

        Log($"  Auth server ({_mangosFixture.Config.AuthServerPort}): {_mangosFixture.IsAuthAvailable}");
        Log($"  World server ({_mangosFixture.Config.WorldServerPort}): {_mangosFixture.IsWorldAvailable}");
        Log($"  MySQL ({_mangosFixture.Config.MySqlPort}): {_mangosFixture.IsMySqlAvailable}");

        // Verify port 8088 is free before starting
        if (IsPortInUse(8088))
        {
            Log("  [StateManager] WARNING: Port 8088 still in use after stale cleanup. Waiting 5s...");
            await Task.Delay(5000);
            if (IsPortInUse(8088))
            {
                UnavailableReason = "Port 8088 is occupied by another process after cleanup. Cannot start StateManager.";
                Log($"SKIP: {UnavailableReason}");
                return;
            }
        }

        // Always start a fresh StateManager (stale ones were killed above)
        Log("  [StateManager] Starting fresh instance...");
        var smReady = await TryStartStateManagerAsync();

        Log($"  StateManager (8088): {smReady}");

        if (smReady)
        {
            ServicesReady = true;
            _activeSettingsPath = CustomSettingsPath != null ? Path.GetFullPath(CustomSettingsPath) : null;
            _activeCoordinatorFlag = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") ?? "1";
            Log($"All services are ready! (settings={Path.GetFileName(CustomSettingsPath ?? "default")}, coordinator={_activeCoordinatorFlag})");

            // Check if PathfindingService is available on its external endpoint.
            // Tests that require pathfinding (corpse run, movement, gathering) should skip when unavailable.
            PathfindingServiceReady = await WaitForPathfindingServiceAsync();
            Log($"  PathfindingService (5001): {PathfindingServiceReady}");
            if (!PathfindingServiceReady)
            {
                Log("  [PathfindingService] WARNING: PathfindingService is not available on port 5001.");
                Log("  [PathfindingService] Likely cause: WWOW_DATA_DIR is not set or does not contain mmaps/, maps/, vmaps/ subdirectories.");
                Log("  [PathfindingService] Set WWOW_DATA_DIR to your nav data directory (e.g., D:\\World of Warcraft) and rebuild.");
                Log("  [PathfindingService] Tests requiring pathfinding will be skipped.");
            }

            // Check if SceneDataService is available on its external endpoint.
            SceneDataServiceReady = await WaitForSceneDataServiceAsync();
            Log($"  SceneDataService (5003): {SceneDataServiceReady}");
            if (!SceneDataServiceReady)
            {
                Log("  [SceneDataService] WARNING: SceneDataService is not available on port 5003.");
                Log("  [SceneDataService] Likely cause: WWOW_DATA_DIR is not set or SceneDataService.dll is not built.");
                Log("  [SceneDataService] BG workers will still launch and retry scene-slice refresh on demand once the service becomes available.");
            }

            // Start background crash monitoring
            StartCrashMonitor();
        }
        else
        {
            UnavailableReason = "WoWStateManager (port 8088) not available. Start it manually before running integration tests.";
            Log($"SKIP: {UnavailableReason}");
        }
    }

    #endregion

    #region Crash Monitoring — Background Process Health Checks

    /// <summary>
    /// Starts a background task that polls StateManager and WoW.exe processes for crashes.
    /// When a crash is detected, sets <see cref="ClientCrashed"/> and <see cref="CrashMessage"/>
    /// so tests can fail fast instead of timing out.
    /// </summary>
    private void StartCrashMonitor()
    {
        _crashMonitorCts = new CancellationTokenSource();
        var ct = _crashMonitorCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Check StateManager — if it exits, nothing can recover it
                    if (_stateManagerProcess != null && _stateManagerProcess.HasExited)
                    {
                        var msg = $"StateManager crashed (exit code {_stateManagerProcess.ExitCode}) at {DateTime.Now:HH:mm:ss}";
                        Log($"  [CrashMonitor] {msg}");
                        ClientCrashed = true;
                        CrashMessage = msg;
                        return; // StateManager is unrecoverable — stop monitoring
                    }

                    // Check tracked WoW.exe PIDs — StateManager auto-restarts WoW.exe,
                    // so set crash flag but keep monitoring. OutputDataReceived handler
                    // clears the flag when a new healthy PID is registered.
                    if (!ClientCrashed)
                    {
                        List<int> pids;
                        lock (_managedWoWPids)
                            pids = new List<int>(_managedWoWPids);

                        foreach (var pid in pids)
                        {
                            if (IsProcessDead(pid))
                            {
                                var msg = $"WoW.exe PID {pid} crashed at {DateTime.Now:HH:mm:ss}";
                                Log($"  [CrashMonitor] {msg}");
                                ClientCrashed = true;
                                CrashMessage = msg;
                                // Remove dead PID so we don't re-detect the same crash
                                lock (_managedWoWPids)
                                    _managedWoWPids.Remove(pid);
                                break; // Continue loop — will detect recovery via OutputDataReceived
                            }
                        }
                    }

                    // Check PathfindingService (port 5001) — if it crashes mid-suite,
                    // navigation tests will fail with mysterious timeouts.
                    // Use TCP connect check instead of bind check — Docker-forwarded ports
                    // appear "free" to IsPortInUse even when the container is healthy.
                    if (PathfindingServiceReady && !await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", 5001, 2000))
                    {
                        PathfindingServiceReady = false;
                        var msg = $"PathfindingService (port 5001) stopped responding at {DateTime.Now:HH:mm:ss}";
                        Log($"  [CrashMonitor] {msg}");
                    }

                    // Check SceneDataService (port 5003)
                    if (SceneDataServiceReady && !await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", 5003, 2000))
                    {
                        SceneDataServiceReady = false;
                        var msg = $"SceneDataService (port 5003) stopped responding at {DateTime.Now:HH:mm:ss}";
                        Log($"  [CrashMonitor] {msg}");
                    }

                    await Task.Delay(2000, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Best-effort monitoring — don't crash the monitor itself
                }
            }
        }, ct);
    }

    /// <summary>
    /// Throws <see cref="Xunit.Sdk.XunitException"/> if a client has crashed.
    /// When a crash is detected, waits up to 30 seconds for StateManager to restart
    /// WoW.exe before failing. The crash flag is cleared when a new healthy PID is
    /// registered (see OutputDataReceived handler).
    /// </summary>
    public void AssertClientAlive()
    {
        if (!ClientCrashed)
            return;

        // Give StateManager time to restart WoW.exe — it auto-recovers from crashes.
        // The OutputDataReceived handler clears ClientCrashed when a new PID arrives.
        var crashMsg = CrashMessage;
        for (int i = 0; i < 30; i++)
        {
            Thread.Sleep(1000);
            if (!ClientCrashed)
            {
                Log($"  [CrashMonitor] Client recovered after {i + 1}s — new WoW.exe launched");
                return;
            }
        }

        Assert.Fail($"Client crashed and did not recover within 30s: {crashMsg}");
    }

    #endregion

    #region Port and Network Utilities

    /// <summary>Check if a TCP port is currently in use.</summary>
    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false; // Port is free
        }
        catch (SocketException)
        {
            return true; // Port in use
        }
    }

    #endregion

    #region FastCall.dll Verification

    /// <summary>
    /// Verifies that FastCall.dll in Bot output is the correct version (13KB, 25 exports
    /// including Safe* SEH wrappers). If a stale >20KB version is found, replaces it from
    /// ForegroundBotRunner/Resources/. This prevents ERROR #132 crashes caused by missing
    /// Safe* entry points (EntryPointNotFoundException on WoW's main thread every second).
    /// </summary>
    private void VerifyFastCallDll()
    {
        try
        {
            var fastCallPath = Path.Combine(BotOutputDirectory, "FastCall.dll");
            if (!File.Exists(fastCallPath))
            {
                Log($"  [FastCall] WARNING: FastCall.dll not found at {fastCallPath}");
                return;
            }

            var fileInfo = new FileInfo(fastCallPath);
            Log($"  [FastCall] {fastCallPath}: {fileInfo.Length} bytes, modified {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

            // The correct FastCall.dll is ~14KB (25 exports with Safe* SEH wrappers).
            // The OLD version is ~40KB (9 exports, no Safe* functions).
            // If we detect a stale version, replace it from the canonical source.
            if (fileInfo.Length > 20_000)
            {
                Log($"  [FastCall] STALE DLL DETECTED ({fileInfo.Length} bytes > 20KB). Replacing from Resources/...");
                var sourcePath = FindCanonicalFastCallDll();
                if (sourcePath != null)
                {
                    File.Copy(sourcePath, fastCallPath, overwrite: true);
                    var newInfo = new FileInfo(fastCallPath);
                    Log($"  [FastCall] Replaced with {sourcePath}: {newInfo.Length} bytes");
                }
                else
                {
                    Log($"  [FastCall] ERROR: Cannot find canonical FastCall.dll in Resources/. Build will likely crash.");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"  [FastCall] Error verifying FastCall.dll: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the canonical FastCall.dll from ForegroundBotRunner/Resources/.
    /// Walks up from test output dir to solution root, then checks the known path.
    /// </summary>
    private static string? FindCanonicalFastCallDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "ForegroundBotRunner", "Resources", "FastCall.dll");
            if (File.Exists(candidate))
            {
                var info = new FileInfo(candidate);
                // Sanity check: canonical version should be <20KB
                if (info.Length < 20_000)
                    return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    #endregion

    #region Settings Management — EnsureSettings / RestartWithSettings

    /// <summary>
    /// Ensures StateManager is running with the given settings. If already running
    /// with identical settings and coordinator flag, this is a no-op — avoiding
    /// the expensive teardown + relaunch cycle.
    /// </summary>
    public async Task EnsureSettingsAsync(string settingsPath)
    {
        var normalized = Path.GetFullPath(settingsPath);
        var coordFlag = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") ?? "1";

        Log($"[EnsureSettings] Requested={Path.GetFileName(settingsPath)}, Active={_activeSettingsPath ?? "(null)"}, " +
            $"coord={coordFlag}/{_activeCoordinatorFlag ?? "(null)"}, ready={ServicesReady}");

        if (ServicesReady && _activeSettingsPath == normalized && _activeCoordinatorFlag == coordFlag)
        {
            Log($"[EnsureSettings] Already running with {Path.GetFileName(settingsPath)} (coordinator={coordFlag}), skipping restart.");
            return;
        }

        Log($"[EnsureSettings] Settings differ — restarting StateManager.");
        await RestartWithSettingsAsync(settingsPath);
    }

    /// <summary>
    /// Unconditionally tears down and relaunches StateManager with the given settings.
    /// Internal implementation — callers should use <see cref="EnsureSettingsAsync"/>.
    /// </summary>
    private async Task RestartWithSettingsAsync(string settingsPath)
    {
        Log($"[Restart] Tearing down for reconfigure with: {settingsPath}");
        await TeardownProcessesAsync();

        // Reset state for fresh init
        ServicesReady = false;
        ClientCrashed = false;
        CrashMessage = null;
        UnavailableReason = null;
        PathfindingServiceReady = false;
        SceneDataServiceReady = false;

        CustomSettingsPath = settingsPath;
        await InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        // Write crash summary to service logs before teardown
        if (ClientCrashed && ServiceLogs != null)
            ServiceLogs.WriteSummary($"CRASH DETECTED: {CrashMessage}");
        ServiceLogs?.Dispose();
        ServiceLogs = null;

        await TeardownProcessesAsync();

        // Always release the machine-wide mutex so the next test run can proceed
        try
        {
            if (_mutexHeld)
            {
                _globalMutex?.ReleaseMutex();
                _mutexHeld = false;
            }
            _globalMutex?.Dispose();
            _globalMutex = null;
        }
        catch (ApplicationException)
        {
            // Mutex was not owned — this is fine (e.g., InitializeAsync failed before acquiring)
        }
    }

    #endregion

    #region Process Lifecycle — Teardown, Stale Cleanup, Force Kill

    private async Task TeardownProcessesAsync()
    {
        // Stop crash monitor first
        try { _crashMonitorCts?.Cancel(); } catch { }

        int wowKilled = 0;

        // Snapshot PIDs we launched so we can check for orphans after cleanup.
        // Copy under lock to avoid racing with the stdout capture callback.
        int? stateManagerPid = null;
        List<int> launchedPids;
        lock (_managedWoWPids)
            launchedPids = new List<int>(_managedWoWPids);
        try
        {
            stateManagerPid = _stateManagerProcess?.Id;
        }
        catch { /* process may have exited */ }

        // 1. Kill StateManager FIRST — prevents its monitoring loop (every 5s) from
        //    relaunching WoW.exe after we kill bot processes. StateManager detects
        //    terminated bots and re-creates them via ApplyDesiredWorkerState.
        if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
        {
            Log("Stopping StateManager (must die first to prevent WoW.exe relaunch)...");
            try
            {
                _stateManagerProcess.Kill(entireProcessTree: true);
                _stateManagerProcess.WaitForExit(5000);
                Log("  StateManager process terminated.");
            }
            catch (Exception ex)
            {
                Log($"  Warning: Could not stop StateManager: {ex.Message}");
            }
            _stateManagerProcess.Dispose();
            _stateManagerProcess = null;
        }

        // 2. Brief delay for the process tree to finish dying
        await Task.Delay(1000);

        // 3. Kill ALL WoW.exe processes spawned by this fixture.
        foreach (var proc in Process.GetProcessesByName("WoW"))
        {
            try
            {
                Log($"Cleanup: killing WoW.exe PID {proc.Id}");
                if (await ForceKillProcessAsync(proc, "WoW"))
                    wowKilled++;
            }
            finally { proc.Dispose(); }
        }

        // 4. Kill orphaned BackgroundBotRunner processes
        int bgKilled = 0;
        foreach (var bgPid in FindDotnetProcessesByDll("BackgroundBotRunner"))
        {
            try
            {
                var proc = Process.GetProcessById(bgPid);
                try
                {
                    Log($"Cleanup: killing BackgroundBotRunner PID {bgPid}");
                    if (await ForceKillProcessAsync(proc, "BGBot"))
                        bgKilled++;
                }
                finally { proc.Dispose(); }
            }
            catch (ArgumentException) { /* already dead */ }
        }

        lock (_managedWoWPids)
            _managedWoWPids.Clear();

        if (wowKilled + bgKilled > 0)
            Log($"Dispose cleanup summary: {wowKilled} WoW.exe, {bgKilled} BackgroundBotRunner killed.");

        // 6. Orphan detection
        await ForceKillOrphanedProcessesAsync(stateManagerPid, launchedPids);
    }

    /// <summary>
    /// Post-cleanup orphan detection and force-kill. Waits briefly after normal cleanup,
    /// then checks if any PIDs that THIS fixture launched are still alive. If found,
    /// force-kills them to prevent runaway resource consumption (e.g., WoW.exe relaunch
    /// loops that exhaust Windows handles overnight).
    ///
    /// Safe: Only kills PIDs from <see cref="_managedWoWPids"/> and the StateManager process —
    /// never blanket-kills by process name.
    /// </summary>
    private async Task ForceKillOrphanedProcessesAsync(int? stateManagerPid, List<int> wowPids)
    {
        var allTrackedPids = new List<(int pid, string source)>();
        if (stateManagerPid.HasValue)
            allTrackedPids.Add((stateManagerPid.Value, "StateManager"));
        foreach (var pid in wowPids)
            allTrackedPids.Add((pid, "WoW.exe"));

        if (allTrackedPids.Count == 0)
        {
            Log("[OrphanCheck] No PIDs were tracked by this fixture — skipping orphan check.");
            return;
        }

        Log($"[OrphanCheck] Waiting 10s before checking {allTrackedPids.Count} tracked PIDs for orphans...");
        await Task.Delay(10_000);

        var orphanCount = 0;
        foreach (var (pid, source) in allTrackedPids)
        {
            if (IsProcessDead(pid))
                continue;

            orphanCount++;
            Log($"[OrphanCheck] Orphan detected: PID {pid} ({source}) — force-killing.");
            try
            {
                var proc = Process.GetProcessById(pid);
                try { await ForceKillProcessAsync(proc, $"OrphanKill-{source}"); }
                finally { proc.Dispose(); }
            }
            catch (ArgumentException) { /* exited between check and kill — fine */ }
        }

        Log(orphanCount > 0
            ? $"[OrphanCheck] Force-killed {orphanCount} orphaned process(es)."
            : $"[OrphanCheck] All {allTrackedPids.Count} tracked PIDs confirmed dead. No orphans detected.");
    }

    /// <summary>
    /// Kill any WoWStateManager.exe and WoW.exe processes
    /// left over from previous test runs. This prevents stale StateManagers from
    /// intercepting new bot connections. WoW.exe kills include a MaNGOS session
    /// cooldown so the auth server doesn't reject the next login attempt.
    /// </summary>
    private async Task KillStaleProcessesAsync()
    {
        int smKilled = 0, wowKilled = 0;

        // 1. Kill stale StateManagers (repo-scoped — only kills processes from this repo)
        foreach (var proc in Process.GetProcessesByName("WoWStateManager"))
        {
            try
            {
                var modulePath = proc.MainModule?.FileName ?? "";
                if (!modulePath.Contains(RepoMarker, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  [Cleanup] Skipping WoWStateManager PID {proc.Id} (not repo-scoped: {modulePath})");
                    continue;
                }

                Log($"  [Cleanup] Killing stale WoWStateManager.exe PID {proc.Id} (started {proc.StartTime:HH:mm:ss})");
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
                smKilled++;
            }
            catch (Exception ex)
            {
                Log($"  [Cleanup] Could not kill WoWStateManager PID {proc.Id}: {ex.Message}");
            }
            finally { proc.Dispose(); }
        }

        // 2. Kill orphaned WoW.exe (FG bot shells from previous StateManager runs)
        foreach (var proc in Process.GetProcessesByName("WoW"))
        {
            try
            {
                Log($"  [Cleanup] Killing orphaned WoW.exe PID {proc.Id}");
                if (await ForceKillProcessAsync(proc, "Cleanup-WoW"))
                    wowKilled++;
            }
            finally { proc.Dispose(); }
        }

        int totalKilled = smKilled + wowKilled;
        if (totalKilled > 0)
        {
            Log($"  [Cleanup] Killed: {smKilled} StateManager, {wowKilled} WoW.exe");

            // Wait for MaNGOS auth server to clear stale sessions (rejects duplicates otherwise)
            // Also wait for port release after StateManager kill
            int waitMs = wowKilled > 0 ? 5000 : 3000;
            Log($"  [Cleanup] Waiting {waitMs / 1000}s for MaNGOS session cleanup and port release...");
            await Task.Delay(waitMs);
        }
        else
        {
            Log("  [Cleanup] No stale processes found — clean environment.");
        }
    }

    #endregion

    #region External Service Readiness — Pathfinding and SceneData

    /// <summary>
    /// Waits for PathfindingService to become available on port 5001.
    /// The service is treated as an external dependency and must be launched separately.
    /// </summary>
    private async Task<bool> WaitForPathfindingServiceAsync()
    {
        const int pathfindingPort = 5001;
        const int maxWaitSeconds = 30;

        // If port is already in use, it's ready
        if (IsPortInUse(pathfindingPort))
        {
            Log($"  [PathfindingService] Already listening on port {pathfindingPort}.");
            return true;
        }

        Log($"  [PathfindingService] Waiting up to {maxWaitSeconds}s for port {pathfindingPort}...");
        for (int i = 0; i < maxWaitSeconds; i++)
        {
            var ready = await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", pathfindingPort, 1000);
            if (ready)
            {
                Log($"  [PathfindingService] Ready on port {pathfindingPort} after {i + 1}s.");
                return true;
            }

            if (i % 10 == 9)
                Log($"  [PathfindingService] Still waiting... ({i + 1}s)");

            await Task.Delay(1000);
        }

        Log($"  [PathfindingService] Did not become ready within {maxWaitSeconds}s.");
        return false;
    }

    /// <summary>
    /// Waits for SceneDataService to become available on port 5003.
    /// The service is treated as an external dependency and must be launched separately.
    /// </summary>
    private async Task<bool> WaitForSceneDataServiceAsync()
    {
        const int sceneDataPort = 5003;
        const int maxWaitSeconds = 30;

        // If port is already in use, it's ready
        if (IsPortInUse(sceneDataPort))
        {
            Log($"  [SceneDataService] Already listening on port {sceneDataPort}.");
            return true;
        }

        Log($"  [SceneDataService] Waiting up to {maxWaitSeconds}s for port {sceneDataPort}...");
        for (int i = 0; i < maxWaitSeconds; i++)
        {
            var ready = await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", sceneDataPort, 1000);
            if (ready)
            {
                Log($"  [SceneDataService] Ready on port {sceneDataPort} after {i + 1}s.");
                return true;
            }

            if (i % 10 == 9)
                Log($"  [SceneDataService] Still waiting... ({i + 1}s)");

            await Task.Delay(1000);
        }

        Log($"  [SceneDataService] Did not become ready within {maxWaitSeconds}s.");
        return false;
    }

    #endregion

    #region StateManager Launch — Process Start, Output Capture, Ready Wait

    private async Task<bool> TryStartStateManagerAsync()
    {
        try
        {
            var smBinary = FindStateManagerExecutable();
            if (smBinary == null)
            {
                Log("  [StateManager] Could not find WoWStateManager.exe or WoWStateManager.dll - cannot auto-start.");
                Log("  [StateManager] Build the solution first, then re-run the test.");
                return false;
            }

            var smDir = Path.GetDirectoryName(smBinary)!;
            var useDotnetHost = smBinary.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            Log($"  [StateManager] Starting {(useDotnetHost ? "dll" : "exe")}: {smBinary}");

            var envRecording = Environment.GetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING");
            var envRecordingArtifacts = Environment.GetEnvironmentVariable("WWOW_ENABLE_RECORDING_ARTIFACTS");

            var psi = new ProcessStartInfo
            {
                FileName = useDotnetHost ? "dotnet" : smBinary,
                Arguments = useDotnetHost ? $"\"{smBinary}\"" : string.Empty,
                WorkingDirectory = smDir,
                UseShellExecute = false,
                CreateNoWindow = Environment.GetEnvironmentVariable("WWOW_SHOW_WINDOWS") != "1",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var x86DotnetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet");
            if (Directory.Exists(x86DotnetRoot))
            {
                psi.Environment["DOTNET_ROOT(x86)"] = x86DotnetRoot;
                Log($"  [StateManager] Set DOTNET_ROOT(x86) = {x86DotnetRoot}");
            }

            var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(dotnetEnvironment))
                dotnetEnvironment = "test";
            psi.Environment["DOTNET_ENVIRONMENT"] = dotnetEnvironment;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = dotnetEnvironment;
            Log($"  [StateManager] DOTNET_ENVIRONMENT={dotnetEnvironment}");

            // Always show console windows for child processes (BG/FG bot runners)
            // so test runners can observe bot output in real time.
            psi.Environment["WWOW_SHOW_WINDOWS"] = "1";

            if (!string.IsNullOrEmpty(envRecording))
                psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = envRecording;
            if (!string.IsNullOrEmpty(envRecordingArtifacts))
                psi.Environment["WWOW_ENABLE_RECORDING_ARTIFACTS"] = envRecordingArtifacts;

            // Pass custom settings override to StateManager if configured
            Log($"  [StateManager] CustomSettingsPath={CustomSettingsPath ?? "(null)"}");
            if (!string.IsNullOrEmpty(CustomSettingsPath) && File.Exists(CustomSettingsPath))
            {
                psi.Environment["WWOW_SETTINGS_OVERRIDE"] = CustomSettingsPath;
                Log($"  [StateManager] Using custom settings: {CustomSettingsPath}");
            }
            else if (!string.IsNullOrEmpty(CustomSettingsPath))
            {
                Log($"  [StateManager] WARNING: CustomSettingsPath set but file not found: {CustomSettingsPath}");
            }

            var loaderDllPath = Path.Combine(BotOutputDirectory, "Loader.dll");
            if (File.Exists(loaderDllPath))
            {
                psi.Environment["WWOW_LOADER_DLL_PATH"] = loaderDllPath;
                Log($"  [StateManager] WWOW_LOADER_DLL_PATH={loaderDllPath}");
            }
            else
            {
                Log($"  [StateManager] WARNING: Loader.dll not found at expected bot output path: {loaderDllPath}");
            }

            // Reduce log level to Warning to prevent stdout pipe saturation with 10+ bots.
            // .NET Host reads Logging__LogLevel__Default from environment (double underscore = : separator).
            psi.Environment["Logging__LogLevel__Default"] = "Warning";
            psi.Environment["WWOW_LOG_LEVEL"] = "Warning";
            psi.Environment["WWOW_CONSOLE_LOG_LEVEL"] = "Warning";
            psi.Environment["WWOW_FILE_LOG_LEVEL"] = "Warning";

            // Explicitly forward coordinator toggle so restarts inherit the test's intent
            var coordDisable = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR");
            if (coordDisable != null)
            {
                psi.Environment["WWOW_TEST_DISABLE_COORDINATOR"] = coordDisable;
                Log($"  [StateManager] WWOW_TEST_DISABLE_COORDINATOR={coordDisable}");
            }

            _stateManagerProcess = Process.Start(psi);
            if (_stateManagerProcess == null)
            {
                Log("  [StateManager] Failed to start process.");
                return false;
            }

            Log($"  [StateManager] Process started (PID: {_stateManagerProcess.Id}). Waiting for port 8088...");

            _stateManagerProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Capture ALL output lines for test assertions (e.g., SMSG_NEW_WORLD detection)
                    _capturedOutput.Add(e.Data);

                    // Write ALL lines to per-service log files (no filtering)
                    ServiceLogs?.ClassifyAndWrite(e.Data);

                    // Throttle verbose repetitive lines to reduce test host memory pressure.
                    // StateManager emits snapshot queries, equipment dumps, and skill snapshots
                    // at 350ms intervals — thousands of lines per minute. Only log important lines.
                    if (!IsVerboseStateManagerLine(e.Data))
                        Log($"  [StateManager-OUT] {e.Data}");
                    var match = WoWPidRegex.Match(e.Data);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var wowPid))
                    {
                        lock (_managedWoWPids)
                        {
                            // Prune dead PIDs from previous launch attempts — StateManager
                            // auto-restarts WoW.exe on crash, so stale PIDs would trigger
                            // false crash detection.
                            _managedWoWPids.RemoveAll(p => IsProcessDead(p));
                            _managedWoWPids.Add(wowPid);

                            // Clear sticky crash flag if a healthy replacement has launched
                            if (ClientCrashed)
                            {
                                Log($"  [BotServiceFixture] Clearing crash flag — WoW.exe restarted (new PID {wowPid})");
                                ClientCrashed = false;
                                CrashMessage = null;
                            }
                        }
                        Log($"  [BotServiceFixture] Tracking WoW.exe PID {wowPid} for cleanup");
                    }
                }
            };
            _stateManagerProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ServiceLogs?.WriteStateManagerError(e.Data);
                    Log($"  [StateManager-ERR] {e.Data}");
                }
            };
            _stateManagerProcess.BeginOutputReadLine();
            _stateManagerProcess.BeginErrorReadLine();

            const int maxWaitSeconds = 90;
            for (int i = 0; i < maxWaitSeconds; i++)
            {
                if (_stateManagerProcess.HasExited)
                {
                    Log($"  [StateManager] Process exited with code {_stateManagerProcess.ExitCode} before becoming ready.");
                    return false;
                }

                var ready = await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", 8088, 1000);
                if (ready)
                {
                    Log($"  [StateManager] Ready on port 8088 after {i + 1}s.");
                    return true;
                }

                if (i % 10 == 9)
                    Log($"  [StateManager] Still waiting... ({i + 1}s)");

                await Task.Delay(1000);
            }

            Log($"  [StateManager] Did not become ready within {maxWaitSeconds}s.");
            return false;
        }
        catch (Exception ex)
        {
            Log($"  [StateManager] Error starting: {ex.Message}");
            return false;
        }
    }

    private static string? FindStateManagerExecutable()
    {
        var botDir = BotOutputDirectory;
        var exeInBot = Path.Combine(botDir, "WoWStateManager.exe");
        if (File.Exists(exeInBot))
            return exeInBot;
        var dllInBot = Path.Combine(botDir, "WoWStateManager.dll");
        if (File.Exists(dllInBot))
            return dllInBot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Bot", "Debug", "net8.0", "WoWStateManager.exe");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "Bot", "Debug", "net8.0", "WoWStateManager.dll");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "Bot", "net8.0", "WoWStateManager.exe");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "Bot", "net8.0", "WoWStateManager.dll");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        const string fallback = @"E:\repos\BloogBot\Bot\Debug\net8.0\WoWStateManager.exe";
        if (File.Exists(fallback))
            return fallback;

        const string fallbackDll = @"E:\repos\BloogBot\Bot\Debug\net8.0\WoWStateManager.dll";
        return File.Exists(fallbackDll) ? fallbackDll : null;
    }

    /// <summary>
    /// Reliably kill a process by PID. Uses "taskkill /F /PID" as the primary method
    /// (proven to work for WoW.exe created via native CreateProcess), with Process.Kill()
    /// and CloseMainWindow() as fallbacks. Verifies the process is actually dead before
    /// returning success.
    ///
    /// Async to avoid blocking the test thread during post-kill waits.
    /// </summary>
    private async Task<bool> ForceKillProcessAsync(Process proc, string label)
    {
        var pid = proc.Id;
        try
        {
            if (proc.HasExited) return true;
        }
        catch { /* can't check — proceed with kill attempts */ }

        // Attempt 1: taskkill /F /PID — most reliable for WoW.exe (native CreateProcess child)
        try
        {
            var taskKill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /PID {pid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (taskKill != null)
            {
                taskKill.WaitForExit(5000);
                var stdout = taskKill.StandardOutput.ReadToEnd();
                var stderr = taskKill.StandardError.ReadToEnd();
                var exitCode = taskKill.ExitCode;
                taskKill.Dispose();
                Log($"  [{label}] taskkill /F /PID {pid}: exit={exitCode} stdout='{stdout.Trim()}' stderr='{stderr.Trim()}'");

                if (exitCode == 0)
                {
                    // Wait for the process to actually terminate
                    await Task.Delay(1000);
                    if (IsProcessDead(pid))
                        return true;
                    Log($"  [{label}] taskkill reported success but PID {pid} still alive!");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"  [{label}] taskkill failed for PID {pid}: {ex.Message}");
        }

        // Attempt 2: .NET Process.Kill()
        try
        {
            proc.Kill();
            proc.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            Log($"  [{label}] Process.Kill() failed for PID {pid}: {ex.Message}");
        }
        if (IsProcessDead(pid))
        {
            Log($"  [{label}] Process.Kill() worked for PID {pid}");
            return true;
        }

        // Attempt 3: CloseMainWindow (sends WM_CLOSE — graceful)
        try
        {
            proc.CloseMainWindow();
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Log($"  [{label}] CloseMainWindow failed for PID {pid}: {ex.Message}");
        }
        if (IsProcessDead(pid))
        {
            Log($"  [{label}] CloseMainWindow worked for PID {pid}");
            return true;
        }

        Log($"  [{label}] FAILED to kill PID {pid} after all attempts!");
        return false;
    }

    /// <summary>
    /// Find dotnet.exe processes hosting a specific DLL by querying wmic for command lines.
    /// Returns PIDs of matching processes.
    /// </summary>
    private static List<int> FindDotnetProcessesByDll(string dllNameFragment)
    {
        var pids = new List<int>();
        try
        {
            using var wmic = Process.Start(new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "process where \"name='dotnet.exe'\" get ProcessId,CommandLine /FORMAT:LIST",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (wmic == null) return pids;
            var output = wmic.StandardOutput.ReadToEnd();
            wmic.WaitForExit(5000);

            // wmic LIST format: "CommandLine=...\nProcessId=...\n"
            string? currentCmdLine = null;
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("CommandLine=", StringComparison.OrdinalIgnoreCase))
                    currentCmdLine = line["CommandLine=".Length..];
                else if (line.StartsWith("ProcessId=", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentCmdLine != null
                        && currentCmdLine.Contains(dllNameFragment, StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(line["ProcessId=".Length..].Trim(), out var pid)
                        && pid > 0)
                    {
                        pids.Add(pid);
                    }
                    currentCmdLine = null;
                }
            }
        }
        catch { /* best-effort */ }
        return pids;
    }

    /// <summary>Check if a process has actually terminated (by PID).</summary>
    private static bool IsProcessDead(int pid)
    {
        try
        {
            Process.GetProcessById(pid);
            return false; // still alive
        }
        catch (ArgumentException)
        {
            return true; // no process with this PID — it's dead
        }
    }

    #endregion

    #region Logging and Output Filtering

    /// <summary>
    /// Filters out high-frequency StateManager output lines that flood test output.
    /// These lines are emitted every ~350ms per bot and accumulate thousands of entries
    /// during a full test run, consuming significant memory in the test host process.
    /// </summary>
    private static bool IsVerboseStateManagerLine(string line)
    {
        // These patterns appear hundreds/thousands of times per test run.
        // Suppressing them prevents ITestOutputHelper memory pressure crash.
        if (line.Contains("Snapshot query:")
            || line.Contains("[SkillSnapshot]")
            || line.Contains("[BOT RUNNER] Equipment:")
            || line.Contains("[BOT RUNNER] Protobuf Inventory")
            || line.Contains("SMSG_MONSTER_MOVE")
            || line.Contains("SNAPSHOT_RECEIVED:")
            || line.Contains("[BOT RUNNER] Equipment slot")
            || line.Contains("Received world state update message")
            || line.Contains("[ReadValuesUpdateBlock]")
            || line.Contains("GAMEOBJ CREATED")
            || line.Contains("INVENTORY_CHANGE_FAILURE")
            || line.Contains("[WorldClient] INVENTORY_CHANGE_FAILURE")
            || line.Contains("[BOT RUNNER] Inventory changed:")
            // High-frequency per-tick lines that saturate stdout with 10+ bots
            || line.Contains("[DIAG] DeathRecovery:")
            || line.Contains("HandleUpdateObject")
            || line.Contains("ParseCreateObject")
            || line.Contains("[DIAG] [TICK#")
            || line.Contains("SMSG_COMPRESSED_UPDATE_OBJECT")
            || line.Contains("StateChangeResponse dispatched")
            || line.Contains("No handler registered for opcode SMSG_PARTY_MEMBER_STATS")
            || line.Contains("[SplineController] Added spline")
            || line.Contains("[SplineController] Spline finished")
            // Action delivery logs temporarily unfiltered for GOTO debugging
            // || line.Contains("QUEUED ACTION for")
            // || line.Contains("Action forward: queued")
            // || line.Contains("INJECTING PENDING ACTION")
            // || line.Contains("DELIVERING ACTION")
            // FG crash/relaunch events must NOT be filtered — need visibility
            // || line.Contains("Detected terminated bot process")
            // || line.Contains("attempting re-launch")
            // Packet hex dump spam from Console.WriteLine in PacketPipeline
            || line.Contains("[RX]")
            || line.Contains("[TX]")
            || line.Contains("payload:"))
            return true;

        // Filter orphaned .NET logger prefix lines (no content, just the logger category)
        // e.g. "info: WoWStateManager.StateManagerWorker[0]" with no trailing content
        var trimmed = line.TrimEnd();
        if (trimmed.EndsWith("[0]") && trimmed.StartsWith("info:"))
            return true;

        return false;
    }

    private void Log(string message)
    {
        if (_output != null)
        {
            try
            {
                _output.WriteLine(message);
                return;
            }
            catch
            {
                // ITestOutputHelper may throw if the test has finished.
            }
        }

        Console.WriteLine($"[BotServiceFixture] {message}");
    }

    #endregion
}
