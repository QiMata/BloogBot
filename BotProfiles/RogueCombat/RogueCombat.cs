using System.ComponentModel.Composition;
using BotRunner.Interfaces;
using RogueCombat.Tasks;

using BotProfiles.Common;
namespace RogueCombat
{
    [Export(typeof(IBot))]
    internal class RogueCombat : BotBase
    {
        public override string Name => "Combat Rogue";

        public override string FileName => "RogueCombat.dll";


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
