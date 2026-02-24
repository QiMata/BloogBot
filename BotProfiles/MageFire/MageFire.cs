using BotRunner.Interfaces;
using MageFire.Tasks;

using BotProfiles.Common;
namespace MageFire
{
    public class MageFire : BotBase
    {
        public override string Name => "Fire Mage";

        public override string FileName => "MageFire.dll";


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
