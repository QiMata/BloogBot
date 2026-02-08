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

        // Hotspot patrol
        private readonly Position[] _hotspots;
        private int _currentHotspotIndex;
        private const float HOTSPOT_REACH_DISTANCE = 10f;

        // Tuning constants
        private const float AGGRO_RANGE = 30f;
        private const float MELEE_RANGE = 5f;
        private const float LOOT_RANGE = 5f;
        private const int LOOT_TIMEOUT_MS = 8000;
        private const int COMBAT_TIMEOUT_MS = 60000;
        private const int REST_THRESHOLD_PCT = 50;
        private const int REST_RESUME_PCT = 80;
        private const int LOG_INTERVAL_MS = 5000;
        private const float STUCK_THRESHOLD = 0.05f;
        private const int STUCK_DETECTION_MS = 1000;
        private const int UNSTUCK_DURATION_MS = 300;

        public GrindBotPhase CurrentPhase => _phase;

        public GrindBot(ObjectManager objectManager, PathfindingClient? pathfindingClient = null, IQuestRepository? questRepo = null, GroupManager? groupManager = null)
        {
            _objectManager = objectManager;
            _navigation = new NavigationPath(pathfindingClient);
            _hotspots = HotspotConfig.Load();
            _groupManager = groupManager;
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
                _currentTarget = null;
                _objectManager.StopAllMovement();
                SetPhase(GrindBotPhase.FindTarget);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GrindBot] Update error in {Phase}", _phase);
            }
        }

        private void SetPhase(GrindBotPhase newPhase)
        {
            if (_phase != newPhase)
            {
                Log.Information("[GrindBot] {OldPhase} -> {NewPhase}", _phase, newPhase);
                _phase = newPhase;
                _phaseStartTime = Environment.TickCount;
                _navigation.Clear();
                ResetStuckState();
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
                _currentTarget = target;
                TargetUnit(target);
                _autoAttackStarted = false;
                SetPhase(GrindBotPhase.MoveToTarget);
                return;
            }

            // No targets - patrol to next hotspot if configured
            if (_hotspots.Length > 0)
            {
                var hotspot = _hotspots[_currentHotspotIndex % _hotspots.Length];
                var distToHotspot = player.Position.DistanceTo(hotspot);

                if (distToHotspot < HOTSPOT_REACH_DISTANCE)
                {
                    // Reached this hotspot - advance to next
                    _currentHotspotIndex = (_currentHotspotIndex + 1) % _hotspots.Length;
                    hotspot = _hotspots[_currentHotspotIndex];
                    _navigation.Clear();
                    Log.Debug("[GrindBot] Reached hotspot, advancing to #{Index}", _currentHotspotIndex);
                }

                // Navigate toward hotspot
                var mapId = _objectManager.ContinentId;
                var nextWaypoint = _navigation.GetNextWaypoint(player.Position, hotspot, mapId);
                if (nextWaypoint != null)
                {
                    _objectManager.Face(nextWaypoint);
                    _objectManager.StartMovement(ControlBits.Front);
                }

                LogPeriodic($"Patrolling to hotspot #{_currentHotspotIndex} ({distToHotspot:F0}y away)");
            }
            else
            {
                LogPeriodic($"No targets found within {AGGRO_RANGE}y (no hotspots configured)");
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
                    BlacklistTarget(_currentTarget.Guid);
                    _objectManager.StopAllMovement();
                    _currentTarget = null;
                    SetPhase(GrindBotPhase.FindTarget);
                }
                return; // Let unstuck movement play out
            }

            var combatRange = _rotation?.DesiredRange ?? MELEE_RANGE;
            var distance = player.Position.DistanceTo(_currentTarget.Position);
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
            var nextWaypoint = _navigation.GetNextWaypoint(player.Position, _currentTarget.Position, mapId);
            if (nextWaypoint != null)
            {
                _objectManager.Face(nextWaypoint);
                _objectManager.StartMovement(ControlBits.Front);
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
                var playerUnit = player as WoWUnit;
                if (playerUnit != null && playerUnit.IsMoving)
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
                    if (_unstuckStartPosition != null && player.Position.DistanceTo(_unstuckStartPosition) > 3)
                    {
                        _objectManager.StopAllMovement();
                        _isUnstucking = false;
                        return false;
                    }
                    // Try another random direction
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
            _objectManager.StartMovement(ControlBits.Jump);
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

            // Check unit reaction for each candidate (native call via ThreadSynchronizer)
            foreach (var candidate in candidates)
            {
                try
                {
                    var reaction = ThreadSynchronizer.RunOnMainThread(() =>
                        Functions.GetUnitReaction(playerPtr, candidate.Pointer));

                    if (reaction <= UnitReaction.Hostile)
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
