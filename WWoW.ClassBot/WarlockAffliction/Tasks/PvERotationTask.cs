using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    internal class PvERotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override IEnumerable<string> DotSpells =>
            new[] { CurseOfAgony, Immolate, Corruption, SiphonLife, Haunt };

        protected override void AfterDots() =>
            TryCastSpell(Haunt, 0, 30, ShouldReapply(Haunt));
    }
}
