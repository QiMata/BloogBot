using BotRunner;
using BotRunner.Interfaces;
using Communication;
using System.ComponentModel.Composition;
using WarlockDemonology.Tasks;

using BotProfiles.Common;
namespace WarlockDemonology
{
    [Export(typeof(IBot))]
    internal class WarlockDemonology : BotBase
    {
        public override string Name => "Demonology Warlock";

        public override string FileName => "WarlockDemonology.dll";


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
