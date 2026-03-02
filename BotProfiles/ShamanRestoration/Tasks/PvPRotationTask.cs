using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanRestoration.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 shaman base spell ranges
        private const float LightningBoltBaseRange = 30f;

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

            TryCastSpell(HealingWave, condition: ObjectManager.Player.HealthPercent < 50, castOnSelf: true);

            TryCastSpell(GroundingTotem, condition: ObjectManager.Aggressors.Any(a => a.IsCasting), castOnSelf: true);

            TryCastSpell(LightningBolt, 0f, GetSpellRange(LightningBoltBaseRange), ObjectManager.Player.ManaPercent > 20);
        }
    }
}
