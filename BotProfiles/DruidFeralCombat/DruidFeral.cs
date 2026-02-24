using BotRunner.Interfaces;
using DruidFeral.Tasks;

using BotProfiles.Common;
namespace DruidFeral
{
    public class DruidFeral : BotBase
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
