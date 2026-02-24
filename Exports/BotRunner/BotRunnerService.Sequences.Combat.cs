using GameData.Core.Interfaces;
using Serilog;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to stop any active auto-attacks, including melee, ranged, and wand.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages stopping auto-attacks.</returns>
        private IBehaviourTreeNode BuildStartMeleeAttackSequence(ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Start Melee Attack Sequence")
                .Splice(CheckForTarget(targetGuid))
                .Do("Start Melee Attack", time =>
                {
                    if (targetGuid == 0)
                    {
                        Log.Warning("[BOT RUNNER] StartMeleeAttack requested with targetGuid=0; ignoring.");
                        return BehaviourTreeStatus.Failure;
                    }

                    _objectManager.SetTarget(targetGuid);
                    _objectManager.StartMeleeAttack();
                    Log.Information($"[BOT RUNNER] Started melee attack on target {targetGuid:X}");
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
