using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarriorArms.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        public PvPRotationTask(IBotContext botContext) : base(botContext) { }

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
                ObjectManager.SetTarget(ObjectManager.Aggressors.First().Guid);
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

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);
            ObjectManager.StartMeleeAttack();

            string stance = ObjectManager.Player.CurrentStance;

            TryCastSpell(BattleStance, 0, int.MaxValue, stance != BattleStance);

            TryUseAbility(BerserkerRage, condition: stance == BerserkerStance);

            TryUseAbility(Pummel, 10, stance == BerserkerStance && target.IsCasting);
            TryUseAbility(ShieldBash, 10, stance == DefensiveStance && target.IsCasting);

            TryUseAbility(Hamstring, 10, !target.HasDebuff(Hamstring));

            TryUseAbility(IntimidatingShout, 25, ObjectManager.Aggressors.Count() > 1);

            TryUseAbility(MortalStrike, 30);
            TryUseAbility(Overpower, 5, stance == BattleStance);
        }
    }
}
