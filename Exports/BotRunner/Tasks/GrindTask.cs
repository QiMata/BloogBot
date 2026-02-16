using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Grind orchestrator: FindTarget → Push PullTask → (combat sub-tasks run) → Loot → Rest → Explore.
/// Uses ClassContainer task factories for class-specific pull, combat, rest, and buff behavior.
/// This task sits at the bottom of the stack and never pops itself.
/// </summary>
public class GrindTask : BotTask, IBotTask
{
    private readonly ILootingService _lootingService;

    private enum GrindState { Scan, Loot, Rest, Buff, Explore, FollowLeader }
    private GrindState _state = GrindState.Scan;

    // Target tracking
    private ulong _lastKilledGuid;
    private readonly HashSet<ulong> _blacklist = new();

    // Explore state
    private Position? _exploreTarget;
    private Position? _exploreOrigin;
    private int _exploreCount;
    private readonly List<Position> _recentExplorePositions = new();
    private static readonly Random _rng = new();
    private DateTime _lastExploreMove = DateTime.MinValue;

    // Timing
    private DateTime _lastStateChange = DateTime.Now;
    private const int STUCK_TIMEOUT_MS = 60000;
    private const float MAX_PULL_RANGE = 80.0f;
    private const float FOLLOW_RANGE = 25.0f;
    private const float FOLLOW_CLOSE = 10.0f;

    public GrindTask(
        IBotContext botContext,
        ILootingService lootingService) : base(botContext)
    {
        _lootingService = lootingService;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null) return;

        // Dead — wait for resurrection
        if (player.Health <= 0) return;

        // If we got pulled into combat while exploring/scanning, handle it
        if (ObjectManager.Aggressors.Any() && _state != GrindState.Loot)
        {
            Log.Information("[GRIND] Aggressors detected, pushing combat rotation");
            ObjectManager.SetTarget(ObjectManager.Aggressors.First().Guid);
            _lastKilledGuid = ObjectManager.Aggressors.First().Guid;
            PushCombatTask();
            return;
        }

        // Stuck detection
        if ((DateTime.Now - _lastStateChange).TotalMilliseconds > STUCK_TIMEOUT_MS)
        {
            Log.Warning("[GRIND] Stuck in {State} for {Timeout}ms, resetting", _state, STUCK_TIMEOUT_MS);
            _state = GrindState.Scan;
            _exploreTarget = null;
            _lastStateChange = DateTime.Now;
        }

        switch (_state)
        {
            case GrindState.Scan:
                Scan(player);
                break;
            case GrindState.Loot:
                Loot();
                break;
            case GrindState.Rest:
                PushRestTask();
                break;
            case GrindState.Buff:
                PushBuffTask();
                break;
            case GrindState.Explore:
                Explore(player);
                break;
            case GrindState.FollowLeader:
                FollowLeader(player);
                break;
        }
    }

    // ======== SCAN: find nearest hostile and push pull task ========
    private void Scan(IWoWUnit player)
    {
        // Check if we should follow leader first
        if (ShouldFollowLeader(player))
        {
            SetState(GrindState.FollowLeader);
            return;
        }

        // Check if player needs rest
        if (NeedsRest(player))
        {
            SetState(GrindState.Rest);
            return;
        }

        // Check if we need to loot a recent kill
        if (_lastKilledGuid != 0)
        {
            SetState(GrindState.Loot);
            return;
        }

        // Find nearest attackable unit
        var target = FindBestTarget(player);
        if (target != null)
        {
            Log.Information("[GRIND] Found target: {Name} (HP: {HP}/{MaxHP}, Dist: {Dist:F1})",
                target.Name, target.Health, target.MaxHealth,
                player.Position.DistanceTo(target.Position));

            ObjectManager.SetTarget(target.Guid);
            _lastKilledGuid = target.Guid;

            // Reset expanding explore state when we find combat
            _exploreOrigin = null;
            _exploreTarget = null;

            // Push class-specific pull task (it handles movement + pull + transitions to combat)
            PushPullTask();
            return;
        }

        // No targets — explore
        SetState(GrindState.Explore);
    }

    private static readonly NPCFlags NonHostileNpcFlags =
        NPCFlags.UNIT_NPC_FLAG_GOSSIP | NPCFlags.UNIT_NPC_FLAG_VENDOR |
        NPCFlags.UNIT_NPC_FLAG_TRAINER | NPCFlags.UNIT_NPC_FLAG_QUESTGIVER |
        NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER | NPCFlags.UNIT_NPC_FLAG_INNKEEPER;

    private IWoWUnit? FindBestTarget(IWoWUnit player)
    {
        return ObjectManager.Units
            .Where(u => u.Health > 0
                && u.ObjectType != WoWObjectType.Player
                && u.Position != null
                && player.Position!.DistanceTo(u.Position) <= MAX_PULL_RANGE
                && u.Level <= player.Level + 3
                && u.Level >= player.Level - 7
                && !u.TappedByOther
                && !_blacklist.Contains(u.Guid)
                && (u.NpcFlags & NonHostileNpcFlags) == 0)
            .OrderBy(u => player.Position!.DistanceTo(u.Position!))
            .FirstOrDefault();
    }

    // ======== LOOT ========
    private void Loot()
    {
        if (_lastKilledGuid == 0)
        {
            SetState(GrindState.Scan);
            return;
        }

        try
        {
            _lootingService.TryLootAsync(_lastKilledGuid, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning("[GRIND] Looting failed: {Error}", ex.Message);
        }

        _lastKilledGuid = 0;
        SetState(GrindState.Scan);
    }

    // ======== REST: push class-specific rest task ========
    private void PushRestTask()
    {
        Log.Information("[GRIND] Pushing rest task");
        _state = GrindState.Buff; // After rest pops, we'll buff then scan
        BotTasks.Push(Container.ClassContainer.CreateRestTask(BotContext));
    }

    // ======== BUFF: push class-specific buff task ========
    private void PushBuffTask()
    {
        Log.Information("[GRIND] Pushing buff task");
        _state = GrindState.Scan; // After buff pops, we scan
        BotTasks.Push(Container.ClassContainer.CreateBuffTask(BotContext));
    }

    // ======== PUSH COMBAT SUB-TASKS ========
    private void PushPullTask()
    {
        _state = GrindState.Scan; // When pull/combat chain finishes and pops, we'll scan again
        BotTasks.Push(Container.ClassContainer.CreateMoveToTargetTask(BotContext));
    }

    private void PushCombatTask()
    {
        _state = GrindState.Scan;
        BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
    }

    // ======== EXPLORE: pathfind to random positions ========
    private void Explore(IWoWUnit player)
    {
        // Check if we should follow leader
        if (ShouldFollowLeader(player))
        {
            SetState(GrindState.FollowLeader);
            return;
        }

        // Re-scan for targets periodically
        var target = FindBestTarget(player);
        if (target != null)
        {
            _exploreTarget = null;
            _exploreOrigin = null;
            SetState(GrindState.Scan);
            return;
        }

        // Pick a random explore target
        if (_exploreTarget == null || player.Position.DistanceTo(_exploreTarget) < 5.0f)
        {
            _exploreTarget = PickExplorePosition(player);
            if (_exploreTarget == null)
            {
                SetState(GrindState.Scan);
                return;
            }
            Log.Information("[GRIND] Exploring toward ({X:F0},{Y:F0},{Z:F0})",
                _exploreTarget.X, _exploreTarget.Y, _exploreTarget.Z);
        }

        // Pathfind and move toward explore target
        if ((DateTime.Now - _lastExploreMove).TotalMilliseconds > 500)
        {
            _lastExploreMove = DateTime.Now;
            MoveTowardPosition(player, _exploreTarget);
        }
    }

    private Position? PickExplorePosition(IWoWUnit player)
    {
        // Record where exploration started; expand outward from there
        if (_exploreOrigin == null)
        {
            _exploreOrigin = player.Position;
            _exploreCount = 0;
        }

        float minDist = 40 + _exploreCount * 20f;
        float maxDist = 80 + _exploreCount * 20f;

        for (int attempts = 0; attempts < 10; attempts++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2); // Full 360°
            float distance = minDist + (float)(_rng.NextDouble() * (maxDist - minDist));

            float x = _exploreOrigin.X + MathF.Cos(angle) * distance;
            float y = _exploreOrigin.Y + MathF.Sin(angle) * distance;

            var candidate = new Position(x, y, player.Position.Z);

            // Avoid recently visited positions
            if (_recentExplorePositions.Any(p => p.DistanceTo(candidate) < 30))
                continue;

            _exploreCount++;
            _recentExplorePositions.Add(candidate);
            if (_recentExplorePositions.Count > 10)
                _recentExplorePositions.RemoveAt(0);

            return candidate;
        }

        // Fallback: clear history and push outward
        _recentExplorePositions.Clear();
        _exploreCount++;
        float fallbackAngle = (float)(_rng.NextDouble() * Math.PI * 2);
        return new Position(
            _exploreOrigin.X + MathF.Cos(fallbackAngle) * maxDist,
            _exploreOrigin.Y + MathF.Sin(fallbackAngle) * maxDist,
            player.Position.Z);
    }

    // ======== FOLLOW LEADER ========
    private void FollowLeader(IWoWUnit player)
    {
        var leader = ObjectManager.PartyLeader;
        if (leader == null || leader.Position == null)
        {
            SetState(GrindState.Scan);
            return;
        }

        float dist = player.Position.DistanceTo(leader.Position);

        // Close enough — go grind
        if (dist < FOLLOW_CLOSE)
        {
            ObjectManager.StopAllMovement();
            SetState(GrindState.Scan);
            return;
        }

        // Still far — pathfind toward leader
        MoveTowardPosition(player, leader.Position);
    }

    private bool ShouldFollowLeader(IWoWUnit player)
    {
        var leader = ObjectManager.PartyLeader;
        if (leader == null || leader.Position == null) return false;
        if (leader.Guid == player.Guid) return false; // I am the leader

        return player.Position.DistanceTo(leader.Position) > FOLLOW_RANGE;
    }

    // ======== HELPERS ========
    private bool NeedsRest(IWoWUnit player)
    {
        if (player.HealthPercent < 50) return true;
        if (player.MaxMana > 0 && player.ManaPercent < 30) return true;
        return false;
    }

    private void MoveTowardPosition(IWoWUnit player, Position target)
    {
        try
        {
            var path = Container.PathfindingClient.GetPath(
                ObjectManager.Player.MapId, player.Position, target, true);

            if (path != null && path.Length > 0)
            {
                // Pass the full path to the movement controller for Z interpolation
                ObjectManager.SetNavigationPath(path);

                // Use second waypoint if available (first is often current position)
                var waypoint = path.Length > 1 ? path[1] : path[0];
                ObjectManager.MoveToward(waypoint);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[GRIND] Pathfinding failed: {Error}", ex.Message);
            // Fallback: just face and move toward target directly
            ObjectManager.MoveToward(target);
        }
    }

    private void SetState(GrindState newState)
    {
        if (_state != newState)
        {
            _state = newState;
            _lastStateChange = DateTime.Now;
        }
    }
}
