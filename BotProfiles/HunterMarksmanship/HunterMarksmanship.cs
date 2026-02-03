using BotRunner;
using BotRunner.Interfaces;
using Communication;
using HunterMarksmanship.Tasks;
using System.ComponentModel.Composition;

using BotProfiles.Common;
namespace HunterMarksmanship
{
    [Export(typeof(IBot))]
    internal class HunterMarksmanship : BotBase
    {
        public override string Name => "Marksmanship Hunter";

        public override string FileName => "HunterMarksmanship.dll";


        public override IBotTask CreateRestTask(IBotContext botContext) =>
            new RestTask(botContext);

        public override IBotTask CreateMoveToTargetTask(IBotContext botContext) =>
            new PullTargetTask(botContext);

        public override IBotTask CreateBuffTask(IBotContext botContext) =>
            new BuffTask(botContext);

        public override IBotTask CreatePvERotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext);

        public override IBotTask CreatePvPRotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext);
    }
}
