using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace RogueAssassin.Tasks
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

            // Evasion when low HP or multiple aggressors
            TryUseAbility(Evasion, condition: player.HealthPercent < 40 || ObjectManager.Aggressors.Count() > 1);

            // Gouge to buy time if low HP
            TryUseAbility(Gouge, 45, player.HealthPercent < 30 && !target.IsStunned);

            // Kidney Shot for CC on casters
            TryUseAbility(KidneyShot, 25, combo >= 2 && target.IsCasting);

            // Keep Slice and Dice up
            TryUseAbility(SliceAndDice, 25, combo >= 2 && !player.HasBuff(SliceAndDice));

            // Blade Flurry for multi-target
            TryUseAbility(BladeFlurry, condition: ObjectManager.Aggressors.Count() > 1);

            // Finisher at 4+ combo points
            TryUseAbility(Eviscerate, 35, combo >= 4);

            // Combo point builders
            TryUseAbility(GhostlyStrike, 40, combo < 5);
            TryUseAbility(Riposte, 10, combo < 5);
            TryUseAbility(SinisterStrike, 45, combo < 5);
        }
    }
}
