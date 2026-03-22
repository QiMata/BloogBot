using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
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
        }

        protected override void AfterImmolate() =>
            TryCastSpell(Conflagrate, 0f, GetSpellRange(ConflagrateBaseRange), ObjectManager.GetTarget(ObjectManager.Player)?.HasDebuff(Immolate) == true);
    }
}
