using BotRunner.Interfaces;
using GameData.Core.Models;
using WarriorProtection.Tasks;

using BotProfiles.Common;
namespace WarriorProtection
{
    public class WarriorProtection : BotBase
    {
        public override string Name => "Protection Warrior";

        public override string FileName => "WarriorProtection.dll";


        public override IBotTask CreateRestTask(IBotContext botContext) =>
            new RestTask(botContext);

        public override IBotTask CreateMoveToTargetTask(IBotContext botContext) =>
            new PullTargetTask(botContext);

        public override IBotTask CreateBuffTask(IBotContext botContext) =>
            new BuffTask(botContext);

        public override IBotTask CreatePvERotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext, botContext.ObjectManager.Player.Position);

        public override IBotTask CreatePvPRotationTask(IBotContext botContext) =>
            new PvPRotationTask(botContext);
    }
}
