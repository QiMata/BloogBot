using BotRunner.Interfaces;
using WarriorArms.Tasks;
using BotProfiles.Common;

namespace WarriorArms
{
    public class WarriorArms : BotBase
    {
        public override string Name => "Arms Warrior";

        public override string FileName => "WarriorArms.dll";

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
