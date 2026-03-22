using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanElemental.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 shaman base spell ranges
        private const float LightningBoltBaseRange = 30f;
        private const float EarthShockBaseRange = 20f;
        private const float FlameShockBaseRange = 20f;

        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (Update(GetSpellRange(LightningBoltBaseRange)))
                return;

            PerformCombatRotation();
        }
        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(GroundingTotem, condition: ObjectManager.Aggressors.Any(a => a.IsCasting), castOnSelf: true);
            TryCastSpell(EarthShock, 0f, GetSpellRange(EarthShockBaseRange), ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(FlameShock, 0f, GetSpellRange(FlameShockBaseRange), !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock));
            TryCastSpell(LightningBolt, 0f, GetSpellRange(LightningBoltBaseRange), ObjectManager.Player.ManaPercent > 10);
        }
    }
}
