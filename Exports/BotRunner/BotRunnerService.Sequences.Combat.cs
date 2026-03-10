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
        // Melee reach: weapon range (~2y) + both capsule radii (~0.4y each) + margin.
        private const float MeleeChaseArrivalDist = 3.5f;
        // Re-chase threshold: if target moves beyond this, start chasing again.
        private const float MeleeChaseLeashDist = 8f;

        /// <summary>
        /// Melee combat sequence: select target → chase to melee range → auto-attack.
        /// Keeps running (returning Running) while the target is alive, chasing the
        /// mob if it moves out of melee range. Returns Success when the target dies.
        ///
        /// Auto-attack in WoW does NOT move the character — the bot must explicitly
        /// chase via pathfinding to stay in melee range. This matches real player
        /// behavior: click mob → character runs to it → swings when in range.
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

                        // Ensure target is selected (once).
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

                        // Find the target unit.
                        var target = _objectManager.Units.FirstOrDefault(u => u.Guid == targetGuid);
                        if (target == null || target.Health == 0)
                        {
                            // Target dead or despawned — combat complete.
                            _objectManager.StopAllMovement();
                            navPath?.Clear();
                            Log.Information("[BOT RUNNER] Target 0x{Guid:X} dead or gone — combat complete.", targetGuid);
                            return BehaviourTreeStatus.Success;
                        }

                        if (target.Position == null)
                            return BehaviourTreeStatus.Running;

                        var dist = player.Position.DistanceTo(target.Position);

                        if (dist > MeleeChaseArrivalDist)
                        {
                            // Out of melee range — chase the target.
                            if (navPath == null)
                            {
                                var (radius, height) = RaceDimensions.GetCapsuleForRace(player.Race, player.Gender);
                                navPath = new NavigationPath(_container.PathfindingClient,
                                    capsuleRadius: radius, capsuleHeight: height);
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
                                Log.Information("[BOT RUNNER] Chasing target 0x{Guid:X}, dist={Dist:F1}y", targetGuid, dist);
                                lastChaseLogUtc = DateTime.UtcNow;
                            }

                            // If attack was started but we drifted out of range, keep Running.
                            // Auto-attack will resume swinging when back in range.
                            return BehaviourTreeStatus.Running;
                        }

                        // In melee range — stop movement and ensure auto-attack is on.
                        if (!attackStarted || dist <= MeleeChaseArrivalDist)
                        {
                            _objectManager.StopAllMovement();
                            navPath?.Clear();
                            _objectManager.Face(target.Position);
                            _objectManager.StartMeleeAttack();

                            if (!attackStarted)
                            {
                                Log.Information("[BOT RUNNER] In melee range ({Dist:F1}y) — started auto-attack on 0x{Guid:X}",
                                    dist, targetGuid);
                                attackStarted = true;
                            }
                        }

                        // Stay in Running — mob is alive, keep monitoring.
                        return BehaviourTreeStatus.Running;
                    })
                .End()
                .Build();
        }

        /// <summary>
        /// Sequence to start ranged auto-attack (bow/gun/thrown) on a target.
        /// Uses the same CMSG_ATTACKSWING opcode — the server determines
        /// melee vs ranged based on equipped weapon and distance.
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
                    Log.Information($"[BOT RUNNER] Started ranged attack on target {targetGuid:X}");
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        private IBehaviourTreeNode StopAttackSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Attack Sequence")
                // Check if any auto-attack (melee, ranged, or wand) is active
                .Condition("Is Any Auto-Attack Active", time => _objectManager.Player.IsAutoAttacking)

                // Disable all auto-attacks
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
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <returns>IBehaviourTreeNode that manages casting a spell.</returns>
        private IBehaviourTreeNode BuildCastSpellSequence(int spellId, ulong targetGuid)
        {
            Log.Information($"[BOT RUNNER] BuildCastSpellSequence: spell={spellId}, target=0x{targetGuid:X}");
            return new BehaviourTreeBuilder()
                .Sequence("Cast Spell Sequence")
                    .Splice(CheckForTarget(targetGuid))

                    .Condition("Can Cast Spell", time =>
                    {
                        var canCast = _objectManager.CanCastSpell(spellId, targetGuid);
                        if (!canCast)
                            Log.Debug($"[BOT RUNNER] CanCastSpell({spellId}, 0x{targetGuid:X}) = false");
                        return canCast;
                    })

                    .Do("Stop and Face Target", time =>
                    {
                        // Stop movement before casting to prevent INTERRUPTED failures
                        _objectManager.StopAllMovement();

                        // Face the target to prevent UNIT_NOT_INFRONT failures
                        var target = _objectManager.Units.FirstOrDefault(u => u.Guid == targetGuid);
                        if (target?.Position != null)
                        {
                            _objectManager.Face(target.Position);
                        }
                        return BehaviourTreeStatus.Success;
                    })

                    .Do("Cast Spell", time =>
                    {
                        Log.Information($"[BOT RUNNER] Casting spell {spellId} on target 0x{targetGuid:X}");
                        _objectManager.CastSpell(spellId);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }
        /// <summary>
        /// Sequence to stop the current spell cast. This will stop any spell the bot is currently casting.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages stopping a spell cast.</returns>
        private IBehaviourTreeNode StopCastSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Cast Sequence")
                // Ensure the bot is currently casting a spell
                .Condition("Is Casting", time => _objectManager.Player.IsCasting || _objectManager.Player.IsChanneling)

                // Stop the current spell cast
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
        /// <returns>IBehaviourTreeNode that manages the resurrection process.</returns>
        private IBehaviourTreeNode ResurrectSequence => new BehaviourTreeBuilder()
            .Sequence("Resurrect Sequence")
                // Ensure the bot or target can be resurrected
                .Condition("Can Resurrect", time => IsGhostState(_objectManager.Player) && _objectManager.Player.CanResurrect)

                // Perform the resurrection action
                .Do("Resurrect", time =>
                {
                    _objectManager.AcceptResurrect();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
