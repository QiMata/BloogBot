using BotRunner;
using Communication;
using GameData.Core.Interfaces;

namespace BotProfiles.Common
{
    internal abstract class BotBase : IBot
    {
        protected BotBase(IObjectManager objectManager)
        {
            ObjectManager = objectManager;
        }

        public abstract string Name { get; }
        public abstract string FileName { get; }

        protected IObjectManager ObjectManager { get; }

        public virtual IClassContainer GetClassContainer(ActivityMemberState probe) =>
            new ClassContainer(
                Name,
                CreateRestTask,
                CreateBuffTask,
                CreateMoveToTargetTask,
                CreatePvERotationTask,
                CreatePvPRotationTask,
                probe);

        public abstract IBotTask CreateRestTask(IBotContext botContext);
        public abstract IBotTask CreateMoveToTargetTask(IBotContext botContext);
        public abstract IBotTask CreateBuffTask(IBotContext botContext);
        public abstract IBotTask CreatePvERotationTask(IBotContext botContext);
        public abstract IBotTask CreatePvPRotationTask(IBotContext botContext);
    }
}
