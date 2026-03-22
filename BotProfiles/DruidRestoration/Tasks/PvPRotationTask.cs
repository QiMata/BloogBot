using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Basic PvP rotation for a restoration druid. Uses simple heals and damage spells.
    /// </summary>
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 druid base spell ranges
        private const float WrathBaseRange = 30f;
        private const float MoonfireBaseRange = 30f;

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null)
                return;

            if (Update(GetSpellRange(WrathBaseRange)))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(Rejuvenation, condition: ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation), castOnSelf: true);
            TryCastSpell(HealingTouch, condition: ObjectManager.Player.HealthPercent < 60, castOnSelf: true);

            TryCastSpell(Moonfire, 0f, GetSpellRange(MoonfireBaseRange), !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Moonfire));
            TryCastSpell(Wrath, 0f, GetSpellRange(WrathBaseRange));
        }
    }
}
