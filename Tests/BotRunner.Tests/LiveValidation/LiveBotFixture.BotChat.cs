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

        // Wait for inventory updates to propagate
        await Task.Delay(2000);
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
        => SendGmChatCommandAsync(accountName, command, captureResponse: false);

    /// <summary>
    /// Send a GM command through bot chat and optionally capture the bot's immediate chat/error response
    /// from the next snapshot poll.
    /// </summary>


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
        // CRITICAL: .gm on / .gm off via chat disconnects the BG headless client.
        // MaNGOS responds with a packet that kills the connection, breaking all
        // subsequent commands and position tracking. Skip for BG bots silently.
        if (accountName == BgAccountName &&
            command.StartsWith(".gm ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[CMD-SKIP] [{Account}] Skipping '{Command}' — .gm commands disconnect BG headless client",
                accountName, command);
            return new GmChatCommandTrace(0, ResponseResult.Success, Array.Empty<string>(), Array.Empty<string>());
        }

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


    /// <summary>
    /// Make the bot select itself (CMSG_SET_SELECTION → own GUID).
    /// Required before GM commands like .setskill that need a selected target.
    /// This is an internal bot command — nothing is sent to server chat.
    /// </summary>
    public Task BotSelectSelfAsync(string accountName)
        => SendGmChatCommandAsync(accountName, ".targetself");

    /// <summary>Learn a spell for a specific bot. Automatically selects self first (required by .learn).</summary>


    /// <summary>Learn a spell for a specific bot. Automatically selects self first (required by .learn).</summary>
    public async Task BotLearnSpellAsync(string accountName, uint spellId)
    {
        await BotSelectSelfAsync(accountName);
        await Task.Delay(300);
        await SendGmChatCommandAsync(accountName, $".learn {spellId}");
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
    }

    /// <summary>Add an item to a bot's own bags by having it type .additem in chat.</summary>


    /// <summary>Add an item to a bot's own bags by having it type .additem in chat.</summary>
    public Task BotAddItemAsync(string accountName, uint itemId, int count = 1)
        => SendGmChatCommandAsync(accountName, $".additem {itemId} {count}");

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

        for (int attempt = 0; attempt < 2; attempt++)
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
                if (attempt == 0) continue;
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
                if (!settled && attempt == 0)
                {
                    _logger.LogWarning("[TELEPORT] Position check: {Account} not near target after 5s — retrying", accountName);
                    continue;
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
