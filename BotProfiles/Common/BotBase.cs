using BotRunner;
using BotRunner.Interfaces;
using Communication;

namespace BotProfiles.Common
{
    internal abstract class BotBase : IBot
    {
        public abstract string Name { get; }
        public abstract string FileName { get; }

        public virtual IClassContainer GetClassContainer(WoWActivitySnapshot probe) =>
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
