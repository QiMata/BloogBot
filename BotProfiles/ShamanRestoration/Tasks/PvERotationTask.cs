using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace ShamanRestoration.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (Update(12))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(HealingWave, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 50, castOnSelf: true);

            TryCastSpell(ManaSpringTotem, 0, int.MaxValue, !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(ManaSpringTotem)));

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting && ObjectManager.GetTarget(ObjectManager.Player).Mana > 0));

            TryCastSpell(LightningBolt, 0, 30, ObjectManager.Player.ManaPercent > 20);

            TryCastSpell(FlameShock, 0, 20, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock));
        }
    }
}
