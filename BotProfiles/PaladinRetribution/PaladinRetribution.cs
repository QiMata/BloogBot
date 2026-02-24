using BotRunner.Interfaces;
using PaladinRetribution.Tasks;

using BotProfiles.Common;
namespace PaladinRetribution
{
    public class PaladinRetribution : BotBase
    {
        public override string Name => "Retribution Paladin";

        public override string FileName => "PaladinRetribution.dll";


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
