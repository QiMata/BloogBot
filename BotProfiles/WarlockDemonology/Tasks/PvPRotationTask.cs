using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void BeforeRotation()
        {
            TryCastSpell(DeathCoil, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(Fear, 0, 20,
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting ||
                 ObjectManager.GetTarget(ObjectManager.Player).IsChanneling) &&
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Fear));
            TryCastSpell(LifeTap, 0, int.MaxValue,
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);
            TryCastSpell(DemonicEmpowerment, 0, int.MaxValue,
                ObjectManager.Pet != null && !ObjectManager.Pet.HasBuff(DemonicEmpowerment));
        }
    }
}
