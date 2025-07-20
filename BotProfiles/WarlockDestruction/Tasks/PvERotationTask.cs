using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
{
    internal class PvERotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void AfterImmolate() =>
            TryCastSpell(Conflagrate, 0, 28, GetDebuffRemaining(Immolate) > 3);
    }
}
