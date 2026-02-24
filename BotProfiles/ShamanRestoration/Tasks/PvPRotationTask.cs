using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanRestoration.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
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

            TryCastSpell(HealingWave, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 50, castOnSelf: true);

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting));

            TryCastSpell(LightningBolt, 0, 30, ObjectManager.Player.ManaPercent > 20);
        }
    }
}
