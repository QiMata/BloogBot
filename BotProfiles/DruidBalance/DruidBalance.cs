using BotRunner.Interfaces;
using DruidBalance.Tasks;
using System.ComponentModel.Composition;

using BotProfiles.Common;
namespace DruidBalance
{
    [Export(typeof(IBot))]
    internal class DruidBalance : BotBase
    {
        public override string Name => "Balance Druid";

        public override string FileName => "DruidBalance.dll";


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
