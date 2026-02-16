using ForegroundBotRunner.Objects;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;

namespace ForegroundBotRunner.Grouping
{
    public enum DungeonPhase
    {
        Idle,
        FollowingRoute,
        ClearingTrash,
        WaitingForGroup,
        BossEncounter,
        Looting,
        Complete
    }

    /// <summary>
    /// Follows a dungeon route, managing waypoint progression, trash clearing,
    /// and boss encounter transitions. Works with GroupManager for role-based behavior.
    /// </summary>
    public class DungeonNavigator(ObjectManager objectManager, GroupManager groupManager)
    {
        private readonly ObjectManager _objectManager = objectManager;
        private readonly GroupManager _groupManager = groupManager;
        private DungeonData? _dungeon;
        private int _currentWaypointIndex;
        private DungeonPhase _phase = DungeonPhase.Idle;
        private int _phaseStartTick;
        private int _lastLogTick;

        private const float WAYPOINT_REACH_DISTANCE = 5f;
        private const float BOSS_ENGAGE_DISTANCE = 30f;
        private const int WAIT_TIMEOUT_MS = 30000;
        private const int LOG_INTERVAL_MS = 5000;

        public DungeonPhase CurrentPhase => _phase;
        public DungeonData? CurrentDungeon => _dungeon;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public bool IsActive => _dungeon != null && _phase != DungeonPhase.Idle && _phase != DungeonPhase.Complete;

        /// <summary>
        /// Check if current map ID is a known dungeon and activate navigation.
        /// Call when entering a new map.
        /// </summary>
        public bool TryActivate(uint mapId)
        {
            var dungeon = DungeonRegistry.GetByMapId(mapId);
            if (dungeon == null) return false;

            _dungeon = dungeon;
            _currentWaypointIndex = 0;
            SetPhase(DungeonPhase.FollowingRoute);
            Log.Information("[DungeonNav] Activated for {Name} (mapId={MapId}, {Count} waypoints)",
                dungeon.Name, dungeon.MapId, dungeon.Route.Length);
            return true;
        }

        /// <summary>
        /// Deactivate dungeon navigation (e.g., on zone change).
        /// </summary>
        public void Deactivate()
        {
            if (_dungeon != null)
                Log.Information("[DungeonNav] Deactivated (was in {Name})", _dungeon.Name);
            _dungeon = null;
            _phase = DungeonPhase.Idle;
        }

        /// <summary>
        /// Main update - returns a position to move toward, or null if no movement needed.
        /// The caller (GrindBot) handles actual movement and combat.
        /// </summary>
        public Position? Update(IWoWLocalPlayer player)
        {
            if (_dungeon == null || _phase == DungeonPhase.Idle || _phase == DungeonPhase.Complete)
                return null;

            if (_dungeon.Route.Length == 0)
            {
                SetPhase(DungeonPhase.Complete);
                return null;
            }

            switch (_phase)
            {
                case DungeonPhase.FollowingRoute:
                    return UpdateFollowRoute(player);

                case DungeonPhase.ClearingTrash:
                    return UpdateClearTrash(player);

                case DungeonPhase.WaitingForGroup:
                    return UpdateWaitForGroup(player);

                case DungeonPhase.BossEncounter:
                    return UpdateBossEncounter(player);

                case DungeonPhase.Looting:
                    return UpdateLooting(player);

                default:
                    return null;
            }
        }

        private Position? UpdateFollowRoute(IWoWLocalPlayer player)
        {
            if (_currentWaypointIndex >= _dungeon!.Route.Length)
            {
                Log.Information("[DungeonNav] Route complete!");
                SetPhase(DungeonPhase.Complete);
                return null;
            }

            var waypoint = _dungeon.Route[_currentWaypointIndex];
            var waypointPos = waypoint.ToPosition();
            var distance = player.Position.DistanceTo(waypointPos);

            // Check if nearby mobs need clearing
            if (HasNearbyHostiles(player))
            {
                SetPhase(DungeonPhase.ClearingTrash);
                return null; // Let GrindBot handle combat
            }

            // Reached waypoint
            if (distance < WAYPOINT_REACH_DISTANCE)
            {
                LogPeriodic($"Reached waypoint #{_currentWaypointIndex} ({waypoint.Action})");

                switch (waypoint.Action)
                {
                    case "boss":
                        // Transition to boss encounter
                        SetPhase(DungeonPhase.BossEncounter);
                        return null;

                    case "wait":
                        // Wait for group to catch up
                        SetPhase(DungeonPhase.WaitingForGroup);
                        return null;

                    case "clear":
                        // Clear area before advancing
                        if (HasNearbyHostiles(player))
                        {
                            SetPhase(DungeonPhase.ClearingTrash);
                            return null;
                        }
                        AdvanceWaypoint();
                        return UpdateFollowRoute(player);

                    default: // "move"
                        AdvanceWaypoint();
                        if (_currentWaypointIndex >= _dungeon.Route.Length)
                        {
                            SetPhase(DungeonPhase.Complete);
                            return null;
                        }
                        return _dungeon.Route[_currentWaypointIndex].ToPosition();
                }
            }

            // Non-leader follows leader position instead of route
            if (_groupManager is { IsInGroup: true, IsLeader: false })
            {
                var leaderPos = _groupManager.GetLeaderPosition();
                if (leaderPos != null)
                    return leaderPos;
            }

            LogPeriodic($"Route wp#{_currentWaypointIndex}/{_dungeon.Route.Length} ({distance:F0}y away)");
            return waypointPos;
        }

        private Position? UpdateClearTrash(IWoWLocalPlayer player)
        {
            // Stay in ClearingTrash until no hostiles nearby
            if (!HasNearbyHostiles(player) && !player.IsInCombat)
            {
                SetPhase(DungeonPhase.FollowingRoute);
                return null;
            }

            // Return null to let GrindBot's combat system handle the fighting
            return null;
        }

        private Position? UpdateWaitForGroup(IWoWLocalPlayer player)
        {
            // Check if all party members are nearby
            if (_groupManager.IsInGroup)
            {
                var allNear = _objectManager.PartyMembers
                    .All(m => player.Position.DistanceTo(m.Position) < 20f);

                if (allNear)
                {
                    AdvanceWaypoint();
                    SetPhase(DungeonPhase.FollowingRoute);
                    return null;
                }
            }

            // Timeout - advance anyway
            if (Environment.TickCount - _phaseStartTick > WAIT_TIMEOUT_MS)
            {
                Log.Warning("[DungeonNav] Wait timeout, advancing");
                AdvanceWaypoint();
                SetPhase(DungeonPhase.FollowingRoute);
            }

            LogPeriodic("Waiting for group...");
            return null;
        }

        private Position? UpdateBossEncounter(IWoWLocalPlayer player)
        {
            var waypoint = _dungeon!.Route[_currentWaypointIndex];

            // Find the boss by name or creature ID
            BossEncounter? boss = null;
            if (waypoint.BossName != null)
                boss = _dungeon.Bosses.FirstOrDefault(b => b.Name == waypoint.BossName);

            // Check if boss is dead (no longer in object list or health=0)
            if (boss != null)
            {
                var bossUnit = _objectManager.Units
                    .OfType<WoWUnit>()
                    .FirstOrDefault(u => u.Entry == boss.CreatureId && u.Health > 0);

                if (bossUnit == null)
                {
                    // Boss is dead or despawned - advance route
                    Log.Information("[DungeonNav] Boss '{Boss}' defeated!", boss.Name);
                    AdvanceWaypoint();
                    SetPhase(DungeonPhase.FollowingRoute);
                    return null;
                }

                // Return role-based positioning for the boss fight
                if (_groupManager.MyRole == GroupRole.Tank && boss.TankPosition != null)
                    return boss.TankPosition.ToPosition();
                if (_groupManager.MyRole != GroupRole.Tank && boss.RangedPosition != null)
                    return boss.RangedPosition.ToPosition();
            }

            // Default: if in combat, let GrindBot handle it
            if (player.IsInCombat)
                return null;

            // Not in combat and no boss found → advance
            if (!HasNearbyHostiles(player))
            {
                AdvanceWaypoint();
                SetPhase(DungeonPhase.FollowingRoute);
            }

            return null;
        }

        private Position? UpdateLooting(IWoWLocalPlayer player)
        {
            // Brief pause for looting, then continue route
            if (Environment.TickCount - _phaseStartTick > 3000)
            {
                SetPhase(DungeonPhase.FollowingRoute);
            }
            return null;
        }

        private void AdvanceWaypoint()
        {
            _currentWaypointIndex++;
            Log.Debug("[DungeonNav] Advanced to waypoint #{Index}/{Total}",
                _currentWaypointIndex, _dungeon?.Route.Length ?? 0);
        }

        private void SetPhase(DungeonPhase phase)
        {
            if (_phase != phase)
            {
                Log.Debug("[DungeonNav] {Old} -> {New}", _phase, phase);
                _phase = phase;
                _phaseStartTick = Environment.TickCount;
            }
        }

        private bool HasNearbyHostiles(IWoWLocalPlayer player)
        {
            try
            {
                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Any(u => u is not WoWPlayer &&
                             u.Health > 0 &&
                             !u.NotAttackable &&
                             u.Position.DistanceTo(player.Position) < 25f &&
                             (u.IsInCombat || u.UnitReaction <= UnitReaction.Hostile));
            }
            catch { return false; }
        }

        /// <summary>
        /// Get the position offset for formation positioning relative to a center point.
        /// Tank is at the front, healer at the back, DPS on flanks.
        /// </summary>
        public Position GetFormationPosition(Position center, float facing, GroupRole role)
        {
            // Calculate offsets based on role
            var (dx, dy) = role switch
            {
                GroupRole.Tank => (MathF.Cos(facing) * 3f, MathF.Sin(facing) * 3f),       // 3y ahead
                GroupRole.Healer => (-MathF.Cos(facing) * 8f, -MathF.Sin(facing) * 8f),   // 8y behind
                _ => (MathF.Sin(facing) * 4f, -MathF.Cos(facing) * 4f)                     // 4y to the side
            };

            return new Position(center.X + dx, center.Y + dy, center.Z);
        }

        private void LogPeriodic(string message)
        {
            if (Environment.TickCount - _lastLogTick > LOG_INTERVAL_MS)
            {
                Log.Debug("[DungeonNav] {Message}", message);
                _lastLogTick = Environment.TickCount;
            }
        }
    }
}
