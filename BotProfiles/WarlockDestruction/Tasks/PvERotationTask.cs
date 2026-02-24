using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
{
    public class PvERotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void AfterImmolate() =>
            TryCastSpell(Conflagrate, 0, 28, ObjectManager.GetTarget(ObjectManager.Player)?.HasDebuff(Immolate) == true);
    }
}
