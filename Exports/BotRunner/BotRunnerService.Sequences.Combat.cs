using BotRunner.Movement;
using GameData.Core.Constants;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        // Stop inside the theoretical melee range to absorb packet/snapshot drift.
        // Previous value of 2.0 was too aggressive — with small creature combat reaches
        // the arrival distance bottomed out at NOMINAL_MELEE_RANGE (1.67y), which
        // pathfinding/navmesh can't reliably achieve.  0.5y buffer leaves the bot well
        // within melee range while still reachable via navigation.
        private const float MeleeChaseStickBuffer = 0.5f;

        /// <summary>
        /// Melee combat sequence: select target -> chase to melee range -> auto-attack.
        /// Keeps running while the target is alive, chasing the mob if it moves out of
        /// range. Returns Success when the target dies.
        ///
        /// Auto-attack in WoW does not move the character; the bot must explicitly
        /// chase via pathfinding to stay in melee range.
        /// </summary>
        private IBehaviourTreeNode BuildStartMeleeAttackSequence(ulong targetGuid)
        {
            NavigationPath? navPath = null;
            bool targetSelected = false;
            bool attackStarted = false;
            DateTime lastChaseLogUtc = DateTime.MinValue;

            return new BehaviourTreeBuilder()
                .Sequence("Start Melee Attack Sequence")
                    .Splice(CheckForTarget(targetGuid))
                    .Do("Chase and Melee Attack", time =>
                    {
                        if (targetGuid == 0)
                        {
                            Log.Warning("[BOT RUNNER] StartMeleeAttack requested with targetGuid=0; ignoring.");
                            return BehaviourTreeStatus.Failure;
                        }

                        if (!targetSelected)
                        {
                            _objectManager.SetTarget(targetGuid);
                            // Small delay ensures CMSG_SET_SELECTION is flushed before CMSG_ATTACKSWING.
                            System.Threading.Thread.Sleep(50);
                            targetSelected = true;
                            Log.Information("[BOT RUNNER] Target selected: 0x{Guid:X}", targetGuid);
                        }

                        var player = _objectManager.Player;
                        if (player?.Position == null)
                            return BehaviourTreeStatus.Running;

                        var target = _objectManager.Units.FirstOrDefault(u => u.Guid == targetGuid);
                        if (target == null || target.Health == 0)
                        {
                            _objectManager.StopAllMovement();
                            navPath?.Clear();
                            Log.Information("[BOT RUNNER] Target 0x{Guid:X} dead or gone - combat complete.", targetGuid);
                            return BehaviourTreeStatus.Success;
                        }

                        if (target.Position == null)
                            return BehaviourTreeStatus.Running;

                        // MaNGOS melee range check is 2D (XY plane).  Using 3D distance
                        // causes false "out of range" when the bot and mob are at different
                        // Z levels (e.g. terrain step, physics ground snap discrepancy).
                        var dist = player.Position.DistanceTo2D(target.Position);
                        var chaseArrivalDist = GetMeleeChaseArrivalDistance(player, target);

                        if (dist > chaseArrivalDist)
                        {
                            // Out of melee range - chase the target.
                            // Do not send CMSG_ATTACKSTOP here: if the server still considers
                            // auto-attack active, it will resume swings as soon as the player
                            // gets back inside real melee range.
                            if (navPath == null)
                            {
                                var (radius, height) = RaceDimensions.GetCapsuleForRace(player.Race, player.Gender);
                                navPath = new NavigationPath(_container.PathfindingClient,
                                    capsuleRadius: radius,
                                    capsuleHeight: height,
                                    nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(_objectManager, start, end));
                            }

                            if (player.RunSpeed > 0)
                                navPath.UpdateCharacterSpeed(player.RunSpeed);

                            bool hitWall = _objectManager is WoWSharpClient.WoWSharpObjectManager wsOm && wsOm.PhysicsHitWall;
                            var waypoint = navPath.GetNextWaypoint(player.Position, target.Position,
                                player.MapId, allowDirectFallback: true, physicsHitWall: hitWall);

                            if (waypoint != null)
                            {
                                var dx = waypoint.X - player.Position.X;
                                var dy = waypoint.Y - player.Position.Y;
                                var facing = MathF.Atan2(dy, dx);
                                _objectManager.MoveToward(waypoint, facing);
                            }

                            if (DateTime.UtcNow - lastChaseLogUtc > TimeSpan.FromSeconds(3))
                            {
                                Log.Information("[BOT RUNNER] Chasing target 0x{Guid:X}, dist={Dist:F2}y, arrival={Arrival:F2}y (pReach={PR:F2}, tReach={TR:F2})",
                                    targetGuid, dist, chaseArrivalDist, player.CombatReach, target.CombatReach);
                                lastChaseLogUtc = DateTime.UtcNow;
                            }

                            return BehaviourTreeStatus.Running;
                        }

                        // In melee range.  Stop movement and start auto-attack on
                        // the first arrival, then let the server run the swing timer.
                        // Do NOT spam StopAllMovement/Face/StartMeleeAttack every tick
                        // — flooding the server with movement packets disrupts the
                        // auto-attack swing timer.
                        if (!attackStarted)
                        {
                            _objectManager.StopAllMovement();
                            navPath?.Clear();
                            _objectManager.Face(target.Position);
                            _objectManager.StartMeleeAttack();
                            Log.Information("[BOT RUNNER] In melee range ({Dist:F1}y <= {Arrival:F1}y) - engaging 0x{Guid:X}",
                                dist, chaseArrivalDist, targetGuid);
                            attackStarted = true;
                        }
                        else
                        {
                            // Re-send CMSG_ATTACKSWING only if auto-attack was cancelled
                            // (server sent SMSG_ATTACKSTOP / SMSG_CANCEL_COMBAT).
                            _objectManager.StartMeleeAttack();
                        }
                        return BehaviourTreeStatus.Running;
                    })
                .End()
                .Build();
        }

        private static float GetMeleeChaseArrivalDistance(IWoWUnit player, IWoWUnit target)
        {
            var playerReach = player.CombatReach > 0f
                ? player.CombatReach
                : CombatDistance.DEFAULT_PLAYER_COMBAT_REACH;
            var targetReach = target.CombatReach > 0f
                ? target.CombatReach
                : CombatDistance.DEFAULT_CREATURE_COMBAT_REACH;

            bool bothMoving = CombatDistance.IsMovingXZ((uint)player.MovementFlags)
                           && CombatDistance.IsMovingXZ((uint)target.MovementFlags);

            var maxMeleeRange = CombatDistance.GetMeleeAttackRange(playerReach, targetReach, bothMoving);
            return MathF.Max(CombatDistance.NOMINAL_MELEE_RANGE, maxMeleeRange - MeleeChaseStickBuffer);
        }

        /// <summary>
        /// Sequence to start ranged auto-attack (bow/gun/thrown) on a target.
        /// Uses the same CMSG_ATTACKSWING opcode; the server determines melee vs ranged
        /// based on equipped weapon and distance.
        /// </summary>
        private IBehaviourTreeNode BuildStartRangedAttackSequence(ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Start Ranged Attack Sequence")
                .Splice(CheckForTarget(targetGuid))
                .Do("Start Ranged Attack", time =>
                {
                    if (targetGuid == 0)
                    {
                        Log.Warning("[BOT RUNNER] StartRangedAttack requested with targetGuid=0; ignoring.");
                        return BehaviourTreeStatus.Failure;
                    }

                    _objectManager.SetTarget(targetGuid);
                    _objectManager.StartRangedAttack();
                    Log.Information("[BOT RUNNER] Started ranged attack on target {Guid:X}", targetGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        private IBehaviourTreeNode StopAttackSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Attack Sequence")
                .Condition("Is Any Auto-Attack Active", time => _objectManager.Player.IsAutoAttacking)
                .Do("Stop All Auto-Attacks", time =>
                {
                    _objectManager.StopAttack();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to cast a specific spell. This checks if the bot has sufficient resources,
        /// if the spell is off cooldown, and if the target is in range before casting the spell.
        /// </summary>
        private IBehaviourTreeNode BuildCastSpellSequence(int spellId, ulong targetGuid)
        {
            Log.Information("[BOT RUNNER] BuildCastSpellSequence: spell={SpellId}, target=0x{Target:X}", spellId, targetGuid);
            return new BehaviourTreeBuilder()
                .Sequence("Cast Spell Sequence")
                    .Splice(CheckForTarget(targetGuid))
                    .Condition("Can Cast Spell", time =>
                    {
                        var canCast = _objectManager.CanCastSpell(spellId, targetGuid);
                        if (!canCast)
                            Log.Debug("[BOT RUNNER] CanCastSpell({SpellId}, 0x{Target:X}) = false", spellId, targetGuid);
                        return canCast;
                    })
                    .Do("Stop and Face Target", time =>
                    {
                        _objectManager.StopAllMovement();

                        var target = _objectManager.Units.FirstOrDefault(u => u.Guid == targetGuid);
                        if (target?.Position != null)
                            _objectManager.Face(target.Position);

                        return BehaviourTreeStatus.Success;
                    })
                    .Do("Cast Spell", time =>
                    {
                        Log.Information("[BOT RUNNER] Casting spell {SpellId} on target 0x{Target:X}", spellId, targetGuid);
                        _objectManager.CastSpell(spellId);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        /// <summary>
        /// Sequence to stop the current spell cast.
        /// </summary>
        private IBehaviourTreeNode StopCastSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Cast Sequence")
                .Condition("Is Casting", time => _objectManager.Player.IsCasting || _objectManager.Player.IsChanneling)
                .Do("Stop Spell Cast", time =>
                {
                    _objectManager.StopCasting();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to resurrect the bot or another target.
        /// </summary>
        private IBehaviourTreeNode ResurrectSequence => new BehaviourTreeBuilder()
            .Sequence("Resurrect Sequence")
                .Condition("Can Resurrect", time => IsGhostState(_objectManager.Player) && _objectManager.Player.CanResurrect)
                .Do("Resurrect", time =>
                {
                    _objectManager.AcceptResurrect();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
