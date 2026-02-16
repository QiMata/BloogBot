using BotRunner.Interfaces;

namespace BotProfiles.Common
{
    public abstract class BotBase
    {
        public abstract string Name { get; }
        public abstract string FileName { get; }

        public abstract IBotTask CreateRestTask(IBotContext botContext);
        public abstract IBotTask CreateMoveToTargetTask(IBotContext botContext);
        public abstract IBotTask CreateBuffTask(IBotContext botContext);
        public abstract IBotTask CreatePvERotationTask(IBotContext botContext);
        public abstract IBotTask CreatePvPRotationTask(IBotContext botContext);
    }
}
