using BotRunner;
using BotRunner.Interfaces;
using Communication;
using DruidFeral.Tasks;
using System.ComponentModel.Composition;

using BotProfiles.Common;
namespace DruidFeral
{
    [Export(typeof(IBot))]
    internal class DruidFeral : BotBase
    {
        public override string Name => "Feral Druid";

        public override string FileName => "DruidFeral.dll";


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
