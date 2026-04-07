using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BotRunner.Travel;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates N bots for battleground entry after the fixture has completed
/// level/teleport prep and staged both factions at their battlemaster areas.
/// </summary>
public class BattlegroundCoordinator
{
    public readonly record struct StagingTarget(uint MapId, float X, float Y, float Z);

    private const float QueueReadyRadius = 80f;

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
    private readonly uint _bgTypeId;
    private readonly uint _bgMapId;
    private readonly int _minimumLevel;
    private readonly IReadOnlyDictionary<string, StagingTarget> _stagingTargets;
    private readonly ILogger _logger;

    private CoordState _state = CoordState.WaitingForBots;
    public CoordState State => _state;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _tickCount;

    private readonly ConcurrentDictionary<string, int> _queueSent = new();
    private readonly ConcurrentDictionary<string, int> _acceptSent = new();

    public BattlegroundCoordinator(
        string leaderAccount,
        IEnumerable<string> allAccounts,
        uint bgTypeId,
        uint bgMapId,
        ILogger logger,
        IReadOnlyDictionary<string, StagingTarget>? stagingTargets = null)
    {
        _leaderAccount = leaderAccount;
        _memberAccounts = allAccounts
            .Where(account => !account.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _bgTypeId = bgTypeId;
        _bgMapId = bgMapId;
        _minimumLevel = BattlemasterData.GetMinimumLevel(bgTypeId);
        _logger = logger;
        _stagingTargets = stagingTargets
            ?? new Dictionary<string, StagingTarget>(StringComparer.OrdinalIgnoreCase);

        _logger.LogWarning(
            "BG_COORD: Initialized - Leader='{Leader}', Members={Count}, BG={BgType}, Map={Map}, StagingTargets={Staging}",
            leaderAccount,
            _memberAccounts.Count,
            bgTypeId,
            bgMapId,
            _stagingTargets.Count);
    }

    public ActionMessage? GetAction(
        string requestingAccount,
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
            CoordState.InBattleground => null,
            _ => null,
        };
    }

    private void TransitionTo(CoordState newState)
    {
        _logger.LogWarning("BG_COORD: {Old} -> {New}", _state, newState);
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
    }

    private ActionMessage? HandleWaitingForBots(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var total = _memberAccounts.Count + 1;
        var ready = 0;
        var staged = 0;
        var pendingAccounts = new List<string>();

        foreach (var account in _memberAccounts.Append(_leaderAccount))
        {
            if (!snapshots.TryGetValue(account, out var snapshot))
            {
                pendingAccounts.Add($"{account}(no snapshot)");
                continue;
            }

            if (!IsWorldReady(snapshot, _minimumLevel))
            {
                pendingAccounts.Add($"{account}(world not ready)");
                continue;
            }

            ready++;
            if (IsQueuedAtStagingTarget(account, snapshot))
            {
                staged++;
                continue;
            }

            pendingAccounts.Add(DescribeUnstagedAccount(account, snapshot));
        }

        if (ready < total || staged < total)
        {
            if (_tickCount % 20 == 1)
            {
                _logger.LogWarning(
                    "BG_COORD: Waiting for bots (ready={Ready}/{Total}, staged={Staged}/{Total}). Pending: {Pending}",
                    ready,
                    total,
                    staged,
                    total,
                    string.Join(", ", pendingAccounts.Take(10)));
            }

            return null;
        }

        _logger.LogWarning(
            "BG_COORD: {Ready}/{Total} bots staged at battlemaster areas. Starting BG queue.",
            ready,
            total);
        TransitionTo(CoordState.QueueForBattleground);
        return null;
    }

    private ActionMessage? HandleQueueForBattleground(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        ActionMessage? action = null;

        if (!_queueSent.ContainsKey(requestingAccount)
            && snapshots.TryGetValue(requestingAccount, out var snapshot)
            && snapshot.IsObjectManagerValid
            && (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0) != _bgMapId)
        {
            if (_queueSent.TryAdd(requestingAccount, 0))
            {
                _logger.LogInformation(
                    "BG_COORD: Sending JOIN_BATTLEGROUND to '{Account}' (bgType={BgType})",
                    requestingAccount,
                    _bgTypeId);

                action = new ActionMessage { ActionType = ActionType.JoinBattleground };
                action.Parameters.Add(new RequestParameter { IntParam = (int)_bgTypeId });
                action.Parameters.Add(new RequestParameter { IntParam = (int)_bgMapId });
            }
        }

        if (action == null
            && !_queueSent.ContainsKey(requestingAccount)
            && _tickCount % 20 == 1)
        {
            _logger.LogInformation(
                "BG_COORD: Waiting for '{Account}' to be world-ready before queueing",
                requestingAccount);
        }

        var allQueued = _queueSent.ContainsKey(_leaderAccount)
            && _memberAccounts.All(member => _queueSent.ContainsKey(member));

        if (allQueued)
        {
            _logger.LogInformation("BG_COORD: All {Count} bots queued. Waiting for invite.", _queueSent.Count);
            TransitionTo(CoordState.WaitForInvite);
        }

        return action;
    }

    private ActionMessage? HandleWaitForInvite(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var onBgMap = snapshots.Values.Count(snapshot =>
            (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == _bgMapId);

        var total = _memberAccounts.Count + 1;
        if (onBgMap >= total)
        {
            _logger.LogInformation("BG_COORD: {Count}/{Total} bots on BG map. BG is active.", onBgMap, total);
            TransitionTo(CoordState.InBattleground);
        }

        return null;
    }

    private ActionMessage? HandleAcceptInvite(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (!_acceptSent.TryAdd(requestingAccount, 0))
            return null;

        _logger.LogInformation("BG_COORD: Sending ACCEPT_BATTLEGROUND to '{Account}'", requestingAccount);
        return new ActionMessage { ActionType = ActionType.AcceptBattleground };
    }

    private ActionMessage? HandleWaitForEntry(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var onBgMap = snapshots.Values.Count(snapshot =>
            (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == _bgMapId);

        var total = _memberAccounts.Count + 1;
        if (onBgMap >= total)
        {
            _logger.LogInformation(
                "BG_COORD: {Count}/{Total} bots entered BG map {Map}",
                onBgMap,
                total,
                _bgMapId);
            TransitionTo(CoordState.InBattleground);
        }

        return null;
    }

    private string DescribeUnstagedAccount(string accountName, WoWActivitySnapshot snapshot)
    {
        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;

        if (!_stagingTargets.TryGetValue(accountName, out var target))
            return $"{accountName}(map={mapId}, no staging target)";

        if (position == null)
            return $"{accountName}(map={mapId}, pos=unknown)";

        var distance = Distance2D(position.X, position.Y, target.X, target.Y);
        return $"{accountName}(map={mapId}, dist={distance:F0})";
    }

    private bool IsQueuedAtStagingTarget(string accountName, WoWActivitySnapshot snapshot)
    {
        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId == _bgMapId)
            return true;

        if (!_stagingTargets.TryGetValue(accountName, out var target))
            return true;

        if (mapId != target.MapId)
            return false;

        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (position == null)
            return false;

        return Distance2D(position.X, position.Y, target.X, target.Y) <= QueueReadyRadius;
    }

    private static bool IsWorldReady(WoWActivitySnapshot snapshot, int minimumLevel)
    {
        return snapshot.IsObjectManagerValid
            && (snapshot.Player?.Unit?.GameObject?.Level ?? 0) >= minimumLevel;
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
