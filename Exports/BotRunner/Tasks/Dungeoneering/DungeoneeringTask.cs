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
    private const float AggroCheckRange = 12f;         // While navigating — only pull mobs this close
    private const float FollowDistance = 15f;
    private const float FollowStopDistance = 8f;
    private const int RestHealthPercent = 85;
    private const int RestManaPercent = 80;
    private const int StuckTimeoutMs = 10000;

    private readonly bool _isLeader;
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

        if (_isLeader && _waypoints.Count == 0)
            Log.Warning("[DUNGEONEERING] Leader task created with no waypoints — will only react to visible hostiles.");

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
        if (_updateCount % 50 == 1) // Log every ~25s (500ms tick assumed)
        {
            Log.Information("[DUNGEONEERING] Tick #{Count}: state={State}, leader={IsLeader}, wp={WpIdx}/{WpCount}, " +
                "pos=({X:F0},{Y:F0},{Z:F0}), hostiles={Hostiles}, aggressors={Aggressors}, partyLeader={PartyLeader}",
                _updateCount, _state, _isLeader, _waypointIndex, _waypoints.Count,
                player.Position.X, player.Position.Y, player.Position.Z,
                ObjectManager.Hostiles.Count(), ObjectManager.Aggressors.Count(),
                ObjectManager.PartyLeader != null ? "yes" : "null");
        }

        // Combat interrupts everything — push combat rotation task
        if (ObjectManager.Aggressors.Any())
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
            BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            return;
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

        NavigateToward(destination);
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

        // Mark skull and target for the party
        ObjectManager.SetTarget(target.Guid);
        ObjectManager.SetRaidTarget(target, TargetMarker.Skull);

        var dist = player.Position.DistanceTo(target.Position);

        // If mob is already an aggressor (in combat), push combat rotation directly
        if (ObjectManager.Aggressors.Any(a => a.Guid == target.Guid))
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
            Log.Information("[DUNGEONEERING] Engaging aggressor: {Name} (0x{Guid:X}) at {Dist:F0}y",
                target.Name, target.Guid, dist);
            BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            _killCount++;
            TransitionTo(DungeonState.WaitForCombatClear);
            return;
        }

        // Not an aggressor yet — navigate toward the mob to enter its aggro range.
        // The aggressor check in Update() (line 90) will push combat when the mob aggros.
        // This prevents the push-pop cycle where PvERotation pops immediately because
        // EnsureTarget() finds no aggressors.
        if (dist > 8f)
        {
            NavigateToward(target.Position);
        }
        else
        {
            // Very close but not agroed — try auto-attack to initiate combat
            ObjectManager.StartMeleeAttack();
            Log.Information("[DUNGEONEERING] Auto-attacking to pull: {Name} (0x{Guid:X}) at {Dist:F0}y",
                target.Name, target.Guid, dist);
        }
    }

    private void HandleWaitForCombatClear()
    {
        // Combat rotation task is on the stack — when it pops, we resume here.
        // ALWAYS return to NavigateToWaypoint after combat clears.
        // NavigateToWaypoint will check for close aggro targets (12y) while advancing.
        // This prevents the infinite pull loop where the bot would re-check for
        // hostiles within 25y after every kill and never advance past dense packs.
        if (!ObjectManager.Aggressors.Any())
        {
            TransitionTo(DungeonState.NavigateToWaypoint);
        }
    }

    private void HandleRestBeforePull()
    {
        if (CanProceed)
        {
            TransitionTo(_isLeader ? DungeonState.NavigateToWaypoint : DungeonState.FollowLeader);
            return;
        }

        ObjectManager.StopAllMovement();
        // Push rest task — class container provides class-specific rest (eat/drink/bandage)
        BotTasks.Push(Container.ClassContainer.CreateRestTask(BotContext));
    }

    private int _leaderLostTicks;
    private const int LeaderLostThreshold = 20; // ~10s at 500ms tick — then fall back to autonomous waypoints

    private void HandleFollowLeader(IWoWPlayer player)
    {
        // Non-leader: follow party leader
        // Note: DO NOT check for pull targets here — only the leader pulls.
        // Aggressors (mobs that attack followers) are handled in Update() line 90-96.
        var leader = ObjectManager.PartyLeader;
        if (leader?.Position == null)
        {
            _leaderLostTicks++;
            if (_leaderLostTicks >= LeaderLostThreshold)
            {
                // Leader invisible too long — navigate waypoints autonomously
                if (_updateCount % 50 == 2)
                    Log.Warning("[DUNGEONEERING] Follower lost leader for {Ticks} ticks — navigating waypoints autonomously. PartyMembers={Count}",
                        _leaderLostTicks, ObjectManager.PartyMembers.Count());
                HandleNavigateToWaypoint(player);
                return;
            }
            if (_updateCount % 50 == 2)
                Log.Warning("[DUNGEONEERING] Follower has no party leader — waiting ({Ticks}/{Threshold}). PartyMembers={Count}",
                    _leaderLostTicks, LeaderLostThreshold, ObjectManager.PartyMembers.Count());
            return;
        }

        _leaderLostTicks = 0; // Reset when leader is visible

        var dist = player.Position.DistanceTo(leader.Position);

        if (dist > FollowDistance)
        {
            NavigateToward(leader.Position);
        }
        else if (dist < FollowStopDistance)
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();
        }
        // else: in follow band, keep moving toward leader

        // Check rest
        if (!CanProceed)
        {
            TransitionTo(DungeonState.RestBeforePull);
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

            if (player.HealthPercent <= RestHealthPercent)
                return false;

            // Only check local player's mana (warriors/rogues have ManaPercent < 0)
            if (player.ManaPercent >= 0 && player.ManaPercent < RestManaPercent)
                return false;

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
