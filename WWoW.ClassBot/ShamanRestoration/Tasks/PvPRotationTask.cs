using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace ShamanRestoration.Tasks
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
            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(HealingWave, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 50, castOnSelf: true);

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting));

            TryCastSpell(LightningBolt, 0, 30, ObjectManager.Player.ManaPercent > 20);
        }
    }
}
