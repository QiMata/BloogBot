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
/// Coordinated dungeon crawling task — faithful port of the original (commit 0e7e0bf).
///
/// ALL bots share the same Update() entry point:
///   1. If aggressors exist → everyone stops and fights (PvERotation)
///   2. Leader: if hostiles in LOS → stop, mark skull, push PullTargetTask
///   3. Leader: else → navigate to next waypoint
///   4. Follower: if leader >15y → path to leader. If ≤15y → stop.
///   5. Everyone: if party needs rest → stop and rest. Then buff.
///
/// The group moves as a unit. Nobody acts independently.
/// </summary>
public class DungeoneeringTask : BotTask, IBotTask
{
    private const float HostilePullRange = 25f;
    private const float FollowDistance = 15f;
    private const float WaypointReachDistance = 3f;
    private const int StuckTimeoutMs = 8000;

    private readonly bool _isLeader;
    public bool IsLeader => _isLeader;
    private readonly List<Position> _waypoints;
    private readonly uint _targetMapId;

    private int _waypointIndex;
    private Position? _lastPosition;
    private DateTime _lastMoveTime = DateTime.UtcNow;
    private DateTime _lastCombatPush = DateTime.MinValue;
    private const double CombatRepushCooldownSec = 20.0; // After combat timeout, navigate this long before re-engaging
    private int _updateCount;

    public DungeoneeringTask(IBotContext botContext, bool isLeader, IReadOnlyList<Position>? waypoints = null, uint targetMapId = 0)
        : base(botContext)
    {
        _isLeader = isLeader;
        _waypoints = waypoints?.Select(p => new Position(p.X, p.Y, p.Z)).ToList() ?? [];
        _targetMapId = targetMapId;

        if (_isLeader && _waypoints.Count == 0)
            Log.Warning("[DUNGEONEERING] Leader created with NO waypoints — will not navigate!");

        Log.Information("[DUNGEONEERING] Task started: leader={IsLeader}, waypoints={Count}, targetMap={MapId}",
            _isLeader, _waypoints.Count, _targetMapId);
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null) return;

        _updateCount++;
        if (_updateCount % 50 == 1)
        {
            Log.Information("[DUNGEONEERING] Tick #{Count}: leader={IsLeader}, wp={WpIdx}/{WpCount}, " +
                "pos=({X:F0},{Y:F0},{Z:F0}), hp={HP}%, hostiles={Hostiles}, aggressors={Aggressors}, map={Map}",
                _updateCount, _isLeader, _waypointIndex, _waypoints.Count,
                player.Position.X, player.Position.Y, player.Position.Z,
                player.HealthPercent, ObjectManager.Hostiles.Count(), ObjectManager.Aggressors.Count(), player.MapId);
        }

        // Wrong-map guard: stop and wait for coordinator to re-teleport
        if (_targetMapId != 0 && player.MapId != _targetMapId)
        {
            ObjectManager.StopAllMovement();
            if (_updateCount % 50 == 1)
                Log.Warning("[DUNGEONEERING] On wrong map {Map} (expected {Expected}), waiting for teleport",
                    player.MapId, _targetMapId);
            return;
        }

        // =====================================================================
        // STEP 1: ALL bots — if aggressors exist, stop and fight.
        // This is the SAME for leader and followers. The group fights together.
        // After combat timeout pops PvERotation, there's a cooldown period where
        // the bot navigates instead of re-engaging — this prevents the infinite
        // combat loop when GM bots can't kill mobs.
        // =====================================================================
        bool inCombatCooldown = (DateTime.UtcNow - _lastCombatPush).TotalSeconds < CombatRepushCooldownSec;
        if (ObjectManager.Aggressors.Any() && !inCombatCooldown)
        {
            ObjectManager.StopAllMovement();
            ClearNavigation();

            // Mark skull on closest aggressor so the whole raid assists
            var aggressor = ObjectManager.Aggressors
                .OrderBy(a => player.Position.DistanceTo(a.Position))
                .First();
            if (_isLeader)
            {
                ObjectManager.SetTarget(aggressor.Guid);
                ObjectManager.SetRaidTarget(aggressor, TargetMarker.Skull);
            }

            // Push PvE rotation if not already fighting
            if (BotTasks.Count <= 1 || BotTasks.Peek() == this)
            {
                _lastCombatPush = DateTime.UtcNow;
                BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
            }

            return;
        }

        // =====================================================================
        // STEP 2: No aggressors — role-specific behavior
        // =====================================================================
        if (_isLeader)
        {
            // LEADER: check for hostiles to pull, or advance to next waypoint
            // Only pull hostiles if NOT in combat cooldown. During cooldown,
            // the leader navigates past mobs instead of pulling new ones.
            if (!inCombatCooldown)
            {
                var hostileInRange = ObjectManager.Hostiles
                    .Where(h => h.Health > 0
                        && h.Position != null
                        && player.Position.DistanceTo(h.Position) < HostilePullRange
                        && player.InLosWith(h))
                    .OrderBy(h => player.Position.DistanceTo(h.Position))
                    .FirstOrDefault();

                if (hostileInRange != null)
                {
                    // Pull: stop, target, mark skull, push PullTargetTask
                    ObjectManager.StopAllMovement();
                    ClearNavigation();
                    ObjectManager.SetTarget(hostileInRange.Guid);
                    ObjectManager.SetRaidTarget(hostileInRange, TargetMarker.Skull);

                    Log.Information("[DUNGEONEERING] Leader pulling: {Name} (0x{Guid:X}) at {Dist:F0}y",
                        hostileInRange.Name, hostileInRange.Guid, player.Position.DistanceTo(hostileInRange.Position));

                    _lastCombatPush = DateTime.UtcNow;
                    BotTasks.Push(Container.ClassContainer.CreatePullTargetTask(BotContext));
                    return;
                }
            }

            // No hostiles — advance to next waypoint
            if (_waypointIndex >= _waypoints.Count)
            {
                Log.Information("[DUNGEONEERING] Dungeon clear! All waypoints visited.");
                ObjectManager.StopAllMovement();
                PopTask("dungeon_clear");
                return;
            }

            var destination = _waypoints[_waypointIndex];
            if (player.Position.DistanceTo(destination) < WaypointReachDistance)
            {
                _waypointIndex++;
                ClearNavigation();
                Log.Information("[DUNGEONEERING] Reached waypoint {Index}/{Total}",
                    _waypointIndex, _waypoints.Count);
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
        else
        {
            // FOLLOWER: path to the party leader. If close enough, stop.
            var partyLeader = ObjectManager.PartyLeader;

            if (partyLeader?.Position == null)
            {
                // No leader visible — navigate waypoints as fallback (inside dungeon only)
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

            var distToLeader = player.Position.DistanceTo(partyLeader.Position);

            if (distToLeader > FollowDistance)
            {
                TryNavigateToward(partyLeader.Position, allowDirectFallback: true);
            }
            else
            {
                ObjectManager.StopAllMovement();
                ClearNavigation();
            }
        }

        // Buff task intentionally NOT pushed every cycle — the BG bot's buff detection
        // may not properly track active auras, causing repeated cast spam. Buffs are
        // handled by the coordinator's prep phase and the combat rotation's own logic.
    }
}
