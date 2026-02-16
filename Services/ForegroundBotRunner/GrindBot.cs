using BotRunner.Clients;
using BotRunner.Interfaces;
using ForegroundBotRunner.CombatRotations;
using ForegroundBotRunner.Grouping;
using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using ForegroundBotRunner.Questing;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner
{
    public enum GrindBotPhase
    {
        Idle,
        FindTarget,
        MoveToTarget,
        Combat,
        Loot,
        Rest,
        Dead
    }

    /// <summary>
    /// Simple grind bot that finds hostile mobs, auto-attacks them, loots, and repeats.
    /// Uses a flat enum-based state machine called from the ForegroundBotWorker main loop.
    /// </summary>
    public class GrindBot
    {
        private readonly ObjectManager _objectManager;
        private readonly NavigationPath _navigation;
        private readonly QuestCoordinator? _questCoordinator;
        private readonly GroupManager? _groupManager;
        private readonly DungeonNavigator? _dungeonNavigator;
        private readonly Random _random = new();
        private readonly Action<string>? _diagLog;
        private ICombatRotation? _rotation;
        private GrindBotPhase _phase = GrindBotPhase.Idle;
        private WoWUnit? _currentTarget;
        private int _phaseStartTime;
        private int _lootAttempts;
        private bool _autoAttackStarted;
        private int _lastLogTick;
        private uint _lastMapId;

        // Stuck detection
        private Position? _lastPosition;
        private int _lastStuckCheckTick;
        private int _stuckDuration;
        private int _stuckCount;
        private bool _isUnstucking;
        private int _unstuckStartTick;
        private Position? _unstuckStartPosition;

        // Target blacklist (guid → expiry tick)
        private readonly Dictionary<ulong, int> _blacklist = new();
        private const int BLACKLIST_DURATION_MS = 120_000; // 2 minutes

        // Exploration (when no targets nearby)
        private Position? _exploreTarget;
        private Position? _exploreOrigin; // Where we started exploring from
        private int _exploreCount; // Number of explore legs completed
        private readonly List<Position> _recentExplorePositions = new();
        private const float EXPLORE_REACH_DISTANCE = 8f;
        private const float EXPLORE_MIN_RADIUS = 40f;
        private const float EXPLORE_MAX_RADIUS = 80f;
        private const int MAX_RECENT_EXPLORE = 10;

        // Tuning constants
        private const float AGGRO_RANGE = 80f;
        private const float MELEE_RANGE = 5f;
        private const float LOOT_RANGE = 5f;
        private const int LOOT_TIMEOUT_MS = 8000;
        private const int COMBAT_TIMEOUT_MS = 60000;
        private const int REST_THRESHOLD_PCT = 50;
        private const int REST_RESUME_PCT = 80;
        private const int LOG_INTERVAL_MS = 5000;
        private const float STUCK_THRESHOLD = 1.0f;    // Must move >1y per check interval to not be stuck
        private const int STUCK_DETECTION_MS = 3000;   // 3 seconds of no movement before triggering
        private const int UNSTUCK_DURATION_MS = 1000;  // 1 second per unstuck attempt

        public GrindBotPhase CurrentPhase => _phase;

        public GrindBot(ObjectManager objectManager, PathfindingClient? pathfindingClient = null, IQuestRepository? questRepo = null, GroupManager? groupManager = null, Action<string>? diagLog = null)
        {
            _objectManager = objectManager;
            _navigation = new NavigationPath(pathfindingClient);
            _groupManager = groupManager;
            _diagLog = diagLog;
            if (groupManager != null)
                _dungeonNavigator = new DungeonNavigator(objectManager, groupManager);
            if (questRepo != null)
                _questCoordinator = new QuestCoordinator(questRepo, objectManager);
        }

        public void Update()
        {
            try
            {
                var player = _objectManager.Player;
                if (player == null) return;

                // Update group manager (invite handling, role detection)
                _groupManager?.Update();

                // Detect dungeon zone changes
                var currentMapId = _objectManager.ContinentId;
                if (currentMapId != _lastMapId && _dungeonNavigator != null)
                {
                    _lastMapId = currentMapId;
                    if (DungeonRegistry.IsDungeon(currentMapId))
                        _dungeonNavigator.TryActivate(currentMapId);
                    else
                        _dungeonNavigator.Deactivate();
                }

                // Handle loot rolls in group (greed on everything by default)
                if (_groupManager is { IsInGroup: true })
                    HandleLootRolls();

                // Death check overrides everything
                if (player.Health <= 0)
                {
                    if (_phase != GrindBotPhase.Dead)
                        SetPhase(GrindBotPhase.Dead);
                }

                // Ghost form check
                if (player is LocalPlayer lp && lp.InGhostForm && _phase != GrindBotPhase.Dead)
                {
                    SetPhase(GrindBotPhase.Dead);
                }

                switch (_phase)
                {
                    case GrindBotPhase.Idle:
                        UpdateIdle();
                        break;
                    case GrindBotPhase.FindTarget:
                        UpdateFindTarget();
                        break;
                    case GrindBotPhase.MoveToTarget:
                        UpdateMoveToTarget();
                        break;
                    case GrindBotPhase.Combat:
                        UpdateCombat();
                        break;
                    case GrindBotPhase.Loot:
                        UpdateLoot();
                        break;
                    case GrindBotPhase.Rest:
                        UpdateRest();
                        break;
                    case GrindBotPhase.Dead:
                        UpdateDead();
                        break;
                }
            }
            catch (AccessViolationException)
            {
                // Stale pointer - target likely despawned
                Log.Warning("[GrindBot] Access violation in {Phase}, resetting to FindTarget", _phase);
                _diagLog?.Invoke($"[GrindBot] ACCESS VIOLATION in {_phase}, resetting to FindTarget");
                _currentTarget = null;
                _objectManager.StopAllMovement();
                SetPhase(GrindBotPhase.FindTarget);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] Update error in {Phase}", _phase);
                _diagLog?.Invoke($"[GrindBot] EXCEPTION in {_phase}: {ex.Message}");
            }
        }

        private void SetPhase(GrindBotPhase newPhase)
        {
            if (_phase != newPhase)
            {
                Log.Information("[GrindBot] {OldPhase} -> {NewPhase}", _phase, newPhase);
                _diagLog?.Invoke($"[GrindBot] {_phase} -> {newPhase}");
                _phase = newPhase;
                _phaseStartTime = Environment.TickCount;
                _navigation.Clear();
                ResetStuckState();

                // Reset expanding explore when transitioning to combat or loot
                if (newPhase == GrindBotPhase.MoveToTarget || newPhase == GrindBotPhase.Combat)
                {
                    _exploreOrigin = null;
                    _exploreTarget = null;
                }
            }
        }

        private void LogPeriodic(string message)
        {
            if (Environment.TickCount - _lastLogTick > LOG_INTERVAL_MS)
            {
                Log.Debug("[GrindBot] {Message}", message);
                _lastLogTick = Environment.TickCount;
            }
        }

        // ── State Handlers ──

        private void UpdateIdle()
        {
            SetPhase(GrindBotPhase.FindTarget);
        }

        private void UpdateFindTarget()
        {
            var player = _objectManager.Player;
            if (player == null) return;

            // Check if we need to rest first
            if (ShouldRest(player))
            {
                SetPhase(GrindBotPhase.Rest);
                return;
            }

            // Check for aggressors first (mobs attacking us or party members)
            var aggressor = FindAggressor(player);
            if (aggressor == null && player.IsInCombat)
            {
                // Fallback: player is in combat but FindAggressor didn't find a mob targeting us.
                // Look for nearby in-combat mobs (handles neutral mobs like boars whose TargetGuid
                // may not read correctly from memory).
                aggressor = FindNearbyCombatMob(player);
            }
            if (aggressor != null)
            {
                _currentTarget = aggressor;
                TargetUnit(aggressor);
                _autoAttackStarted = false;
                SetPhase(GrindBotPhase.Combat);
                return;
            }

            // Group: check for group aggressors (mobs attacking other party members)
            if (_groupManager is { IsInGroup: true })
            {
                var groupTarget = _groupManager.FindGroupTarget();
                if (groupTarget != null)
                {
                    _currentTarget = groupTarget;
                    TargetUnit(groupTarget);
                    _autoAttackStarted = false;
                    SetPhase(GrindBotPhase.Combat);
                    return;
                }
            }

            // Apply buffs while searching
            if (_rotation != null && player is LocalPlayer lp2)
            {
                try { _rotation.Buff(lp2); } catch { }
            }

            // Group: non-leader follows the leader when out of range
            if (_groupManager is { IsInGroup: true, IsLeader: false })
            {
                if (_groupManager.ShouldFollowLeader())
                {
                    var leaderPos = _groupManager.GetLeaderPosition();
                    if (leaderPos != null)
                    {
                        var mapId = _objectManager.ContinentId;
                        var nextWaypoint = _navigation.GetNextWaypoint(player.Position, leaderPos, mapId);
                        if (nextWaypoint != null)
                        {
                            _objectManager.Face(nextWaypoint);
                            _objectManager.StartMovement(ControlBits.Front);
                        }
                        LogPeriodic($"Following leader ({player.Position.DistanceTo(leaderPos):F0}y away)");
                        return;
                    }
                }
            }

            // Dungeon navigation: follow route when in a dungeon instance
            if (_dungeonNavigator is { IsActive: true })
            {
                var dungeonTarget = _dungeonNavigator.Update(player);
                if (dungeonTarget != null)
                {
                    var mapId = _objectManager.ContinentId;
                    var nextWaypoint = _navigation.GetNextWaypoint(player.Position, dungeonTarget, mapId);
                    if (nextWaypoint != null)
                    {
                        _objectManager.Face(nextWaypoint);
                        _objectManager.StartMovement(ControlBits.Front);
                    }
                    LogPeriodic($"Dungeon: {_dungeonNavigator.CurrentPhase} wp#{_dungeonNavigator.CurrentWaypointIndex}");
                    return;
                }
                // null return from dungeon navigator means combat or waiting - fall through to normal target finding
            }

            // Check quest coordinator for priority actions (turn-in, pickup, move to objective)
            if (_questCoordinator != null)
            {
                var questTarget = _questCoordinator.Update(player);
                if (questTarget != null)
                {
                    var mapId = _objectManager.ContinentId;
                    var nextWaypoint = _navigation.GetNextWaypoint(player.Position, questTarget, mapId);
                    if (nextWaypoint != null)
                    {
                        _objectManager.Face(nextWaypoint);
                        _objectManager.StartMovement(ControlBits.Front);
                    }
                    LogPeriodic($"Quest: {_questCoordinator.CurrentAction}");
                    return;
                }
            }

            // Find nearest hostile mob (leader finds targets; non-leaders only solo-grind if near leader)
            var target = FindNearestHostile(player);
            if (target != null)
            {
                _diagLog?.Invoke($"[GrindBot] Found hostile: {target.Name} ({target.Guid:X}) at {target.Position.DistanceTo(player.Position):F0}y, hp={target.Health}");
                _currentTarget = target;
                TargetUnit(target);
                _autoAttackStarted = false;
                SetPhase(GrindBotPhase.MoveToTarget);
                return;
            }

            // No targets - explore with pathfinding, biased forward to avoid circling
            {
                if (_exploreTarget == null || player.Position.DistanceTo(_exploreTarget) < EXPLORE_REACH_DISTANCE)
                {
                    _exploreTarget = PickExplorePosition(player);
                    _navigation.Clear();
                    _diagLog?.Invoke($"[GrindBot] New explore target: ({_exploreTarget.X:F0},{_exploreTarget.Y:F0},{_exploreTarget.Z:F0}), origin=({_exploreOrigin?.X:F0},{_exploreOrigin?.Y:F0}), count={_exploreCount}");
                    Log.Debug("[GrindBot] New explore target: ({X:F0},{Y:F0},{Z:F0})",
                        _exploreTarget.X, _exploreTarget.Y, _exploreTarget.Z);
                }

                var exploreMapId = _objectManager.ContinentId;
                var exploreWp = _navigation.GetNextWaypoint(player.Position, _exploreTarget, exploreMapId);
                if (exploreWp != null)
                {
                    _objectManager.Face(exploreWp);
                    _objectManager.StartMovement(ControlBits.Front);
                }
                else
                {
                    _diagLog?.Invoke($"[GrindBot] GetNextWaypoint returned NULL for explore target ({_exploreTarget.X:F0},{_exploreTarget.Y:F0})");
                }

                LogPeriodic($"Exploring toward ({_exploreTarget.X:F0},{_exploreTarget.Y:F0}) - no targets within {AGGRO_RANGE}y");
            }
        }

        private void UpdateMoveToTarget()
        {
            var player = _objectManager.Player;
            if (player == null || _currentTarget == null)
            {
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Target died while approaching
            if (_currentTarget.Health == 0)
            {
                _objectManager.StopAllMovement();
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Got pulled into combat
            if (player.IsInCombat)
            {
                _objectManager.StopAllMovement();
                _autoAttackStarted = false;
                SetPhase(GrindBotPhase.Combat);
                return;
            }

            // Stuck detection
            if (CheckIfStuck(player))
            {
                if (_stuckCount > 20)
                {
                    Log.Warning("[GrindBot] Stuck too many times moving to target, blacklisting");
                    _diagLog?.Invoke($"[GrindBot] Blacklisting target {_currentTarget.Guid:X} after {_stuckCount} stuck events");
                    BlacklistTarget(_currentTarget.Guid);
                    _objectManager.StopAllMovement();
                    _currentTarget = null;
                    SetPhase(GrindBotPhase.FindTarget);
                }
                else if (Environment.TickCount - _lastLogTick > LOG_INTERVAL_MS)
                {
                    _diagLog?.Invoke($"[GrindBot] STUCK #{_stuckCount}, unstucking={_isUnstucking}, moveFlags=0x{player.MovementFlags:X}");
                }
                return; // Let unstuck movement play out
            }

            var combatRange = _rotation?.DesiredRange ?? MELEE_RANGE;
            var distance = player.Position.DistanceTo(_currentTarget.Position);

            // Periodic diagnostic for MoveToTarget
            if (Environment.TickCount - _lastLogTick > LOG_INTERVAL_MS)
            {
                _diagLog?.Invoke($"[GrindBot] MoveToTarget: dist={distance:F1}y, combatRange={combatRange:F1}, target=({_currentTarget.Position.X:F0},{_currentTarget.Position.Y:F0},{_currentTarget.Position.Z:F0}), facing={player.Facing:F2}, moveFlags=0x{player.MovementFlags:X}");
            }

            if (distance <= combatRange)
            {
                _objectManager.StopAllMovement();
                _autoAttackStarted = false;
                SetPhase(GrindBotPhase.Combat);
                return;
            }

            // Ranged pull: if within pull range, stop and cast pull spell
            var pullRange = _rotation?.PullRange ?? 0f;
            if (pullRange > 0 && distance <= pullRange && player is LocalPlayer lp2)
            {
                // Initialize rotation if needed
                _rotation ??= CombatRotationFactory.Create(lp2);
                _objectManager.StopAllMovement();
                _objectManager.Face(_currentTarget.Position);
                if (_rotation.Pull(lp2, _currentTarget))
                {
                    Log.Debug("[GrindBot] Pull spell cast at {Distance:F0}y", distance);
                    _autoAttackStarted = false;
                    SetPhase(GrindBotPhase.Combat);
                    return;
                }
                // Pull failed (no mana, spell not ready, etc.) - keep approaching
            }

            // Use pathfinding to navigate to target
            var mapId = _objectManager.ContinentId;

            // Close range: face target directly with instant SetFacing to avoid spiral orbiting.
            // The multi-step Face() overshoots when the target is nearby and moving.
            if (distance < 20f)
            {
                var targetPos = _currentTarget.Position;
                var dx = targetPos.X - player.Position.X;
                var dy = targetPos.Y - player.Position.Y;
                var angle = (float)Math.Atan2(dy, dx);
                if (angle < 0) angle += (float)(Math.PI * 2);
                _objectManager.SetFacing(angle);
                _objectManager.StartMovement(ControlBits.Front);
            }
            else
            {
                var nextWaypoint = _navigation.GetNextWaypoint(player.Position, _currentTarget.Position, mapId);
                if (nextWaypoint != null)
                {
                    _objectManager.Face(nextWaypoint);
                    _objectManager.StartMovement(ControlBits.Front);
                }
            }
        }

        private void UpdateCombat()
        {
            var player = _objectManager.Player;
            if (player == null)
            {
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Timeout check
            if (Environment.TickCount - _phaseStartTime > COMBAT_TIMEOUT_MS)
            {
                Log.Warning("[GrindBot] Combat timeout reached");
                if (_currentTarget != null)
                    BlacklistTarget(_currentTarget.Guid);
                _objectManager.StopAllMovement();
                _currentTarget = null;
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Target dead
            if (_currentTarget == null || _currentTarget.Health == 0)
            {
                _objectManager.StopAllMovement();

                // Check if target is lootable
                if (_currentTarget != null && _currentTarget.CanBeLooted)
                {
                    _lootAttempts = 0;
                    SetPhase(GrindBotPhase.Loot);
                    return;
                }

                // Check for more aggressors (including party members' attackers)
                var nextAggressor = FindAggressor(player);
                if (nextAggressor == null && _groupManager is { IsInGroup: true })
                    nextAggressor = _groupManager.FindGroupTarget();

                if (nextAggressor != null)
                {
                    _currentTarget = nextAggressor;
                    TargetUnit(nextAggressor);
                    _autoAttackStarted = false;
                    return;
                }

                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Initialize rotation on first combat (lazy, needs player class)
            if (_rotation == null && player is LocalPlayer localPlayer)
            {
                _rotation = CombatRotationFactory.Create(localPlayer);
            }

            // Ensure target is selected
            if (player.TargetGuid != _currentTarget.Guid)
            {
                TargetUnit(_currentTarget);
            }

            // Face target
            if (!player.IsFacing(_currentTarget.Position))
            {
                _objectManager.Face(_currentTarget.Position);
            }

            // Range management
            var combatRange = _rotation?.DesiredRange ?? MELEE_RANGE;
            var distance = player.Position.DistanceTo(_currentTarget.Position);
            if (distance > combatRange)
            {
                _objectManager.MoveToward(_currentTarget.Position);
            }
            else
            {
                if (player is WoWUnit playerUnit && playerUnit.IsMoving)
                {
                    _objectManager.StopAllMovement();
                }
            }

            // Start auto-attack (only once per target to avoid toggling)
            if (!_autoAttackStarted && distance <= combatRange + 1)
            {
                StartAutoAttack();
                _autoAttackStarted = true;
            }

            // Execute combat rotation
            if (_rotation != null && player is LocalPlayer lp2 && distance <= combatRange + 2)
            {
                var aggressorCount = CountAggressors(player);
                _rotation.Execute(lp2, _currentTarget, aggressorCount);
            }
        }

        private void UpdateLoot()
        {
            var player = _objectManager.Player;
            if (player == null || _currentTarget == null)
            {
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Check for aggressors while looting
            if (player.IsInCombat)
            {
                var aggressor = FindAggressor(player);
                if (aggressor != null)
                {
                    _currentTarget = aggressor;
                    TargetUnit(aggressor);
                    _autoAttackStarted = false;
                    SetPhase(GrindBotPhase.Combat);
                    return;
                }
            }

            // Timeout
            if (Environment.TickCount - _phaseStartTime > LOOT_TIMEOUT_MS)
            {
                Log.Debug("[GrindBot] Loot timeout");
                Functions.LuaCall("CloseLoot()");
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Move to corpse if needed
            var distance = player.Position.DistanceTo(_currentTarget.Position);
            if (distance > LOOT_RANGE)
            {
                // Stuck detection while moving to loot
                if (CheckIfStuck(player))
                {
                    if (_stuckCount > 5)
                    {
                        Log.Warning("[GrindBot] Stuck too many times moving to loot, skipping");
                        Functions.LuaCall("CloseLoot()");
                        SetPhase(GrindBotPhase.FindTarget);
                    }
                    return;
                }

                _objectManager.MoveToward(_currentTarget.Position);
                return;
            }

            _objectManager.StopAllMovement();

            // Loot sequence: interact → wait → loot all → close
            switch (_lootAttempts)
            {
                case 0:
                    // Right-click corpse to open loot frame
                    ThreadSynchronizer.RunOnMainThread(() =>
                    {
                        ((WoWObject)_currentTarget).Interact();
                    });
                    _lootAttempts++;
                    break;
                case 1:
                    // Loot all items via Lua
                    Functions.LuaCall("for i=1,GetNumLootItems() do LootSlot(i) end");
                    _lootAttempts++;
                    break;
                default:
                    // Close loot and move on
                    Functions.LuaCall("CloseLoot()");
                    SetPhase(GrindBotPhase.FindTarget);
                    break;
            }
        }

        private void UpdateRest()
        {
            var player = _objectManager.Player;
            if (player == null)
            {
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Interrupted by combat
            if (player.IsInCombat)
            {
                var aggressor = FindAggressor(player);
                if (aggressor != null)
                {
                    _currentTarget = aggressor;
                    TargetUnit(aggressor);
                    _autoAttackStarted = false;
                    SetPhase(GrindBotPhase.Combat);
                    return;
                }
            }

            var hpPct = GetHealthPct(player);
            var isLocalPlayer = player is LocalPlayer;
            var manaPct = isLocalPlayer ? RestHelper.GetManaPct((LocalPlayer)player) : 100f;
            var usesMana = isLocalPlayer && RestHelper.UsesMana((LocalPlayer)player);

            // Check if fully recovered
            bool healthOk = hpPct >= REST_RESUME_PCT || (hpPct >= REST_THRESHOLD_PCT && !(player is WoWPlayer wp && wp.IsEating));
            bool manaOk = !usesMana || manaPct >= REST_RESUME_PCT || (manaPct >= 60 && !(player is WoWPlayer wp2 && wp2.IsDrinking));

            if (healthOk && manaOk)
            {
                SetPhase(GrindBotPhase.FindTarget);
                return;
            }

            // Apply buffs while resting
            if (_rotation != null && isLocalPlayer)
            {
                try { _rotation.Buff((LocalPlayer)player); } catch { }
            }

            // Try to eat food if health is low
            if (hpPct < REST_RESUME_PCT && player is WoWPlayer foodPlayer)
            {
                RestHelper.TryEatFood(foodPlayer);
            }

            // Try to drink water if mana is low
            if (usesMana && manaPct < REST_RESUME_PCT && player is WoWPlayer drinkPlayer)
            {
                RestHelper.TryDrinkWater(drinkPlayer);
            }

            LogPeriodic($"Resting... HP={hpPct:F0}% Mana={manaPct:F0}%");
        }

        private void UpdateDead()
        {
            var player = _objectManager.Player;
            if (player == null) return;

            // Alive and not ghost → recovered
            if (player.Health > 0 && !(player is LocalPlayer lp && lp.InGhostForm))
            {
                _currentTarget = null;
                SetPhase(GrindBotPhase.Rest);
                return;
            }

            // Need to release spirit
            if (!(player is LocalPlayer ghost && ghost.InGhostForm))
            {
                Log.Information("[GrindBot] Releasing spirit...");
                ThreadSynchronizer.RunOnMainThread(() =>
                {
                    Functions.ReleaseCorpse(((WoWObject)player).Pointer);
                });
                return;
            }

            // In ghost form → move to corpse
            if (player is LocalPlayer localPlayer)
            {
                var corpsePos = localPlayer.CorpsePosition;
                if (corpsePos != null && player.Position.DistanceTo(corpsePos) > 5)
                {
                    // Stuck detection during corpse run
                    if (CheckIfStuck(player))
                    {
                        if (_stuckCount > 10)
                        {
                            Log.Warning("[GrindBot] Stuck too many times during corpse run, resetting navigation");
                            _navigation.Clear();
                            ResetStuckState();
                        }
                        return;
                    }

                    // Use pathfinding for corpse run
                    var mapId = _objectManager.ContinentId;
                    var nextWaypoint = _navigation.GetNextWaypoint(player.Position, corpsePos, mapId);
                    if (nextWaypoint != null)
                        _objectManager.MoveToward(nextWaypoint);
                    LogPeriodic($"Moving to corpse... dist={player.Position.DistanceTo(corpsePos):F0}y wp={_navigation.RemainingWaypoints}");
                }
                else
                {
                    // At corpse → retrieve
                    _objectManager.StopAllMovement();
                    Log.Information("[GrindBot] Retrieving corpse...");
                    ThreadSynchronizer.RunOnMainThread(() =>
                    {
                        Functions.RetrieveCorpse();
                    });
                }
            }
        }

        // ── Explore Position Picking ──

        /// <summary>
        /// Pick an explore position that expands outward from the origin.
        /// Uses an expanding spiral: each successive leg pushes further from
        /// where exploration started, preventing circular loops.
        /// </summary>
        private Position PickExplorePosition(IWoWLocalPlayer player)
        {
            // Record where we started exploring (reset when we find a target)
            if (_exploreOrigin == null)
            {
                _exploreOrigin = player.Position;
                _exploreCount = 0;
            }

            // Expanding radius: start at 40-80y, grow by 20y per completed leg
            float minDist = EXPLORE_MIN_RADIUS + _exploreCount * 20f;
            float maxDist = EXPLORE_MAX_RADIUS + _exploreCount * 20f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Pick positions relative to the ORIGIN (not current position)
                // to ensure we spiral outward from where we started
                float angle = (float)(_random.NextDouble() * Math.PI * 2); // Full 360°
                float dist = minDist + (float)(_random.NextDouble() * (maxDist - minDist));

                var candidate = new Position(
                    _exploreOrigin.X + (float)(Math.Cos(angle) * dist),
                    _exploreOrigin.Y + (float)(Math.Sin(angle) * dist),
                    player.Position.Z);

                // Skip if too close to a recently visited explore point
                bool tooClose = false;
                foreach (var recent in _recentExplorePositions)
                {
                    if (recent.DistanceTo(candidate) < 30f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                _exploreCount++;
                TrackExplorePosition(candidate);
                return candidate;
            }

            // Fallback: clear history and push outward from origin
            _recentExplorePositions.Clear();
            _exploreCount++;
            float fallbackAngle = (float)(_random.NextDouble() * Math.PI * 2);
            float fallbackDist = maxDist;
            var fallback = new Position(
                _exploreOrigin.X + (float)(Math.Cos(fallbackAngle) * fallbackDist),
                _exploreOrigin.Y + (float)(Math.Sin(fallbackAngle) * fallbackDist),
                player.Position.Z);
            TrackExplorePosition(fallback);
            return fallback;
        }

        private void TrackExplorePosition(Position pos)
        {
            _recentExplorePositions.Add(pos);
            if (_recentExplorePositions.Count > MAX_RECENT_EXPLORE)
                _recentExplorePositions.RemoveAt(0);
        }

        // ── Stuck Detection ──

        /// <summary>
        /// Check if player hasn't moved significantly. Returns true if stuck was detected.
        /// Call every tick during movement phases.
        /// </summary>
        private bool CheckIfStuck(IWoWLocalPlayer player)
        {
            // If currently doing unstuck movement, check if done
            if (_isUnstucking)
            {
                if (Environment.TickCount - _unstuckStartTick > UNSTUCK_DURATION_MS)
                {
                    // Check if we moved enough
                    if (_unstuckStartPosition != null && player.Position.DistanceTo(_unstuckStartPosition) > 2)
                    {
                        _objectManager.StopAllMovement();
                        _isUnstucking = false;
                        return false;
                    }
                    // Try another random direction (stuckCount increments here too)
                    _stuckCount++;
                    DoUnstuckMovement();
                }
                return true; // Still unstucking
            }

            var now = Environment.TickCount;
            if (_lastPosition != null && player.Position.DistanceTo(_lastPosition) <= STUCK_THRESHOLD)
            {
                _stuckDuration += now - _lastStuckCheckTick;
            }
            else
            {
                _stuckDuration = 0;
            }

            _lastPosition = player.Position;
            _lastStuckCheckTick = now;

            if (_stuckDuration >= STUCK_DETECTION_MS)
            {
                _stuckDuration = 0;
                _stuckCount++;
                Log.Debug("[GrindBot] Stuck detected (count={Count})", _stuckCount);
                _objectManager.StopAllMovement();
                DoUnstuckMovement();
                return true;
            }

            return false;
        }

        private void DoUnstuckMovement()
        {
            _isUnstucking = true;
            _unstuckStartTick = Environment.TickCount;
            _unstuckStartPosition = _objectManager.Player?.Position;

            // CRITICAL: Stop all movement first to clear conflicting control bits
            _objectManager.StopAllMovement();

            var direction = _random.Next(0, 4);
            switch (direction)
            {
                case 0:
                    _objectManager.StartMovement(ControlBits.Front);
                    _objectManager.StartMovement(ControlBits.StrafeLeft);
                    break;
                case 1:
                    _objectManager.StartMovement(ControlBits.Front);
                    _objectManager.StartMovement(ControlBits.StrafeRight);
                    break;
                case 2:
                    _objectManager.StartMovement(ControlBits.Back);
                    _objectManager.StartMovement(ControlBits.StrafeLeft);
                    break;
                case 3:
                    _objectManager.StartMovement(ControlBits.Back);
                    _objectManager.StartMovement(ControlBits.StrafeRight);
                    break;
            }
            ThreadSynchronizer.SimulateSpacebarPress(); // Jump to clear obstacles
        }

        private void ResetStuckState()
        {
            _lastPosition = null;
            _stuckDuration = 0;
            _stuckCount = 0;
            _isUnstucking = false;
        }

        /// <summary>
        /// Blacklist a target so we skip it for 2 minutes.
        /// </summary>
        private void BlacklistTarget(ulong guid)
        {
            _blacklist[guid] = Environment.TickCount + BLACKLIST_DURATION_MS;
            Log.Information("[GrindBot] Blacklisted target {Guid} for {Duration}s", guid, BLACKLIST_DURATION_MS / 1000);
        }

        private bool IsBlacklisted(ulong guid)
        {
            if (_blacklist.TryGetValue(guid, out var expiry))
            {
                if (Environment.TickCount < expiry)
                    return true;
                _blacklist.Remove(guid);
            }
            return false;
        }

        // ── Helper Methods ──

        private bool ShouldRest(IWoWLocalPlayer player)
        {
            if (player.IsInCombat) return false;
            var hpPct = GetHealthPct(player);
            if (hpPct < REST_THRESHOLD_PCT) return true;

            // Also rest if mana is low (for caster classes)
            if (player is LocalPlayer lp && RestHelper.UsesMana(lp))
            {
                var manaPct = RestHelper.GetManaPct(lp);
                if (manaPct < 30) return true;
            }

            return false;
        }

        private static float GetHealthPct(IWoWLocalPlayer player)
        {
            return player.MaxHealth > 0 ? (float)player.Health / player.MaxHealth * 100 : 100;
        }

        private WoWUnit? FindAggressor(IWoWLocalPlayer player)
        {
            var playerGuid = player.Guid;

            try
            {
                // Build set of GUIDs to check (self + party members if in group)
                var targetGuids = new HashSet<ulong> { playerGuid };
                if (_groupManager is { IsInGroup: true })
                {
                    foreach (var member in _objectManager.PartyMembers)
                        targetGuids.Add(member.Guid);
                }

                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u is not WoWPlayer &&
                               u.Health > 0 &&
                               targetGuids.Contains(u.TargetGuid) &&
                               u.IsInCombat &&
                               !u.NotAttackable)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] FindAggressor error");
                return null;
            }
        }

        /// <summary>
        /// Fallback for when the player is in combat but FindAggressor didn't match any mob.
        /// Finds the nearest non-player mob that is in combat and within aggro range.
        /// </summary>
        private WoWUnit? FindNearbyCombatMob(IWoWLocalPlayer player)
        {
            try
            {
                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u is not WoWPlayer &&
                               u.Health > 0 &&
                               u.IsInCombat &&
                               !u.NotAttackable &&
                               u.Position.DistanceTo(player.Position) <= AGGRO_RANGE)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] FindNearbyCombatMob error");
                return null;
            }
        }

        private int CountAggressors(IWoWLocalPlayer player)
        {
            try
            {
                var targetGuids = new HashSet<ulong> { player.Guid };
                if (_groupManager is { IsInGroup: true })
                {
                    foreach (var member in _objectManager.PartyMembers)
                        targetGuids.Add(member.Guid);
                }

                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Count(u => u is not WoWPlayer &&
                               u.Health > 0 &&
                               targetGuids.Contains(u.TargetGuid) &&
                               u.IsInCombat);
            }
            catch { return 1; }
        }

        private WoWUnit? FindNearestHostile(IWoWLocalPlayer player)
        {
            nint playerPtr;
            try
            {
                playerPtr = ((WoWObject)player).Pointer;
            }
            catch
            {
                return null;
            }

            // Pre-filter candidates using memory reads only (fast)
            List<WoWUnit> candidates;
            try
            {
                var inDungeon = _dungeonNavigator is { IsActive: true };
                candidates = _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u is not WoWPlayer &&
                               u.Health > 0 &&
                               !u.NotAttackable &&
                               !u.TappedByOther &&
                               !u.IsPet &&
                               !IsBlacklisted(u.Guid) &&
                               // In dungeons, allow targeting elites (but never bosses alone)
                               (inDungeon || (u.CreatureRank != CreatureRank.Elite &&
                                              u.CreatureRank != CreatureRank.RareElite)) &&
                               u.CreatureRank != CreatureRank.Boss &&
                               u.Position.DistanceTo(player.Position) <= AGGRO_RANGE &&
                               (inDungeon || u.Level <= player.Level + 2))
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] FindNearestHostile filter error");
                return null;
            }

            // Periodic diagnostic: log candidate count
            if (Environment.TickCount - _lastLogTick > LOG_INTERVAL_MS)
                _diagLog?.Invoke($"[GrindBot] FindNearestHostile: {candidates.Count} candidates");

            // Check unit reaction for each candidate (native call via ThreadSynchronizer)
            foreach (var candidate in candidates)
            {
                try
                {
                    var reaction = ThreadSynchronizer.RunOnMainThread(() =>
                        Functions.GetUnitReaction(playerPtr, candidate.Pointer));

                    if (reaction <= UnitReaction.Neutral)
                    {
                        return candidate;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[GrindBot] GetUnitReaction error for unit {Guid}", candidate.Guid);
                }
            }

            return null;
        }

        private void TargetUnit(WoWUnit target)
        {
            try
            {
                ThreadSynchronizer.RunOnMainThread(() =>
                {
                    Functions.SetTarget(target.Guid);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] TargetUnit error");
            }
        }

        private void StartAutoAttack()
        {
            try
            {
                // Scan action bar for Attack action and activate if not already active
                Functions.LuaCall(
                    "for i=1,120 do " +
                        "if IsAttackAction(i) then " +
                            "if not IsCurrentAction(i) then UseAction(i) end " +
                            "break " +
                        "end " +
                    "end");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] StartAutoAttack error");
            }
        }

        /// <summary>
        /// Auto-greed on loot roll windows when in a group.
        /// </summary>
        private void HandleLootRolls()
        {
            try
            {
                // Click greed on any visible GroupLootFrame
                Functions.LuaCall(
                    "for i=1,4 do " +
                        "local f = getglobal('GroupLootFrame'..i) " +
                        "if f and f:IsVisible() then " +
                            "local b = getglobal('GroupLootFrame'..i..'GreedButton') " +
                            "if b then b:Click() end " +
                        "end " +
                    "end");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[GrindBot] HandleLootRolls error");
            }
        }
    }
}
