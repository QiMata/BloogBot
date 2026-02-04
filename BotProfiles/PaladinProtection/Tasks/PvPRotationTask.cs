using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinProtection.Tasks
{
    internal class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null ||
                ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.SetTarget(ObjectManager.Aggressors.First().Guid);
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
            TryCastSpell(RighteousFury, !ObjectManager.Player.HasBuff(RighteousFury));
            TryCastSpell(SealOfRighteousness, !ObjectManager.Player.HasBuff(SealOfRighteousness));
            TryCastSpell(Judgement, ObjectManager.Player.HasBuff(SealOfRighteousness));
            TryCastSpell(HolyShield, !ObjectManager.Player.HasBuff(HolyShield));
        }
    }
}

