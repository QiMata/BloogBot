using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public class LiveBotFixture : IAsyncLifetime
{
    public IntegrationTestConfig Config { get; } = IntegrationTestConfig.FromEnvironment();
    private readonly BotServiceFixture _serviceFixture = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private StateManagerTestClient? _stateManagerClient;
    private ITestOutputHelper? _testOutput;
    private readonly object _commandTrackingLock = new();
    private readonly object _snapshotMessageDeltaLock = new();
    private readonly Dictionary<string, int> _soapCommandCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, int>> _chatCommandCountsByAccount = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastPrintedChatCountByAccount = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastPrintedErrorCountByAccount = new(StringComparer.OrdinalIgnoreCase);

    public bool IsReady { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Whether PathfindingService is listening on port 5001.
    /// Tests that require pathfinding (corpse run, movement, gathering) should
    /// check this via <c>Skip.IfNot(fixture.IsPathfindingReady, ...)</c>.
    /// </summary>
    public bool IsPathfindingReady => _serviceFixture.PathfindingServiceReady;

    /// <summary>Snapshot of the Background (headless) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public WoWActivitySnapshot? BackgroundBot { get; private set; }

    /// <summary>Snapshot of the Foreground (injected) bot. Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public WoWActivitySnapshot? ForegroundBot { get; private set; }

    /// <summary>All bot snapshots (both BG + FG). Updated by calling <see cref="RefreshSnapshotsAsync"/>.</summary>
    public List<WoWActivitySnapshot> AllBots { get; private set; } = [];

    /// <summary>Character name of the Background bot.</summary>
    public string? BgCharacterName => BackgroundBot?.CharacterName;

    /// <summary>Character name of the Foreground bot.</summary>
    public string? FgCharacterName => ForegroundBot?.CharacterName;

    /// <summary>Account name of the Background bot (from config).</summary>
    public string? BgAccountName { get; private set; }

    /// <summary>Account name of the Foreground bot (from config).</summary>
    public string? FgAccountName { get; private set; }

    // ---- Backward-compatible adapter properties (for test migration) ----
    // These expose the BG bot state through the old API surface so tests compile.
    // Tests should be migrated to use snapshot-based queries with dual-client assertions.

    /// <summary>Character name of the primary (BG) bot. Backward-compatible alias.</summary>
    public string? CharacterName => BgCharacterName;

    /// <summary>Character GUID of the primary (BG) bot from snapshot.</summary>
    public ulong CharacterGuid => BackgroundBot?.Player?.Unit?.GameObject?.Base?.Guid ?? 0;

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
        _testOutput = output;
        _serviceFixture.SetOutput(output);
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Live corpse-flow tests must own the release step via explicit client action.
            // Disable BotRunner auto-release so death setup remains deterministic.
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORELEASE_CORPSE_TASK", "1");
            Environment.SetEnvironmentVariable("WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK", "1");
            Environment.SetEnvironmentVariable("WWOW_TEST_COORD_SUPPRESS_SECONDS", "300");
            _logger.LogInformation("[FIXTURE] Set WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1, WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1, WWOW_TEST_COORD_SUPPRESS_SECONDS=300 for live validation run.");

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

            // Poll until we see at least one bot in-world
            // (If only BG is configured, we still proceed — dual-client config is recommended but not hard-required)
            var sw = Stopwatch.StartNew();
            WoWActivitySnapshot? bgSnap = null;
            WoWActivitySnapshot? fgSnap = null;

            while (sw.Elapsed < TimeSpan.FromSeconds(120) && !worldCts.Token.IsCancellationRequested)
            {
                var snapshots = await _stateManagerClient.QuerySnapshotsAsync(null, worldCts.Token);

                foreach (var snap in snapshots)
                {
                    if (snap.ScreenState == "InWorld" && !string.IsNullOrEmpty(snap.CharacterName))
                    {
                        _logger.LogInformation("[FIXTURE] Bot in-world: Account='{Account}', Character='{Name}'",
                            snap.AccountName, snap.CharacterName);
                    }
                }

                var inWorld = snapshots
                    .Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName))
                    .ToList();

                if (inWorld.Count >= 1)
                {
                    // Identify BG vs FG from account names and log
                    AllBots = inWorld;
                    IdentifyBots(inWorld);

                    if (inWorld.Count >= 2)
                    {
                        _logger.LogInformation("[FIXTURE] Both bots in-world after {Elapsed:F1}s.", sw.Elapsed.TotalSeconds);
                        break;
                    }

                    // If we've waited a while and only have 1, log stuck bots and proceed
                    if (sw.Elapsed > TimeSpan.FromSeconds(60))
                    {
                        // Log any bots that are NOT in-world (stuck in LoadingWorld, etc.)
                        var stuckBots = snapshots
                            .Where(s => s.ScreenState != "InWorld" && !string.IsNullOrEmpty(s.AccountName))
                            .ToList();
                        foreach (var stuck in stuckBots)
                        {
                            _logger.LogWarning("[FIXTURE] Bot stuck: Account='{Account}', ScreenState='{State}' — skipping it.",
                                stuck.AccountName, stuck.ScreenState);
                        }

                        _logger.LogWarning("[FIXTURE] Only {Count} bot(s) in-world after {Elapsed:F1}s. Proceeding with available bot(s).",
                            inWorld.Count, sw.Elapsed.TotalSeconds);
                        break;
                    }
                }

                if ((int)sw.Elapsed.TotalSeconds % 10 == 0 && sw.Elapsed.TotalSeconds > 0)
                    _logger.LogInformation("[FIXTURE] Still waiting for bots... ({Elapsed:F0}s)", sw.Elapsed.TotalSeconds);

                await Task.Delay(3000, worldCts.Token);
            }

            if (AllBots.Count == 0)
            {
                FailureReason = "No bots entered world within 120s. Check StateManagerSettings.json CharacterSettings.";
                _logger.LogError("[FIXTURE] {Reason}", FailureReason);
                return;
            }

            // 5. Ensure clean state: revive dead characters, disband existing groups
            _logger.LogInformation("[FIXTURE] Ensuring clean character state (revive + disband)...");
            await EnsureCleanCharacterStateAsync();

            _logger.LogInformation("[FIXTURE] Ready. BG='{Bg}', FG='{Fg}'", BgCharacterName ?? "N/A", FgCharacterName ?? "N/A");
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
        // Always rebuild bot-role assignment from the latest in-world snapshots.
        // Without resetting, stale references can survive when one bot briefly
        // drops out of InWorld and pollute role-specific test reads.
        ForegroundBot = null;
        BackgroundBot = null;
        FgAccountName = null;
        BgAccountName = null;

        // Match by account name: ORWR1 = Foreground (Warrior/injected), ORSH1 = Background (Shaman/headless).
        foreach (var snap in inWorldBots)
        {
            if (snap.AccountName.Contains("WR", StringComparison.OrdinalIgnoreCase))
            {
                ForegroundBot = snap;
                FgAccountName = snap.AccountName;
            }
            else
            {
                BackgroundBot = snap;
                BgAccountName = snap.AccountName;
            }
        }

        // Fallback: if only one bot and it wasn't matched above
        if (BackgroundBot == null && ForegroundBot == null && inWorldBots.Count >= 1)
        {
            BackgroundBot = inWorldBots[0];
            BgAccountName = BackgroundBot.AccountName;
        }
    }

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

            // Clear stale grouping only when snapshot indicates group state.
            if (BgAccountName != null)
                await EnsureNotGroupedAsync(BgAccountName, "BG");
            if (FgAccountName != null)
                await EnsureNotGroupedAsync(FgAccountName, "FG");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FIXTURE] Clean state failed (non-fatal): {Error}", ex.Message);
        }
    }

    private async Task EnsureAliveForSetupAsync(string accountName, string characterName, string label)
    {
        var baseline = await GetSnapshotAsync(accountName);
        if (IsStrictAliveState(baseline))
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
            if (IsStrictAliveState(snapshot))
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
    public async Task RefreshSnapshotsAsync()
    {
        if (_stateManagerClient == null) return;
        var snapshots = await _stateManagerClient.QuerySnapshotsAsync();

        AllBots = snapshots.Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName)).ToList();
        IdentifyBots(AllBots);

        // Surface chat/error messages from snapshots to test output (via ITestOutputHelper so they appear in xUnit output)
        foreach (var snap in AllBots)
        {
            var label = snap.AccountName?.Contains("WR") == true ? "FG" : "BG";
            var accountKey = string.IsNullOrWhiteSpace(snap.AccountName) ? label : snap.AccountName;
            WriteSnapshotMessageDelta(accountKey, label, snap.RecentChatMessages, _lastPrintedChatCountByAccount, "CHAT");
            WriteSnapshotMessageDelta(accountKey, label, snap.RecentErrors, _lastPrintedErrorCountByAccount, "ERROR");
        }
    }

    private void WriteSnapshotMessageDelta(
        string accountKey,
        string label,
        IReadOnlyList<string> messages,
        Dictionary<string, int> lastPrintedCountByAccount,
        string channel)
    {
        lock (_snapshotMessageDeltaLock)
        {
            lastPrintedCountByAccount.TryGetValue(accountKey, out var lastPrintedCount);

            // Snapshot windows are rolling (MaxBufferedMessages). If they shrink or reset, print from 0.
            var startIndex = messages.Count >= lastPrintedCount ? lastPrintedCount : 0;
            for (var i = startIndex; i < messages.Count; i++)
                _testOutput?.WriteLine($"[{label}:{channel}] {messages[i]}");

            lastPrintedCountByAccount[accountKey] = messages.Count;
        }
    }

    /// <summary>Log key diagnostic fields from a snapshot. Useful for debugging FG issues.</summary>
    public void DumpSnapshotDiagnostics(WoWActivitySnapshot? snap, string label)
    {
        if (snap == null) { _testOutput?.WriteLine($"[{label}] Snapshot is null"); return; }
        var p = snap.Player;
        _testOutput?.WriteLine($"[{label}] Screen={snap.ScreenState}, Char={snap.CharacterName}, " +
            $"Inventory={p?.Inventory?.Count ?? -1}, Bags={p?.BagContents?.Count ?? -1}, Skills={p?.SkillInfo?.Count ?? -1}");
        if (p?.Inventory != null)
            foreach (var kvp in p.Inventory)
                _testOutput?.WriteLine($"[{label}]   Equip[{kvp.Key}] = 0x{kvp.Value:X}");
        if (snap.RecentChatMessages.Count > 0)
            foreach (var msg in snap.RecentChatMessages)
                _testOutput?.WriteLine($"[{label}]   Chat: {msg}");
        if (snap.RecentErrors.Count > 0)
            foreach (var err in snap.RecentErrors)
                _testOutput?.WriteLine($"[{label}]   Error: {err}");
    }

    /// <summary>Get a fresh snapshot for a specific account.</summary>
    public async Task<WoWActivitySnapshot?> GetSnapshotAsync(string accountName)
    {
        if (_stateManagerClient == null) return null;
        var snapshots = await _stateManagerClient.QuerySnapshotsAsync(accountName);
        return snapshots.FirstOrDefault();
    }

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

    private int TrackSoapCommand(string command)
    {
        lock (_commandTrackingLock)
        {
            _soapCommandCounts.TryGetValue(command, out var count);
            count++;
            _soapCommandCounts[command] = count;
            return count;
        }
    }

    private int TrackChatCommand(string accountName, string command)
    {
        lock (_commandTrackingLock)
        {
            if (!_chatCommandCountsByAccount.TryGetValue(accountName, out var accountCommands))
            {
                accountCommands = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _chatCommandCountsByAccount[accountName] = accountCommands;
            }

            accountCommands.TryGetValue(command, out var count);
            count++;
            accountCommands[command] = count;
            return count;
        }
    }

    public int GetTrackedChatCommandTotal(string accountName)
    {
        lock (_commandTrackingLock)
        {
            if (!_chatCommandCountsByAccount.TryGetValue(accountName, out var accountCommands))
                return 0;

            var total = 0;
            foreach (var count in accountCommands.Values)
                total += count;

            return total;
        }
    }

    private void LogDuplicateCommand(string channel, string command, int count, string? accountName = null)
    {
        if (count <= 1)
            return;

        var message = accountName == null
            ? $"[CMD-TRACK] Duplicate {channel} command (x{count}): {command}"
            : $"[CMD-TRACK] Duplicate {channel} command for {accountName} (x{count}): {command}";

        _logger.LogWarning("{Message}", message);
        _testOutput?.WriteLine(message);
    }

    private void LogChatCommandResponses(string accountName, string command, IReadOnlyList<string> chats, IReadOnlyList<string> errors)
    {
        if (chats.Count == 0 && errors.Count == 0)
        {
            var noResponse = $"[CMD-RESP] [{accountName}] '{command}' produced no new chat/error lines.";
            _logger.LogInformation("{Message}", noResponse);
            _testOutput?.WriteLine(noResponse);
            return;
        }

        foreach (var message in chats)
        {
            _logger.LogInformation("[CMD-RESP] [{Account}] '{Command}' [CHAT] {Message}", accountName, command, message);
            _testOutput?.WriteLine($"[CMD-RESP] [{accountName}] '{command}' [CHAT] {message}");
        }

        foreach (var error in errors)
        {
            _logger.LogInformation("[CMD-RESP] [{Account}] '{Command}' [ERROR] {Message}", accountName, command, error);
            _testOutput?.WriteLine($"[CMD-RESP] [{accountName}] '{command}' [ERROR] {error}");
        }
    }

    private static List<string> GetDeltaMessages(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var remainingBaseline = new List<string>(baseline);
        var delta = new List<string>();

        foreach (var message in current)
        {
            var index = remainingBaseline.IndexOf(message);
            if (index >= 0)
                remainingBaseline.RemoveAt(index);
            else
                delta.Add(message);
        }

        return delta;
    }

    private static bool IsDeadOrGhostState(WoWActivitySnapshot? snap, out string reason)
    {
        reason = string.Empty;

        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
        const uint standStateMask = 0xFF;
        const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

        var reasons = new List<string>();
        if (unit.Health == 0)
            reasons.Add("health=0");

        if ((player.PlayerFlags & playerFlagGhost) != 0)
            reasons.Add("ghostFlag=1");

        var standState = unit.Bytes1 & standStateMask;
        if (standState == standStateDead)
            reasons.Add("standState=dead");

        if (reasons.Count == 0)
            return false;

        reason = string.Join(", ", reasons);
        return true;
    }

    private static bool IsCorpseOrGhostTransitionState(WoWActivitySnapshot? snap, out string reason)
    {
        reason = string.Empty;

        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
        const uint standStateMask = 0xFF;
        const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

        var reasons = new List<string>();
        if (unit.Health == 0)
            reasons.Add("health=0");

        if ((player.PlayerFlags & playerFlagGhost) != 0)
            reasons.Add("ghostFlag=1");

        var standState = unit.Bytes1 & standStateMask;
        if (standState == standStateDead)
            reasons.Add("standState=dead");

        if (reasons.Count == 0)
            return false;

        if (player.CorpseRecoveryDelaySeconds > 0)
            reasons.Add($"reclaimDelay={player.CorpseRecoveryDelaySeconds}s");

        reason = string.Join(", ", reasons);
        return true;
    }

    private static bool IsCorpseState(WoWActivitySnapshot? snap, out string reason)
    {
        reason = string.Empty;

        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
        const uint standStateMask = 0xFF;
        const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

        var hasGhostFlag = (player.PlayerFlags & playerFlagGhost) != 0;
        var standState = unit.Bytes1 & standStateMask;
        var isCorpse = !hasGhostFlag && (unit.Health == 0 || standState == standStateDead);
        if (!isCorpse)
            return false;

        reason = $"health={unit.Health}, ghostFlag={(hasGhostFlag ? 1 : 0)}, standState={standState}";
        return true;
    }

    private static bool IsStrictAliveState(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        const uint playerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
        const uint standStateMask = 0xFF;
        const uint standStateDead = 7; // UNIT_STAND_STATE_DEAD

        var standState = unit.Bytes1 & standStateMask;
        var hasGhostFlag = (player.PlayerFlags & playerFlagGhost) != 0;
        return unit.Health > 0 && !hasGhostFlag && standState != standStateDead;
    }

    // ---- GM Command Helpers (via SOAP — independent of bots) ----

    /// <summary>Execute any GM command via SOAP.</summary>
    public async Task<string> ExecuteGMCommandAsync(string command)
    {
        var attemptCount = TrackSoapCommand(command);
        LogDuplicateCommand("SOAP", command, attemptCount);
        _logger.LogInformation("[GM] {Command}", command);

        var xmlPayload = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
        <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
          <soap:Body>
            <ns1:executeCommand xmlns:ns1=""urn:MaNGOS"">
              <command>{command}</command>
            </ns1:executeCommand>
          </soap:Body>
        </soap:Envelope>";

        using var client = new HttpClient();
        var byteArray = Encoding.ASCII.GetBytes("ADMINISTRATOR:PASSWORD");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");

        try
        {
            var response = await client.PostAsync($"http://127.0.0.1:{Config.SoapPort}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[GM] Command failed: {Status}", response.StatusCode);
                return string.Empty;
            }

            var xml = XDocument.Parse(responseContent);

            // Check for SOAP fault
            var faultString = xml.Descendants("faultstring").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(faultString))
            {
                _logger.LogWarning("[GM] SOAP fault for '{Command}': {Fault}", command, faultString);

                // "There is no such command." means the command doesn't exist in MaNGOS command table — always a bug.
                if (faultString.Contains("no such command", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"[GM] Command not found in MaNGOS command table: '{command}'. " +
                        $"SOAP fault: {faultString}. Fix the command or remove it.");
                }

                return $"FAULT: {faultString}";
            }

            // Try namespace-qualified result first, then unqualified
            var result = xml.Descendants(XName.Get("result", "urn:MaNGOS")).FirstOrDefault()?.Value
                      ?? xml.Descendants("result").FirstOrDefault()?.Value;
            _logger.LogInformation("[GM] Result: {Result}", result);
            return result ?? "OK (no output)";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[GM] Error: {Error}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Teleport a character to coordinates. Uses bot chat (.go xyz) since SOAP
    /// .teleport name with coordinates does NOT exist on this MaNGOS server.
    /// Resolves character name to account name automatically.
    /// </summary>
    public async Task<string> TeleportAsync(string? characterName, int mapId, float x, float y, float z)
    {
        var account = ResolveAccountName(characterName);
        if (account == null)
        {
            _logger.LogWarning("[TELEPORT] Cannot resolve account for character '{Name}' — falling back to BG", characterName);
            account = BgAccountName!;
        }
        await BotTeleportAsync(account, mapId, x, y, z);
        return "OK";
    }

    /// <summary>Teleport the BG bot to a position (backward-compatible overload).</summary>
    public async Task<string> TeleportAsync(int mapId, float x, float y, float z)
    {
        await BotTeleportAsync(BgAccountName!, mapId, x, y, z);
        return "OK";
    }

    /// <summary>Resolve a character name to its account name.</summary>
    private string? ResolveAccountName(string? characterName)
    {
        if (characterName == null) return BgAccountName;
        if (characterName == BgCharacterName) return BgAccountName;
        if (characterName == FgCharacterName) return FgAccountName;
        var match = AllBots.FirstOrDefault(b => b.CharacterName == characterName);
        return match?.AccountName ?? BgAccountName;
    }

    /// <summary>Teleport a character to a named tele location via SOAP (.tele name). Works for offline characters.</summary>
    public Task<string> TeleportToNamedAsync(string? characterName, string locationName)
        => ExecuteGMCommandWithRetryAsync($".tele name {characterName ?? BgCharacterName} {locationName}");

    /// <summary>Teleport the BG bot to a named tele location via SOAP.</summary>
    public Task<string> TeleportToNamedAsync(string locationName)
        => TeleportToNamedAsync(BgCharacterName, locationName);

    /// <summary>Set a character's level.</summary>
    public Task<string> SetLevelAsync(string? characterName, int level)
        => ExecuteGMCommandAsync($".character level {characterName ?? BgCharacterName} {level}");

    public Task<string> SetLevelAsync(int level) => SetLevelAsync(BgCharacterName, level);

    /// <summary>Add an item to a character's bags.</summary>
    public Task<string> AddItemAsync(string? characterName, uint itemId, int count = 1)
        => ExecuteGMCommandAsync($".send items {characterName ?? BgCharacterName} \"Test\" \"item\" {itemId}:{count}");

    public Task<string> AddItemAsync(uint itemId, int count = 1) => AddItemAsync(BgCharacterName, itemId, count);

    public Task<string> SetGMOnAsync() => ExecuteGMCommandAsync(".gm on");
    public Task<string> SetGMOffAsync() => ExecuteGMCommandAsync(".gm off");

    public Task<string> KillPlayerAsync(string? characterName = null)
        => ExecuteGMCommandAsync($".die");

    public Task<string> RevivePlayerAsync(string? characterName = null)
        => ExecuteGMCommandAsync($".revive {characterName ?? BgCharacterName}");

    public Task<string> SetFullHealthManaAsync() => ExecuteGMCommandAsync(".modify hp 9999");

    public Task<string> AddMoneyAsync(uint copper)
        => ExecuteGMCommandAsync($".modify money {copper}");

    public Task<string> LearnSpellAsync(uint spellId)
        => ExecuteGMCommandAsync($".learn {spellId}");

    public Task<string> ResetSpellsAsync(string? characterName = null)
        => ExecuteGMCommandAsync($".reset spells {characterName ?? BgCharacterName}");

    public Task<string> UnlearnSpellAsync(uint spellId)
        => ExecuteGMCommandAsync($".unlearn {spellId}");

    /// <summary>
    /// Reset a character's items via SOAP GM command.
    /// Strips ALL equipment + inventory. Character must be online.
    /// </summary>
    public Task<string> ResetItemsAsync(string characterName)
        => ExecuteGMCommandAsync($".reset items {characterName}");

    /// <summary>
    /// Clear a bot's backpack by sending DestroyItem actions for all 16 slots.
    /// Alternative to `.reset items` (SOAP) — useful when you want to preserve equipped gear.
    /// `.reset items` strips ALL gear + inventory; this method only clears bag contents.
    /// Also destroys items in extra bag containers (bags 1-4, up to 16 slots each).
    /// Equipment slots (0-18) are NOT touched.
    /// </summary>
    public async Task BotClearInventoryAsync(string accountName, bool includeExtraBags = true)
    {
        _logger.LogInformation("[CLEANUP] Clearing inventory for {Account}...", accountName);

        // Backpack: bag=0, slots 0-15
        for (int slot = 0; slot < 16; slot++)
        {
            await SendActionAsync(accountName, new ActionMessage
            {
                ActionType = ActionType.DestroyItem,
                Parameters =
                {
                    new RequestParameter { IntParam = 0 },     // bagId = 0 (backpack)
                    new RequestParameter { IntParam = slot },   // slotId
                    new RequestParameter { IntParam = -1 }      // quantity = -1 (all)
                }
            });
        }

        if (includeExtraBags)
        {
            // Extra bags: bags 1-4, each up to 16 slots
            for (int bag = 1; bag <= 4; bag++)
            {
                for (int slot = 0; slot < 16; slot++)
                {
                    await SendActionAsync(accountName, new ActionMessage
                    {
                        ActionType = ActionType.DestroyItem,
                        Parameters =
                        {
                            new RequestParameter { IntParam = bag },
                            new RequestParameter { IntParam = slot },
                            new RequestParameter { IntParam = -1 }
                        }
                    });
                }
            }
        }

        // Wait for inventory updates to propagate
        await Task.Delay(3000);
        _logger.LogInformation("[CLEANUP] Inventory cleared for {Account}.", accountName);
    }

    // ---- Snapshot-based helpers (replace old ObjectManager-direct queries) ----

    /// <summary>
    /// Wait for player position to change (via snapshot polling).
    /// Returns true if position changed by more than 1 unit on X or Y axis.
    /// </summary>
    public async Task<bool> WaitForPositionChangeAsync(string? accountName, float startX, float startY, float startZ, int timeoutMs = 10000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return false;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snap = await GetSnapshotAsync(acct);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                if (Math.Abs(pos.X - startX) > 1 || Math.Abs(pos.Y - startY) > 1)
                    return true;
            }
            await Task.Delay(1000);
        }
        return false;
    }

    public Task<bool> WaitForPositionChangeAsync(float startX, float startY, float startZ, int timeoutMs = 10000)
        => WaitForPositionChangeAsync(BgAccountName, startX, startY, startZ, timeoutMs);

    /// <summary>
    /// Wait for Z to stabilize (not falling through world).
    /// Polls snapshots and checks Z samples over the given window.
    /// </summary>
    public async Task<(bool stable, float finalZ)> WaitForZStabilizationAsync(string? accountName, int waitMs = 5000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return (false, float.MinValue);

        await Task.Delay(1000); // Let physics settle
        var samples = new List<float>();
        for (int i = 0; i < waitMs / 1000; i++)
        {
            var snap = await GetSnapshotAsync(acct);
            var z = snap?.Player?.Unit?.GameObject?.Base?.Position?.Z;
            if (z.HasValue)
                samples.Add(z.Value);
            await Task.Delay(1000);
        }

        if (samples.Count < 2)
            return (false, float.MinValue);

        var finalZ = samples[^1];

        if (finalZ < -500)
            return (false, finalZ);

        var zDelta = Math.Abs(samples[^1] - samples[0]);
        return (zDelta < 50, finalZ);
    }

    public Task<(bool stable, float finalZ)> WaitForZStabilizationAsync(int waitMs = 5000)
        => WaitForZStabilizationAsync(BgAccountName, waitMs);

    /// <summary>
    /// Wait for a nearby unit with specified NPC flags to appear in the bot's snapshot.
    /// </summary>
    public async Task<Game.WoWUnit?> WaitForNearbyUnitAsync(string? accountName, uint npcFlags, int timeoutMs = 10000)
    {
        var acct = accountName ?? BgAccountName;
        if (acct == null || _stateManagerClient == null) return null;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var snap = await GetSnapshotAsync(acct);
            if (snap != null)
            {
                var unit = snap.NearbyUnits.FirstOrDefault(u => (u.NpcFlags & npcFlags) != 0);
                if (unit != null) return unit;
            }
            await Task.Delay(1000);
        }
        return null;
    }

    public Task<Game.WoWUnit?> WaitForNearbyUnitAsync(uint npcFlags, int timeoutMs = 10000)
        => WaitForNearbyUnitAsync(BgAccountName, npcFlags, timeoutMs);

    // ---- Bot-chat GM command helpers (bots are GM accounts, send commands through their own chat) ----

    /// <summary>
    /// Send a GM command through a bot's in-game chat. The bot types the command in chat,
    /// and because the account is GM-level, the server processes it. This is used for
    /// commands like .learn, .additem that need to target the current character (self).
    /// </summary>
    public Task SendGmChatCommandAsync(string accountName, string command)
        => SendGmChatCommandAsync(accountName, command, captureResponse: false);

    /// <summary>
    /// Send a GM command through bot chat and optionally capture the bot's immediate chat/error response
    /// from the next snapshot poll.
    /// </summary>
    public async Task SendGmChatCommandAsync(string accountName, string command, bool captureResponse)
    {
        _ = await SendGmChatCommandTrackedAsync(accountName, command, captureResponse);
    }

    public async Task<GmChatCommandTrace> SendGmChatCommandTrackedAsync(
        string accountName,
        string command,
        bool captureResponse = true,
        int delayMs = 2000,
        bool allowWhenDead = false)
    {
        var attemptCount = TrackChatCommand(accountName, command);
        LogDuplicateCommand("CHAT", command, attemptCount, accountName);

        if (_stateManagerClient != null)
        {
            var stateSnapshot = await GetSnapshotAsync(accountName);
            if (!allowWhenDead && IsDeadOrGhostState(stateSnapshot, out var deadReason))
            {
                var guardMessage = $"[CMD-GUARD] [{accountName}] Skipping '{command}' because sender is dead/ghost ({deadReason}).";
                _logger.LogInformation("{Message}", guardMessage);
                _testOutput?.WriteLine(guardMessage);

                return new GmChatCommandTrace(
                    attemptCount,
                    ResponseResult.Failure,
                    Array.Empty<string>(),
                    new[] { $"blocked by dead-state guard: {deadReason}" });
            }
        }

        var commandMessage = $"[CMD-SEND] [{accountName}] '{command}'";
        _logger.LogInformation("{Message}", commandMessage);
        _testOutput?.WriteLine(commandMessage);

        var baselineChats = Array.Empty<string>();
        var baselineErrors = Array.Empty<string>();
        if (captureResponse && _stateManagerClient != null)
        {
            // Snapshot before dispatch so we can log deltas for this command only.
            var baseline = await GetSnapshotAsync(accountName);
            if (baseline != null)
            {
                baselineChats = baseline.RecentChatMessages.ToArray();
                baselineErrors = baseline.RecentErrors.ToArray();
            }
        }

        var action = new ActionMessage
        {
            ActionType = ActionType.SendChat,
            Parameters = { new RequestParameter { StringParam = command } }
        };

        var dispatchResult = await SendActionAsync(accountName, action);
        _logger.LogInformation("[ACTION] Sent {Type} to {Account} → {Result}", action.ActionType, accountName, dispatchResult);
        await Task.Delay(delayMs);

        var chats = new List<string>();
        var errors = new List<string>();
        if (captureResponse && _stateManagerClient != null)
        {
            var responseSnapshot = await GetSnapshotAsync(accountName);
            if (responseSnapshot != null)
            {
                var chatDelta = GetDeltaMessages(baselineChats, responseSnapshot.RecentChatMessages);
                var errorDelta = GetDeltaMessages(baselineErrors, responseSnapshot.RecentErrors);

                chats.AddRange(chatDelta);
                errors.AddRange(errorDelta);
            }

            LogChatCommandResponses(accountName, command, chats, errors);
        }

        return new GmChatCommandTrace(attemptCount, dispatchResult, chats, errors);
    }

    internal static bool ContainsCommandRejection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("no such command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no such subcommand", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unknown command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not available to you", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToEvidenceSnippet(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength)
            return normalized;

        return normalized[..maxLength] + "...";
    }

    /// <summary>
    /// Induce death deterministically for a specific character using a single setup path:
    /// select self, issue one direct kill command, then verify transition in snapshots.
    /// </summary>
    public async Task<DeathInductionResult> InduceDeathForTestAsync(
        string accountName,
        string characterName,
        int timeoutMs = 15000,
        bool requireCorpseTransition = false,
        bool singleCommandOnly = false)
    {
        var baseline = await GetSnapshotAsync(accountName);
        if (baseline == null)
            return new DeathInductionResult(false, string.Empty, "No baseline snapshot available.");

        if (IsDeadOrGhostState(baseline, out var alreadyDeadReason))
        {
            if (requireCorpseTransition)
                return new DeathInductionResult(false, "(already-dead)", $"already dead/ghost before kill setup ({alreadyDeadReason})");

            return new DeathInductionResult(true, "(already-dead)", $"already dead/ghost ({alreadyDeadReason})");
        }

        // Some self-directed commands require a selected self target.
        var selfGuid = baseline.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (selfGuid == 0)
            return new DeathInductionResult(false, ".kill", "Unable to resolve self GUID for target selection.");

        async Task<(ResponseResult dispatch, ulong selected)> EnsureSelfTargetAsync()
        {
            var lastDispatch = ResponseResult.Failure;
            ulong lastSelectedGuid = 0;

            for (var attempt = 0; attempt < 3; attempt++)
            {
                lastDispatch = await SendActionAsync(accountName, new ActionMessage
                {
                    ActionType = ActionType.StartMeleeAttack,
                    Parameters = { new RequestParameter { LongParam = (long)selfGuid } }
                });

                if (lastDispatch != ResponseResult.Success)
                {
                    await Task.Delay(150);
                    continue;
                }

                // Stop any accidental swing attempts; only target selection side effect is needed.
                _ = await SendActionAsync(accountName, new ActionMessage { ActionType = ActionType.StopAttack });

                var pollSw = Stopwatch.StartNew();
                while (pollSw.ElapsedMilliseconds < 2500)
                {
                    await Task.Delay(150);
                    var selectSnapshot = await GetSnapshotAsync(accountName);
                    lastSelectedGuid = selectSnapshot?.Player?.Unit?.TargetGuid ?? 0UL;
                    if (lastSelectedGuid == selfGuid)
                        return (lastDispatch, lastSelectedGuid);
                }
            }

            return (lastDispatch, lastSelectedGuid);
        }

        const string selfDamageCommand = ".damage 5000";
        var resolvedKillCommand = await ResolveSelfKillCommandAsync();
        var commandCandidates = new List<string>();
        if (requireCorpseTransition)
        {
            // Corpse-run setup should use .die deterministically, with self-damage fallback.
            commandCandidates.Add(".die");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(resolvedKillCommand))
                commandCandidates.Add(resolvedKillCommand);
            if (!commandCandidates.Contains(".kill", StringComparer.OrdinalIgnoreCase))
                commandCandidates.Add(".kill");
            if (!commandCandidates.Contains(".die", StringComparer.OrdinalIgnoreCase))
                commandCandidates.Add(".die");
        }
        if (singleCommandOnly)
        {
            var deterministicSingle = requireCorpseTransition
                ? ".die"
                : commandCandidates.FirstOrDefault(c => string.Equals(c, ".die", StringComparison.OrdinalIgnoreCase))
                    ?? commandCandidates.FirstOrDefault()
                    ?? ".die";
            commandCandidates = [deterministicSingle];
        }

        if (requireCorpseTransition
            && !singleCommandOnly
            && !commandCandidates.Contains(".damage", StringComparer.OrdinalIgnoreCase))
            commandCandidates.Add(".damage");

        if (requireCorpseTransition)
            commandCandidates = commandCandidates
                .Where(command => !string.Equals(command, ".kill", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var killAttempts = new List<(string BaseCommand, string CommandToSend)>();
        foreach (var killCommand in commandCandidates)
        {
            if (string.Equals(killCommand, ".die", StringComparison.OrdinalIgnoreCase)
                && requireCorpseTransition)
            {
                var trimmedName = characterName?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedName))
                {
                    // Different server builds disagree on which ".die" form transitions
                    // the issuing player into corpse state, so probe both deterministically.
                    killAttempts.Add((killCommand, $".die {trimmedName}"));
                }
            }

            if (string.Equals(killCommand, ".damage", StringComparison.OrdinalIgnoreCase))
            {
                killAttempts.Add((killCommand, selfDamageCommand));
                continue;
            }

            killAttempts.Add((killCommand, killCommand));
        }

        var observedCorpseState = false;
        Game.Position? observedCorpsePosition = null;
        var usedCorpsePositionFallback = false;
        var attemptEvidence = new List<string>();
        var perCommandTimeoutMs = Math.Max(4000, timeoutMs / Math.Max(1, killAttempts.Count));

        async Task<DeathInductionResult?> ProbeTransitionAsync(string commandLabel, int commandTimeoutMs)
        {
            uint? lastHealth = null;
            bool? lastGhost = null;
            uint? lastStandState = null;
            uint? lastReclaimDelay = null;
            var pollCount = 0;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < commandTimeoutMs)
            {
                await Task.Delay(100);
                pollCount++;
                var snap = await GetSnapshotAsync(accountName);
                var player = snap?.Player;
                var unit = player?.Unit;
                if (player != null && unit != null)
                {
                    const uint standStateMask = 0xFF;
                    const uint playerFlagGhost = 0x10;
                    var standState = unit.Bytes1 & standStateMask;
                    var hasGhostFlag = (player.PlayerFlags & playerFlagGhost) != 0;
                    var reclaimDelay = player.CorpseRecoveryDelaySeconds;
                    var stateChanged = !lastHealth.HasValue
                        || lastHealth.Value != unit.Health
                        || !lastGhost.HasValue
                        || lastGhost.Value != hasGhostFlag
                        || !lastStandState.HasValue
                        || lastStandState.Value != standState
                        || !lastReclaimDelay.HasValue
                        || lastReclaimDelay.Value != reclaimDelay;

                    if (stateChanged || pollCount % 10 == 0)
                    {
                        _logger.LogInformation("[DEATH-SETUP] Probe({Command}): HP={Health}, ghost={Ghost}, stand={StandState}, reclaimDelay={Delay}s",
                            commandLabel, unit.Health, hasGhostFlag, standState, reclaimDelay);
                    }

                    lastHealth = unit.Health;
                    lastGhost = hasGhostFlag;
                    lastStandState = standState;
                    lastReclaimDelay = reclaimDelay;
                }

                if (IsCorpseState(snap, out var corpseReason))
                {
                    observedCorpseState = true;
                    var corpsePos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                    observedCorpsePosition = corpsePos == null
                        ? null
                        : new Game.Position { X = corpsePos.X, Y = corpsePos.Y, Z = corpsePos.Z };
                    _logger.LogInformation("[DEATH-SETUP] Transitioned to corpse via {Command} ({Reason})",
                        commandLabel, corpseReason);
                    return new DeathInductionResult(
                        true,
                        commandLabel,
                        string.Join("; ", attemptEvidence) + $"; transition=corpse ({corpseReason})",
                        observedCorpseState,
                        observedCorpsePosition,
                        usedCorpsePositionFallback);
                }

                if (IsCorpseOrGhostTransitionState(snap, out var deadReason))
                {
                    if (requireCorpseTransition)
                    {
                        attemptEvidence.Add($"{commandLabel}:ghost-before-corpse({deadReason})");
                        continue;
                    }

                    _logger.LogInformation("[DEATH-SETUP] Transitioned to dead/ghost via {Command} ({Reason})",
                        commandLabel, deadReason);
                    return new DeathInductionResult(
                        true,
                        commandLabel,
                        string.Join("; ", attemptEvidence) + $"; transition={deadReason}",
                        observedCorpseState,
                        observedCorpsePosition,
                        usedCorpsePositionFallback);
                }
            }

            if (requireCorpseTransition)
                attemptEvidence.Add($"{commandLabel}:no-corpse-transition");
            else
                attemptEvidence.Add($"{commandLabel}:no-transition");

            return null;
        }

        foreach (var attempt in killAttempts)
        {
            var killCommand = attempt.BaseCommand;
            var commandToSend = attempt.CommandToSend;

            var requiresSelfTarget = string.Equals(killCommand, ".kill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killCommand, ".damage", StringComparison.OrdinalIgnoreCase);
            if (requiresSelfTarget)
            {
                var (selectDispatch, selectedGuid) = await EnsureSelfTargetAsync();
                if (selectDispatch != ResponseResult.Success)
                {
                    attemptEvidence.Add($"{killCommand}:self-target-dispatch={selectDispatch}");
                    continue;
                }

                _logger.LogInformation("[DEATH-SETUP] Self-target guid for {Account}: 0x{TargetGuid:X} (self=0x{SelfGuid:X})",
                    accountName, selectedGuid, selfGuid);
                if (selectedGuid != selfGuid)
                {
                    attemptEvidence.Add($"{killCommand}:self-target-mismatch(selected=0x{selectedGuid:X},self=0x{selfGuid:X})");
                    // Some BG snapshots intermittently report TargetGuid=0 for self-select attempts.
                    // In corpse-transition mode we still probe .damage once to avoid false negatives.
                    var allowAmbiguousDamageFallback =
                        requireCorpseTransition
                        && string.Equals(killCommand, ".damage", StringComparison.OrdinalIgnoreCase);
                    if (!allowAmbiguousDamageFallback)
                        continue;
                }
            }

            var captureCommandResponse = true;
            var killTrace = await SendGmChatCommandTrackedAsync(
                accountName,
                commandToSend,
                captureResponse: captureCommandResponse,
                delayMs: requireCorpseTransition ? 300 : 200,
                allowWhenDead: false);

            attemptEvidence.Add($"{commandToSend}:dispatch={killTrace.DispatchResult}");
            if (killTrace.DispatchResult != ResponseResult.Success)
                continue;

            var rejectionSignals = killTrace.ChatMessages
                .Concat(killTrace.ErrorMessages)
                .Where(ContainsCommandRejection)
                .Select(message => ToEvidenceSnippet(message))
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var rejected = rejectionSignals.Length > 0;
            if (rejected)
            {
                foreach (var rejectionSignal in rejectionSignals)
                    attemptEvidence.Add($"{commandToSend}:rejected({rejectionSignal})");
                continue;
            }

            var commandTimeoutMs = perCommandTimeoutMs;
            var transitionResult = await ProbeTransitionAsync(commandToSend, commandTimeoutMs);
            if (transitionResult != null)
                return transitionResult;
        }

        if (requireCorpseTransition && !string.IsNullOrWhiteSpace(characterName))
        {
            // Chat-command dispatch can report success without applying state changes on some
            // server builds; SOAP .die is the deterministic fallback used only in corpse mode.
            var soapCommands = new[]
            {
                $".die {characterName.Trim()}",
                ".die"
            };

            foreach (var soapCommand in soapCommands.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var soapResult = await ExecuteGMCommandAsync(soapCommand);
                if (!string.IsNullOrWhiteSpace(soapResult))
                    attemptEvidence.Add($"{soapCommand}:soap={soapResult.Trim()}");
                else
                    attemptEvidence.Add($"{soapCommand}:soap=(empty)");

                if (ContainsCommandRejection(soapResult)
                    || soapResult.StartsWith("FAULT:", StringComparison.OrdinalIgnoreCase))
                {
                    attemptEvidence.Add($"{soapCommand}:soap-rejected");
                    continue;
                }

                var transitionResult = await ProbeTransitionAsync($"{soapCommand}[SOAP]", perCommandTimeoutMs);
                if (transitionResult != null)
                    return transitionResult;
            }
        }

        return new DeathInductionResult(false, commandCandidates.FirstOrDefault() ?? ".kill",
            requireCorpseTransition
                ? $"{string.Join("; ", attemptEvidence)}; no corpse transition observed within timeout"
                : $"{string.Join("; ", attemptEvidence)}; no dead/ghost transition observed within timeout",
            observedCorpseState,
            observedCorpsePosition,
            usedCorpsePositionFallback);
    }

    /// <summary>Learn a spell for a specific bot by having it type .learn in chat.</summary>
    public Task BotLearnSpellAsync(string accountName, uint spellId)
        => SendGmChatCommandAsync(accountName, $".learn {spellId}");

    /// <summary>Unlearn a spell for a specific bot by having it type .unlearn in chat.</summary>
    public Task BotUnlearnSpellAsync(string accountName, uint spellId)
        => SendGmChatCommandAsync(accountName, $".unlearn {spellId}");

    /// <summary>Add an item to a bot's own bags by having it type .additem in chat.</summary>
    public Task BotAddItemAsync(string accountName, uint itemId, int count = 1)
        => SendGmChatCommandAsync(accountName, $".additem {itemId} {count}");

    /// <summary>
    /// Teleport a bot by having it type .go xyz in chat (self-teleport).
    /// Tries map-form syntax first and falls back to map-less syntax when rejected.
    /// </summary>
    public async Task BotTeleportAsync(string accountName, int mapId, float x, float y, float z)
    {
        var xText = x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var yText = y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var zText = z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        var commandWithMap = $".go xyz {xText} {yText} {zText} {mapId}";
        var withMapTrace = await SendGmChatCommandTrackedAsync(
            accountName,
            commandWithMap,
            captureResponse: true,
            delayMs: 1000,
            allowWhenDead: false);

        var rejectedWithMap =
            withMapTrace.ChatMessages.Any(ContainsCommandRejection)
            || withMapTrace.ErrorMessages.Any(ContainsCommandRejection)
            || withMapTrace.ChatMessages.Any(m => m.Contains("subcommand", StringComparison.OrdinalIgnoreCase));
        if (!rejectedWithMap)
            return;

        var commandWithoutMap = $".go xyz {xText} {yText} {zText}";
        _logger.LogInformation("[TELEPORT] Retrying map-less syntax: {Command}", commandWithoutMap);
        await SendGmChatCommandTrackedAsync(
            accountName,
            commandWithoutMap,
            captureResponse: true,
            delayMs: 1000,
            allowWhenDead: false);
    }

    /// <summary>Teleport a bot to a named location by having it type .tele in chat (self-teleport).</summary>
    public Task BotTeleportToNamedAsync(string accountName, string locationName)
        => SendGmChatCommandAsync(accountName, $".tele {locationName}");

    // ---- MySQL direct helpers (bypass disabled GM commands in some repacks) ----

    private string MangosWorldDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=mangos;Connection Timeout=5;";

    private string MangosCharDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=characters;Connection Timeout=5;";

    private string MangosRealmDbConnectionString
        => $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Database=realmd;Connection Timeout=5;";

    private static readonly (string Name, uint Security, string Help, byte Flags)[] CommandTableBaselineRows =
    [
        ("wareffortget", 6, "Syntax: .wareffortget \"[ResourceName]\"", 0),
        ("wareffortset", 6, "Syntax: .wareffortset \"[ResourceName]\" [NewResourceCount]", 0),
        ("debug setvaluebyindex", 5, "Syntax: .debug setvaluebyindex [index] [type] [value]", 2),
        ("debug setvaluebyname", 5, "Syntax: .debug setvaluebyname [name] [value]", 2)
    ];

    private async Task<int> RestoreCommandTableBaselineAsync(MySql.Data.MySqlClient.MySqlConnection conn)
    {
        // Backup current state once per run for local recovery.
        using (var backupCmd = conn.CreateCommand())
        {
            backupCmd.CommandText = "CREATE TABLE IF NOT EXISTS command_backup_fixture AS SELECT * FROM command";
            await backupCmd.ExecuteNonQueryAsync();
        }

        using (var truncateCmd = conn.CreateCommand())
        {
            truncateCmd.CommandText = "TRUNCATE TABLE command";
            await truncateCmd.ExecuteNonQueryAsync();
        }

        int inserted = 0;
        foreach (var row in CommandTableBaselineRows)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO command (name, security, help, flags)
                VALUES (@name, @security, @help, @flags)";
            insertCmd.Parameters.AddWithValue("@name", row.Name);
            insertCmd.Parameters.AddWithValue("@security", row.Security);
            insertCmd.Parameters.AddWithValue("@help", row.Help);
            insertCmd.Parameters.AddWithValue("@flags", row.Flags);
            inserted += await insertCmd.ExecuteNonQueryAsync();
        }

        return inserted;
    }

    /// <summary>
    /// Resolve a deterministic self-kill command for the current server build.
    /// Prefers .kill when present; otherwise falls back to .die if available.
    /// </summary>
    public async Task<string?> ResolveSelfKillCommandAsync()
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT name
                                FROM command
                                WHERE name IN ('kill', 'die')
                                ORDER BY CASE WHEN name = 'kill' THEN 0 WHEN name = 'die' THEN 1 ELSE 2 END
                                LIMIT 1";

            var obj = await cmd.ExecuteScalarAsync();
            var selected = obj?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                _logger.LogWarning("[MySQL] No supported self-kill command found (kill/die).");
                return null;
            }

            var command = $".{selected}";
            _logger.LogInformation("[MySQL] Resolved self-kill command: {Command}", command);
            return command;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to resolve self-kill command: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Ensure test accounts have GM level and sanitize stale fixture-injected rows from the MaNGOS
    /// command table. We avoid inserting/overwriting command definitions because that can drift from
    /// the server's compiled 1.12.1 command hierarchy and produce misleading runtime warnings.
    /// </summary>
    public async Task EnsureGmCommandsEnabledAsync()
    {
        // Step 1: Ensure test + SOAP accounts have GM level 6 in both account and account_access tables.
        try
        {
            using var realmConn = new MySql.Data.MySqlClient.MySqlConnection(MangosRealmDbConnectionString);
            await realmConn.OpenAsync();

            var gmAccounts = new[] { "ADMINISTRATOR", "ORWR1", "ORSH1" };
            var updatedAccounts = 0;
            foreach (var accountName in gmAccounts)
            {
                using var gmCmd = realmConn.CreateCommand();
                gmCmd.CommandText = "UPDATE account SET gmlevel = 6 WHERE username = @username";
                gmCmd.Parameters.AddWithValue("@username", accountName);
                updatedAccounts += await gmCmd.ExecuteNonQueryAsync();

                using var accessCmd = realmConn.CreateCommand();
                accessCmd.CommandText = @"
                    INSERT INTO account_access (id, gmlevel, RealmID)
                    SELECT id, 6, 1 FROM account WHERE username = @username
                    ON DUPLICATE KEY UPDATE gmlevel = VALUES(gmlevel)";
                accessCmd.Parameters.AddWithValue("@username", accountName);
                _ = await accessCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("[MySQL] Ensured GM level 6 for test accounts (account rows updated: {Rows})", updatedAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to enforce account GM levels: {Error}", ex.Message);
        }

        // Step 2: sanitize stale command-table rows created by older fixture behavior.
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            var useBaselineRestore = string.Equals(
                Environment.GetEnvironmentVariable("WWOW_TEST_RESTORE_COMMAND_TABLE"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            int removedRows = 0;
            var cleanupCommands = new[]
            {
                "DELETE FROM command WHERE name = '' OR name IS NULL",
                // Only remove rows explicitly marked as fixture-owned; never delete core server commands.
                "DELETE FROM command WHERE help LIKE '%Enabled by test fixture%' AND name NOT IN ('die', 'revive')"
            };

            foreach (var sql in cleanupCommands)
            {
                using var cleanupCmd = conn.CreateCommand();
                cleanupCmd.CommandText = sql;
                removedRows += await cleanupCmd.ExecuteNonQueryAsync();
            }

            // Normalize stale fixture help text for commands we still rely on in live tests.
            var normalizeHelpCommands = new[]
            {
                ("die", "Syntax: .die [name]"),
                ("revive", "Syntax: .revive [name]")
            };

            var normalizedRows = 0;
            foreach (var (name, helpText) in normalizeHelpCommands)
            {
                using var normalizeCmd = conn.CreateCommand();
                normalizeCmd.CommandText = @"
                    UPDATE command
                    SET help = @help
                    WHERE name = @name
                      AND (help LIKE '%Enabled by test fixture%' OR help = '' OR help IS NULL)";
                normalizeCmd.Parameters.AddWithValue("@help", helpText);
                normalizeCmd.Parameters.AddWithValue("@name", name);
                normalizedRows += await normalizeCmd.ExecuteNonQueryAsync();
            }

            if (useBaselineRestore)
            {
                var insertedRows = await RestoreCommandTableBaselineAsync(conn);
                _logger.LogInformation("[MySQL] Command table baseline restore enabled (WWOW_TEST_RESTORE_COMMAND_TABLE=1). Inserted {Rows} row(s).",
                    insertedRows);
            }

            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM command";
            var remaining = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

            _logger.LogInformation("[MySQL] Command table sanitized: removed {Removed} stale row(s), remaining rows={Remaining}",
                removedRows, remaining);
            _logger.LogInformation("[MySQL] Command table help normalization: updated {Rows} row(s) for die/revive", normalizedRows);

            // Log key command rows used by live test setup to keep command behavior debuggable.
            using var inspectCmd = conn.CreateCommand();
            inspectCmd.CommandText = @"
                SELECT name, security, help
                FROM command
                WHERE name IN ('kill', 'die', 'damage', 'aoedamage', 'revive', 'select', 'select player')
                ORDER BY name";
            using var reader = await inspectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                var security = reader.IsDBNull(1) ? 0U : Convert.ToUInt32(reader.GetValue(1));
                var help = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                _logger.LogInformation("[MySQL] Command row: name='{Name}', security={Security}, help='{Help}'",
                    name, security, help);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to sanitize command table: {Error}", ex.Message);
        }

        // Step 3: Reload command table so cleanup takes effect without server restart.
        try
        {
            var result = await ExecuteGMCommandAsync(".reload command");
            _logger.LogInformation("[MySQL] Reload command result: {Result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] Failed to reload command table: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Teach a spell directly via MySQL character_spell table + SOAP trigger.
    /// Fallback when .learn is disabled in the repack's command table.
    /// </summary>
    public async Task<bool> DirectLearnSpellAsync(string characterName, uint spellId)
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosCharDbConnectionString);
            await conn.OpenAsync();

            // Get character GUID
            using var guidCmd = conn.CreateCommand();
            guidCmd.CommandText = "SELECT guid FROM characters WHERE name = @name";
            guidCmd.Parameters.AddWithValue("@name", characterName);
            var guidObj = await guidCmd.ExecuteScalarAsync();
            if (guidObj == null)
            {
                _logger.LogWarning("[MySQL] Character '{Name}' not found in characters table", characterName);
                return false;
            }
            var charGuid = Convert.ToUInt32(guidObj);

            // Insert spell (ignore if already exists)
            using var spellCmd = conn.CreateCommand();
            spellCmd.CommandText = "INSERT IGNORE INTO character_spell (guid, spell, active, disabled) VALUES (@guid, @spell, 1, 0)";
            spellCmd.Parameters.AddWithValue("@guid", charGuid);
            spellCmd.Parameters.AddWithValue("@spell", spellId);
            var rows = await spellCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("[MySQL] DirectLearnSpell: char={Name} (guid={Guid}) spell={Spell} rows={Rows}",
                characterName, charGuid, spellId, rows);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[MySQL] DirectLearnSpell failed: {Error}", ex.Message);
            return false;
        }
    }

    // ---- Dual-bot helpers ----

    /// <summary>Execute a GM command for a specific character, with retry.</summary>
    public async Task<string> ExecuteGMCommandWithRetryAsync(string command, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var result = await ExecuteGMCommandAsync(command);
            if (!string.IsNullOrEmpty(result) || i == maxRetries - 1)
                return result;
            _logger.LogWarning("[GM] Retry {Attempt}/{Max} for: {Command}", i + 1, maxRetries, command);
            await Task.Delay(1000);
        }
        return string.Empty;
    }

    /// <summary>Teleport a character and verify the position changed.</summary>
    public async Task<bool> TeleportAndVerifyAsync(string charName, string accountName, int mapId, float x, float y, float z, int timeoutMs = 10000)
    {
        async Task<bool> WaitForPositionAsync(int waitTimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitTimeoutMs)
            {
                var snap = await GetSnapshotAsync(accountName);
                var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (pos != null)
                {
                    var dist = Math.Sqrt(Math.Pow(pos.X - x, 2) + Math.Pow(pos.Y - y, 2));
                    var zDiff = Math.Abs(pos.Z - z);
                    if (dist < 50 && zDiff < 40) // avoid false positives when player keeps falling after teleport
                    {
                        _logger.LogInformation("[VERIFY] {Name} teleported to ({X:F0},{Y:F0},{Z:F0}), actual ({AX:F0},{AY:F0},{AZ:F0})",
                            charName, x, y, z, pos.X, pos.Y, pos.Z);
                        return true;
                    }
                }

                await Task.Delay(1000);
            }

            return false;
        }

        // Use bot chat .go xyz - SOAP .teleport name with coordinates does not exist on MaNGOS.
        await BotTeleportAsync(accountName, mapId, x, y, z);
        await Task.Delay(3000); // Let physics settle

        var firstWindowMs = Math.Max(3000, timeoutMs / 2);
        if (await WaitForPositionAsync(firstWindowMs))
            return true;

        // Retry with explicit map-less syntax when first attempt misses target.
        var xText = x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var yText = y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var zText = z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var fallbackCommand = $".go xyz {xText} {yText} {zText}";
        _logger.LogWarning("[VERIFY] {Name} teleport not near target yet; retrying with '{Command}'", charName, fallbackCommand);
        await SendGmChatCommandTrackedAsync(
            accountName,
            fallbackCommand,
            captureResponse: true,
            delayMs: 1000,
            allowWhenDead: false);
        await Task.Delay(2000);

        var secondWindowMs = Math.Max(2000, timeoutMs - firstWindowMs);
        if (await WaitForPositionAsync(secondWindowMs))
            return true;

        _logger.LogWarning("[VERIFY] {Name} teleport failed - position not near target", charName);
        return false;
    }

    /// <summary>Send an action to a bot and wait for a brief processing delay.</summary>
    public async Task SendActionAndWaitAsync(string accountName, ActionMessage action, int delayMs = 500)
    {
        var result = await SendActionAsync(accountName, action);
        _logger.LogInformation("[ACTION] Sent {Type} to {Account} → {Result}", action.ActionType, accountName, result);
        await Task.Delay(delayMs);
    }

    // ---- MaNGOS Server Management ----

    /// <summary>
    /// Restart MaNGOS world server via SOAP and wait for it to come back online.
    /// This cleans up stale in-memory state (despawned objects, stuck NPCs, etc.).
    /// Note: manually-added gameobjects via .gobject add persist in the DB across restarts.
    /// </summary>
    public async Task RestartMangosdAsync(int timeoutSeconds = 120)
    {
        _logger.LogInformation("[RESTART] Sending .server restart 5 via SOAP...");
        await ExecuteGMCommandAsync(".server restart 5");

        // Wait for the server to actually go down
        await Task.Delay(10_000);

        // Poll until world server is back
        var health = new ServiceHealthChecker();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var isUp = await health.IsMangosdAvailableAsync(Config);
            if (isUp)
            {
                _logger.LogInformation("[RESTART] MaNGOS back online after {Elapsed:F0}s", sw.Elapsed.TotalSeconds);
                await Task.Delay(5000); // Extra settle time for full initialization
                return;
            }
            await Task.Delay(3000);
        }
        throw new TimeoutException($"MaNGOS did not restart within {timeoutSeconds}s");
    }

    /// <summary>
    /// Clean up zombie gameobjects created by previous .gobject add test runs.
    /// Cleans known test areas (Orgrimmar, Valley of Trials, Durotar) where nodes
    /// were manually spawned during earlier test development.
    /// </summary>
    private async Task CleanupZombieGameObjectsAsync()
    {
        // Known ore/herb entries used in tests
        var nodeEntries = new uint[] { 1731, 1732, 1617, 1618, 1619 }; // Copper, Tin, Peacebloom, Silverleaf, Earthroot

        // Known test locations where .gobject add was used in earlier sessions
        var testLocations = new (int map, float x, float y)[] {
            (1, 1629f, -4373f),   // Orgrimmar (GM setup area)
            (1, -600f, -4200f),   // Valley of Trials
            (1, -900f, -4500f),   // Durotar coast
        };

        int totalCleaned = 0;
        foreach (var entry in nodeEntries)
        {
            foreach (var (map, x, y) in testLocations)
            {
                totalCleaned += await CleanupGameObjectsNearAsync(entry, map, x, y, radius: 50);
            }
        }
        if (totalCleaned > 0)
            _logger.LogInformation("[CLEANUP] Total zombie gameobjects removed: {Count}", totalCleaned);
    }

    /// <summary>
    /// Delete manually-added gameobjects near a specific location.
    /// Used to clean up zombie nodes from previous test runs (.gobject add creates permanent DB entries).
    /// </summary>
    public async Task<int> CleanupGameObjectsNearAsync(uint entry, int map, float x, float y, float radius = 10)
    {
        try
        {
            using var conn = new MySql.Data.MySqlClient.MySqlConnection(MangosWorldDbConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM gameobject
                WHERE id = @entry AND map = @map
                AND SQRT(POW(position_x - @x, 2) + POW(position_y - @y, 2)) < @radius";
            cmd.Parameters.AddWithValue("@entry", entry);
            cmd.Parameters.AddWithValue("@map", map);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@radius", radius);
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
                _logger.LogInformation("[CLEANUP] Deleted {Rows} gameobjects (entry={Entry}) near ({X:F0},{Y:F0})", rows, entry, x, y);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CLEANUP] Failed to clean up gameobjects: {Error}", ex.Message);
            return 0;
        }
    }

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
                ? "SELECT map, position_x, position_y, position_z FROM gameobject WHERE id = @id AND map = @map LIMIT @limit"
                : "SELECT map, position_x, position_y, position_z FROM gameobject WHERE id = @id LIMIT @limit";
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
