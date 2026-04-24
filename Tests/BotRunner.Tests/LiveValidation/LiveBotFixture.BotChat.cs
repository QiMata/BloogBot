using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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


    private static bool MatchesExecutedSendChatCommand(WoWActivitySnapshot? snapshot, string command)
    {
        var previousAction = snapshot?.PreviousAction;
        if (previousAction?.ActionType != ActionType.SendChat || previousAction.Parameters.Count == 0)
            return false;

        var sentCommand = previousAction.Parameters[0].StringParam;
        return string.Equals(sentCommand, command, StringComparison.OrdinalIgnoreCase);
    }

    internal static int GetTrackedChatCommandDelayMs(string command, int requestedDelayMs)
    {
        if (string.IsNullOrWhiteSpace(command))
            return requestedDelayMs;

        if (command.Equals(".taxicheat on", StringComparison.OrdinalIgnoreCase))
            return Math.Max(requestedDelayMs, 4500);

        if (command.StartsWith(".pool spawns ", StringComparison.OrdinalIgnoreCase))
            return Math.Max(requestedDelayMs, 3500);

        if (command.StartsWith(".pool info ", StringComparison.OrdinalIgnoreCase))
            return Math.Max(requestedDelayMs, 3500);

        if (command.StartsWith(".pool update ", StringComparison.OrdinalIgnoreCase))
            return Math.Max(requestedDelayMs, 2500);

        return requestedDelayMs;
    }

    internal static int GetTrackedChatCommandPostActionTailMs(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return 1000;

        if (command.Equals(".taxicheat on", StringComparison.OrdinalIgnoreCase))
            return 2500;

        if (command.StartsWith(".pool spawns ", StringComparison.OrdinalIgnoreCase))
            return 2500;

        if (command.StartsWith(".pool info ", StringComparison.OrdinalIgnoreCase))
            return 2500;

        if (command.StartsWith(".pool update ", StringComparison.OrdinalIgnoreCase))
            return 1500;

        return 1000;
    }

    internal static int GetTrackedChatCommandResponseSettleMs(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return 300;

        if (command.Equals(".taxicheat on", StringComparison.OrdinalIgnoreCase))
            return 1200;

        if (command.StartsWith(".pool spawns ", StringComparison.OrdinalIgnoreCase))
            return 800;

        if (command.StartsWith(".pool info ", StringComparison.OrdinalIgnoreCase))
            return 800;

        if (command.StartsWith(".pool update ", StringComparison.OrdinalIgnoreCase))
            return 400;

        return 300;
    }

    internal static bool ContainsTaxiNodesGrantedMessage(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains("has access to all taxi nodes now", StringComparison.OrdinalIgnoreCase);

    public async Task EnsureTaxiNodesEnabledAsync(string accountName, string label, int maxAttempts = 3)
    {
        await BotSelectSelfAsync(accountName);
        await Task.Delay(300);

        var baseline = await GetSnapshotAsync(accountName);
        var baselineChats = baseline?.RecentChatMessages.ToArray() ?? Array.Empty<string>();
        var baselineErrors = baseline?.RecentErrors.ToArray() ?? Array.Empty<string>();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var trace = await SendGmChatCommandTrackedAsync(
                accountName,
                ".taxicheat on",
                captureResponse: true,
                delayMs: 4500);

            Assert.Equal(ResponseResult.Success, trace.DispatchResult);

            var responses = trace.ChatMessages.Concat(trace.ErrorMessages).ToArray();
            foreach (var response in responses)
                _testOutput?.WriteLine($"[{label}] taxicheat: {response}");

            Assert.DoesNotContain(
                responses,
                response => response.Contains("no such command", StringComparison.OrdinalIgnoreCase)
                    || response.Contains("not available", StringComparison.OrdinalIgnoreCase)
                    || response.Contains("unknown command", StringComparison.OrdinalIgnoreCase));

            if (responses.Any(ContainsTaxiNodesGrantedMessage))
                return;

            var confirmed = await WaitForSnapshotConditionAsync(
                accountName,
                snapshot =>
                {
                    if (snapshot == null)
                        return false;

                    var chatDelta = GetDeltaMessages(baselineChats, snapshot.RecentChatMessages);
                    if (chatDelta.Any(ContainsTaxiNodesGrantedMessage))
                        return true;

                    var errorDelta = GetDeltaMessages(baselineErrors, snapshot.RecentErrors);
                    return errorDelta.Any(ContainsTaxiNodesGrantedMessage);
                },
                TimeSpan.FromSeconds(6),
                pollIntervalMs: 300,
                progressLabel: $"{label} taxicheat-confirm");

            if (confirmed)
                return;

            await RefreshSnapshotsAsync();
            var current = await GetSnapshotAsync(accountName);
            baselineChats = current?.RecentChatMessages.ToArray() ?? baselineChats;
            baselineErrors = current?.RecentErrors.ToArray() ?? baselineErrors;
            _testOutput?.WriteLine($"[{label}] taxicheat confirmation not seen on attempt {attempt}/{maxAttempts}; retrying.");
        }

        var finalSnapshot = await GetSnapshotAsync(accountName);
        var recentChatTail = string.Join(" || ", finalSnapshot?.RecentChatMessages.TakeLast(6) ?? Array.Empty<string>());
        var recentErrorTail = string.Join(" || ", finalSnapshot?.RecentErrors.TakeLast(6) ?? Array.Empty<string>());
        throw new Xunit.Sdk.XunitException(
            $"[{label}] taxi-node grant was not confirmed after {maxAttempts} '.taxicheat on' attempts. " +
            $"Recent chat: {recentChatTail}. Recent errors: {recentErrorTail}.");
    }


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
            // Throttle to avoid flooding the action queue and crashing the bot/StateManager
            if (slot % 4 == 3) await Task.Delay(100);
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
                    if (slot % 4 == 3) await Task.Delay(100);
                }
            }
        }

        // Poll for inventory to clear (bags should be empty after destroy)
        await WaitForSnapshotConditionAsync(accountName,
            s => (s.Player?.BagContents?.Count ?? 99) == 0, TimeSpan.FromSeconds(5));
        _logger.LogInformation("[CLEANUP] Inventory cleared for {Account}.", accountName);
    }

    // ---- Snapshot-based helpers (replace old ObjectManager-direct queries) ----

    /// <summary>
    /// Wait for player position to change (via snapshot polling).
    /// Returns true if position changed by more than 1 unit on X or Y axis.
    /// </summary>


    // ---- Bot-chat GM command helpers (bots are GM accounts, send commands through their own chat) ----

    /// <summary>
    /// Send a GM command through a bot's in-game chat. The bot types the command in chat,
    /// and because the account is GM-level, the server processes it. This is used for
    /// commands like .learn, .additem that need to target the current character (self).
    /// </summary>
    public Task SendGmChatCommandAsync(string accountName, string command)
        => SendGmChatCommandAsync(accountName, command, captureResponse: false, emitOutput: true);

    protected async Task SendSilentGmChatCommandAsync(string accountName, string command)
    {
        TrackChatCommand(accountName, command);

        if (_stateManagerClient == null)
            return;

        var action = new ActionMessage
        {
            ActionType = ActionType.SendChat,
            Parameters = { new RequestParameter { StringParam = command } }
        };

        _ = await _stateManagerClient.ForwardActionAsync(accountName, action);
    }

    /// <summary>
    /// Send a GM command through bot chat and optionally capture the bot's immediate chat/error response
    /// from the next snapshot poll.
    /// </summary>


    /// <summary>
    /// Send a GM command through bot chat and optionally capture the bot's immediate chat/error response
    /// from the next snapshot poll.
    /// </summary>
    public Task SendGmChatCommandAsync(string accountName, string command, bool captureResponse)
        => SendGmChatCommandAsync(accountName, command, captureResponse, emitOutput: true);

    private async Task SendGmChatCommandAsync(string accountName, string command, bool captureResponse, bool emitOutput)
    {
        var trace = await SendGmChatCommandTrackedAsync(accountName, command, captureResponse, emitOutput: emitOutput);
        // BT-VERIFY-001: Surface dead-state guard blocks clearly in test output.
        // Previously callers silently continued with half-configured state.
        if (trace.DispatchResult != ResponseResult.Success &&
            trace.ErrorMessages.Any(e => e.Contains("dead-state guard", StringComparison.OrdinalIgnoreCase)) &&
            emitOutput)
        {
            _testOutput?.WriteLine($"[DEAD-GUARD] [{accountName}] Command '{command}' blocked — bot is dead/ghost. " +
                "This usually means EnsureCleanSlateAsync failed or a prior test leaked dead state.");
        }
    }


    public async Task<GmChatCommandTrace> SendGmChatCommandTrackedAsync(
        string accountName,
        string command,
        bool captureResponse = true,
        int delayMs = 2000,
        bool allowWhenDead = false,
        bool emitOutput = true)
    {
        var attemptCount = TrackChatCommand(accountName, command);
        if (emitOutput)
            LogDuplicateCommand("CHAT", command, attemptCount, accountName);

        if (_stateManagerClient != null)
        {
            var stateSnapshot = await GetSnapshotAsync(accountName);
            if (!allowWhenDead && IsDeadOrGhostState(stateSnapshot, out var deadReason))
            {
                var guardMessage = $"[CMD-GUARD] [{accountName}] Skipping '{command}' because sender is dead/ghost ({deadReason}).";
                if (emitOutput)
                {
                    _logger.LogInformation("{Message}", guardMessage);
                    _testOutput?.WriteLine(guardMessage);
                }

                return new GmChatCommandTrace(
                    attemptCount,
                    ResponseResult.Failure,
                    Array.Empty<string>(),
                    new[] { $"blocked by dead-state guard: {deadReason}" });
            }
        }

        var commandMessage = $"[CMD-SEND] [{accountName}] '{command}'";
        if (emitOutput)
        {
            _logger.LogInformation("{Message}", commandMessage);
            _testOutput?.WriteLine(commandMessage);
        }

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

        // P4.5.2: stamp a test-owned correlation id so we can correlate the
        // CommandAckEvent in RecentCommandAcks back to this exact dispatch.
        var correlationId = $"test:{accountName}:{Interlocked.Increment(ref _testCorrelationSequence).ToString(CultureInfo.InvariantCulture)}";
        var action = new ActionMessage
        {
            ActionType = ActionType.SendChat,
            CorrelationId = correlationId,
            Parameters = { new RequestParameter { StringParam = command } }
        };

        var dispatchResult = await SendActionAsync(accountName, action, emitOutput);
        _logger.LogInformation("[ACTION] Sent {Type} to {Account} → {Result} [{Corr}]", action.ActionType, accountName, dispatchResult, correlationId);
        var chats = new List<string>();
        var errors = new List<string>();
        CommandAckEvent.Types.AckStatus? ackStatus = null;
        string? ackFailureReason = null;
        if (captureResponse && _stateManagerClient != null)
        {
            var pollDeadlineUtc = DateTime.UtcNow.AddMilliseconds(GetTrackedChatCommandDelayMs(command, delayMs));
            var actionSeen = false;
            var responseSeen = false;

            while (DateTime.UtcNow < pollDeadlineUtc)
            {
                await Task.Delay(200);
                await RefreshSnapshotsAsync();

                var responseSnapshot = await GetSnapshotAsync(accountName);
                if (responseSnapshot == null)
                    continue;

                if (!actionSeen && MatchesExecutedSendChatCommand(responseSnapshot, command))
                {
                    actionSeen = true;
                    var postActionDeadlineUtc = DateTime.UtcNow.AddMilliseconds(GetTrackedChatCommandPostActionTailMs(command));
                    if (postActionDeadlineUtc > pollDeadlineUtc)
                        pollDeadlineUtc = postActionDeadlineUtc;
                }

                chats = GetDeltaMessages(baselineChats, responseSnapshot.RecentChatMessages);
                errors = GetDeltaMessages(baselineErrors, responseSnapshot.RecentErrors);

                var ackMatch = FindLatestMatchingAck(responseSnapshot, correlationId);
                if (ackMatch != null)
                {
                    ackStatus = ackMatch.Status;
                    ackFailureReason = string.IsNullOrEmpty(ackMatch.FailureReason) ? null : ackMatch.FailureReason;
                }

                if (actionSeen && !responseSeen && (chats.Count > 0 || errors.Count > 0))
                {
                    responseSeen = true;
                    pollDeadlineUtc = DateTime.UtcNow.AddMilliseconds(GetTrackedChatCommandResponseSettleMs(command));
                }
            }

            LogChatCommandResponses(accountName, command, chats, errors);
        }

        return new GmChatCommandTrace(attemptCount, dispatchResult, chats, errors, correlationId, ackStatus, ackFailureReason);
    }

    private static CommandAckEvent? FindLatestMatchingAck(WoWActivitySnapshot snapshot, string correlationId)
    {
        CommandAckEvent? pendingMatch = null;
        for (var i = snapshot.RecentCommandAcks.Count - 1; i >= 0; i--)
        {
            var ack = snapshot.RecentCommandAcks[i];
            if (!string.Equals(ack.CorrelationId, correlationId, StringComparison.Ordinal))
                continue;
            if (ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                return ack;
            pendingMatch ??= ack;
        }
        return pendingMatch;
    }


    /// <summary>
    /// Send a chat command and block until the server actually executes it (not just
    /// queues it). Stamps a unique correlation id on the ActionMessage and polls the
    /// account's snapshot until either the <c>previousAction</c> echoes that correlation,
    /// or a <see cref="CommandAckEvent"/> with that correlation appears in
    /// <c>recent_command_acks</c>. Exits as soon as either signal fires.
    ///
    /// This is the synchronous server-ACK primitive used by multi-step setup helpers
    /// (e.g. pool rotate + respawn) that issue many same-string chat commands in a row
    /// — <see cref="SendGmChatCommandAsync"/>'s existing tracked dispatcher returns on
    /// queue-ack, which lets the caller pile commands into the bot's outbound queue
    /// faster than the bot actually sends them to the world server, and
    /// <c>MatchesExecutedSendChatCommand</c>'s command-string match can't tell
    /// same-string sends apart. Correlation id avoids both problems.
    /// </summary>
    /// <returns>True if the server echoed the correlation within the timeout; false
    /// on timeout. Terminal ack failure returns false as well.</returns>
    public async Task<bool> SendGmChatCommandAndAwaitServerAckAsync(
        string accountName,
        string command,
        int timeoutMs = 6000,
        int pollIntervalMs = 100)
    {
        if (_stateManagerClient == null)
            return false;

        var correlationId = $"shodan-sync:{accountName}:{Interlocked.Increment(ref _testCorrelationSequence).ToString(CultureInfo.InvariantCulture)}";
        var action = new ActionMessage
        {
            ActionType = ActionType.SendChat,
            CorrelationId = correlationId,
            Parameters = { new RequestParameter { StringParam = command } }
        };

        var dispatchResult = await SendActionAsync(accountName, action, emitOutput: false);
        if (dispatchResult != ResponseResult.Success)
            return false;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollIntervalMs);
            await RefreshSnapshotsAsync();

            var snap = await GetSnapshotAsync(accountName);
            if (snap == null)
                continue;

            // Preferred signal: a terminal CommandAckEvent for our correlation id.
            var ack = FindLatestMatchingAck(snap, correlationId);
            if (ack != null && ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                return ack.Status == CommandAckEvent.Types.AckStatus.Success;

            // Fallback signal: previousAction reports our correlation — the bot's
            // runtime dispatched this specific action to the server.
            if (snap.PreviousAction != null
                && string.Equals(snap.PreviousAction.CorrelationId, correlationId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Make the bot select itself (CMSG_SET_SELECTION → own GUID).
    /// Required before GM commands like .setskill that need a selected target.
    /// This is an internal bot command — nothing is sent to server chat.
    /// </summary>
    public Task BotSelectSelfAsync(string accountName)
        => SendGmChatCommandAsync(accountName, ".targetself");

    /// <summary>
    /// Learn a spell for a specific bot. Automatically selects self first (required by .learn).
    /// Polls snapshot to verify spell appeared in SpellList (BT-VERIFY-004).
    /// </summary>
    public async Task BotLearnSpellAsync(string accountName, uint spellId)
    {
        await BotSelectSelfAsync(accountName);
        await Task.Delay(300);
        await SendGmChatCommandAsync(accountName, $".learn {spellId}");

        // BT-VERIFY-004: Verify spell appeared in snapshot
        var verified = await WaitForSnapshotConditionAsync(accountName,
            s => s.Player?.SpellList?.Contains(spellId) == true,
            TimeSpan.FromSeconds(3), pollIntervalMs: 300);
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{accountName}] Spell {spellId} not confirmed in SpellList after .learn");
    }

    /// <summary>Unlearn a spell for a specific bot. Automatically selects self first (required by .unlearn).</summary>


    /// <summary>Unlearn a spell for a specific bot. Automatically selects self first (required by .unlearn).</summary>
    public async Task BotUnlearnSpellAsync(string accountName, uint spellId)
    {
        await BotSelectSelfAsync(accountName);
        await Task.Delay(300);
        await SendGmChatCommandAsync(accountName, $".unlearn {spellId}");
    }

    /// <summary>
    /// Set a skill value for a bot. Automatically selects self first (required by .setskill).
    /// </summary>


    /// <summary>
    /// Set a skill value for a bot. Automatically selects self first (required by .setskill).
    /// </summary>
    public async Task BotSetSkillAsync(string accountName, uint skillId, int currentValue, int maxValue)
    {
        await BotSelectSelfAsync(accountName);
        await Task.Delay(300);
        await SendGmChatCommandAsync(accountName, $".setskill {skillId} {currentValue} {maxValue}");

        var verified = await WaitForSnapshotConditionAsync(accountName,
            snapshot => snapshot.Player?.SkillInfo != null
                && snapshot.Player.SkillInfo.TryGetValue(skillId, out var skillValue)
                && skillValue >= currentValue,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300);
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{accountName}] Skill {skillId} not confirmed at {currentValue}/{maxValue} after .setskill");
    }

    /// <summary>
    /// Add an item to a bot's own bags by having it type .additem in chat.
    /// Polls snapshot to verify item appeared in BagContents (BT-VERIFY-003).
    /// </summary>
    public async Task BotAddItemAsync(string accountName, uint itemId, int count = 1)
    {
        await SendGmChatCommandAsync(accountName, $".additem {itemId} {count}");

        // BT-VERIFY-003: Verify item appeared in bag snapshot
        var verified = await WaitForSnapshotConditionAsync(accountName,
            s => s.Player?.BagContents?.Values.Any(v => v == itemId) == true,
            TimeSpan.FromSeconds(3), pollIntervalMs: 300);
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{accountName}] Item {itemId} not confirmed in bags after .additem");
    }

    /// <summary>
    /// Teleport a bot by having it type .go xyz in chat (self-teleport).
    /// Tries map-form syntax first and falls back to map-less syntax when rejected.
    /// Uses allowWhenDead: true because .go xyz is a GM command that works in any state.
    /// If the teleport silently fails (position didn't change), retries once.
    /// </summary>


    /// <summary>
    /// Teleport a bot by having it type .go xyz in chat (self-teleport).
    /// Tries map-form syntax first and falls back to map-less syntax when rejected.
    /// Uses allowWhenDead: true because .go xyz is a GM command that works in any state.
    /// If the teleport silently fails (position didn't change), retries once.
    /// </summary>
    public async Task BotTeleportAsync(string accountName, int mapId, float x, float y, float z)
    {
        var xText = x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var yText = y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var zText = z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        var commandWithMap = $".go xyz {xText} {yText} {zText} {mapId}";

        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var withMapTrace = await SendGmChatCommandTrackedAsync(
                accountName,
                commandWithMap,
                captureResponse: true,
                delayMs: 1000,
                allowWhenDead: true);

            // Check if the dispatch itself failed (e.g. client not connected)
            if (withMapTrace.DispatchResult != ResponseResult.Success)
            {
                _logger.LogWarning("[TELEPORT] Dispatch failed for {Account}: {Result}", accountName, withMapTrace.DispatchResult);
                if (attempt < maxAttempts - 1) continue;
                return;
            }

            var rejectedWithMap =
                withMapTrace.ChatMessages.Any(ContainsCommandRejection)
                || withMapTrace.ErrorMessages.Any(ContainsCommandRejection)
                || withMapTrace.ChatMessages.Any(m => m.Contains("subcommand", StringComparison.OrdinalIgnoreCase));
            if (rejectedWithMap)
            {
                var commandWithoutMap = $".go xyz {xText} {yText} {zText}";
                _logger.LogInformation("[TELEPORT] Retrying map-less syntax: {Command}", commandWithoutMap);
                await SendGmChatCommandTrackedAsync(
                    accountName,
                    commandWithoutMap,
                    captureResponse: true,
                    delayMs: 1000,
                    allowWhenDead: true);
                return;
            }

            // Poll for position update — the snapshot may lag behind the teleport ACK
            if (_stateManagerClient != null)
            {
                var settled = false;
                for (int poll = 0; poll < 10; poll++)
                {
                    await RefreshSnapshotsAsync();
                    var snap = await GetSnapshotAsync(accountName);
                    var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                    if (pos != null)
                    {
                        var dx = pos.X - x;
                        var dy = pos.Y - y;
                        var dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist <= 80f)
                        {
                            settled = true;
                            break;
                        }
                    }
                    await Task.Delay(500);
                }
                if (!settled && attempt < maxAttempts - 1)
                {
                    _logger.LogWarning("[TELEPORT] Position check: {Account} not near target after 5s — retrying (attempt {Attempt}/{Max})",
                        accountName, attempt + 1, maxAttempts);
                    continue;
                }
                if (!settled)
                {
                    _logger.LogWarning("[TELEPORT] Position check: {Account} FAILED to reach target ({X},{Y},{Z}) after {Max} attempts",
                        accountName, x, y, z, maxAttempts);
                }
            }

            return;
        }
    }

    /// <summary>
    /// Wait for a bot to settle at the expected position after a teleport.
    /// Polls snapshots until XY is within 50y of target and Z is stable (2 consecutive samples within 1y).
    /// </summary>


    /// <summary>Teleport a bot to a named location via SOAP (.tele name charName location).</summary>
    public Task BotTeleportToNamedAsync(string accountName, string characterName, string locationName)
        => ExecuteGMCommandAsync($".tele name {characterName} {locationName}");

    /// <summary>Teleport a bot to a named location by having it type .tele in chat (self-teleport).</summary>
    public Task BotTeleportToNamedViaChatAsync(string accountName, string locationName)
        => SendGmChatCommandAsync(accountName, $".tele {locationName}");

    // ---- MySQL direct helpers (bypass disabled GM commands in some repacks) ----


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
}
