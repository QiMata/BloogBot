using BotRunner.Interfaces;
using System.ComponentModel.Composition;
using WarlockDestruction.Tasks;

using BotProfiles.Common;
namespace WarlockDestruction
{
    [Export(typeof(IBot))]
    internal class WarlockDestruction : BotBase
    {
        public override string Name => "Destruction Warlock";

        public override string FileName => "WarlockDestruction.dll";


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
