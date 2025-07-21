using BotRunner;
using BotRunner.Interfaces;
using Communication;
using HunterBeastMastery.Tasks;
using System.ComponentModel.Composition;

using BotProfiles.Common;
namespace HunterBeastMastery
{
    [Export(typeof(IBot))]
    internal class HunterBeastMastery : BotBase
    {
        public override string Name => "Beast Mastery Hunter";

        public override string FileName => "HunterBeastMastery.dll";


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
