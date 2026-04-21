using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Walks a natural-node route, scans for visible gatherable objects near each candidate
/// coordinate, and performs the first successful gather before completing.
/// </summary>
public class GatheringRouteTask : BotTask, IBotTask
{
    private enum GatheringState
    {
        BuildRoute,
        MoveToCandidate,
        SearchVisibleNode,
        MoveToVisibleNode,
        AwaitGatherCast,
        AwaitGatherChannel,
        AwaitGatherRetry,
        LootNode,
        PostGatherCooldown,
    }

    internal const float CandidateReachDistance = 12f;
    internal const float VisibleNodeDistance = 80f;
    internal const float GatherRange = 5f;
    internal const float GatherApproachBuffer = 0.5f;
    internal const float ShortRangeNodeFallbackDistance = 12f;
    internal const int CandidateTimeoutMs = 45000;
    internal const int NodeSearchTimeoutMs = 3000;
    internal const int GatherCastDelayMs = 500;
    internal const int GatherChannelMs = 5000;
    internal const int GatherRetryDelayMs = 1000;
    internal const int MaxGatherAttempts = 4;
    internal const int PostGatherCooldownMs = 3000;

    private readonly List<Position> _originalCandidates;
    private readonly HashSet<uint> _nodeEntries;
    private readonly int _gatherSpellId;
    private readonly int _targetSuccessCount;
    private readonly int _maxRouteLoops;
    private readonly object _gatherFailureLock = new();

    private readonly List<Position> _orderedRoute = [];
    private GatheringState _state = GatheringState.BuildRoute;
    private int _routeLoopCount;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _routeIndex;
    private Position? _currentCandidate;
    private ulong _activeNodeGuid;
    private Position? _activeNodePosition;
    private uint _activeNodeEntry;
    private bool _gatherLikelySucceeded;
    private int _successfulGathers;
    private int _activeNodeAttempts;
    private string? _pendingGatherFailure;
    private bool _eventsSubscribed;
    private bool _combatPaused;

    public GatheringRouteTask(
        IBotContext botContext,
        IReadOnlyList<Position> routeCandidates,
        IReadOnlyCollection<uint> nodeEntries,
        int gatherSpellId,
        int targetSuccessCount = 1,
        int maxRouteLoops = 1) : base(botContext)
    {
        _originalCandidates = routeCandidates
            .Where(candidate => candidate != null)
            .Select(candidate => new Position(candidate.X, candidate.Y, candidate.Z))
            .ToList();
        _nodeEntries = nodeEntries.Where(entry => entry != 0).ToHashSet();
        _gatherSpellId = gatherSpellId;
        _targetSuccessCount = Math.Max(1, targetSuccessCount);
        _maxRouteLoops = Math.Max(1, maxRouteLoops);
        SubscribeToGatherEvents();
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            CompleteTask("no_player");
            return;
        }

        if (player.IsInCombat)
        {
            PauseForCombat();
            return;
        }

        if (_combatPaused)
            ResumeAfterCombat();

        switch (_state)
        {
            case GatheringState.BuildRoute:
                BuildRoute(player.Position);
                return;
            case GatheringState.MoveToCandidate:
                MoveToCandidate(player);
                return;
            case GatheringState.SearchVisibleNode:
                SearchVisibleNode();
                return;
            case GatheringState.MoveToVisibleNode:
                MoveToVisibleNode(player);
                return;
            case GatheringState.AwaitGatherCast:
                AwaitGatherCast();
                return;
            case GatheringState.AwaitGatherChannel:
                AwaitGatherChannel();
                return;
            case GatheringState.AwaitGatherRetry:
                AwaitGatherRetry();
                return;
            case GatheringState.LootNode:
                LootNode();
                return;
            case GatheringState.PostGatherCooldown:
                PostGatherCooldown();
                return;
        }
    }

    internal static IReadOnlyList<Position> OptimizeRoute(Position start, IReadOnlyList<Position> candidates)
    {
        var remaining = candidates
            .Where(candidate => candidate != null)
            .Select(candidate => new Position(candidate.X, candidate.Y, candidate.Z))
            .ToList();
        var ordered = new List<Position>(remaining.Count);
        var cursor = new Position(start.X, start.Y, start.Z);

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderBy(candidate => candidate.DistanceTo2D(cursor))
                .ThenBy(candidate => candidate.DistanceTo(cursor))
                .First();
            ordered.Add(next);
            remaining.Remove(next);
            cursor = next;
        }

        return ordered;
    }

    private void BuildRoute(Position playerPosition)
    {
        if (_originalCandidates.Count == 0 || _nodeEntries.Count == 0)
        {
            CompleteTask("no_candidates");
            return;
        }

        _orderedRoute.Clear();
        _orderedRoute.AddRange(OptimizeRoute(playerPosition, _originalCandidates));
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask route_ready candidates={_orderedRoute.Count} entries={string.Join(",", _nodeEntries.OrderBy(entry => entry))} spell={_gatherSpellId}");
        AdvanceToNextCandidate("route_ready");
    }

    private void MoveToCandidate(IWoWLocalPlayer player)
    {
        if (_currentCandidate == null)
        {
            AdvanceToNextCandidate("missing_candidate");
            return;
        }

        var distance = player.Position.DistanceTo(_currentCandidate);
        if (distance <= CandidateReachDistance)
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask candidate_reached index={_routeIndex}/{_orderedRoute.Count} distance={distance:F1}");
            SetState(GatheringState.SearchVisibleNode);
            return;
        }

        if (ElapsedMs >= CandidateTimeoutMs)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask candidate_timeout index={_routeIndex}/{_orderedRoute.Count} distance={distance:F1}");
            AdvanceToNextCandidate("candidate_timeout");
            return;
        }

        if (!TryNavigateToward(_currentCandidate))
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask candidate_no_path index={_routeIndex}/{_orderedRoute.Count} distance={distance:F1}");
            AdvanceToNextCandidate("candidate_no_path");
        }
    }

    private void SearchVisibleNode()
    {
        var node = FindVisibleNodeNearCandidate();
        if (node != null)
        {
            _activeNodeGuid = node.Guid;
            _activeNodePosition = new Position(node.Position.X, node.Position.Y, node.Position.Z);
            _activeNodeEntry = node.Entry;
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask node_visible guid=0x{_activeNodeGuid:X} entry={node.Entry} candidate={_routeIndex}/{_orderedRoute.Count}");
            SetState(GatheringState.MoveToVisibleNode);
            return;
        }

        if (ElapsedMs >= NodeSearchTimeoutMs)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask node_missing candidate={_routeIndex}/{_orderedRoute.Count}");
            AdvanceToNextCandidate("node_missing");
        }
    }

    private void MoveToVisibleNode(IWoWLocalPlayer player)
    {
        var node = FindActiveNode();
        if (node == null || _activeNodePosition == null)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask node_lost candidate={_routeIndex}/{_orderedRoute.Count}");
            AdvanceToNextCandidate("node_lost");
            return;
        }

        var distance = player.Position.DistanceTo(_activeNodePosition);
        if (distance > GatherRange)
        {
            var approachPosition = ComputeApproachPosition(player.Position, _activeNodePosition);
            if (!TryNavigateToward(approachPosition, allowDirectFallback: true))
            {
                if (distance <= ShortRangeNodeFallbackDistance)
                {
                    BotContext.AddDiagnosticMessage(
                        $"[TASK] GatheringRouteTask node_short_direct_fallback guid=0x{_activeNodeGuid:X} distance={distance:F1}");
                    ObjectManager.MoveToward(approachPosition);
                    return;
                }

                BotContext.AddDiagnosticMessage(
                    $"[TASK] GatheringRouteTask node_no_path guid=0x{_activeNodeGuid:X} distance={distance:F1}");
                AdvanceToNextCandidate("node_no_path");
            }
            return;
        }

        ClearNavigation();
        ObjectManager.ForceStopImmediate();
        ObjectManager.Face(_activeNodePosition);
        ClearPendingGatherFailure();
        _activeNodeAttempts++;
        ObjectManager.InteractWithGameObject(_activeNodeGuid);
        Logger.LogInformation("[GATHER-ROUTE] Used gathering node 0x{Guid:X} entry={Entry}; attempt={Attempt}/{MaxAttempts}; delaying spell={SpellId} for {DelayMs}ms",
            _activeNodeGuid, node.Entry, _activeNodeAttempts, MaxGatherAttempts, _gatherSpellId, GatherCastDelayMs);
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_use_started guid=0x{_activeNodeGuid:X} entry={node.Entry} spell={_gatherSpellId} attempt={_activeNodeAttempts}/{MaxGatherAttempts} delayMs={GatherCastDelayMs}");
        SetState(GatheringState.AwaitGatherCast);
    }

    private void AwaitGatherCast()
    {
        if (ElapsedMs < GatherCastDelayMs)
            return;

        if (_gatherSpellId > 0)
            ObjectManager.CastSpellOnGameObject(_gatherSpellId, _activeNodeGuid);
        ObjectManager.SetTarget(0);
        Logger.LogInformation("[GATHER-ROUTE] Gathering node 0x{Guid:X} entry={Entry} spell={SpellId}",
            _activeNodeGuid, _activeNodeEntry, _gatherSpellId);
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_started guid=0x{_activeNodeGuid:X} entry={_activeNodeEntry} spell={_gatherSpellId}");
        SetState(GatheringState.AwaitGatherChannel);
    }

    private void AwaitGatherChannel()
    {
        if (TryConsumeGatherFailure(out var failure))
        {
            ScheduleGatherRetry($"cast_failed:{failure}");
            return;
        }

        if (ElapsedMs < GatherChannelMs)
            return;

        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_channel_complete guid=0x{_activeNodeGuid:X}");
        SetState(GatheringState.LootNode);
    }

    private void AwaitGatherRetry()
    {
        if (ElapsedMs < GatherRetryDelayMs)
            return;

        if (FindActiveNode() == null)
        {
            _gatherLikelySucceeded = true;
            SetState(GatheringState.PostGatherCooldown);
            return;
        }

        if (_activeNodeAttempts >= MaxGatherAttempts)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask gather_retry_exhausted guid=0x{_activeNodeGuid:X} attempts={_activeNodeAttempts}/{MaxGatherAttempts}");
            AdvanceToNextCandidate("gather_retry_exhausted");
            return;
        }

        ClearNavigation();
        SetState(GatheringState.MoveToVisibleNode);
    }

    private void LootNode()
    {
        if (ObjectManager.LootFrame?.IsOpen == true)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask loot_window_open guid=0x{_activeNodeGuid:X} count={ObjectManager.LootFrame.LootCount}");
            ObjectManager.LootFrame.LootAll();
            ObjectManager.LootFrame.Close();
            _gatherLikelySucceeded = true;
        }
        else if (FindActiveNode() == null)
        {
            // The node despawned even though the loot frame is already closed.
            _gatherLikelySucceeded = true;
        }
        else if (_activeNodeAttempts < MaxGatherAttempts)
        {
            ScheduleGatherRetry("gather_no_result");
            return;
        }

        SetState(GatheringState.PostGatherCooldown);
    }

    private void PostGatherCooldown()
    {
        if (ElapsedMs < PostGatherCooldownMs)
            return;

        ObjectManager.SetTarget(0);
        if (_gatherLikelySucceeded)
        {
            _successfulGathers++;
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask gather_success guid=0x{_activeNodeGuid:X} successes={_successfulGathers}");
            if (_successfulGathers >= _targetSuccessCount)
            {
                CompleteTask("gather_success");
                return;
            }
        }

        AdvanceToNextCandidate(_gatherLikelySucceeded ? "continue_after_success" : "gather_no_result");
    }

    private IWoWGameObject? FindVisibleNodeNearCandidate()
    {
        if (_currentCandidate == null)
            return null;

        return ObjectManager.GameObjects
            .Where(go => go.Position != null && _nodeEntries.Contains(go.Entry))
            .Where(go => go.Position.DistanceTo(_currentCandidate) <= VisibleNodeDistance)
            .OrderBy(go => go.Position.DistanceTo(_currentCandidate))
            .FirstOrDefault();
    }

    private IWoWGameObject? FindActiveNode()
        => _activeNodeGuid == 0
            ? null
            : ObjectManager.GameObjects.FirstOrDefault(go => go.Guid == _activeNodeGuid);

    internal static Position ComputeApproachPosition(Position playerPosition, Position nodePosition)
    {
        ArgumentNullException.ThrowIfNull(playerPosition);
        ArgumentNullException.ThrowIfNull(nodePosition);

        var desiredDistance = MathF.Max(0f, GatherRange - GatherApproachBuffer);
        var dx = nodePosition.X - playerPosition.X;
        var dy = nodePosition.Y - playerPosition.Y;
        var dz = nodePosition.Z - playerPosition.Z;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (distance <= desiredDistance || distance < 0.001f)
            return new Position(nodePosition.X, nodePosition.Y, nodePosition.Z);

        var scale = (distance - desiredDistance) / distance;
        return new Position(
            playerPosition.X + (dx * scale),
            playerPosition.Y + (dy * scale),
            playerPosition.Z + (dz * scale));
    }

    private void AdvanceToNextCandidate(string reason)
    {
        ClearNavigation();
        _activeNodeGuid = 0;
        _activeNodePosition = null;
        _activeNodeEntry = 0;
        _gatherLikelySucceeded = false;
        _activeNodeAttempts = 0;
        _combatPaused = false;
        ClearPendingGatherFailure();

        if (_routeIndex >= _orderedRoute.Count)
        {
            if (_routeLoopCount < _maxRouteLoops - 1)
            {
                _routeLoopCount++;
                _routeIndex = 0;
                var player = ObjectManager.Player;
                if (player?.Position != null)
                {
                    _orderedRoute.Clear();
                    _orderedRoute.AddRange(OptimizeRoute(player.Position, _originalCandidates));
                }
                BotContext.AddDiagnosticMessage(
                    $"[TASK] GatheringRouteTask route_loop iteration={_routeLoopCount + 1}/{_maxRouteLoops} candidates={_orderedRoute.Count}");
            }
            else
            {
                CompleteTask(_successfulGathers > 0 ? "route_complete_success" : "route_complete_no_nodes");
                return;
            }
        }

        ResequenceRemainingCandidatesIfNeeded(reason);

        _currentCandidate = _orderedRoute[_routeIndex++];
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask candidate_start index={_routeIndex}/{_orderedRoute.Count} reason={reason} pos=({_currentCandidate.X:F1},{_currentCandidate.Y:F1},{_currentCandidate.Z:F1})");
        SetState(GatheringState.MoveToCandidate);
    }

    private void ResequenceRemainingCandidatesIfNeeded(string reason)
    {
        if (!ShouldResequenceCandidates(reason))
            return;

        var playerPosition = ObjectManager.Player?.Position;
        if (playerPosition == null)
            return;

        if (_routeIndex < 0 || _routeIndex >= _orderedRoute.Count - 1)
            return;

        var remaining = _orderedRoute.Skip(_routeIndex).ToList();
        if (remaining.Count < 2)
            return;

        var reordered = OptimizeRoute(playerPosition, remaining);
        _orderedRoute.RemoveRange(_routeIndex, remaining.Count);
        _orderedRoute.InsertRange(_routeIndex, reordered);
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask candidate_resequence reason={reason} remaining={remaining.Count}");
    }

    private static bool ShouldResequenceCandidates(string reason)
        => reason == "candidate_timeout"
            || reason == "candidate_no_path"
            || reason == "node_no_path"
            || reason == "node_lost";

    private void PauseForCombat()
    {
        ObjectManager.StopAllMovement();
        ClearNavigation();
        var alreadyPausedForCombat = _combatPaused;

        if (!alreadyPausedForCombat)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask pause reason=combat state={_state} candidate={_routeIndex}/{_orderedRoute.Count}");
            _combatPaused = true;
        }

        PushCombatTaskIfNeeded(alreadyPausedForCombat
            ? "combat_task_repush"
            : "combat_task_pushed");

        ResetStateTimer();
    }

    private void ResumeAfterCombat()
    {
        _combatPaused = false;
        ClearNavigation();
        ResetStateTimer();
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask resume state={_state} candidate={_routeIndex}/{_orderedRoute.Count}");
    }

    private int ElapsedMs => (int)(DateTime.UtcNow - _stateEnteredAt).TotalMilliseconds;

    private void SetState(GatheringState state)
    {
        _state = state;
        ResetStateTimer();
    }

    private void PushCombatTaskIfNeeded(string diagnosticEvent)
    {
        if (BotTasks.Count > 0 && BotTasks.Peek() != this)
            return;

        var classContainer = Container.ClassContainer;
        var combatFactory = classContainer?.CreatePvERotationTask;
        if (combatFactory == null)
            return;

        var combatTask = combatFactory(BotContext);
        if (combatTask == null)
            return;

        BotTasks.Push(combatTask);
        BotContext.AddDiagnosticMessage($"[TASK] GatheringRouteTask {diagnosticEvent}");
    }

    private void ResetStateTimer() => _stateEnteredAt = DateTime.UtcNow;

    private void ScheduleGatherRetry(string reason)
    {
        if (_activeNodeGuid == 0)
        {
            AdvanceToNextCandidate(reason);
            return;
        }

        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_retry guid=0x{_activeNodeGuid:X} entry={_activeNodeEntry} attempts={_activeNodeAttempts}/{MaxGatherAttempts} reason={reason}");
        Logger.LogWarning("[GATHER-ROUTE] Retrying gather node 0x{Guid:X} entry={Entry} attempts={Attempts}/{MaxAttempts} reason={Reason}",
            _activeNodeGuid, _activeNodeEntry, _activeNodeAttempts, MaxGatherAttempts, reason);
        _gatherLikelySucceeded = false;
        SetState(GatheringState.AwaitGatherRetry);
    }

    private void SubscribeToGatherEvents()
    {
        if (_eventsSubscribed || _gatherSpellId <= 0)
            return;

        EventHandler.OnErrorMessage += OnGatherErrorMessage;
        _eventsSubscribed = true;
    }

    private void UnsubscribeFromGatherEvents()
    {
        if (!_eventsSubscribed)
            return;

        EventHandler.OnErrorMessage -= OnGatherErrorMessage;
        _eventsSubscribed = false;
    }

    private void OnGatherErrorMessage(object? sender, OnUiMessageArgs args)
    {
        var message = args.Message;
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!message.Contains($"Cast failed for spell {_gatherSpellId}", StringComparison.OrdinalIgnoreCase))
            return;

        if (!message.Contains("TRY_AGAIN", StringComparison.OrdinalIgnoreCase))
            return;

        lock (_gatherFailureLock)
            _pendingGatherFailure = message;
    }

    private bool TryConsumeGatherFailure(out string failure)
    {
        lock (_gatherFailureLock)
        {
            failure = _pendingGatherFailure ?? string.Empty;
            _pendingGatherFailure = null;
            return failure.Length > 0;
        }
    }

    private void ClearPendingGatherFailure()
    {
        lock (_gatherFailureLock)
            _pendingGatherFailure = null;
    }

    private void CompleteTask(string reason)
    {
        UnsubscribeFromGatherEvents();
        PopTask(reason);
    }
}
