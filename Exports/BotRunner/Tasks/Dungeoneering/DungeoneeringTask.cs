using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Dungeoneering;

/// <summary>
/// Dungeon crawling task. The raid leader navigates pre-defined waypoints,
/// pulls visible hostiles, and marks skull targets. Non-leaders follow the
/// party leader within 15y via pathfinding.
///
/// Adapted from the original DungeoneeringTask (commit 0e7e0bf) to the
/// current BotTask / behavior tree architecture.
/// </summary>
public class DungeoneeringTask : BotTask, IBotTask
{
    private enum DungeonState
    {
        NavigateToWaypoint,
        PullHostiles,
        WaitForCombatClear,
        RestBeforePull,
        FollowLeader,
        Complete,
    }

    // Configuration
    private const float WaypointReachDistance = 5f;
    private const float HostilePullRange = 25f;       // Deliberate pull (standing still to clear area)
    private const float AggroCheckRange = 15f;         // While navigating — pull mobs this close
    private const float FollowDistance = 15f;
    private const float FollowStopDistance = 8f;
    private const float GroupPaceDistance = 40f;    // Leader waits if fewer than half the group is within this range
    private const int RestHealthPercent = 0;        // Never rest on HP — GM bots are immortal at HP 0/1
    private const int RestManaPercent = 0;          // Never rest on mana — proceed always
    private const int StuckTimeoutMs = 8000;

    private readonly bool _isLeader;
    public bool IsLeader => _isLeader;
    private readonly List<Position> _waypoints;
    private readonly uint _targetMapId;
    private DungeonState _state;
    private int _waypointIndex;
    private Position? _lastPosition;
    private DateTime _lastMoveTime = DateTime.UtcNow;
    private DateTime _combatStartTime = DateTime.UtcNow;
    private const double CombatTimeoutSec = 10.0; // Max seconds to fight before resuming navigation
    private int _killCount;

    /// <summary>
    /// Create a dungeoneering task.
    /// </summary>
    /// <param name="botContext">Bot context with ObjectManager, pathfinding, etc.</param>
    /// <param name="isLeader">True if this bot is the raid leader (navigates waypoints, pulls).</param>
    /// <param name="waypoints">Ordered list of dungeon waypoints to navigate.</param>
    /// <param name="targetMapId">Expected instance map ID (e.g., 389 for RFC). Used for wrong-map detection.</param>
    public DungeoneeringTask(IBotContext botContext, bool isLeader, IReadOnlyList<Position>? waypoints = null, uint targetMapId = 0)
        : base(botContext)
    {
        _isLeader = isLeader;
        _waypoints = waypoints?.Select(p => new Position(p.X, p.Y, p.Z)).ToList() ?? [];
        _targetMapId = targetMapId;

        if (_waypoints.Count == 0)
            Log.Warning("[DUNGEONEERING] Task created with NO waypoints (leader={IsLeader}) — bot will not navigate!", _isLeader);

        _state = _isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader;

        Log.Information("[DUNGEONEERING] Task started: leader={IsLeader}, waypoints={Count}, targetMap={MapId}",
            _isLeader, _waypoints.Count, _targetMapId);
    }

    private int _updateCount;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
            return;

        _updateCount++;
        if (_updateCount % 50 == 1) // Log every ~5s
        {
            Log.Information("[DUNGEONEERING] Tick #{Count}: state={State}, leader={IsLeader}, wp={WpIdx}/{WpCount}, " +
                "pos=({X:F0},{Y:F0},{Z:F0}), hp={HP}%, flags=0x{Flags:X}, hostiles={Hostiles}, aggressors={Aggressors}, map={Map}",
                _updateCount, _state, _isLeader, _waypointIndex, _waypoints.Count,
                player.Position.X, player.Position.Y, player.Position.Z,
                player.HealthPercent, (uint)player.MovementFlags,
                ObjectManager.Hostiles.Count(), ObjectManager.Aggressors.Count(), player.MapId);
        }

        // Leader: when aggressors are present, mark skull, fight with a timeout.
        // Push PullTargetTask (class-specific approach + opener) then PvERotation pops when done.
        // After CombatTimeoutSec, give up and resume navigation — GM bots are immortal.
        if (_isLeader && ObjectManager.Aggressors.Any())
        {
            var aggressor = ObjectManager.Aggressors
                .OrderBy(a => player.Position.DistanceTo(a.Position))
                .First();
            ObjectManager.SetTarget(aggressor.Guid);
            ObjectManager.SetRaidTarget(aggressor, TargetMarker.Skull);

            // Push combat if we aren't already in a combat task
            if (BotTasks.Count <= 1 || BotTasks.Peek() == this)
            {
                var elapsed = (DateTime.UtcNow - _combatStartTime).TotalSeconds;
                if (elapsed < CombatTimeoutSec)
                {
                    // Use the class-specific pull task (Charge, ranged pull, etc.)
                    BotTasks.Push(Container.ClassContainer.CreatePullTargetTask(BotContext));
                    return;
                }
                // Combat timeout — stop fighting, resume navigation
                _combatStartTime = DateTime.UtcNow; // Reset for next pull
            }
            else
            {
                // Already in a combat task (PullTarget or PvERotation) — let it run
                return;
            }
        }
        else if (_isLeader)
        {
            // No aggressors — reset combat timer for the next encounter
            _combatStartTime = DateTime.UtcNow;
        }

        switch (_state)
        {
            case DungeonState.NavigateToWaypoint:
                HandleNavigateToWaypoint(player);
                break;
            case DungeonState.PullHostiles:
                HandlePullHostiles(player);
                break;
            case DungeonState.WaitForCombatClear:
                HandleWaitForCombatClear();
                break;
            case DungeonState.RestBeforePull:
                HandleRestBeforePull();
                break;
            case DungeonState.FollowLeader:
                HandleFollowLeader(player);
                break;
            case DungeonState.Complete:
                HandleComplete();
                break;
        }
    }

    private void HandleNavigateToWaypoint(IWoWPlayer player)
    {
        // Wrong-map guard: don't navigate dungeon waypoints on the overworld
        if (_targetMapId != 0 && player.MapId != _targetMapId)
        {
            ObjectManager.StopAllMovement();
            if (_updateCount % 50 == 1)
                Log.Warning("[DUNGEONEERING] On wrong map {Map} (expected {Expected}), waiting for teleport",
                    player.MapId, _targetMapId);
            return;
        }

        // While navigating, only pull mobs within close aggro range.
        var aggroTarget = FindPullTarget(player, AggroCheckRange);
        if (aggroTarget != null)
        {
            TransitionTo(DungeonState.PullHostiles);
            return;
        }

        // Check if we need to rest
        if (!CanProceed)
        {
            TransitionTo(DungeonState.RestBeforePull);
            return;
        }

        // Leader pacing: wait for followers to catch up before advancing.
        // The MT should not run ahead alone — wait until at least half the
        // raid is within GroupPaceDistance before continuing to the next waypoint.
        if (_isLeader && !IsGroupNearby(player))
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
            if (_updateCount % 50 == 1)
                Log.Information("[DUNGEONEERING] Leader waiting for group to catch up");
            return;
        }

        // All waypoints visited
        if (_waypointIndex >= _waypoints.Count)
        {
            // Check if there are any remaining hostiles within pull range
            if (FindPullTarget(player) != null)
            {
                TransitionTo(DungeonState.PullHostiles);
                return;
            }

            TransitionTo(DungeonState.Complete);
            return;
        }

        var destination = _waypoints[_waypointIndex];
        var dist = player.Position.DistanceTo(destination);

        if (dist < WaypointReachDistance)
        {
            _waypointIndex++;
            ClearNavigation();
            Log.Information("[DUNGEONEERING] Reached waypoint {Index}/{Total}, kills={Kills}",
                _waypointIndex, _waypoints.Count, _killCount);
            return;
        }

        // Stuck detection
        if (_lastPosition != null && player.Position.DistanceTo(_lastPosition) < 0.5f)
        {
            if ((DateTime.UtcNow - _lastMoveTime).TotalMilliseconds > StuckTimeoutMs)
            {
                Log.Warning("[DUNGEONEERING] Stuck at waypoint {Index}, skipping.", _waypointIndex);
                _waypointIndex++;
                ClearNavigation();
                _lastMoveTime = DateTime.UtcNow;
                return;
            }
        }
        else
        {
            _lastMoveTime = DateTime.UtcNow;
        }
        _lastPosition = player.Position;

        TryNavigateToward(destination, allowDirectFallback: true);
    }

    private void HandlePullHostiles(IWoWPlayer player)
    {
        if (!CanProceed)
        {
            TransitionTo(DungeonState.RestBeforePull);
            return;
        }

        var target = FindPullTarget(player);
        if (target == null)
        {
            // No more hostiles in range — resume waypoint navigation
            TransitionTo(DungeonState.NavigateToWaypoint);
            return;
        }

        // Mark skull for the raid, set target, push the class-specific pull task.
        // PullTargetTask handles approach + opener (Charge, ranged pull, etc.)
        // then pops itself and pushes PvERotation. When PvERotation pops,
        // WaitForCombatClear transitions back to NavigateToWaypoint.
        ObjectManager.SetTarget(target.Guid);
        ObjectManager.SetRaidTarget(target, TargetMarker.Skull);

        Log.Information("[DUNGEONEERING] Leader pulling: {Name} (0x{Guid:X}) at {Dist:F0}y",
            target.Name, target.Guid, player.Position.DistanceTo(target.Position));

        _combatStartTime = DateTime.UtcNow;
        BotTasks.Push(Container.ClassContainer.CreatePullTargetTask(BotContext));
        TransitionTo(DungeonState.WaitForCombatClear);
    }

    private void HandleWaitForCombatClear()
    {
        // Followers: never wait, resume following immediately
        if (!_isLeader)
        {
            TransitionTo(DungeonState.FollowLeader);
            return;
        }

        // Leader: resume navigation when aggressors clear OR combat times out
        if (!ObjectManager.Aggressors.Any())
        {
            _killCount++;
            TransitionTo(DungeonState.NavigateToWaypoint);
        }
        else if ((DateTime.UtcNow - _combatStartTime).TotalSeconds > CombatTimeoutSec)
        {
            Log.Information("[DUNGEONEERING] Combat timeout ({Sec}s), resuming navigation with {Aggressors} aggressors",
                CombatTimeoutSec, ObjectManager.Aggressors.Count());
            TransitionTo(DungeonState.NavigateToWaypoint);
        }
    }

    private int _restTicksWaiting;
    private const int RestGiveUpTicks = 100; // ~10s — if rest can't help, proceed anyway

    private void HandleRestBeforePull()
    {
        if (CanProceed)
        {
            _restTicksWaiting = 0;
            TransitionTo(_isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader);
            return;
        }

        _restTicksWaiting++;

        // Give up waiting if health isn't recovering (no food, no bandages)
        if (_restTicksWaiting >= RestGiveUpTicks)
        {
            Log.Warning("[DUNGEONEERING] Rest gave up after {Ticks} ticks — proceeding at {HP}% HP",
                _restTicksWaiting, ObjectManager.Player?.HealthPercent ?? 0);
            _restTicksWaiting = 0;
            TransitionTo(_isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader);
            return;
        }

        // Only push a rest task if one isn't already on top (prevents stack growth)
        if (BotTasks.Count == 0 || BotTasks.Peek() is DungeoneeringTask)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Push(Container.ClassContainer.CreateRestTask(BotContext));
        }
    }


    private void HandleFollowLeader(IWoWPlayer player)
    {
        // Find the party member furthest ahead in the dungeon (deepest from entrance).
        // This is the actual dungeon leader — follow them, not the nearest member.
        // Don't filter on Health > 0 because GM accounts sit at HP 0/1 permanently.
        var entrance = _waypoints.Count > 0 ? _waypoints[0] : player.Position;
        var partyMembers = ObjectManager.PartyMembers
            .Where(m => m.Position != null && m.Guid != player.Guid)
            .ToList();

        var leader = partyMembers
            .OrderByDescending(m => m.Position!.DistanceTo(entrance))
            .FirstOrDefault();

        // If no party members visible, navigate toward next waypoint as fallback.
        // But ONLY if we're on the correct dungeon map — waypoint coordinates exist
        // on both the instance map and the overworld, causing bots to wander Kalimdor.
        if (leader?.Position == null)
        {
            var expectedMapId = _waypoints.Count > 0 ? _targetMapId : 0u;
            if (expectedMapId != 0 && player.MapId != expectedMapId)
            {
                // Wrong map — just stop and wait. The coordinator will re-teleport us.
                ObjectManager.StopAllMovement();
                if (_updateCount % 50 == 1)
                    Log.Warning("[DUNGEONEERING] Follower on wrong map {Map} (expected {Expected}), waiting for teleport",
                        player.MapId, expectedMapId);
                return;
            }

            if (_waypointIndex < _waypoints.Count)
            {
                var wp = _waypoints[_waypointIndex];
                if (player.Position.DistanceTo(wp) < WaypointReachDistance)
                    _waypointIndex++;
                else
                    TryNavigateToward(wp, allowDirectFallback: true);
            }
            return;
        }

        var distToLeader = player.Position.DistanceTo(leader.Position);

        // Priority 1: if close to leader and have aggressors, fight them
        if (distToLeader < FollowDistance && ObjectManager.Aggressors.Any())
        {
            if (BotTasks.Count <= 1 || BotTasks.Peek() == this)
            {
                var aggressor = ObjectManager.Aggressors.OrderBy(a => player.Position.DistanceTo(a.Position)).First();
                ObjectManager.SetTarget(aggressor.Guid);
                ClearNavigation();
                BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            }
            return;
        }

        // Priority 2: catch up to the leader — movement takes precedence over combat
        // when the leader is far away. Don't let entrance mobs trap followers.
        if (distToLeader > FollowDistance)
        {
            TryNavigateToward(leader.Position, allowDirectFallback: true);
        }
        else if (distToLeader < FollowStopDistance)
        {
            // Close enough — assist leader's target if they have one
            if (leader.TargetGuid != 0 && leader.TargetGuid != leader.Guid)
            {
                ObjectManager.SetTarget(leader.TargetGuid);
                if (BotTasks.Count <= 1 || BotTasks.Peek() == this)
                    BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            }
            else
            {
                ObjectManager.StopAllMovement();
                ClearNavigation();
            }
        }
    }

    private void HandleComplete()
    {
        Log.Information("[DUNGEONEERING] Dungeon clear! Kills={Kills}, waypoints={WP}",
            _killCount, _waypoints.Count);
        ObjectManager.StopAllMovement();
        PopTask("dungeon_clear");
    }

    private IWoWUnit? FindPullTarget(IWoWPlayer player, float range = HostilePullRange)
    {
        return ObjectManager.Hostiles
            .Where(h => h.Health > 0
                && h.Position != null
                && player.Position.DistanceTo(h.Position) < range)
            .OrderBy(h => player.Position.DistanceTo(h.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns true if at least half the raid/party members are within GroupPaceDistance
    /// of the leader. Prevents the MT from outrunning the group and dying solo.
    /// </summary>
    private bool IsGroupNearby(IWoWPlayer player)
    {
        var partyMembers = ObjectManager.PartyMembers.ToList();
        if (partyMembers.Count == 0)
            return true; // Solo — no one to wait for

        int nearbyCount = partyMembers.Count(m =>
            m.Position != null
            && m.Health > 0
            && player.Position.DistanceTo(m.Position) <= GroupPaceDistance);

        int threshold = Math.Max(1, partyMembers.Count / 2);
        return nearbyCount >= threshold;
    }

    /// <summary>
    /// Whether the LOCAL player can proceed. Only checks the local player's HP and mana.
    /// Previously checked all party members' HP, which caused deadlocks when the leader
    /// took damage during combat — all followers would enter RestBeforePull indefinitely.
    /// </summary>
    private bool CanProceed
    {
        get
        {
            var player = ObjectManager.Player;
            if (player == null) return false;

            // Never rest during dungeoneering — GM bots are immortal.
            // HealthPercent can be 0 for GM accounts (server clamps HP, doesn't kill).

            return true;
        }
    }

    private void TransitionTo(DungeonState newState)
    {
        if (_state != newState)
        {
            Log.Debug("[DUNGEONEERING] {Old} -> {New}", _state, newState);
            _state = newState;
        }
    }
}
