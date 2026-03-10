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

public partial class LiveBotFixture
{

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


    public static bool IsStrictAlive(WoWActivitySnapshot? snap)
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

    /// <summary>
    /// Generic snapshot polling helper. Refreshes snapshots in a loop until the predicate
    /// returns true or the timeout expires. Replaces ad-hoc Stopwatch + while + RefreshSnapshots
    /// polling loops scattered across test files.
    /// </summary>
    /// <param name="accountName">Account to poll snapshots for.</param>
    /// <param name="predicate">Condition to check on each snapshot. Null snapshots are treated as non-matching.</param>
    /// <param name="timeout">Maximum time to wait before returning false.</param>
    /// <param name="pollIntervalMs">Milliseconds between polls (default 400ms).</param>
    /// <returns>True if the predicate was satisfied within the timeout, false otherwise.</returns>


    /// <summary>
    /// Ensure a bot is strict-alive before test setup. If dead or ghost, issues a SOAP revive
    /// and polls snapshots until the alive state is confirmed. Skips the test if alive state
    /// cannot be established (infrastructure issue, not a test failure).
    /// </summary>
    /// <param name="account">Account name of the bot to check.</param>
    /// <param name="label">Human-readable label for log messages (e.g. "BG", "FG").</param>
    /// <param name="timeoutSeconds">Seconds to wait for strict-alive after revive (default 15).</param>
    public async Task EnsureStrictAliveAsync(string account, string label, int timeoutSeconds = 15)
    {
        await RefreshSnapshotsAsync();
        var snap = await GetSnapshotAsync(account);
        if (IsStrictAlive(snap))
            return;

        var characterName = snap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(characterName),
            $"{label}: missing character name for revive setup.");

        _logger.LogInformation("[{Label}] Not strict-alive at setup; reviving.", label);
        await RevivePlayerAsync(characterName!);

        var restored = await WaitForSnapshotConditionAsync(
            account,
            IsStrictAlive,
            TimeSpan.FromSeconds(timeoutSeconds));

        global::Tests.Infrastructure.Skip.If(!restored,
            $"{label}: failed to restore strict-alive setup state.");
    }

    // ---- Shared Safe Zone Constants ----

    /// <summary>Orgrimmar safe zone (no hostile mobs) for test setup/teardown.</summary>
    public const int SafeZoneMap = 1;
    public const float SafeZoneX = 1629f;
    public const float SafeZoneY = -4373f;
    public const float SafeZoneZ = 34f;

    /// <summary>
    /// Standardized test setup: ensures the bot is in a known-good state before any test.
    /// 1) Logs the bot's current state (position, health, flags) for debugging.
    /// 2) If dead/ghost, logs WHY (previous test state leak) and revives.
    /// 3) Teleports to Orgrimmar safe zone.
    /// 4) Ensures GM mode is ON.
    /// This replaces ad-hoc EnsureStrictAlive + manual teleport patterns (BT-SETUP-001).
    /// </summary>
    public async Task EnsureCleanSlateAsync(string account, string label)
    {
        await RefreshSnapshotsAsync();
        var snap = await GetSnapshotAsync(account);
        var characterName = snap?.CharacterName;

        // Log initial state so we can debug cross-test contamination
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var health = snap?.Player?.Unit?.Health ?? 0;
        var maxHealth = snap?.Player?.Unit?.MaxHealth ?? 0;
        var moveFlags = snap?.Player?.Unit?.MovementFlags ?? 0;
        _logger.LogInformation("[{Label}] CleanSlate entry: char={Char}, hp={HP}/{MaxHP}, " +
            "pos=({X:F0},{Y:F0},{Z:F0}), moveFlags=0x{Flags:X}",
            label, characterName ?? "?", health, maxHealth,
            pos?.X ?? 0, pos?.Y ?? 0, pos?.Z ?? 0, moveFlags);

        // Step 1: Revive if dead — but log the reason (state leak from previous test)
        if (!IsStrictAlive(snap))
        {
            IsDeadOrGhostState(snap, out var deathReason);
            _logger.LogWarning("[{Label}] Bot NOT alive at test start (state leak from previous test). " +
                "Reason: {Reason}. Reviving.", label, deathReason);
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                await RevivePlayerAsync(characterName);
                var revived = await WaitForSnapshotConditionAsync(
                    account, IsStrictAlive, TimeSpan.FromSeconds(10));
                if (!revived)
                {
                    _logger.LogWarning("[{Label}] Revive failed after 10s. Skipping test.", label);
                    global::Tests.Infrastructure.Skip.If(true,
                        $"{label}: failed to revive bot at test start (death reason: {deathReason}).");
                }
            }
            else
            {
                global::Tests.Infrastructure.Skip.If(true,
                    $"{label}: missing character name for clean slate setup.");
            }
        }

        // Step 2: Teleport to safe zone (prevents position contamination from previous test)
        await BotTeleportAsync(account, SafeZoneMap, SafeZoneX, SafeZoneY, SafeZoneZ);
        await WaitForZStabilizationAsync(account, waitMs: 2000);

        // Step 3: Ensure GM mode is on (prevents state leak if previous test toggled it off)
        await SendGmChatCommandAsync(account, ".gm on");
        await Task.Delay(300);
    }

    // ---- GM Command Helpers (via SOAP — independent of bots) ----

    /// <summary>Execute any GM command via SOAP.</summary>


    internal static bool ContainsCommandRejection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Note: "player not found" is intentionally NOT matched here — it's a
        // .targetself response that can bleed into subsequent command capture windows
        // due to async chat message delivery. Real command rejections use specific
        // messages like "Quest not found", "Spell not known", etc.
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

    /// <summary>
    /// Make the bot select itself (CMSG_SET_SELECTION → own GUID).
    /// Required before GM commands like .setskill that need a selected target.
    /// This is an internal bot command — nothing is sent to server chat.
    /// </summary>


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

    // ---- Shared Distance Helpers (BT-LOGIC-001) ----

    /// <summary>2D distance (XY plane only).</summary>
    public static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>3D distance.</summary>
    public static float Distance3D(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
