using BotRunner.Interfaces;
using ShamanEnhancement.Tasks;
using BotProfiles.Common;

namespace ShamanEnhancement
{
    public class ShamanEnhancement : BotBase
    {
        public override string Name => "Enhancement Shaman";

        public override string FileName => "ShamanEnhancement.dll";

        public override IBotTask CreateRestTask(IBotContext botContext) =>
            new RestTask(botContext);

        public override IBotTask CreateMoveToTargetTask(IBotContext botContext) =>
            new PullTargetTask(botContext);

        public override IBotTask CreateBuffTask(IBotContext botContext) =>
            new BuffTask(botContext);

        public override IBotTask CreatePvERotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext);

        public override IBotTask CreatePvPRotationTask(IBotContext botContext) =>
            new PvPRotationTask(botContext);
    }
}
