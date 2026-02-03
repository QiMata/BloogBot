using WarlockAffliction.Tasks;
using System.ComponentModel.Composition;
using BotRunner;
using BotRunner.Interfaces;
using Communication;

using BotProfiles.Common;
namespace WarlockAffliction
{
    [Export(typeof(IBot))]
    internal class WarlockAffliction : BotBase
    {
        public override string Name => "Warlock Affliction";

        public override string FileName => "WarlockAffliction.dll";


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
