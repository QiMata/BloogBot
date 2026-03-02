using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void BeforeRotation()
        {
            TryCastSpell(DeathCoil, 0f, GetSpellRange(DeathCoilBaseRange), ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(Fear, 0f, GetSpellRange(FearBaseRange),
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting ||
                 ObjectManager.GetTarget(ObjectManager.Player).IsChanneling) &&
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Fear));
            TryCastSpell(LifeTap, condition:
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80, castOnSelf: true);
            TryCastSpell(DemonicEmpowerment, condition:
                ObjectManager.Pet != null && !ObjectManager.Pet.HasBuff(DemonicEmpowerment), castOnSelf: true);
        }
    }
}
