using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinProtection.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public override void PerformCombatRotation()
        {
            ExecuteRotation();
        }

        public void Update()
        {
            if (ObjectManager.Player.HealthPercent < 30 &&
                ObjectManager.Player.Mana >= ObjectManager.Player.GetManaCost(HolyLight))
            {
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null ||
                ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (Update(3))
                return;

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            TryCastSpell(RighteousFury, !ObjectManager.Player.HasBuff(RighteousFury));
            TryCastSpell(DevotionAura, !ObjectManager.Player.HasBuff(DevotionAura));
            TryCastSpell(SealOfTheCrusader, !ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(SealOfRighteousness, !ObjectManager.Player.HasBuff(SealOfRighteousness));
            TryCastSpell(Consecration, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);
            TryCastSpell(HolyShield, !ObjectManager.Player.HasBuff(HolyShield));
            TryCastSpell(DivineProtection, ObjectManager.Player.HealthPercent < 20, castOnSelf: true);
            TryCastSpell(LayOnHands, ObjectManager.Player.HealthPercent < 10, castOnSelf: true);
        }
    }
}

