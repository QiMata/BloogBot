using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace ShamanElemental.Tasks
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

            if (Update(30))
                return;

            PerformCombatRotation();
        }
        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting));
            TryCastSpell(EarthShock, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(FlameShock, 0, 20, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock));
            TryCastSpell(LightningBolt, 0, 30, ObjectManager.Player.ManaPercent > 10);
        }
    }
}
