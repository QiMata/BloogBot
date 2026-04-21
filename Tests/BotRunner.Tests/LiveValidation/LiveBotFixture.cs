using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Communication;
using Microsoft.Extensions.Logging;
using Tests.Infrastructure;
using WoWStateManager.Coordination;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// xUnit fixture that launches WoWStateManager (which spawns both a Background
/// headless client and a Foreground injected client) and waits for both bots
/// to enter the world before tests run.
///
/// Tests observe bots through <see cref="WoWActivitySnapshot"/> queries via
/// port 8088 and command them through <see cref="ActionForwardRequest"/>.
/// GM commands for test setup (teleport, level, items) go through SOAP.
///
/// Rules enforced:
///   - No WoWClient, BackgroundBotRunner, or ForegroundBotRunner created directly
///   - All bot lifecycle managed by StateManager
///   - Dual-client: both BG and FG bots must be in-world before tests start
/// </summary>
public partial class LiveBotFixture : IAsyncLifetime
{
    private const string CombatTestAccount = "COMBATTEST";
    private const string RecordingArtifactsEnvVar = "WWOW_ENABLE_RECORDING_ARTIFACTS";

    public IntegrationTestConfig Config { get; } = IntegrationTestConfig.FromEnvironment();

    private readonly BotServiceFixture _serviceFixture = new();

    private readonly ILoggerFactory _loggerFactory;

    private readonly ILogger _logger;


    private StateManagerTestClient? _stateManagerClient;

    private ITestOutputHelper? _testOutput;

    private readonly object _commandTrackingLock = new();

    private readonly Dictionary<string, int> _soapCommandCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<string, int>> _chatCommandCountsByAccount = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _lastPrintedChatCountByAccount = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _lastPrintedErrorCountByAccount = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _knownCharacterNamesByAccount = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _accountsWithConfirmedTaxiNodes = new(StringComparer.OrdinalIgnoreCase);

    private bool _fgResponsive = true;
    private string? _presetSettingsPath;
    private string? _previousRecordingArtifactsFlag;

    /// <summary>
    /// When true, <see cref="EnsureCleanCharacterStateAsync"/> skips group disbanding.
    /// Derived fixtures (e.g. RfcBotFixture) set this when a coordinator owns group lifecycle.
    /// </summary>
    protected bool SkipGroupCleanup { get; set; }

    /// <summary>
    /// Set custom settings path before InitializeAsync. Used by derived fixtures
    /// (e.g. RfcBotFixture) to launch with a specific config from the start.
    /// </summary>
    protected void SetCustomSettingsPath(string path)
    {
        _presetSettingsPath = path;
        _serviceFixture.CustomSettingsPath = path;
    }

    /// <summary>
    /// Resolves the path to a per-test settings file in LiveValidation/Settings/.
    /// Checks the build output directory first, then walks up to find the source file.
    /// </summary>
    protected static string? ResolveTestSettingsPath(string fileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "LiveValidation", "Settings", fileName);
        if (File.Exists(outputPath)) return outputPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "LiveValidation", "Settings", fileName);
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(dir.FullName, "Bot", "Release", "net8.0", "LiveValidation", "Settings", fileName);
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Tracks the normalized settings path that StateManager was last started with.
    /// Used by <see cref="EnsureSettingsAsync"/> to skip redundant teardown/relaunch cycles.
    /// </summary>
    private string? _activeSettingsPath;

    /// <summary>
    /// Snapshot of coordinator env var at last init, to detect when it changes between tests.
    /// </summary>
    private string? _activeCoordinatorFlag;

    public bool IsReady { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// Expected bot count from the settings JSON. ALL configured bots MUST connect.
    /// Any missing bot is an automatic failure — no partial-count workarounds.
    /// </summary>
    public int ExpectedBotCount { get; private set; }

    /// <summary>
    /// Timeout budget for the initial "wait for bots to enter world" loop.
    /// Derived fixtures can extend this for first-login cinematic flows.
    /// </summary>
    protected virtual TimeSpan InitialWorldEntryTimeout => TimeSpan.FromSeconds(120);

    /// <summary>
    /// Optional configured account set whose character names should be pre-seeded into the
    /// snapshot normalizer. Coordinator-backed fixtures override this to include their full
    /// roster so blank CharacterName snapshots can still satisfy the hydration gate.
    /// </summary>
    protected virtual IReadOnlyCollection<string> KnownAccountNamesForCharacterResolution => Array.Empty<string>();

    /// <summary>
    /// Whether PathfindingService is listening on port 5001.
    /// Tests that require pathfinding (corpse run, movement, gathering) should
    /// check this via <c>Skip.IfNot(fixture.IsPathfindingReady, ...)</c>.
    /// </summary>


    /// <summary>
    /// Whether PathfindingService is listening on port 5001.
    /// Tests that require pathfinding (corpse run, movement, gathering) should
    /// check this via <c>Skip.IfNot(fixture.IsPathfindingReady, ...)</c>.
    /// </summary>
    public bool IsPathfindingReady => _serviceFixture.PathfindingServiceReady;

    /// <summary>Per-service log manager. Null if no test name has been set.</summary>
    public ServiceLogManager? ServiceLogs => _serviceFixture.ServiceLogs;

    /// <summary>Initialize per-service logging for a specific test name.</summary>
    public void SetTestName(string testName) => _serviceFixture.SetTestName(testName);

    /// <summary>Whether any StateManager-managed process (StateManager or WoW.exe) has crashed.</summary>
    public bool ClientCrashed => _serviceFixture.ClientCrashed;

    /// <summary>Get all captured StateManager output lines for test assertions.</summary>
    public IReadOnlyCollection<string> GetStateManagerOutput() => _serviceFixture.GetCapturedOutput();

    /// <summary>Descriptive crash message, if any.</summary>
    public string? CrashMessage => _serviceFixture.CrashMessage;

    /// <summary>Snapshot of the Background (headless) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public WoWActivitySnapshot? BackgroundBot { get; private set; }

    /// <summary>Snapshot of the Foreground (injected) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>


    /// <summary>Snapshot of the Foreground (injected) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public WoWActivitySnapshot? ForegroundBot { get; private set; }

    /// <summary>All bot snapshots (both BG + FG). Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>


    /// <summary>All bot snapshots (both BG + FG). Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public List<WoWActivitySnapshot> AllBots { get; private set; } = [];

    /// <summary>Character name of the Background bot. Preserved across transient state flickers.</summary>


    /// <summary>Character name of the Background bot. Preserved across transient state flickers.</summary>
    public string? BgCharacterName { get; private set; }

    /// <summary>Character name of the Foreground bot. Preserved across transient state flickers.</summary>


    /// <summary>Character name of the Foreground bot. Preserved across transient state flickers.</summary>
    public string? FgCharacterName { get; private set; }

    /// <summary>Account name of the Background bot (from config).</summary>


    /// <summary>Account name of the Background bot (from config).</summary>
    public string? BgAccountName { get; private set; }

    /// <summary>Account name of the Foreground bot (from config).</summary>


    /// <summary>Account name of the Foreground bot (from config).</summary>
    public string? FgAccountName { get; private set; }

    /// <summary>Snapshot of the Combat Test bot (non-GM, never receives .gm on).</summary>
    public WoWActivitySnapshot? CombatTestBot { get; private set; }

    /// <summary>Account name of the Combat Test bot.</summary>
    public string? CombatTestAccountName { get; private set; }

    /// <summary>Character name of the Combat Test bot.</summary>
    public string? CombatTestCharacterName { get; private set; }

    // ---- Backward-compatible adapter properties (for test migration) ----
    // These expose the BG bot state through the old API surface so tests compile.
    // Tests should be migrated to use snapshot-based queries with dual-client assertions.

    /// <summary>Character name of the primary (BG) bot. Backward-compatible alias.</summary>


    // ---- Backward-compatible adapter properties (for test migration) ----
    // These expose the BG bot state through the old API surface so tests compile.
    // Tests should be migrated to use snapshot-based queries with dual-client assertions.

    /// <summary>Character name of the primary (BG) bot. Backward-compatible alias.</summary>
    public string? CharacterName => BgCharacterName;

    /// <summary>Character GUID of the primary (BG) bot from snapshot.</summary>


    /// <summary>Character GUID of the primary (BG) bot from snapshot.</summary>
    public ulong CharacterGuid => BackgroundBot?.Player?.Unit?.GameObject?.Base?.Guid ?? 0;

    /// <summary>
    /// True if FG bot snapshot exists AND the bot is strict-alive AND actions are not being dropped.
    /// Tests should use this instead of <c>ForegroundBot != null</c> to avoid cascading failures
    /// when FG WoW.exe crashed and relaunched but is stuck in dead/ghost/login state.
    /// </summary>
    public bool IsFgActionable => _fgResponsive && ForegroundBot != null && IsStrictAlive(ForegroundBot);

    /// <summary>
    /// Probes FG actionability by sending a harmless .targetself command and checking the result.
    /// Returns true if the action was forwarded successfully.
    /// </summary>
    public async Task<bool> CheckFgActionableAsync(bool requireTeleportProbe = true)
    {
        SeedExpectedAccountsFromStateManagerSettings();
        if (FgAccountName == null)
        {
            _fgResponsive = false;
            return false;
        }

        if (ForegroundBot == null)
        {
            await WaitForConfiguredAccountInWorldAsync(FgAccountName, TimeSpan.FromSeconds(20), "FG");
            await RefreshSnapshotsAsync();
        }

        if (ForegroundBot == null)
        {
            _fgResponsive = false;
            _logger.LogWarning("[FG-PROBE] FG snapshot never reached InWorld for configured account '{Account}'", FgAccountName);
            return false;
        }

        await RefreshSnapshotsAsync();
        if (ForegroundBot == null)
        {
            _fgResponsive = false;
            _logger.LogWarning("[FG-PROBE] FG snapshot null after refresh");
            return false;
        }

        if (!IsStrictAlive(ForegroundBot))
        {
            // Bot is connected but dead/ghost — attempt revive before giving up
            IsDeadOrGhostState(ForegroundBot, out var deathReason);
            _logger.LogWarning("[FG-PROBE] FG not strict-alive ({Reason}); attempting revive", deathReason);

            var charName = ForegroundBot.CharacterName ?? GetKnownCharacterNameForAccount(FgAccountName);
            if (!string.IsNullOrWhiteSpace(charName))
            {
                await RevivePlayerAsync(charName);
                var revived = await WaitForSnapshotConditionAsync(
                    FgAccountName,
                    IsStrictAlive,
                    TimeSpan.FromSeconds(10),
                    progressLabel: "FG revive-probe");

                if (!revived)
                {
                    _fgResponsive = false;
                    _logger.LogWarning("[FG-PROBE] Revive failed after 10s; FG not actionable");
                    return false;
                }

                _logger.LogInformation("[FG-PROBE] Revive succeeded, continuing probe");
                await RefreshSnapshotsAsync();
            }
            else
            {
                _fgResponsive = false;
                _logger.LogWarning("[FG-PROBE] No character name available for revive");
                return false;
            }
        }

        // Probe with a harmless self-target command
        var result = await SendActionAsync(FgAccountName, new ActionMessage
        {
            ActionType = ActionType.SendChat,
            Parameters = { new RequestParameter { StringParam = ".targetself" } }
        });

        if (result != ResponseResult.Success)
        {
            _fgResponsive = false;
            _logger.LogWarning("[FG-PROBE] Action forwarding returned {Result}; FG is not actionable", result);
            return false;
        }

        if (requireTeleportProbe)
        {
            var currentPos = ForegroundBot.Player?.Unit?.GameObject?.Base?.Position;
            var probeZ = currentPos != null && Math.Abs(currentPos.Z - SafeZoneZ) <= 5f ? 15f : SafeZoneZ;

            await BotTeleportAsync(FgAccountName, SafeZoneMap, SafeZoneX, SafeZoneY, probeZ);
            var probeSettled = await WaitForSnapshotConditionAsync(
                FgAccountName,
                snap =>
                {
                    if (!IsStrictAlive(snap))
                        return false;

                    var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
                    return pos != null && Distance3D(pos.X, pos.Y, pos.Z, SafeZoneX, SafeZoneY, probeZ) <= 8f;
                },
                TimeSpan.FromSeconds(6),
                pollIntervalMs: 500,
                progressLabel: "FG teleport probe");

            if (!probeSettled)
            {
                _fgResponsive = false;
                var fgSnap = await GetSnapshotAsync(FgAccountName);
                var pos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
                _logger.LogWarning("[FG-PROBE] FG failed teleport responsiveness probe. pos=({X:F1},{Y:F1},{Z:F1}) target=({TargetX:F1},{TargetY:F1},{TargetZ:F1})",
                    pos?.X ?? 0, pos?.Y ?? 0, pos?.Z ?? 0, SafeZoneX, SafeZoneY, probeZ);
                return false;
            }

            if (Math.Abs(probeZ - SafeZoneZ) > 0.1f)
            {
                await BotTeleportAsync(FgAccountName, SafeZoneMap, SafeZoneX, SafeZoneY, SafeZoneZ);
                var restored = await WaitForSnapshotConditionAsync(
                    FgAccountName,
                    snap =>
                    {
                        if (!IsStrictAlive(snap))
                            return false;

                        var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
                        return pos != null && Distance3D(pos.X, pos.Y, pos.Z, SafeZoneX, SafeZoneY, SafeZoneZ) <= 8f;
                    },
                    TimeSpan.FromSeconds(6),
                    pollIntervalMs: 500,
                    progressLabel: "FG teleport probe restore");

                if (!restored)
                {
                    _fgResponsive = false;
                    _logger.LogWarning("[FG-PROBE] FG failed restore leg of teleport responsiveness probe.");
                    return false;
                }
            }
        }
        else
        {
            _logger.LogInformation("[FG-PROBE] Skipping teleport responsiveness probe for this caller.");
        }

        _fgResponsive = true;
        return true;
    }


    public LiveBotFixture()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        _logger = _loggerFactory.CreateLogger<LiveBotFixture>();
    }


    public void SetOutput(ITestOutputHelper output)
    {
        // Wrap with DualOutputHelper to also write to a per-test-class log file.
        // StackFrame(1) gets the calling test class constructor.
        var callerType = new StackFrame(1).GetMethod()?.DeclaringType?.Name ?? "UnknownTest";
        var logDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults", "LiveLogs");
        var logPath = Path.Combine(logDir, $"{callerType}.log");

        (_testOutput as IDisposable)?.Dispose(); // close previous log if any
        _testOutput = new DualOutputHelper(output, logPath);
        _serviceFixture.SetOutput(_testOutput);
    }


    public async Task InitializeAsync()
    {
        try
        {
            _previousRecordingArtifactsFlag = Environment.GetEnvironmentVariable(RecordingArtifactsEnvVar);
            Environment.SetEnvironmentVariable(RecordingArtifactsEnvVar, "1");
            EnsurePhysicsDataDirectory();

            // Live corpse-flow tests must own the release step via explicit client action.
            // Disable BotRunner auto-release so death setup remains deterministic.
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK", "1");
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK", "1");
            // Native physics ERR logs can explode during mass live runs and exhaust test-host memory.
            // Keep them masked unless explicitly enabled for focused diagnostics.
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VMAP_PHYS_LOG_MASK")))
                Environment.SetEnvironmentVariable("VMAP_PHYS_LOG_MASK", "0");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VMAP_PHYS_LOG_LEVEL")))
                Environment.SetEnvironmentVariable("VMAP_PHYS_LOG_LEVEL", "0");
            // Only disable coordinator if not already set by a derived fixture (e.g. RfcBotFixture enables it)
            if (Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") == null)
                Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "1");
            _logger.LogInformation(
                "[FIXTURE] Set WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1, WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1, " +
                "WWOW_TEST_DISABLE_COORDINATOR={CoordFlag}, VMAP_PHYS_LOG_MASK={PhysMask}, VMAP_PHYS_LOG_LEVEL={PhysLevel} for live validation run.",
                Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR"),
                Environment.GetEnvironmentVariable("VMAP_PHYS_LOG_MASK"),
                Environment.GetEnvironmentVariable("VMAP_PHYS_LOG_LEVEL"));

            // 1. Start StateManager (which launches all configured bots)
            _logger.LogInformation("[FIXTURE] Starting BotServiceFixture (StateManager)...");
            await _serviceFixture.InitializeAsync();

            if (!_serviceFixture.ServicesReady)
            {
                FailureReason = _serviceFixture.UnavailableReason ?? "BotServiceFixture: services not ready";
                _logger.LogWarning("[FIXTURE] {Reason}", FailureReason);
                return;
            }

            // 2. Check SOAP availability
            var health = _serviceFixture.MangosFixture.Health;
            var soapOk = await health.IsServiceAvailableAsync("127.0.0.1", Config.SoapPort);
            if (!soapOk)
            {
                FailureReason = $"SOAP port {Config.SoapPort} not available";
                return;
            }

            // 2b. Ensure GM commands are enabled in the command table
            await EnsureGmCommandsEnabledAsync();

            // 3. Connect to StateManager on port 8088
            _stateManagerClient = new StateManagerTestClient("127.0.0.1", 8088);
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _stateManagerClient.ConnectAsync(connectCts.Token);
            _logger.LogInformation("[FIXTURE] Connected to StateManager on port 8088.");
            SeedExpectedAccountsFromStateManagerSettings();
            await SeedExpectedCharacterNamesFromDatabaseAsync();

            // 4. Wait for bots to enter world
            _logger.LogInformation("[FIXTURE] Waiting for bots to enter world...");
            using var worldCts = new CancellationTokenSource(InitialWorldEntryTimeout + TimeSpan.FromSeconds(60));

            // Poll until we see at least one bot in-world.
            // BG bot snapshots can oscillate between InWorld and CharacterSelect due to
            // timing (CharacterSelectScreen.IsOpen is always true, so any snapshot taken
            // while HasEnteredWorld is transiently false reports CharacterSelect).
            // Track "ever seen InWorld" per account to handle this flickering.
            var sw = Stopwatch.StartNew();
            var everSeenInWorld = new Dictionary<string, WoWActivitySnapshot>();

            while (sw.Elapsed < InitialWorldEntryTimeout && !worldCts.Token.IsCancellationRequested)
            {
                var snapshots = await _stateManagerClient.QuerySnapshotsAsync(null, worldCts.Token);

                foreach (var snap in snapshots)
                {
                    NormalizeSnapshotCharacterName(snap);
                    if (IsHydratedInWorldSnapshot(snap))
                    {
                        if (!everSeenInWorld.ContainsKey(snap.AccountName))
                        {
                            _logger.LogInformation("[FIXTURE] Bot in-world: Account='{Account}', Character='{Name}'",
                                snap.AccountName, snap.CharacterName);
                        }
                        everSeenInWorld[snap.AccountName] = snap;
                    }
                }

                if (everSeenInWorld.Count >= 1)
                {
                    AllBots = everSeenInWorld.Values.ToList();
                    IdentifyBots(AllBots);

                    // Strict: ALL configured bots must connect. No partial-count workarounds.
                    if (everSeenInWorld.Count >= ExpectedBotCount && HasRequiredRoleCoverage(everSeenInWorld))
                    {
                        _logger.LogInformation("[FIXTURE] All {Count}/{Expected} bots in-world after {Elapsed:F1}s.",
                            everSeenInWorld.Count, ExpectedBotCount, sw.Elapsed.TotalSeconds);
                        break;
                    }

                    if ((int)sw.Elapsed.TotalSeconds % 15 == 0 && sw.Elapsed.TotalSeconds > 0)
                    {
                        _logger.LogInformation("[FIXTURE] {Count}/{Expected} bots in-world so far... ({Elapsed:F0}s)",
                            everSeenInWorld.Count, ExpectedBotCount, sw.Elapsed.TotalSeconds);
                    }
                }

                if ((int)sw.Elapsed.TotalSeconds % 10 == 0 && sw.Elapsed.TotalSeconds > 0)
                    _logger.LogInformation("[FIXTURE] Still waiting for bots... ({Elapsed:F0}s)", sw.Elapsed.TotalSeconds);

                // Poll frequently to catch InWorld windows (BG bot can flicker between
                // InWorld and CharacterSelect every ~100ms due to snapshot timing)
                await Task.Delay(500, worldCts.Token);
            }

            if (AllBots.Count == 0)
            {
                var timeoutSeconds = (int)Math.Round(InitialWorldEntryTimeout.TotalSeconds);
                FailureReason = $"No bots entered world within {timeoutSeconds}s. Check StateManagerSettings.json CharacterSettings.";
                var latestSnapshots = _stateManagerClient != null
                    ? await _stateManagerClient.QuerySnapshotsAsync(null, CancellationToken.None)
                    : [];
                var snapshotDiagnostics = SummarizeLatestSnapshotStates(latestSnapshots);
                _logger.LogError("[FIXTURE] {Reason} LatestSnapshots={Snapshots}", FailureReason, snapshotDiagnostics);
                FailureReason = $"{FailureReason} LatestSnapshots={snapshotDiagnostics}";
                return;
            }

            // Partial readiness: if at least 1 bot entered, proceed with what we have.
            // Tests that need both FG+BG will skip via their own guards.
            // This prevents 156 tests from skipping just because the FG bot was slow.
            if (AllBots.Count < ExpectedBotCount)
            {
                _logger.LogWarning("[FIXTURE] Only {Count}/{Expected} bots entered world. " +
                    "Proceeding with partial readiness — tests needing missing bots will skip individually.",
                    AllBots.Count, ExpectedBotCount);
            }

            _logger.LogInformation("[FIXTURE] Ready. BG='{Bg}' ({BgAccount}), FG='{Fg}' ({FgAccount}), Combat='{Combat}' ({CombatAccount})",
                BgCharacterName ?? "N/A",
                BgAccountName ?? "N/A",
                FgCharacterName ?? "N/A",
                FgAccountName ?? "N/A",
                CombatTestCharacterName ?? "N/A",
                CombatTestAccountName ?? "N/A");
            IsReady = true;

            // Track active settings so EnsureSettingsAsync can skip redundant restarts
            _activeSettingsPath = _serviceFixture.CustomSettingsPath != null
                ? Path.GetFullPath(_serviceFixture.CustomSettingsPath)
                : null;
            _activeCoordinatorFlag = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") ?? "1";
        }
        catch (Exception ex)
        {
            FailureReason = $"Fixture init failed: {ex.Message}";
            _logger.LogError(ex, "[FIXTURE] Initialization failed");
        }
    }

    private void EnsurePhysicsDataDirectory()
    {
        var existing = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (IsUsablePhysicsDataDirectory(existing))
        {
            _logger.LogInformation("[FIXTURE] Using existing WWOW_DATA_DIR={DataDir}", Path.GetFullPath(existing!));
            return;
        }

        foreach (var candidate in EnumeratePhysicsDataDirectoryCandidates())
        {
            if (!IsUsablePhysicsDataDirectory(candidate))
                continue;

            var resolved = Path.GetFullPath(candidate!);
            Environment.SetEnvironmentVariable("WWOW_DATA_DIR", resolved);
            _logger.LogInformation("[FIXTURE] Set WWOW_DATA_DIR={DataDir}", resolved);
            return;
        }

        _logger.LogWarning("[FIXTURE] Could not resolve WWOW_DATA_DIR. Native physics will run without terrain data.");
    }

    private IEnumerable<string?> EnumeratePhysicsDataDirectoryCandidates()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        yield return Path.Combine(baseDir, "Data");
        yield return baseDir;
        yield return Path.Combine(baseDir, "..");
        yield return Path.Combine(baseDir, "..", "..");
        yield return Path.Combine(baseDir, "..", "..", "..");

        if (!string.IsNullOrWhiteSpace(Config.MangosDirectory))
        {
            yield return Path.Combine(Config.MangosDirectory, "..", "data");
            yield return Path.Combine(Config.MangosDirectory, "..", "..", "data");
        }

        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        yield return Path.Combine(systemDrive + Path.DirectorySeparatorChar, "MaNGOS", "data");
        yield return Path.Combine(systemDrive + Path.DirectorySeparatorChar, "Mangos", "data");
        yield return Path.Combine(systemDrive + Path.DirectorySeparatorChar, "mangos", "data");
    }

    private static bool IsUsablePhysicsDataDirectory(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var resolved = Path.GetFullPath(candidate);
        return Directory.Exists(Path.Combine(resolved, "maps"))
            && Directory.Exists(Path.Combine(resolved, "mmaps"))
            && Directory.Exists(Path.Combine(resolved, "vmaps"));
    }

    private static string SummarizeLatestSnapshotStates(IReadOnlyCollection<WoWActivitySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
            return "none";

        static string TrimMessage(string? value, int max = 80)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return compact.Length <= max ? compact : compact[..max] + "...";
        }

        var entries = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .OrderBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .Select(snapshot =>
            {
                var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                var lastError = TrimMessage(snapshot.RecentErrors.LastOrDefault());
                var screen = string.IsNullOrWhiteSpace(snapshot.ScreenState) ? "?" : snapshot.ScreenState;
                var characterName = string.IsNullOrWhiteSpace(snapshot.CharacterName) ? "?" : snapshot.CharacterName;
                return $"{snapshot.AccountName}(screen={screen}, conn={snapshot.ConnectionState}, objMgr={snapshot.IsObjectManagerValid}, map={mapId}, char={characterName}, err={lastError})";
            })
            .ToArray();

        return entries.Length == 0 ? "none" : string.Join(", ", entries);
    }


    /// <summary>
    /// Ensures StateManager is running with the given settings. If already running
    /// with identical settings and coordinator flag, this is a no-op — skipping the
    /// expensive teardown + relaunch cycle that takes 30-120s.
    /// </summary>
    public async Task EnsureSettingsAsync(string settingsPath)
    {
        var normalized = Path.GetFullPath(settingsPath);
        var coordFlag = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") ?? "1";

        if (IsReady && _activeSettingsPath == normalized && _activeCoordinatorFlag == coordFlag)
        {
            _logger.LogInformation("[FIXTURE] Reusing StateManager (settings={File}, coordinator={Flag})",
                Path.GetFileName(settingsPath), coordFlag);
            return;
        }

        await RestartCoreAsync(settingsPath);
    }

    /// <summary>
    /// Unconditionally tears down and relaunches StateManager with the given settings.
    /// This is the internal implementation — callers should use <see cref="EnsureSettingsAsync"/>
    /// which skips the restart when settings haven't changed.
    /// </summary>
    private async Task RestartCoreAsync(string settingsPath)
    {
        _logger.LogInformation("[FIXTURE] Restarting with custom settings: {Path}", settingsPath);

        // Reset fixture state
        IsReady = false;
        FailureReason = null;
        BackgroundBot = null;
        ForegroundBot = null;
        CombatTestBot = null;
        BgAccountName = null;
        FgAccountName = null;
        CombatTestAccountName = null;
        BgCharacterName = null;
        FgCharacterName = null;
        CombatTestCharacterName = null;
        AllBots = [];
        _fgResponsive = true;
        _stateManagerClient = null;

        // Restart BotServiceFixture with new settings
        await _serviceFixture.EnsureSettingsAsync(settingsPath);

        if (!_serviceFixture.ServicesReady)
        {
            FailureReason = _serviceFixture.UnavailableReason ?? "BotServiceFixture: services not ready after restart";
            _logger.LogWarning("[FIXTURE] {Reason}", FailureReason);
            return;
        }

        // Re-run the init pipeline (SOAP, bots, etc.)
        var health = _serviceFixture.MangosFixture.Health;
        var soapOk = await health.IsServiceAvailableAsync("127.0.0.1", Config.SoapPort);
        if (!soapOk)
        {
            FailureReason = $"SOAP port {Config.SoapPort} not available after restart";
            return;
        }

        await EnsureGmCommandsEnabledAsync();

        _stateManagerClient = new StateManagerTestClient("127.0.0.1", 8088);
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _stateManagerClient.ConnectAsync(connectCts.Token);
        _logger.LogInformation("[FIXTURE] Reconnected to StateManager on port 8088.");

        SeedExpectedAccountsFromStateManagerSettings();
        await SeedExpectedCharacterNamesFromDatabaseAsync();

        // Wait for bots to enter world
        _logger.LogInformation("[FIXTURE] Waiting for bots to enter world after restart...");
        var sw = Stopwatch.StartNew();
        var everSeenInWorld = new Dictionary<string, WoWActivitySnapshot>();
        while (sw.Elapsed < InitialWorldEntryTimeout)
        {
            var snapshots = await _stateManagerClient.QuerySnapshotsAsync(null, CancellationToken.None);
            foreach (var snap in snapshots)
            {
                NormalizeSnapshotCharacterName(snap);
                if (IsHydratedInWorldSnapshot(snap))
                    everSeenInWorld[snap.AccountName] = snap;
            }

            if (everSeenInWorld.Count >= 1)
            {
                AllBots = everSeenInWorld.Values.ToList();
                IdentifyBots(AllBots);
                if (HasRequiredRoleCoverage(everSeenInWorld))
                    break;
                if (sw.Elapsed > TimeSpan.FromSeconds(45))
                    break;
            }

            await Task.Delay(500);
        }

        if (AllBots.Count == 0)
        {
            FailureReason = "No bots entered world after restart.";
            _logger.LogError("[FIXTURE] {Reason}", FailureReason);
            return;
        }

        _logger.LogInformation("[FIXTURE] Restart complete. BG='{Bg}' ({BgAccount}), FG='{Fg}' ({FgAccount}), Combat='{Combat}' ({CombatAccount})",
            BgCharacterName ?? "N/A", BgAccountName ?? "N/A",
            FgCharacterName ?? "N/A", FgAccountName ?? "N/A",
            CombatTestCharacterName ?? "N/A", CombatTestAccountName ?? "N/A");
        IsReady = true;

        // Track active settings so EnsureSettingsAsync can skip redundant restarts
        _activeSettingsPath = Path.GetFullPath(settingsPath);
        _activeCoordinatorFlag = Environment.GetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR") ?? "1";
    }

    private void IdentifyBots(List<WoWActivitySnapshot> inWorldBots)
    {
        // Non-destructive: only update snapshot references when we find a matching bot
        // in the current InWorld list. If a bot temporarily drops out (e.g. CharacterSelect
        // flicker during teleport), preserve the last known account name so tests can still
        // send commands. The snapshot reference will be stale but the account name stays valid.
        //
        // Account names are ONLY cleared during the initial call from fixture init (when
        // they haven't been set yet) or when explicitly rebuilding (e.g. during init).

        // Match by account name:
        //   COMBATTEST = dedicated non-GM combat testing bot (never receives .gm on)
        //     - Also assigned to FG or BG based on seeded FgAccountName/BgAccountName
        //   Account matching FgAccountName (seeded from config) = Foreground
        //   Account ending in "1" = Foreground (legacy fallback)
        //   Others = Background (headless)
        WoWActivitySnapshot? newFg = null;
        WoWActivitySnapshot? newBg = null;
        WoWActivitySnapshot? newCombat = null;

        foreach (var snap in inWorldBots)
        {
            if (snap.AccountName.Equals(CombatTestAccount, StringComparison.OrdinalIgnoreCase))
                newCombat = snap;

            // Assign FG/BG: prefer seeded account names from config, fall back to "ends in 1" heuristic
            if (string.Equals(snap.AccountName, FgAccountName, StringComparison.OrdinalIgnoreCase))
                newFg = snap;
            else if (string.Equals(snap.AccountName, BgAccountName, StringComparison.OrdinalIgnoreCase))
                newBg = snap;
            else if (newFg == null && !snap.AccountName.Equals(CombatTestAccount, StringComparison.OrdinalIgnoreCase)
                     && snap.AccountName.EndsWith("1", StringComparison.OrdinalIgnoreCase))
                newFg = snap;
            else if (newBg == null && !snap.AccountName.Equals(CombatTestAccount, StringComparison.OrdinalIgnoreCase))
                newBg = snap;
        }

        // Update snapshot references (always — null if bot dropped out of InWorld)
        ForegroundBot = newFg;
        BackgroundBot = newBg;
        CombatTestBot = newCombat;

        // Update account/character names only when we discover new ones — never null
        // them out once set, since tests need these to send SOAP/chat commands even
        // when the bot's snapshot is temporarily unavailable.
        if (newFg != null)
        {
            FgAccountName = newFg.AccountName;
            if (!string.IsNullOrWhiteSpace(newFg.CharacterName))
                FgCharacterName = newFg.CharacterName;
        }
        if (newBg != null)
        {
            BgAccountName = newBg.AccountName;
            if (!string.IsNullOrWhiteSpace(newBg.CharacterName))
                BgCharacterName = newBg.CharacterName;
        }
        if (newCombat != null)
        {
            CombatTestAccountName = newCombat.AccountName;
            if (!string.IsNullOrWhiteSpace(newCombat.CharacterName))
                CombatTestCharacterName = newCombat.CharacterName;
        }

        // Fallback: if only one bot and neither role was matched, assign it as BG
        if (newBg == null && newFg == null && newCombat == null && inWorldBots.Count >= 1)
        {
            BackgroundBot = inWorldBots[0];
            BgAccountName = BackgroundBot.AccountName;
            BgCharacterName = BackgroundBot.CharacterName;
        }
    }

    private void SeedExpectedAccountsFromStateManagerSettings()
    {
        try
        {
            var settingsPath = ResolveStateManagerSettingsPath();
            if (settingsPath == null || !File.Exists(settingsPath))
                return;

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

            // Count all bots that should run (ShouldRun != false)
            int configuredBotCount = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("ShouldRun", out var sr) && sr.ValueKind == JsonValueKind.False)
                    continue;
                if (element.TryGetProperty("AccountName", out _))
                    configuredBotCount++;
            }
            ExpectedBotCount = configuredBotCount;
            _logger.LogInformation("[FIXTURE] Settings specify {Count} bot(s).", configuredBotCount);

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("ShouldRun", out var shouldRunProperty)
                    && shouldRunProperty.ValueKind == JsonValueKind.False)
                {
                    continue;
                }

                if (!element.TryGetProperty("AccountName", out var accountProperty))
                    continue;

                var accountName = accountProperty.GetString();
                if (string.IsNullOrWhiteSpace(accountName))
                    continue;

                if (!element.TryGetProperty("RunnerType", out var runnerTypeProperty))
                    continue;

                var runnerType = runnerTypeProperty.GetString();
                if (string.Equals(accountName, CombatTestAccount, StringComparison.OrdinalIgnoreCase))
                {
                    CombatTestAccountName ??= accountName;
                    // COMBATTEST can also be FG or BG — fall through to assign runner role
                }

                if (string.Equals(runnerType, "Foreground", StringComparison.OrdinalIgnoreCase))
                {
                    FgAccountName ??= accountName;
                }
                else if (string.Equals(runnerType, "Background", StringComparison.OrdinalIgnoreCase))
                {
                    BgAccountName ??= accountName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FIXTURE] Failed to seed expected account names from StateManagerSettings.json");
        }
    }

    private string? ResolveStateManagerSettingsPath()
    {
        // Prefer custom settings override (set via EnsureSettingsAsync)
        if (!string.IsNullOrEmpty(_serviceFixture.CustomSettingsPath)
            && File.Exists(_serviceFixture.CustomSettingsPath))
            return _serviceFixture.CustomSettingsPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "WoWStateManager", "Settings", "StateManagerSettings.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }

    private bool HasRequiredRoleCoverage(Dictionary<string, WoWActivitySnapshot> everSeenInWorld)
    {
        var bgReady = string.IsNullOrWhiteSpace(BgAccountName)
            || everSeenInWorld.ContainsKey(BgAccountName);
        var fgReady = string.IsNullOrWhiteSpace(FgAccountName)
            || everSeenInWorld.ContainsKey(FgAccountName);

        if (BgAccountName != null || FgAccountName != null)
            return bgReady && fgReady;

        return everSeenInWorld.Count >= 2;
    }

    private List<string> GetMissingRequiredRoles(Dictionary<string, WoWActivitySnapshot> everSeenInWorld)
    {
        var missing = new List<string>();
        if (!string.IsNullOrWhiteSpace(BgAccountName) && !everSeenInWorld.ContainsKey(BgAccountName))
            missing.Add($"BG:{BgAccountName}");
        if (!string.IsNullOrWhiteSpace(FgAccountName) && !everSeenInWorld.ContainsKey(FgAccountName))
            missing.Add($"FG:{FgAccountName}");
        return missing;
    }

    private protected void RememberKnownCharacterName(string? accountName, string? characterName)
    {
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(characterName))
            return;

        _knownCharacterNamesByAccount[accountName] = characterName;
    }

    private string? GetKnownCharacterNameForAccount(string? accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            return null;

        if (_knownCharacterNamesByAccount.TryGetValue(accountName, out var knownCharacterName)
            && !string.IsNullOrWhiteSpace(knownCharacterName))
        {
            return knownCharacterName;
        }

        if (string.Equals(accountName, BgAccountName, StringComparison.OrdinalIgnoreCase))
            return BgCharacterName;

        if (string.Equals(accountName, FgAccountName, StringComparison.OrdinalIgnoreCase))
            return FgCharacterName;

        if (string.Equals(accountName, CombatTestAccountName, StringComparison.OrdinalIgnoreCase))
            return CombatTestCharacterName;

        return AllBots
            .FirstOrDefault(bot => string.Equals(bot.AccountName, accountName, StringComparison.OrdinalIgnoreCase))
            ?.CharacterName;
    }

    private protected void NormalizeSnapshotCharacterName(WoWActivitySnapshot? snapshot)
    {
        if (snapshot == null)
            return;

        if (!string.IsNullOrWhiteSpace(snapshot.CharacterName))
        {
            RememberKnownCharacterName(snapshot.AccountName, snapshot.CharacterName);
            return;
        }

        var knownCharacterName = GetKnownCharacterNameForAccount(snapshot.AccountName);
        if (!string.IsNullOrWhiteSpace(knownCharacterName))
        {
            snapshot.CharacterName = knownCharacterName;
            RememberKnownCharacterName(snapshot.AccountName, knownCharacterName);
        }
    }

    private static bool IsHydratedInWorldSnapshot(WoWActivitySnapshot? snapshot)
    {
        // Use the deterministic IsObjectManagerValid flag (set by BotRunnerService.Snapshot
        // from IObjectManager state) instead of fragile string comparison.
        return snapshot != null
            && snapshot.IsObjectManagerValid
            && !string.IsNullOrWhiteSpace(snapshot.CharacterName)
            && snapshot.Player?.Unit?.MaxHealth > 0
            && snapshot.Player.Unit.GameObject?.Base?.Position != null;
    }

    private async Task<bool> WaitForConfiguredAccountInWorldAsync(string accountName, TimeSpan timeout, string label)
    {
        if (_stateManagerClient == null)
            return false;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var snapshot = await GetSnapshotAsync(accountName);
            NormalizeSnapshotCharacterName(snapshot);
            if (IsHydratedInWorldSnapshot(snapshot))
            {
                AllBots = AllBots
                    .Where(existing => !string.Equals(existing.AccountName, accountName, StringComparison.OrdinalIgnoreCase))
                    .Append(snapshot)
                    .ToList();
                IdentifyBots(AllBots);
                return true;
            }

            await Task.Delay(500);
        }

        _logger.LogWarning("[FIXTURE] Timed out waiting for configured {Label} account '{Account}' to reach InWorld.", label, accountName);
        return false;
    }

    /// <summary>
    /// Ensure both characters are alive and not in a group from a previous test run.
    /// This prevents stale state (dead character, existing party) from breaking tests.
    /// </summary>


    /// <summary>
    /// Ensure both characters are alive and not in a group from a previous test run.
    /// This prevents stale state (dead character, existing party) from breaking tests.
    /// </summary>
    private async Task EnsureCleanCharacterStateAsync()
    {
        try
        {
            // Revive any dead/ghost characters via SOAP, then wait for strict alive snapshots.
            if (BgCharacterName != null && BgAccountName != null)
            {
                await EnsureAliveForSetupAsync(BgAccountName, BgCharacterName, "BG");
            }
            if (FgCharacterName != null && FgAccountName != null)
            {
                await EnsureAliveForSetupAsync(FgAccountName, FgCharacterName, "FG");
            }
            if (CombatTestCharacterName != null && CombatTestAccountName != null)
            {
                await EnsureAliveForSetupAsync(CombatTestAccountName, CombatTestCharacterName, "COMBAT");
            }

            // Clear stale grouping only when snapshot indicates group state.
            // Skip when a coordinator owns group lifecycle (e.g. RFC dungeoneering).
            if (!SkipGroupCleanup)
            {
                if (BgAccountName != null)
                    await EnsureNotGroupedAsync(BgAccountName, "BG");
                if (FgAccountName != null)
                    await EnsureNotGroupedAsync(FgAccountName, "FG");
                if (CombatTestAccountName != null)
                    await EnsureNotGroupedAsync(CombatTestAccountName, "COMBAT");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FIXTURE] Clean state failed (non-fatal): {Error}", ex.Message);
        }
    }


    private async Task EnsureAliveForSetupAsync(string accountName, string characterName, string label)
    {
        await WaitForSnapshotConditionAsync(
            accountName,
            snapshot => snapshot.Player?.Unit?.MaxHealth > 0
                && !string.IsNullOrWhiteSpace(snapshot.CharacterName),
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 500,
            progressLabel: $"{label} setup-hydrate");

        await RefreshSnapshotsAsync();
        var baseline = await GetSnapshotAsync(accountName);
        if (IsStrictAlive(baseline))
        {
            _logger.LogInformation("[FIXTURE] {Label} '{Name}' already strict-alive; skipping revive setup.", label, characterName);
            return;
        }

        await RevivePlayerAsync(characterName);
        _logger.LogInformation("[FIXTURE] Revive requested for {Label} '{Name}' (fallback path)", label, characterName);
        var revived = await WaitForSnapshotConditionAsync(
            accountName,
            IsStrictAlive,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 1000,
            progressLabel: $"{label} setup-revive");
        if (revived)
        {
            _logger.LogInformation("[FIXTURE] {Label} '{Name}' is strict-alive after revive wait window",
                label, characterName);
            return;
        }

        var finalSnapshot = await GetSnapshotAsync(accountName);
        var finalHealth = finalSnapshot?.Player?.Unit?.Health ?? 0;
        var finalMaxHealth = finalSnapshot?.Player?.Unit?.MaxHealth ?? 0;
        var finalScreen = finalSnapshot?.ScreenState ?? "(null)";
        _logger.LogWarning("[FIXTURE] {Label} '{Name}' did not reach strict-alive state after revive wait window. Screen={Screen} hp={Health}/{MaxHealth}",
            label, characterName, finalScreen, finalHealth, finalMaxHealth);
    }

    /// <summary>
    /// Wait until SOAP can resolve a character name (prevents "Player not found!" errors
    /// when the character hasn't fully loaded on the server yet).
    /// Uses ".revive" which returns a known success/failure result on MaNGOS.
    /// </summary>


    /// <summary>
    /// Wait until SOAP can resolve a character name (prevents "Player not found!" errors
    /// when the character hasn't fully loaded on the server yet).
    /// Uses ".revive" which returns a known success/failure result on MaNGOS.
    /// </summary>
    private async Task WaitForSoapPlayerResolutionAsync(string characterName, int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                // Use ".revive <name>" as the probe — it's harmless on an alive character
                // and returns "Player not found" if the character hasn't loaded yet.
                // Avoids ".pinfo" which may not be available at all GM levels.
                var result = await ExecuteGMCommandAsync($".revive {characterName}");
                if (!string.IsNullOrEmpty(result)
                    && !result.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    && !result.Contains("not available", StringComparison.OrdinalIgnoreCase)
                    && !result.StartsWith("FAULT", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[FIXTURE] SOAP resolved '{Name}' after {Elapsed:F1}s",
                        characterName, sw.Elapsed.TotalSeconds);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[FIXTURE] SOAP resolution attempt for '{Name}' failed: {Error}",
                    characterName, ex.Message);
            }

            await Task.Delay(1000);
        }

        _logger.LogWarning("[FIXTURE] SOAP player resolution timed out for '{Name}' after {Timeout}ms",
            characterName, timeoutMs);
    }

    /// <summary>
    /// Wait until all known bots are solidly InWorld (2 consecutive polls).
    /// GM commands and teleports can cause transient CharacterSelect flickers.
    /// </summary>


    /// <summary>
    /// Wait until all known bots are solidly InWorld (2 consecutive polls).
    /// GM commands and teleports can cause transient CharacterSelect flickers.
    /// </summary>
    private async Task WaitForBotsStabilizedAsync(int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        int consecutiveInWorld = 0;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snapshots = await _stateManagerClient!.QuerySnapshotsAsync();
            foreach (var snapshot in snapshots)
                NormalizeSnapshotCharacterName(snapshot);
            var inWorld = snapshots
                .Where(IsHydratedInWorldSnapshot)
                .ToList();

            // Check that every known bot is InWorld
            bool bgOk = BgAccountName == null || inWorld.Any(s => s.AccountName == BgAccountName);
            bool fgOk = FgAccountName == null || inWorld.Any(s => s.AccountName == FgAccountName);

            if (bgOk && fgOk)
            {
                consecutiveInWorld++;
                if (consecutiveInWorld >= 2)
                {
                    // Update snapshot references with the stable data
                    AllBots = inWorld;
                    IdentifyBots(inWorld);
                    _logger.LogInformation("[FIXTURE] Bots stabilized InWorld after {Elapsed:F1}s", sw.Elapsed.TotalSeconds);
                    return;
                }
            }
            else
            {
                consecutiveInWorld = 0;
            }

            await Task.Delay(1000);
        }

        _logger.LogWarning("[FIXTURE] Bot stabilization timed out after {Timeout}ms — proceeding with last known state", timeoutMs);
    }


    private async Task EnsureNotGroupedAsync(string accountName, string label)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var snapshot = await GetSnapshotAsync(accountName);
            if (snapshot == null)
                return;

            if (snapshot.PartyLeaderGuid == 0)
            {
                _logger.LogInformation("[FIXTURE] {Label} '{Account}' not grouped", label, accountName);
                return;
            }

            var selfGuid = snapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
            var isLeader = selfGuid != 0 && snapshot.PartyLeaderGuid == selfGuid;
            var actionType = isLeader ? ActionType.DisbandGroup : ActionType.LeaveGroup;

            _logger.LogInformation("[FIXTURE] {Label} '{Account}' grouped (leader=0x{Leader:X}). Sending {Action} (attempt {Attempt}/3)",
                label, accountName, snapshot.PartyLeaderGuid, actionType, attempt);
            await SendActionAsync(accountName, new ActionMessage { ActionType = actionType });
            await Task.Delay(1200);
        }

        var finalSnapshot = await GetSnapshotAsync(accountName);
        if (finalSnapshot?.PartyLeaderGuid != 0)
        {
            _logger.LogWarning("[FIXTURE] {Label} '{Account}' still grouped after cleanup attempts (leader=0x{Leader:X})",
                label, accountName, finalSnapshot.PartyLeaderGuid);
        }
    }


    public async Task DisposeAsync()
    {
        _stateManagerClient?.Dispose();
        await _serviceFixture.DisposeAsync();
        Environment.SetEnvironmentVariable(RecordingArtifactsEnvVar, _previousRecordingArtifactsFlag);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar, null);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar, null);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetXEnvVar, null);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetYEnvVar, null);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetZEnvVar, null);
        _loggerFactory.Dispose();
    }

    // ---- Snapshot Queries ----

    /// <summary>Refresh all bot snapshots from StateManager. Logs chat/error messages to test output.</summary>


    /// <summary>Forward an action to a specific bot.</summary>
    public Task<ResponseResult> SendActionAsync(string accountName, ActionMessage action)
        => SendActionAsync(accountName, action, emitOutput: true);

    protected Task<ResponseResult> SendSilentActionAsync(string accountName, ActionMessage action)
        => SendActionAsync(accountName, action, emitOutput: false);

    protected async Task<ResponseResult> SendActionAsync(string accountName, ActionMessage action, bool emitOutput)
    {
        if (_stateManagerClient == null)
            return ResponseResult.Failure;

        var result = await _stateManagerClient.ForwardActionAsync(accountName, action);
        if (emitOutput)
        {
            var actionLog = $"[ACTION-FWD] [{accountName}] {action.ActionType} => {result}";
            _logger.LogInformation("{Message}", actionLog);
            _testOutput?.WriteLine(actionLog);
        }

        return result;
    }

    protected async Task<ResponseResult> SetCoordinatorEnabledAsync(bool enabled)
    {
        if (_stateManagerClient == null)
            return ResponseResult.Failure;

        var result = await _stateManagerClient.SetCoordinatorEnabledAsync(enabled);
        var state = enabled ? "enabled" : "disabled";
        _logger.LogInformation("[COORD] runtime coordinator => {State} ({Result})", state, result);
        _testOutput?.WriteLine($"[COORD] runtime coordinator => {state} ({result})");
        return result;
    }


    /// <summary>
    /// Makes the FG bot follow the BG bot by dispatching FOLLOW_TARGET with the BG bot's player GUID.
    /// The FG bot will continuously trail the BG bot at the specified distance.
    /// Call this once at test start — it runs until a new action is dispatched to the FG bot.
    /// </summary>
    public async Task<ResponseResult> StartFgFollowBgAsync(float followDistance = 5.0f)
    {
        if (FgAccountName == null || BgAccountName == null)
            return ResponseResult.Failure;
        return await StartFollowAsync(FgAccountName, BgAccountName, followDistance);
    }

    /// <summary>
    /// Makes followerAccount follow targetAccount by dispatching FOLLOW_TARGET.
    /// The follower will continuously trail the target at the specified distance.
    /// </summary>
    public async Task<ResponseResult> StartFollowAsync(string followerAccount, string targetAccount, float followDistance = 5.0f)
    {
        await RefreshSnapshotsAsync();
        var targetSnap = await GetSnapshotAsync(targetAccount);
        var targetGuid = targetSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (targetGuid == 0)
        {
            _testOutput?.WriteLine($"[FOLLOW] Cannot follow {targetAccount} — player GUID not available.");
            return ResponseResult.Failure;
        }

        _testOutput?.WriteLine($"[FOLLOW] {followerAccount} following {targetAccount} (GUID=0x{targetGuid:X}, distance={followDistance:F1}y)");
        return await SendActionAsync(followerAccount, new ActionMessage
        {
            ActionType = ActionType.FollowTarget,
            Parameters =
            {
                new RequestParameter { LongParam = (long)targetGuid },
                new RequestParameter { FloatParam = followDistance }
            }
        });
    }

    public sealed record GmChatCommandTrace(
        int AttemptCount,
        ResponseResult DispatchResult,
        IReadOnlyList<string> ChatMessages,
        IReadOnlyList<string> ErrorMessages);


    public sealed record DeathInductionResult(
        bool Succeeded,
        string Command,
        string Details,
        bool ObservedCorpseState = false,
        Game.Position? ObservedCorpsePosition = null,
        bool UsedCorpsePositionFallback = false);


    public sealed record PoolGameObjectSpawnState(
        uint PoolEntry,
        string? PoolDescription,
        uint Guid,
        uint Entry,
        int Map,
        float X,
        float Y,
        float Z,
        bool HasRespawnTimer,
        DateTime? RespawnAtUtc);


    /// <summary>
    /// Wait for Z to stabilize (not falling through world).
    /// Polls snapshots and checks Z samples over the given window.
    /// </summary>
    public async Task<(bool stable, float finalZ)> WaitForZStabilizationAsync(string? accountName, int waitMs = 3000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return (false, float.MinValue);

        await Task.Delay(500); // Let physics settle for one tick
        var samples = new List<float>();
        int maxIterations = waitMs / 500;
        for (int i = 0; i < maxIterations; i++)
        {
            var snap = await GetSnapshotAsync(acct);
            var z = snap?.Player?.Unit?.GameObject?.Base?.Position?.Z;
            if (z.HasValue)
            {
                samples.Add(z.Value);

                // Early exit: 3 consecutive samples within 0.1y means Z is stable
                if (samples.Count >= 3)
                {
                    float maxDelta = 0f;
                    for (int j = samples.Count - 2; j >= samples.Count - 3 && j >= 0; j--)
                        maxDelta = Math.Max(maxDelta, Math.Abs(samples[j + 1] - samples[j]));
                    if (maxDelta < 0.1f && samples[^1] > -500f)
                        return (true, samples[^1]);
                }
            }
            await Task.Delay(500);
        }

        if (samples.Count < 2)
            return (false, float.MinValue);

        var finalZ = samples[^1];

        if (finalZ < -500)
            return (false, finalZ);

        var zDelta = Math.Abs(samples[^1] - samples[0]);
        return (zDelta < 50, finalZ);
    }


    public Task<(bool stable, float finalZ)> WaitForZStabilizationAsync(int waitMs = 3000)
        => WaitForZStabilizationAsync(BgAccountName, waitMs);

    /// <summary>
    /// Wait for a nearby unit with specified NPC flags to appear in the bot's snapshot.
    /// </summary>


    private static readonly (string Name, uint Security, string Help, byte Flags)[] CommandTableBaselineRows =
    [
        ("wareffortget", 6, "Syntax: .wareffortget \"[ResourceName]\"", 0),
        ("wareffortset", 6, "Syntax: .wareffortset \"[ResourceName]\" [NewResourceCount]", 0),
        ("debug setvaluebyindex", 5, "Syntax: .debug setvaluebyindex [index] [type] [value]", 2),
        ("debug setvaluebyname", 5, "Syntax: .debug setvaluebyname [name] [value]", 2)
    ];


    // ---- World Database Queries ----

    /// <summary>
    /// Query nearby world gameobject spawns for a set of entries and order them by 2D distance
    /// from the supplied center point. Read-only DB access only; used for live-test staging.
    /// </summary>
    public async Task<List<(uint entry, int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>> QueryGameObjectSpawnsNearAsync(
        IReadOnlyCollection<uint> entries,
        int mapId,
        float centerX,
        float centerY,
        float maxDistance,
        int limit = 10)
    {
        var results = new List<(uint, int, float, float, float, float, uint?, string?)>();
        if (entries.Count == 0)
            return results;

        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            var entryParameters = new List<string>(entries.Count);
            var entryIndex = 0;
            foreach (var entry in entries)
            {
                var parameterName = $"@entry{entryIndex++}";
                entryParameters.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, entry);
            }

            cmd.CommandText = $@"
                SELECT
                    g.id,
                    g.map,
                    g.position_x,
                    g.position_y,
                    g.position_z,
                    pg.pool_entry,
                    pt.description,
                    SQRT(POW(position_x - @centerX, 2) + POW(position_y - @centerY, 2)) AS distance2D
                FROM gameobject g
                LEFT JOIN pool_gameobject pg ON pg.guid = g.guid
                LEFT JOIN pool_template pt ON pt.entry = pg.pool_entry
                WHERE g.map = @map
                  AND g.id IN ({string.Join(", ", entryParameters)})
                  AND SQRT(POW(g.position_x - @centerX, 2) + POW(g.position_y - @centerY, 2)) <= @maxDistance
                ORDER BY distance2D
                LIMIT @limit";

            cmd.Parameters.AddWithValue("@map", mapId);
            cmd.Parameters.AddWithValue("@centerX", centerX);
            cmd.Parameters.AddWithValue("@centerY", centerY);
            cmd.Parameters.AddWithValue("@maxDistance", maxDistance);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add((
                    (uint)reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetFloat(2),
                    reader.GetFloat(3),
                    reader.GetFloat(4),
                    reader.GetFloat(7),
                    reader.IsDBNull(5) ? null : Convert.ToUInt32(reader.GetValue(5)),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }

            _logger.LogInformation("[MySQL] Found {Count} nearby gameobject spawns on map={MapId} within {Distance:F1}y",
                results.Count, mapId, maxDistance);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to query nearby gameobject spawns: {Error}", ex.Message);
        }

        return results;
    }

    /// <summary>
    /// Query all gameobject spawn rows for the child pools under a master pool entry.
    /// Read-only DB access only; used to explain live pooled-spawn behavior when a local slice has no visible node.
    /// </summary>
    public async Task<List<(uint poolEntry, string? poolDescription, uint entry, int map, float x, float y, float z)>> QueryMasterPoolChildSpawnsAsync(
        uint masterPoolEntry)
    {
        var results = new List<(uint, string?, uint, int, float, float, float)>();

        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    pp.pool_id,
                    pt.description,
                    g.id,
                    g.map,
                    g.position_x,
                    g.position_y,
                    g.position_z
                FROM pool_pool pp
                INNER JOIN pool_template pt ON pt.entry = pp.pool_id
                INNER JOIN pool_gameobject pg ON pg.pool_entry = pp.pool_id
                INNER JOIN gameobject g ON g.guid = pg.guid
                WHERE pp.mother_pool = @masterPoolEntry
                ORDER BY pp.pool_id, g.guid";
            cmd.Parameters.AddWithValue("@masterPoolEntry", masterPoolEntry);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add((
                    Convert.ToUInt32(reader.GetValue(0)),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    Convert.ToUInt32(reader.GetValue(2)),
                    reader.GetInt32(3),
                    reader.GetFloat(4),
                    reader.GetFloat(5),
                    reader.GetFloat(6)));
            }

            _logger.LogInformation("[MySQL] Found {Count} child-pool spawn rows under master pool {MasterPoolEntry}",
                results.Count, masterPoolEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to query master pool child spawns: {Error}", ex.Message);
        }

        return results;
    }

    /// <summary>
    /// Query all gameobject rows for the child pools under a master pool entry, including
    /// whether each specific guid currently has a persisted respawn timer.
    /// </summary>
    public async Task<List<PoolGameObjectSpawnState>> QueryMasterPoolChildSpawnStatesAsync(uint masterPoolEntry)
    {
        var results = new List<PoolGameObjectSpawnState>();

        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    pp.pool_id,
                    pt.description,
                    g.guid,
                    g.id,
                    g.map,
                    g.position_x,
                    g.position_y,
                    g.position_z,
                    gr.respawn_time
                FROM pool_pool pp
                INNER JOIN pool_template pt ON pt.entry = pp.pool_id
                INNER JOIN pool_gameobject pg ON pg.pool_entry = pp.pool_id
                INNER JOIN gameobject g ON g.guid = pg.guid
                LEFT JOIN characters.gameobject_respawn gr
                    ON gr.guid = g.guid
                   AND gr.map = g.map
                WHERE pp.mother_pool = @masterPoolEntry
                ORDER BY pp.pool_id, g.guid";
            cmd.Parameters.AddWithValue("@masterPoolEntry", masterPoolEntry);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime? respawnAtUtc = null;
                if (!reader.IsDBNull(8))
                {
                    var respawnUnix = Convert.ToInt64(reader.GetValue(8));
                    respawnAtUtc = DateTimeOffset.FromUnixTimeSeconds(respawnUnix).UtcDateTime;
                }

                results.Add(new PoolGameObjectSpawnState(
                    Convert.ToUInt32(reader.GetValue(0)),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    Convert.ToUInt32(reader.GetValue(2)),
                    Convert.ToUInt32(reader.GetValue(3)),
                    reader.GetInt32(4),
                    reader.GetFloat(5),
                    reader.GetFloat(6),
                    reader.GetFloat(7),
                    !reader.IsDBNull(8),
                    respawnAtUtc));
            }

            _logger.LogInformation("[MySQL] Found {Count} child-pool spawn-state rows under master pool {MasterPoolEntry}",
                results.Count, masterPoolEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to query master pool child spawn states: {Error}", ex.Message);
        }

        return results;
    }
}
