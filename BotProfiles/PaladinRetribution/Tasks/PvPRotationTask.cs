using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinRetribution.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
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
            TryCastSpell(HammerOfJustice, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent < 20);
            TryCastSpell(SealOfTheCrusader, !ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfTheCrusader));
            TryCastSpell(SealOfRighteousness, !ObjectManager.Player.HasBuff(SealOfRighteousness));
        }
    }
}

