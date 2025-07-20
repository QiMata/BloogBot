using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarriorProtection.Tasks
{
    internal class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null ||
                ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (Update(5))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(target.Position);
            ObjectManager.Player.StartMeleeAttack();

            string stance = ObjectManager.Player.CurrentStance;

            TryCastSpell(DefensiveStance, 0, int.MaxValue, stance != DefensiveStance);

            TryUseAbility(BerserkerRage, condition: stance == BerserkerStance);

            TryUseAbility(ShieldBash, 10, target.IsCasting);

            TryUseAbility(ConcussionBlow, 15, !target.IsStunned);

            TryUseAbility(Revenge, 5, stance == DefensiveStance);

            TryUseAbility(ShieldSlam, 20);

            TryUseAbility(Hamstring, 10, !target.HasDebuff(Hamstring));

            TryUseAbility(IntimidatingShout, 25, ObjectManager.Aggressors.Count() > 1);
        }
    }
}
