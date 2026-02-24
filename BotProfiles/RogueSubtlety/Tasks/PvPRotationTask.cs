using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace RogueSubtlety.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        public PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (Update(5))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            // Position behind target
            MoveBehindTarget(5);

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);
            ObjectManager.StartMeleeAttack();

            var player = ObjectManager.Player;
            int combo = player.ComboPoints;

            // Interrupt casters
            TryUseAbility(Kick, 25, target.IsCasting);

            // Defensive CDs
            TryUseAbility(Evasion, condition: player.HealthPercent < 40);

            // Kidney Shot for stun lock
            TryUseAbility(KidneyShot, 25, combo >= 3 && (target.IsCasting || player.HealthPercent < 50));

            // Keep Slice and Dice up
            TryUseAbility(SliceAndDice, 25, combo >= 2 && !player.HasBuff(SliceAndDice));

            // Gouge for breathing room
            TryUseAbility(Gouge, 45, player.HealthPercent < 30 && !target.IsStunned);

            // Finisher
            TryUseAbility(Eviscerate, 35, combo >= 4);

            // Builders
            TryUseAbility(GhostlyStrike, 40, combo < 5);
            TryUseAbility(SinisterStrike, 45, combo < 5);
        }
    }
}
