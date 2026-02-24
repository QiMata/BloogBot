using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinHoly.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.Player.HealthPercent < 40 &&
                ObjectManager.Player.Mana >= ObjectManager.GetManaCost(HolyLight))
            {
                ObjectManager.CastSpell(HolyLight, castOnSelf: true);
                return;
            }

            if (!EnsureTarget())
                return;

            if (Update(3))
                return;

            ExecuteRotation();
        }

        public override void PerformCombatRotation()
        {
            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            TryCastSpell(DevotionAura, !ObjectManager.Player.HasBuff(DevotionAura));

            // Group healing: heal party members before DPS
            if (IsInGroup)
            {
                if (TryCastHeal(HolyLight, 60, 40)) return;
            }
            else if (ObjectManager.Player.HealthPercent < 60)
            {
                TryCastSpell(HolyLight, castOnSelf: true, condition: true);
            }

            TryCastSpell(SealOfTheCrusader, !ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfTheCrusader));
        }
    }
}

