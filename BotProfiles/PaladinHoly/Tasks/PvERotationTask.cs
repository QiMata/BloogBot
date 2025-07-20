using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinHoly.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.Player.HealthPercent < 40 &&
                ObjectManager.Player.Mana >= ObjectManager.Player.GetManaCost(HolyLight))
            {
                ObjectManager.Player.CastSpell(HolyLight, castOnSelf: true);
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

        public override void PerformCombatRotation()
        {
            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            TryCastSpell(DevotionAura, !ObjectManager.Player.HasBuff(DevotionAura));
            TryCastSpell(SealOfTheCrusader, !ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(HolyLight, ObjectManager.Player.HealthPercent < 60, castOnSelf: true);
        }
    }
}

