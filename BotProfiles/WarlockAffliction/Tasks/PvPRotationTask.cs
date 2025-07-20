using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    internal class PvPRotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override IEnumerable<string> DotSpells =>
            new[] { CurseOfAgony, Immolate, Corruption, SiphonLife, Haunt };

        protected override void BeforeRotation()
        {
            TryCastSpell(DeathCoil, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(Fear, 0, 20,
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting ||
                 ObjectManager.GetTarget(ObjectManager.Player).IsChanneling) &&
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Fear));
            TryCastSpell(LifeTap, 0, int.MaxValue,
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);
        }

        protected override void AfterDots() =>
            TryCastSpell(Haunt, 0, 30, ShouldReapply(Haunt));
    }
}
