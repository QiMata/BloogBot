using System.ComponentModel.Composition;
using BotRunner.Interfaces;
using MageFrost.Tasks;

using BotProfiles.Common;
namespace MageFrost
{
    [Export(typeof(IBot))]
    internal class MageFrost : BotBase
    {
        public override string Name => "Frost Mage";

        public override string FileName => "MageFrost.dll";


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
