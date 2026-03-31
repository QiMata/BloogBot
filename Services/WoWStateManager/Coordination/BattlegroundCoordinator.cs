using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Settings;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates N bots for battleground entry. Assumes test fixture has already:
///   - Leveled all bots to BG minimum (10 for WSG)
///   - Teleported Horde bots to Orgrimmar BG master area
///   - Teleported Alliance bots to their faction's BG master area
///   - Turned .gm off on all bots
///
/// Pipeline:
///   WaitingForBots → QueueForBattleground → WaitForInvite → AcceptInvite → InBattleground
///
/// Unlike DungeoneeringCoordinator, this does NOT handle leveling, gear, or teleport.
/// Those are fixture responsibilities. This coordinator only handles the BG lifecycle
/// from "bots are at BG masters" to "bots are in the BG".
/// </summary>
public class BattlegroundCoordinator
{
    public enum CoordState
    {
        WaitingForBots,
        QueueForBattleground,
        WaitForInvite,
        AcceptInvite,
        WaitForEntry,
        InBattleground,
    }

    private readonly string _leaderAccount;
    private readonly List<string> _memberAccounts;
    private readonly uint _bgTypeId;     // 2=WSG, 3=AB, 1=AV
    private readonly uint _bgMapId;      // 489=WSG, 529=AB, 30=AV
    private readonly ILogger _logger;

    private CoordState _state = CoordState.WaitingForBots;
    public CoordState State => _state;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _tickCount;

    // Tracking
    private readonly ConcurrentDictionary<string, int> _queueSent = new();
    private readonly ConcurrentDictionary<string, int> _acceptSent = new();

    public BattlegroundCoordinator(
        string leaderAccount,
        IEnumerable<string> allAccounts,
        uint bgTypeId,
        uint bgMapId,
        ILogger logger)
    {
        _leaderAccount = leaderAccount;
        _memberAccounts = allAccounts
            .Where(a => !a.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _bgTypeId = bgTypeId;
        _bgMapId = bgMapId;
        _logger = logger;

        _logger.LogInformation("BG_COORD: Initialized — Leader='{Leader}', Members={Count}, BG={BgType}, Map={Map}",
            leaderAccount, _memberAccounts.Count, bgTypeId, bgMapId);
    }

    public ActionMessage? GetAction(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        _tickCount++;

        return _state switch
        {
            CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
            CoordState.QueueForBattleground => HandleQueueForBattleground(requestingAccount, snapshots),
            CoordState.WaitForInvite => HandleWaitForInvite(requestingAccount, snapshots),
            CoordState.AcceptInvite => HandleAcceptInvite(requestingAccount, snapshots),
            CoordState.WaitForEntry => HandleWaitForEntry(requestingAccount, snapshots),
            CoordState.InBattleground => null, // Idle — bots do their own thing
            _ => null,
        };
    }

    private void TransitionTo(CoordState newState)
    {
        _logger.LogWarning("BG_COORD: {Old} → {New}", _state, newState);
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
    }

    private ActionMessage? HandleWaitingForBots(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Wait for ALL bots to be in-world with valid ObjectManager AND level >= 10.
        // Fixture handles leveling/teleporting — coordinator must wait for that to complete.
        // Don't transition early — if we queue before all bots are ready, some will
        // have their actions dropped by BotRunnerService (HasEnteredWorld=false).
        var ready = snapshots.Values.Count(s =>
            s.IsObjectManagerValid
            && (s.Player?.Unit?.GameObject?.Level ?? 0) >= 10);
        var total = _memberAccounts.Count + 1;

        if (ready < total)
        {
            if (_tickCount % 20 == 1)
                _logger.LogInformation("BG_COORD: Waiting for bots (level>=10): {Ready}/{Total}", ready, total);

            // Accept N-1 bots (FG bot may crash, leaving 19/20)
            // Also hard timeout after 60s — proceed with at least 4 bots per faction (min for BG)
            var closeEnough = ready >= total - 1;
            var timedOut = (DateTime.UtcNow - _stateEnteredAt).TotalSeconds > 60 && ready >= 4;

            if (!closeEnough && !timedOut)
                return null;

            if (timedOut && !closeEnough)
                _logger.LogWarning("BG_COORD: Timeout waiting for all bots. Proceeding with {Ready}/{Total}", ready, total);
        }

        _logger.LogInformation("BG_COORD: {Ready}/{Total} bots ready. Starting BG queue.", ready, total);
        TransitionTo(CoordState.QueueForBattleground);
        return null;
    }

    private ActionMessage? HandleQueueForBattleground(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Already sent to this bot — skip
        if (_queueSent.ContainsKey(requestingAccount))
            return null;

        // Only send JoinBattleground to bots that are actually world-ready.
        // Bots still loading (HasEnteredWorld=false, Guid=0) will have their actions dropped
        // by BotRunnerService — so we must NOT mark them as queued until they're ready.
        if (snapshots.TryGetValue(requestingAccount, out var snap)
            && snap.IsObjectManagerValid
            && (snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0) != _bgMapId) // not already in BG
        {
            _queueSent.TryAdd(requestingAccount, 0);

            _logger.LogInformation("BG_COORD: Sending JOIN_BATTLEGROUND to '{Account}' (bgType={BgType})",
                requestingAccount, _bgTypeId);

            var action = new ActionMessage { ActionType = ActionType.JoinBattleground };
            action.Parameters.Add(new RequestParameter { IntParam = (int)_bgTypeId });
            action.Parameters.Add(new RequestParameter { IntParam = (int)_bgMapId });

            return action;
        }

        // Bot not ready yet — log periodically
        if (_tickCount % 20 == 1)
            _logger.LogInformation("BG_COORD: Waiting for '{Account}' to be world-ready before queueing", requestingAccount);

        // Check if all bots have been queued
        var allQueued = _queueSent.ContainsKey(_leaderAccount)
            && _memberAccounts.All(m => _queueSent.ContainsKey(m));

        if (allQueued)
        {
            _logger.LogInformation("BG_COORD: All {Count} bots queued. Waiting for invite.", _queueSent.Count);
            TransitionTo(CoordState.WaitForInvite);
        }

        return null;
    }

    private ActionMessage? HandleWaitForInvite(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // The BattlegroundQueueTask on each bot handles the actual queue/invite flow.
        // We just wait here. The task will transition to AcceptInvite state internally.
        // Check if any bot has entered the BG map — that means invites are flowing.

        var onBgMap = snapshots.Values.Count(s =>
            (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == _bgMapId);

        if (onBgMap > 0)
        {
            _logger.LogInformation("BG_COORD: {Count} bot(s) on BG map! BG is active.", onBgMap);
            TransitionTo(CoordState.InBattleground);
            return null;
        }

        // Timeout after 5 minutes
        if ((DateTime.UtcNow - _stateEnteredAt).TotalSeconds > 300)
        {
            _logger.LogWarning("BG_COORD: Invite timeout after 5 minutes");
            TransitionTo(CoordState.InBattleground); // Give up waiting
        }

        return null;
    }

    private ActionMessage? HandleAcceptInvite(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Send AcceptBattleground to each bot
        if (!_acceptSent.TryAdd(requestingAccount, 0))
            return null;

        _logger.LogInformation("BG_COORD: Sending ACCEPT_BATTLEGROUND to '{Account}'", requestingAccount);

        return new ActionMessage { ActionType = ActionType.AcceptBattleground };
    }

    private ActionMessage? HandleWaitForEntry(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var onBgMap = snapshots.Values.Count(s =>
            (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == _bgMapId);

        if (onBgMap >= 2)
        {
            _logger.LogInformation("BG_COORD: {Count} bots entered BG map {Map}", onBgMap, _bgMapId);
            TransitionTo(CoordState.InBattleground);
        }

        return null;
    }
}
