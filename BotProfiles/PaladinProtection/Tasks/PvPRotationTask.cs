using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinProtection.Tasks
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
            TryCastSpell(RighteousFury, !ObjectManager.Player.HasBuff(RighteousFury));
            TryCastSpell(SealOfRighteousness, !ObjectManager.Player.HasBuff(SealOfRighteousness));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfRighteousness));
            TryCastSpell(HolyShield, !ObjectManager.Player.HasBuff(HolyShield));
        }
    }
}

