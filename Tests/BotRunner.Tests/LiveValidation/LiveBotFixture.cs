using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Communication;
using Microsoft.Extensions.Logging;
using Tests.Infrastructure;
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

    private bool _fgResponsive = true;


    public bool IsReady { get; private set; }

    public string? FailureReason { get; private set; }

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

    /// <summary>Snapshot of the Background (headless) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>


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
    public async Task<bool> CheckFgActionableAsync()
    {
        if (FgAccountName == null || ForegroundBot == null)
        {
            _fgResponsive = false;
            return false;
        }

        await RefreshSnapshotsAsync();
        if (ForegroundBot == null || !IsStrictAlive(ForegroundBot))
        {
            _fgResponsive = false;
            var hp = ForegroundBot?.Player?.Unit?.Health ?? 0;
            _logger.LogWarning("[FG-PROBE] FG not strict-alive (health={Health})", hp);
            return false;
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
            // Live corpse-flow tests must own the release step via explicit client action.
            // Disable BotRunner auto-release so death setup remains deterministic.
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK", "1");
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK", "1");
            Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "1");
            _logger.LogInformation("[FIXTURE] Set WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1, WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1, WWOW_TEST_DISABLE_COORDINATOR=1 for live validation run.");

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

            // 2c. Clean up zombie gameobjects from previous .gobject add runs
            await CleanupZombieGameObjectsAsync();

            // 3. Connect to StateManager on port 8088
            _stateManagerClient = new StateManagerTestClient("127.0.0.1", 8088);
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _stateManagerClient.ConnectAsync(connectCts.Token);
            _logger.LogInformation("[FIXTURE] Connected to StateManager on port 8088.");

            // 4. Wait for bots to enter world
            _logger.LogInformation("[FIXTURE] Waiting for bots to enter world...");
            using var worldCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            // Poll until we see at least one bot in-world.
            // BG bot snapshots can oscillate between InWorld and CharacterSelect due to
            // timing (CharacterSelectScreen.IsOpen is always true, so any snapshot taken
            // while HasEnteredWorld is transiently false reports CharacterSelect).
            // Track "ever seen InWorld" per account to handle this flickering.
            var sw = Stopwatch.StartNew();
            WoWActivitySnapshot? bgSnap = null;
            WoWActivitySnapshot? fgSnap = null;
            var everSeenInWorld = new Dictionary<string, WoWActivitySnapshot>();

            while (sw.Elapsed < TimeSpan.FromSeconds(120) && !worldCts.Token.IsCancellationRequested)
            {
                var snapshots = await _stateManagerClient.QuerySnapshotsAsync(null, worldCts.Token);

                foreach (var snap in snapshots)
                {
                    if (snap.ScreenState == "InWorld" && !string.IsNullOrEmpty(snap.CharacterName))
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
                    // Use the "ever seen" list — these bots have confirmed InWorld at least once
                    AllBots = everSeenInWorld.Values.ToList();
                    IdentifyBots(AllBots);

                    if (everSeenInWorld.Count >= 2)
                    {
                        _logger.LogInformation("[FIXTURE] Both bots in-world after {Elapsed:F1}s.", sw.Elapsed.TotalSeconds);
                        break;
                    }

                    // If we've waited a while and only have 1, log stuck bots and proceed
                    if (sw.Elapsed > TimeSpan.FromSeconds(60))
                    {
                        // Log any bots that have never been seen in-world
                        var stuckBots = snapshots
                            .Where(s => !everSeenInWorld.ContainsKey(s.AccountName) && !string.IsNullOrEmpty(s.AccountName))
                            .ToList();
                        foreach (var stuck in stuckBots)
                        {
                            _logger.LogWarning("[FIXTURE] Bot stuck: Account='{Account}', ScreenState='{State}' — skipping it.",
                                stuck.AccountName, stuck.ScreenState);
                        }

                        _logger.LogWarning("[FIXTURE] Only {Count} bot(s) in-world after {Elapsed:F1}s. Proceeding with available bot(s).",
                            everSeenInWorld.Count, sw.Elapsed.TotalSeconds);
                        break;
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
                FailureReason = "No bots entered world within 120s. Check StateManagerSettings.json CharacterSettings.";
                _logger.LogError("[FIXTURE] {Reason}", FailureReason);
                return;
            }

            // 5. Verify SOAP can resolve character names (prevents "Player not found!" errors).
            _logger.LogInformation("[FIXTURE] Verifying SOAP player resolution...");
            if (BgCharacterName != null)
                await WaitForSoapPlayerResolutionAsync(BgCharacterName);
            if (FgCharacterName != null)
                await WaitForSoapPlayerResolutionAsync(FgCharacterName);
            if (CombatTestCharacterName != null)
                await WaitForSoapPlayerResolutionAsync(CombatTestCharacterName);

            // 6. Ensure clean state: revive dead characters, disband existing groups
            _logger.LogInformation("[FIXTURE] Ensuring clean character state (revive + disband)...");
            await EnsureCleanCharacterStateAsync();

            // 7. Stage bots at a shared starting location (Orgrimmar).
            //    COMBATTEST is excluded — its test handles positioning directly,
            //    and rapid back-to-back SOAP teleports can disconnect BG clients.
            _logger.LogInformation("[FIXTURE] Staging bots at Orgrimmar...");
            if (BgCharacterName != null)
                await TeleportToNamedAsync(BgCharacterName, "Orgrimmar");
            if (FgCharacterName != null)
                await TeleportToNamedAsync(FgCharacterName, "Orgrimmar");
            await Task.Delay(1500);

            // 8. Stabilization: wait for all known bots to be solidly InWorld before declaring ready.
            //    Teleport and GM commands can cause transient CharacterSelect flickers.
            await WaitForBotsStabilizedAsync();

            _logger.LogInformation("[FIXTURE] Ready. BG='{Bg}', FG='{Fg}', Combat='{Combat}'", BgCharacterName ?? "N/A", FgCharacterName ?? "N/A", CombatTestCharacterName ?? "N/A");
            IsReady = true;
        }
        catch (Exception ex)
        {
            FailureReason = $"Fixture init failed: {ex.Message}";
            _logger.LogError(ex, "[FIXTURE] Initialization failed");
        }
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
        //   TESTBOT1 (ends in "1") = Foreground (injected, gold standard)
        //   COMBATTEST = dedicated non-GM combat testing bot (never receives .gm on)
        //   Others = Background (headless)
        WoWActivitySnapshot? newFg = null;
        WoWActivitySnapshot? newBg = null;
        WoWActivitySnapshot? newCombat = null;

        foreach (var snap in inWorldBots)
        {
            if (snap.AccountName.Equals("COMBATTEST", StringComparison.OrdinalIgnoreCase))
                newCombat = snap;
            else if (snap.AccountName.EndsWith("1", StringComparison.OrdinalIgnoreCase))
                newFg = snap;
            else
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
            FgCharacterName = newFg.CharacterName;
        }
        if (newBg != null)
        {
            BgAccountName = newBg.AccountName;
            BgCharacterName = newBg.CharacterName;
        }
        if (newCombat != null)
        {
            CombatTestAccountName = newCombat.AccountName;
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
            if (BgAccountName != null)
                await EnsureNotGroupedAsync(BgAccountName, "BG");
            if (FgAccountName != null)
                await EnsureNotGroupedAsync(FgAccountName, "FG");
            if (CombatTestAccountName != null)
                await EnsureNotGroupedAsync(CombatTestAccountName, "COMBAT");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FIXTURE] Clean state failed (non-fatal): {Error}", ex.Message);
        }
    }


    private async Task EnsureAliveForSetupAsync(string accountName, string characterName, string label)
    {
        var baseline = await GetSnapshotAsync(accountName);
        if (IsStrictAlive(baseline))
        {
            _logger.LogInformation("[FIXTURE] {Label} '{Name}' already strict-alive; skipping revive setup.", label, characterName);
            return;
        }

        await RevivePlayerAsync(characterName);
        _logger.LogInformation("[FIXTURE] Revive requested for {Label} '{Name}' (fallback path)", label, characterName);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(12))
        {
            var snapshot = await GetSnapshotAsync(accountName);
            if (IsStrictAlive(snapshot))
            {
                _logger.LogInformation("[FIXTURE] {Label} '{Name}' is strict-alive after {Elapsed:F1}s",
                    label, characterName, sw.Elapsed.TotalSeconds);
                return;
            }

            await Task.Delay(1000);
        }

        _logger.LogWarning("[FIXTURE] {Label} '{Name}' did not reach strict-alive state after revive wait window",
            label, characterName);
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
            var inWorld = snapshots
                .Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName))
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
        _loggerFactory.Dispose();
    }

    // ---- Snapshot Queries ----

    /// <summary>Refresh all bot snapshots from StateManager. Logs chat/error messages to test output.</summary>


    /// <summary>Forward an action to a specific bot.</summary>
    public async Task<ResponseResult> SendActionAsync(string accountName, ActionMessage action)
    {
        if (_stateManagerClient == null) return ResponseResult.Failure;
        var result = await _stateManagerClient.ForwardActionAsync(accountName, action);
        var actionLog = $"[ACTION-FWD] [{accountName}] {action.ActionType} => {result}";
        _logger.LogInformation("{Message}", actionLog);
        _testOutput?.WriteLine(actionLog);
        return result;
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
    /// Query the MaNGOS gameobject table for existing spawn locations of a given template entry.
    /// Returns up to <paramref name="limit"/> (map, x, y, z) tuples.
    /// </summary>
    public async Task<List<(int map, float x, float y, float z)>> QueryGameObjectSpawnsAsync(
        uint entry, int? mapFilter = null, int limit = 10)
    {
        var results = new List<(int, float, float, float)>();
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = mapFilter.HasValue
                ? "SELECT map, position_x, position_y, position_z FROM gameobject WHERE id = @id AND map = @map ORDER BY RAND() LIMIT @limit"
                : "SELECT map, position_x, position_y, position_z FROM gameobject WHERE id = @id ORDER BY RAND() LIMIT @limit";
            cmd.Parameters.AddWithValue("@id", entry);
            cmd.Parameters.AddWithValue("@limit", limit);
            if (mapFilter.HasValue)
                cmd.Parameters.AddWithValue("@map", mapFilter.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add((reader.GetInt32(0), reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3)));
            }

            _logger.LogInformation("[MySQL] Found {Count} spawns for gameobject entry={Entry}", results.Count, entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to query gameobject spawns: {Error}", ex.Message);
        }
        return results;
    }
}
