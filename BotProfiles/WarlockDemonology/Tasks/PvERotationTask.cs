using BotProfiles.Common;
using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class PvERotationTask(IBotContext botContext) : WarlockBaseRotationTask(botContext), IBotTask
    {
        protected override void BeforeRotation() =>
            TryCastSpell(DemonicEmpowerment, condition:
                ObjectManager.Pet != null && !ObjectManager.Pet.HasBuff(DemonicEmpowerment), castOnSelf: true);
    }
}
