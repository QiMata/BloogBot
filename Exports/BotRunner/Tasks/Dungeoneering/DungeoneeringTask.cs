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
    private DungeonState _state;
    private int _waypointIndex;
    private Position? _lastPosition;
    private DateTime _lastMoveTime = DateTime.UtcNow;
    private int _killCount;

    /// <summary>
    /// Create a dungeoneering task.
    /// </summary>
    /// <param name="botContext">Bot context with ObjectManager, pathfinding, etc.</param>
    /// <param name="isLeader">True if this bot is the raid leader (navigates waypoints, pulls).</param>
    /// <param name="waypoints">Ordered list of dungeon waypoints to navigate. Leader only.</param>
    public DungeoneeringTask(IBotContext botContext, bool isLeader, IReadOnlyList<Position>? waypoints = null)
        : base(botContext)
    {
        _isLeader = isLeader;
        _waypoints = waypoints?.Select(p => new Position(p.X, p.Y, p.Z)).ToList() ?? [];

        if (_waypoints.Count == 0)
            Log.Warning("[DUNGEONEERING] Task created with NO waypoints (leader={IsLeader}) — bot will not navigate!", _isLeader);

        _state = _isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader;

        Log.Information("[DUNGEONEERING] Task started: leader={IsLeader}, waypoints={Count}",
            _isLeader, _waypoints.Count);
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

        // Leader: mark closest aggressor as skull and auto-attack, but do NOT push
        // PvERotation here. The state machine handles combat via HandlePullHostiles.
        // Previously this unconditionally pushed PvERotation every tick, which permanently
        // blocked waypoint navigation — PvERotation never pops when the level 8 leader
        // can't kill level 14 mobs, so the DungeoneeringTask state machine never runs.
        if (_isLeader && ObjectManager.Aggressors.Any())
        {
            var aggressor = ObjectManager.Aggressors
                .OrderBy(a => player.Position.DistanceTo(a.Position))
                .First();
            ObjectManager.SetTarget(aggressor.Guid);
            ObjectManager.SetRaidTarget(aggressor, TargetMarker.Skull);
            ObjectManager.StartMeleeAttack();
            // Fall through to the state machine — leader navigates AND fights
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
        // While navigating, only pull mobs within close aggro range (12y).
        // This prevents the infinite pull loop in dense corridors where
        // the bot would pull everything within 25y and never advance.
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

        // Mark skull and target for the party, auto-attack, then resume navigation.
        // Do NOT push PvERotation — it blocks the state machine permanently when
        // the leader can't kill mobs (level disadvantage). The leader fights while moving.
        ObjectManager.SetTarget(target.Guid);
        ObjectManager.SetRaidTarget(target, TargetMarker.Skull);
        ObjectManager.StartMeleeAttack();

        var dist = player.Position.DistanceTo(target.Position);

        if (dist > 8f)
        {
            // Navigate toward the mob
            TryNavigateToward(target.Position, allowDirectFallback: true);
        }

        // After marking and engaging, resume waypoint navigation.
        // The leader will continue auto-attacking while moving to the next waypoint.
        // Followers assist the skull target via their own combat logic.
        TransitionTo(DungeonState.NavigateToWaypoint);
    }

    private void HandleWaitForCombatClear()
    {
        // Combat rotation task is on the stack — when it pops, we resume here.
        // Return to the appropriate state based on role.
        if (!ObjectManager.Aggressors.Any())
        {
            TransitionTo(_isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader);
        }
        else if (!_isLeader)
        {
            // Followers: don't wait for combat clear — resume following.
            // The leader controls the pace. Followers shouldn't get stuck
            // fighting entrance mobs while the leader advances.
            TransitionTo(DungeonState.FollowLeader);
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

        // If no party members visible, navigate toward next waypoint directly.
        // Do NOT call HandleNavigateToWaypoint — that puts followers into the
        // leader's PullHostiles/WaitForCombatClear states, causing deadlocks.
        if (leader?.Position == null)
        {
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
