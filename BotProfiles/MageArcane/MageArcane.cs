using System.ComponentModel.Composition;
using BotRunner.Interfaces;
using MageArcane.Tasks;

using BotProfiles.Common;
namespace MageArcane
{
    [Export(typeof(IBot))]
    internal class MageArcane : BotBase
    {
        public override string Name => "Arcane Mage";

        public override string FileName => "MageArcane.dll";


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
