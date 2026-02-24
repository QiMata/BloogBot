using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class PvERotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void BeforeRotation() =>
            TryCastSpell(DemonicEmpowerment, 0, int.MaxValue,
                ObjectManager.Pet != null && !ObjectManager.Pet.HasBuff(DemonicEmpowerment));
    }
}
