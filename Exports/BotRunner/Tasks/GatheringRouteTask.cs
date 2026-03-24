using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Walks a natural-node route, scans for visible gatherable objects near each candidate
/// coordinate, and performs the first successful gather before completing.
/// </summary>
public class GatheringRouteTask(
    IBotContext botContext,
    IReadOnlyList<Position> routeCandidates,
    IReadOnlyCollection<uint> nodeEntries,
    int gatherSpellId,
    int targetSuccessCount = 1,
    int maxRouteLoops = 1) : BotTask(botContext), IBotTask
{
    private enum GatheringState
    {
        BuildRoute,
        MoveToCandidate,
        SearchVisibleNode,
        MoveToVisibleNode,
        AwaitGatherChannel,
        LootNode,
        PostGatherCooldown,
    }

    internal const float CandidateReachDistance = 12f;
    internal const float VisibleNodeDistance = 80f;
    internal const float GatherRange = 5f;
    internal const int CandidateTimeoutMs = 45000;
    internal const int NodeSearchTimeoutMs = 8000;
    internal const int GatherChannelMs = 5000;
    internal const int PostGatherCooldownMs = 3000;

    private readonly List<Position> _originalCandidates = routeCandidates
        .Where(candidate => candidate != null)
        .Select(candidate => new Position(candidate.X, candidate.Y, candidate.Z))
        .ToList();
    private readonly HashSet<uint> _nodeEntries = nodeEntries.Where(entry => entry != 0).ToHashSet();
    private readonly int _gatherSpellId = gatherSpellId;
    private readonly int _targetSuccessCount = Math.Max(1, targetSuccessCount);
    private readonly int _maxRouteLoops = Math.Max(1, maxRouteLoops);

    private readonly List<Position> _orderedRoute = [];
    private GatheringState _state = GatheringState.BuildRoute;
    private int _routeLoopCount;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _routeIndex;
    private Position? _currentCandidate;
    private ulong _activeNodeGuid;
    private Position? _activeNodePosition;
    private bool _gatherLikelySucceeded;
    private int _successfulGathers;
    private bool _combatPaused;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            PopTask("no_player");
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
            case GatheringState.AwaitGatherChannel:
                AwaitGatherChannel();
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
            PopTask("no_candidates");
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
            if (!TryNavigateToward(_activeNodePosition))
            {
                BotContext.AddDiagnosticMessage(
                    $"[TASK] GatheringRouteTask node_no_path guid=0x{_activeNodeGuid:X} distance={distance:F1}");
                AdvanceToNextCandidate("node_no_path");
            }
            return;
        }

        ClearNavigation();
        ObjectManager.ForceStopImmediate();
        ObjectManager.Face(_activeNodePosition);
        ObjectManager.InteractWithGameObject(_activeNodeGuid);
        if (_gatherSpellId > 0)
            ObjectManager.CastSpellOnGameObject(_gatherSpellId, _activeNodeGuid);
        ObjectManager.SetTarget(0);
        Log.Information("[GATHER-ROUTE] Gathering node 0x{Guid:X} entry={Entry} spell={SpellId}",
            _activeNodeGuid, node.Entry, _gatherSpellId);
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_started guid=0x{_activeNodeGuid:X} entry={node.Entry} spell={_gatherSpellId}");
        SetState(GatheringState.AwaitGatherChannel);
    }

    private void AwaitGatherChannel()
    {
        if (ElapsedMs < GatherChannelMs)
            return;

        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask gather_channel_complete guid=0x{_activeNodeGuid:X}");
        SetState(GatheringState.LootNode);
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
                PopTask("gather_success");
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

    private void AdvanceToNextCandidate(string reason)
    {
        ClearNavigation();
        _activeNodeGuid = 0;
        _activeNodePosition = null;
        _gatherLikelySucceeded = false;
        _combatPaused = false;

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
                PopTask(_successfulGathers > 0 ? "route_complete_success" : "route_complete_no_nodes");
                return;
            }
        }

        _currentCandidate = _orderedRoute[_routeIndex++];
        BotContext.AddDiagnosticMessage(
            $"[TASK] GatheringRouteTask candidate_start index={_routeIndex}/{_orderedRoute.Count} reason={reason} pos=({_currentCandidate.X:F1},{_currentCandidate.Y:F1},{_currentCandidate.Z:F1})");
        SetState(GatheringState.MoveToCandidate);
    }

    private void PauseForCombat()
    {
        ObjectManager.StopAllMovement();
        ClearNavigation();

        if (!_combatPaused)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] GatheringRouteTask pause reason=combat state={_state} candidate={_routeIndex}/{_orderedRoute.Count}");
            PushCombatTaskIfNeeded();
            _combatPaused = true;
        }

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

    private void PushCombatTaskIfNeeded()
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
        BotContext.AddDiagnosticMessage("[TASK] GatheringRouteTask combat_task_pushed");
    }

    private void ResetStateTimer() => _stateEnteredAt = DateTime.UtcNow;
}
