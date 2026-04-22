using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BotRunner.Travel;
using WoWStateManager.Settings;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates N bots for battleground entry after the fixture has completed
/// level/teleport prep and staged both factions at their battlemaster areas.
/// </summary>
public class BattlegroundCoordinator
{
    public readonly record struct StagingTarget(uint MapId, float X, float Y, float Z);

    private const float QueueReadyRadius = 80f;
    private const float QueueRestageStopDistance = 10f;
    private static readonly TimeSpan InviteAcceptRetryStartDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan InviteJoinRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InviteAcceptRetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QueueRestageRetryInterval = TimeSpan.FromSeconds(8);

    public enum CoordState
    {
        WaitingForBots,
        ApplyingLoadouts,
        WaitingForRaidFormation,
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
    private readonly IReadOnlyDictionary<string, string> _desiredPartyLeaderAccounts;
    private readonly IReadOnlyDictionary<string, LoadoutSpecSettings> _loadoutSpecs;
    private readonly ILogger _logger;

    private CoordState _state = CoordState.WaitingForBots;
    public CoordState State => _state;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _tickCount;

    private readonly ConcurrentDictionary<string, int> _queueSent = new();
    private readonly ConcurrentDictionary<string, int> _acceptSent = new();
    private readonly ConcurrentDictionary<string, DateTime> _restageSentAt = new();
    private readonly ConcurrentDictionary<string, DateTime> _joinRetrySentAt = new();
    private readonly ConcurrentDictionary<string, DateTime> _acceptRetrySentAt = new();

    // P3.4 loadout orchestration.
    private readonly ConcurrentDictionary<string, byte> _loadoutSent = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _loadoutReady = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _loadoutFailed = new(StringComparer.OrdinalIgnoreCase);

    // P5.1 loadout ACK correlation — populated when ApplyLoadout is dispatched so
    // RecordLoadoutProgressFromSnapshots can short-circuit on terminal ACKs even
    // when snapshot.LoadoutStatus is slow to flip (e.g. pre-task failures like
    // "loadout_task_already_active").
    private readonly ConcurrentDictionary<string, string> _loadoutCorrelationIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Accounts whose loadout step reported LoadoutFailed. Downstream states
    /// ignore these bots so one broken bot doesn't block the rest from queueing.
    /// </summary>
    public IReadOnlyCollection<string> ExcludedAccounts => _loadoutFailed.Keys.ToArray();

    public BattlegroundCoordinator(
        string leaderAccount,
        IEnumerable<string> allAccounts,
        uint bgTypeId,
        uint bgMapId,
        ILogger logger,
        IReadOnlyDictionary<string, StagingTarget>? stagingTargets = null,
        IReadOnlyDictionary<string, string>? desiredPartyLeaderAccounts = null,
        IReadOnlyDictionary<string, LoadoutSpecSettings>? loadoutSpecs = null)
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
        _desiredPartyLeaderAccounts = desiredPartyLeaderAccounts != null
            ? new Dictionary<string, string>(desiredPartyLeaderAccounts, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _loadoutSpecs = loadoutSpecs != null
            ? new Dictionary<string, LoadoutSpecSettings>(loadoutSpecs, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, LoadoutSpecSettings>(StringComparer.OrdinalIgnoreCase);

        // Accounts without a configured spec are trivially "ready" — no
        // ApplyLoadout action will ever be dispatched to them, so initialise
        // their progress bit up-front to keep ApplyingLoadouts from stalling.
        foreach (var account in AllAccountNames())
        {
            if (!_loadoutSpecs.ContainsKey(account))
                _loadoutReady[account] = 1;
        }

        _logger.LogWarning(
            "BG_COORD: Initialized - Leader='{Leader}', Members={Count}, BG={BgType}, Map={Map}, StagingTargets={Staging}, DesiredPartyAccounts={DesiredPartyAccounts}, Loadouts={Loadouts}",
            leaderAccount,
            _memberAccounts.Count,
            bgTypeId,
            bgMapId,
            _stagingTargets.Count,
            _desiredPartyLeaderAccounts.Count,
            _loadoutSpecs.Count);
    }

    public bool RequiresFactionGroupQueue => _bgTypeId != (uint)BattlemasterData.BattlegroundType.AlteracValley
        && _desiredPartyLeaderAccounts.Count > 0;

    public ActionMessage? GetAction(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        _tickCount++;

        // Chain through purely-internal orchestration states (ApplyingLoadouts,
        // WaitingForRaidFormation) within a single call so callers don't need
        // extra null polls between hidden phases. Stops at the first handler
        // that emits an action, stays in the same state, or transitions into
        // an externally-observable state (e.g. QueueForBattleground) — that
        // preserves the one-null-call-per-external-transition test contract.
        const int MaxChainedTransitions = 4;
        ActionMessage? action = null;
        for (int hop = 0; hop < MaxChainedTransitions; hop++)
        {
            var before = _state;
            action = _state switch
            {
                CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
                CoordState.ApplyingLoadouts => HandleApplyingLoadouts(requestingAccount, snapshots),
                CoordState.WaitingForRaidFormation => HandleWaitingForRaidFormation(requestingAccount, snapshots),
                CoordState.QueueForBattleground => HandleQueueForBattleground(requestingAccount, snapshots),
                CoordState.WaitForInvite => HandleWaitForInvite(requestingAccount, snapshots),
                CoordState.AcceptInvite => HandleAcceptInvite(requestingAccount, snapshots),
                CoordState.WaitForEntry => HandleWaitForEntry(requestingAccount, snapshots),
                CoordState.InBattleground => null,
                _ => null,
            };

            if (action != null || _state == before || !IsInternalOrchestrationState(_state))
                return action;
        }
        return action;
    }

    private static bool IsInternalOrchestrationState(CoordState state)
        => state == CoordState.ApplyingLoadouts
            || state == CoordState.WaitingForRaidFormation;

    private void TransitionTo(CoordState newState)
    {
        _logger.LogWarning("BG_COORD: {Old} -> {New}", _state, newState);
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
        if (newState == CoordState.WaitForInvite)
        {
            _restageSentAt.Clear();
            _joinRetrySentAt.Clear();
            _acceptRetrySentAt.Clear();
            _acceptSent.Clear();
        }
        else if (newState == CoordState.QueueForBattleground)
        {
            _restageSentAt.Clear();
        }
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
            var elapsed = DateTime.UtcNow - _stateEnteredAt;
            if (_tickCount % 20 == 1)
            {
                _logger.LogWarning(
                    "BG_COORD: Waiting for bots (ready={Ready}/{Total}, staged={Staged}/{Total}, elapsed={Elapsed:mm\\:ss}). Pending: {Pending}",
                    ready,
                    total,
                    staged,
                    total,
                    elapsed,
                    string.Join(", ", pendingAccounts.Take(10)));
            }

            // Timeout: if enough bots are ready to form a BG, proceed without stragglers.
            // AV requires 40 per side but VMaNGOS may allow fewer. Proceed if >=75% staged.
            if (elapsed > TimeSpan.FromSeconds(90) && staged >= (total * 3 / 4))
            {
                _logger.LogWarning(
                    "BG_COORD: Timed out waiting for all bots. Proceeding with {Staged}/{Total} staged ({Pending} not ready).",
                    staged, total, string.Join(", ", pendingAccounts));
            }
            else
            {
                return null;
            }
        }

        if (RequiresFactionGroupQueue)
        {
            var groupIssues = DescribeFactionGroupIssues(snapshots);
            if (groupIssues.Count > 0)
            {
                if (_tickCount % 20 == 1)
                {
                    _logger.LogWarning(
                        "BG_COORD: Waiting for faction group formation before queueing. Pending: {Pending}",
                        string.Join(", ", groupIssues.Take(10)));
                }

                return null;
            }
        }

        _logger.LogWarning(
            "BG_COORD: {Ready}/{Total} bots staged at battlemaster areas. Starting loadout hand-off.",
            ready,
            total);
        TransitionTo(CoordState.ApplyingLoadouts);
        return null;
    }

    /// <summary>
    /// P3.4: per-bot single-shot dispatch of <see cref="ActionType.ApplyLoadout"/>.
    /// Each bot gets exactly one ApplyLoadout action (or zero if no spec is
    /// configured for its account), then the coordinator waits until every
    /// bot's snapshot reports <c>LoadoutReady</c> or <c>LoadoutFailed</c>
    /// before advancing. LoadoutFailed accounts go on <see cref="ExcludedAccounts"/>
    /// so one broken bot doesn't block the rest.
    /// </summary>
    private ActionMessage? HandleApplyingLoadouts(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        RecordLoadoutProgressFromSnapshots(snapshots);

        ActionMessage? action = null;
        if (!_loadoutSent.ContainsKey(requestingAccount)
            && !_loadoutReady.ContainsKey(requestingAccount)
            && !_loadoutFailed.ContainsKey(requestingAccount))
        {
            if (_loadoutSpecs.TryGetValue(requestingAccount, out var spec) && spec != null)
            {
                action = LoadoutSpecConverter.BuildApplyLoadoutAction(spec);
                var correlationId = $"bg-coord:loadout:{requestingAccount}:{Guid.NewGuid():N}";
                action.CorrelationId = correlationId;
                _loadoutCorrelationIds[requestingAccount] = correlationId;
                _loadoutSent[requestingAccount] = 1;
                _logger.LogInformation(
                    "BG_COORD: ApplyLoadout dispatched to '{Account}' (corr={CorrelationId}, targetLevel={Lvl}, spells={Spells}, equip={Equip})",
                    requestingAccount,
                    correlationId,
                    spec.TargetLevel,
                    spec.SpellIdsToLearn?.Count() ?? 0,
                    spec.EquipItems?.Count() ?? 0);
            }
            else
            {
                // No spec configured for this bot → auto-ready.
                _loadoutReady[requestingAccount] = 1;
                _logger.LogInformation(
                    "BG_COORD: No loadout spec for '{Account}' — marking loadout-ready",
                    requestingAccount);
            }
        }

        if (AllAccountsLoadoutResolved())
        {
            var readyCount = _loadoutReady.Count;
            var failedCount = _loadoutFailed.Count;
            var total = _memberAccounts.Count + 1;
            _logger.LogWarning(
                "BG_COORD: Loadouts resolved: {Ready}/{Total} ready, {Failed} failed (excluded={Excluded}).",
                readyCount,
                total,
                failedCount,
                string.Join(", ", _loadoutFailed.Keys));
            TransitionTo(CoordState.WaitingForRaidFormation);
        }
        else if (_tickCount % 20 == 1)
        {
            var pending = AllAccountNames()
                .Where(a => !_loadoutReady.ContainsKey(a) && !_loadoutFailed.ContainsKey(a))
                .Select(a => snapshots.TryGetValue(a, out var s)
                    ? $"{a}({DescribeLoadoutStatus(s)})"
                    : $"{a}(no snapshot)")
                .Take(10);
            _logger.LogWarning(
                "BG_COORD: ApplyingLoadouts waiting on: {Pending}",
                string.Join(", ", pending));
        }

        return action;
    }

    /// <summary>
    /// P3.5: gate transition to <see cref="CoordState.QueueForBattleground"/>
    /// on every non-leader bot's snapshot reporting the expected
    /// <c>PartyLeaderGuid</c>. BGs that don't require a faction group short-
    /// circuit immediately. Reuses <see cref="DescribeFactionGroupIssues"/>
    /// so the predicate matches WaitingForBots exactly.
    /// </summary>
    private ActionMessage? HandleWaitingForRaidFormation(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (!RequiresFactionGroupQueue)
        {
            TransitionTo(CoordState.QueueForBattleground);
            return null;
        }

        var groupIssues = DescribeFactionGroupIssues(snapshots);
        if (groupIssues.Count == 0)
        {
            _logger.LogWarning("BG_COORD: Raid formation complete. Starting BG queue.");
            TransitionTo(CoordState.QueueForBattleground);
            return null;
        }

        if (_tickCount % 20 == 1)
        {
            _logger.LogWarning(
                "BG_COORD: Waiting for raid formation. Pending: {Pending}",
                string.Join(", ", groupIssues.Take(10)));
        }

        return null;
    }

    private void RecordLoadoutProgressFromSnapshots(
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        foreach (var account in AllAccountNames())
        {
            if (_loadoutReady.ContainsKey(account) || _loadoutFailed.ContainsKey(account))
                continue;

            // P5.1: ACK-driven short-circuit. If the dispatched ApplyLoadout has a
            // terminal ack, consume it first. This closes the gap where the bot
            // rejects the action before LoadoutStatus advances (e.g.
            // "loadout_task_already_active", "unsupported_action", or a step
            // TimedOut) — without this, the coordinator would wait forever on
            // snapshot.LoadoutStatus that never flips.
            if (_loadoutCorrelationIds.TryGetValue(account, out var correlationId))
            {
                var ack = LastAck(correlationId, snapshots);
                if (ack != null)
                {
                    switch (ack.Status)
                    {
                        case CommandAckEvent.Types.AckStatus.Success:
                            _loadoutReady[account] = 1;
                            _logger.LogInformation(
                                "BG_COORD: Loadout ACK Success for '{Account}' (corr={CorrelationId})",
                                account,
                                correlationId);
                            continue;
                        case CommandAckEvent.Types.AckStatus.Failed:
                        case CommandAckEvent.Types.AckStatus.TimedOut:
                            _loadoutFailed[account] = 1;
                            _logger.LogWarning(
                                "BG_COORD: Loadout ACK {Status} for '{Account}' (corr={CorrelationId}): {Reason}",
                                ack.Status,
                                account,
                                correlationId,
                                string.IsNullOrWhiteSpace(ack.FailureReason) ? "(no reason)" : ack.FailureReason);
                            continue;
                    }
                }
            }

            if (!snapshots.TryGetValue(account, out var snapshot) || snapshot == null)
                continue;

            switch (snapshot.LoadoutStatus)
            {
                case LoadoutStatus.LoadoutReady:
                    _loadoutReady[account] = 1;
                    break;
                case LoadoutStatus.LoadoutFailed:
                    _loadoutFailed[account] = 1;
                    _logger.LogWarning(
                        "BG_COORD: Loadout FAILED for '{Account}': {Reason}",
                        account,
                        snapshot.LoadoutFailureReason ?? "(no reason)");
                    break;
            }
        }
    }

    private bool AllAccountsLoadoutResolved()
    {
        foreach (var account in AllAccountNames())
        {
            if (_loadoutReady.ContainsKey(account) || _loadoutFailed.ContainsKey(account))
                continue;
            return false;
        }
        return true;
    }

    private IEnumerable<string> AllAccountNames()
    {
        yield return _leaderAccount;
        foreach (var member in _memberAccounts)
            yield return member;
    }

    private static string DescribeLoadoutStatus(WoWActivitySnapshot snapshot) => snapshot.LoadoutStatus switch
    {
        LoadoutStatus.LoadoutReady => "ready",
        LoadoutStatus.LoadoutFailed => $"failed:{snapshot.LoadoutFailureReason}",
        LoadoutStatus.LoadoutInProgress => "in-progress",
        _ => "not-started",
    };

    /// <summary>
    /// P4.5.1 / P5.1: Return the most recent <see cref="CommandAckEvent"/> observed for a given
    /// correlation id across all provided bot snapshots, or <c>null</c> if no ACK has been seen yet.
    /// Terminal statuses (Success/Failed/TimedOut) are preferred over Pending when both are present.
    /// Callers that only need the status should prefer <see cref="LastAckStatus"/>; callers that
    /// need the failure reason (e.g. coordinator failure logging) use this richer overload.
    /// </summary>
    public static CommandAckEvent? LastAck(
        string correlationId,
        IReadOnlyDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || snapshots == null)
            return null;

        CommandAckEvent? latestPending = null;
        foreach (var snapshot in snapshots.Values)
        {
            if (snapshot == null)
                continue;

            for (var i = snapshot.RecentCommandAcks.Count - 1; i >= 0; i--)
            {
                var ack = snapshot.RecentCommandAcks[i];
                if (!string.Equals(ack.CorrelationId, correlationId, StringComparison.Ordinal))
                    continue;

                // Terminal beats Pending — stop at first terminal hit in this snapshot;
                // otherwise remember Pending and keep scanning other snapshots.
                if (ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                    return ack;

                latestPending ??= ack;
                break;
            }
        }

        return latestPending;
    }

    /// <summary>
    /// P4.5.1: Thin status-only wrapper around <see cref="LastAck"/>. Preserved for callers
    /// and tests that only need the <see cref="CommandAckEvent.Types.AckStatus"/> enum.
    /// </summary>
    public static CommandAckEvent.Types.AckStatus? LastAckStatus(
        string correlationId,
        IReadOnlyDictionary<string, WoWActivitySnapshot> snapshots)
        => LastAck(correlationId, snapshots)?.Status;

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
            var restageAction = TryBuildRestageGotoAction(
                requestingAccount,
                snapshot,
                phaseName: "QueueForBattleground",
                elapsed: DateTime.UtcNow - _stateEnteredAt);
            if (restageAction != null)
                return restageAction;

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
            (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) == _bgMapId);

        var total = _memberAccounts.Count + 1;
        if (onBgMap >= total)
        {
            _logger.LogInformation("BG_COORD: {Count}/{Total} bots on BG map. BG is active.", onBgMap, total);
            TransitionTo(CoordState.InBattleground);
            return null;
        }

        var elapsed = DateTime.UtcNow - _stateEnteredAt;
        if (elapsed < InviteAcceptRetryStartDelay)
            return null;

        if (!snapshots.TryGetValue(requestingAccount, out var snapshot))
            return null;

        if (!IsWorldReady(snapshot, _minimumLevel))
            return null;

        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId == _bgMapId)
            return null;

        var restageGoto = TryBuildRestageGotoAction(
            requestingAccount,
            snapshot,
            phaseName: "WaitForInvite",
            elapsed: elapsed);
        if (restageGoto != null)
            return restageGoto;

        var now = DateTime.UtcNow;
        if (!_joinRetrySentAt.TryGetValue(requestingAccount, out var lastJoinSentAt)
            || now - lastJoinSentAt >= InviteJoinRetryInterval)
        {
            _joinRetrySentAt[requestingAccount] = now;
            _logger.LogInformation(
                "BG_COORD: WaitForInvite retry JOIN_BATTLEGROUND for '{Account}' (elapsed={Elapsed:mm\\:ss}, map={Map})",
                requestingAccount,
                elapsed,
                mapId);

            var joinAction = new ActionMessage { ActionType = ActionType.JoinBattleground };
            joinAction.Parameters.Add(new RequestParameter { IntParam = (int)_bgTypeId });
            joinAction.Parameters.Add(new RequestParameter { IntParam = (int)_bgMapId });
            return joinAction;
        }

        if (_acceptRetrySentAt.TryGetValue(requestingAccount, out var lastSentAt)
            && now - lastSentAt < InviteAcceptRetryInterval)
        {
            return null;
        }

        _acceptRetrySentAt[requestingAccount] = now;
        _logger.LogInformation(
            "BG_COORD: WaitForInvite retry ACCEPT_BATTLEGROUND for '{Account}' (elapsed={Elapsed:mm\\:ss}, map={Map})",
            requestingAccount,
            elapsed,
            mapId);
        return new ActionMessage { ActionType = ActionType.AcceptBattleground };
    }

    private ActionMessage? TryBuildRestageGotoAction(
        string accountName,
        WoWActivitySnapshot snapshot,
        string phaseName,
        TimeSpan elapsed)
    {
        if (IsQueuedAtStagingTarget(accountName, snapshot))
            return null;

        if (!_stagingTargets.TryGetValue(accountName, out var target))
            return null;

        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId != target.MapId)
            return null;

        var now = DateTime.UtcNow;
        if (_restageSentAt.TryGetValue(accountName, out var lastSentAt)
            && now - lastSentAt < QueueRestageRetryInterval)
        {
            return null;
        }

        _restageSentAt[accountName] = now;
        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        var distance = position == null ? float.NaN : Distance2D(position.X, position.Y, target.X, target.Y);
        _logger.LogInformation(
            "BG_COORD: {Phase} restage GOTO for '{Account}' (elapsed={Elapsed:mm\\:ss}, map={Map}, dist={Distance})",
            phaseName,
            accountName,
            elapsed,
            mapId,
            float.IsNaN(distance) ? "?" : distance.ToString("F0"));

        var action = new ActionMessage { ActionType = ActionType.Goto };
        action.Parameters.Add(new RequestParameter { FloatParam = target.X });
        action.Parameters.Add(new RequestParameter { FloatParam = target.Y });
        action.Parameters.Add(new RequestParameter { FloatParam = target.Z });
        action.Parameters.Add(new RequestParameter { FloatParam = QueueRestageStopDistance });
        return action;
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
            (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) == _bgMapId);

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

    private IReadOnlyList<string> DescribeFactionGroupIssues(
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var issues = new List<string>();
        if (_desiredPartyLeaderAccounts.Count == 0)
            return issues;

        foreach (var leaderAccount in _desiredPartyLeaderAccounts.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!snapshots.TryGetValue(leaderAccount, out var leaderSnapshot))
            {
                issues.Add($"{leaderAccount}(no snapshot)");
                continue;
            }

            var leaderGuid = leaderSnapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
            if (leaderGuid == 0)
            {
                issues.Add($"{leaderAccount}(leader guid missing)");
                continue;
            }

            foreach (var account in _desiredPartyLeaderAccounts
                .Where(entry => entry.Value.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key))
            {
                if (!snapshots.TryGetValue(account, out var snapshot))
                {
                    issues.Add($"{account}(no snapshot)");
                    continue;
                }

                if (!IsWorldReady(snapshot, _minimumLevel))
                {
                    issues.Add($"{account}(world not ready)");
                    continue;
                }

                var selfGuid = snapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
                if (selfGuid == 0)
                {
                    issues.Add($"{account}(self guid missing)");
                    continue;
                }

                if (account.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
                {
                    if (selfGuid != leaderGuid)
                        issues.Add($"{account}(leader guid mismatch)");

                    continue;
                }

                if (snapshot.PartyLeaderGuid != leaderGuid)
                    issues.Add($"{account}(leader=0x{snapshot.PartyLeaderGuid:X})");
            }
        }

        return issues;
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
