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
    private const float GroupPaceDistance = 30f;    // Leader waits if fewer than half the group is within this range
    private const int RestHealthPercent = 30;       // Low threshold — warriors can't eat without food
    private const int RestManaPercent = 30;        // Low threshold — proceed earlier in dungeons
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

        // Combat interrupts navigation — but only for the LEADER.
        // Followers keep following/navigating even when aggroed, because:
        //   1. The DungeoneeringCoordinator handles combat via heal/DPS overlay actions
        //   2. Mobs at the entrance aggro followers but often evade (position desync),
        //      creating a permanent combat state that blocks all movement
        //   3. Followers that stop to fight never catch up to the leader
        // Leader stops to fight because they control the pace of dungeon advancement.
        if (_isLeader && ObjectManager.Aggressors.Any())
        {
            // Only push a new PvERotation if one isn't already on the stack.
            if (BotTasks.Count <= 1 || BotTasks.Peek() == this)
            {
                ClearNavigation();
                BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            }
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
            TryNavigateToward(target.Position, allowDirectFallback: true);
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
        // Followers navigate the same waypoint path as the leader.
        // Previous approach: follow PartyLeader by GUID. This fails because:
        //   - Coordinator may promote a BG bot as dungeoneering leader, but the
        //     game's raid leader GUID (PartyLeader) is still the FG bot (TESTBOT1)
        //   - All followers see TESTBOT1 at the entrance and STOP next to it
        //   - The actual dungeoneering leader (RFCBOT2) advances alone
        // Fix: followers navigate waypoints autonomously. The coordinator handles
        // combat overlay (heal/DPS) via separate actions.
        HandleNavigateToWaypoint(player);
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

            if (player.HealthPercent <= RestHealthPercent)
                return false;

            // Only check mana for classes that use it. Warriors/rogues have MaxMana=0.
            if (player.MaxMana > 0 && player.ManaPercent < RestManaPercent)
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
