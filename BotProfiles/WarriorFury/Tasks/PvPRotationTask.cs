using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarriorFury.Tasks
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

            TryCastSpell(BerserkerStance, 0, int.MaxValue, stance != BerserkerStance);

            TryUseAbility(BerserkerRage, condition: stance == BerserkerStance);

            TryUseAbility(Pummel, 10, stance == BerserkerStance && target.IsCasting);

            TryUseAbility(IntimidatingShout, 25, ObjectManager.Aggressors.Count() > 1);

            TryUseAbility(Hamstring, 10, !target.HasDebuff(Hamstring));

            TryUseAbility(Whirlwind, 25, ObjectManager.Aggressors.Count() > 1 && stance == BerserkerStance);
            TryUseAbility(Bloodthirst, 30);
            TryUseAbility(Execute, 15, target.HealthPercent < 20);
        }
    }
}
