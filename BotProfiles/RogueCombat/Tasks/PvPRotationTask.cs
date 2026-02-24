using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace RogueCombat.Tasks
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

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);
            ObjectManager.StartMeleeAttack();

            var player = ObjectManager.Player;
            int combo = player.ComboPoints;

            // Interrupt casters
            TryUseAbility(Kick, 25, target.IsCasting);

            // Defensive CDs
            TryUseAbility(Evasion, condition: player.HealthPercent < 40);

            // Burst CDs
            TryUseAbility(AdrenalineRush, condition: target.HealthPercent > 50);
            TryUseAbility(BladeFlurry, condition: ObjectManager.Aggressors.Count() > 1);

            // Riposte on parry (reactive, low cost)
            TryUseAbility(Riposte, 10);

            // Keep Slice and Dice up
            TryUseAbility(SliceAndDice, 25, combo >= 2 && !player.HasBuff(SliceAndDice));

            // Gouge to buy time
            TryUseAbility(Gouge, 45, player.HealthPercent < 30 && !target.IsStunned);

            // Finisher
            TryUseAbility(Eviscerate, 35, combo >= 4);

            // Builder
            TryUseAbility(SinisterStrike, 45, combo < 5);
        }
    }
}
