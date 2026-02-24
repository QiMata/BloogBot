using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanElemental.Tasks
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

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting));
            TryCastSpell(EarthShock, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(FlameShock, 0, 20, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock));
            TryCastSpell(LightningBolt, 0, 30, ObjectManager.Player.ManaPercent > 10);
        }
    }
}
