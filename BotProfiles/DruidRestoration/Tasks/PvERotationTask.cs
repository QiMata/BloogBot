using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Basic PvE rotation for a restoration druid.
    /// Focuses on staying alive while dealing modest damage.
    /// </summary>
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 druid base spell ranges
        private const float WrathBaseRange = 30f;
        private const float MoonfireBaseRange = 30f;
        private const float HealBaseRange = 40f;

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

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

            // Group healing: heal party members before DPS
            if (IsInGroup)
            {
                if (TryCastHeal(Rejuvenation, 80, GetSpellRange(HealBaseRange))) return;
                if (TryCastHeal(HealingTouch, 55, GetSpellRange(HealBaseRange))) return;
            }
            else
            {
                TryCastSpell(Rejuvenation, condition: ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation), castOnSelf: true);
                TryCastSpell(HealingTouch, condition: ObjectManager.Player.HealthPercent < 60, castOnSelf: true);
            }

            // offensive abilities
            TryCastSpell(Moonfire, 0f, GetSpellRange(MoonfireBaseRange), !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Moonfire));
            TryCastSpell(Wrath, 0f, GetSpellRange(WrathBaseRange));
        }
    }
}
