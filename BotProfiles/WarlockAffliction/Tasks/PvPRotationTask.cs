using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override IEnumerable<string> DotSpells =>
            new[] { CurseOfAgony, Immolate, Corruption, SiphonLife, Haunt };

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

        protected override void AfterDots() =>
            TryCastSpell(Haunt, 0f, GetSpellRange(HauntBaseRange), ShouldReapply(Haunt));
    }
}
