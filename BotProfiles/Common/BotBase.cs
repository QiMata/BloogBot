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

        /// <summary>
        /// Creates a pull task that approaches the current target and initiates combat
        /// using class-specific openers (Charge, ranged pull, etc.).
        /// Default: delegates to CreateMoveToTargetTask. Override in profiles with PullTargetTask.
        /// </summary>
        public virtual IBotTask CreatePullTargetTask(IBotContext botContext)
            => CreateMoveToTargetTask(botContext);

        public abstract IBotTask CreatePvERotationTask(IBotContext botContext);
        public abstract IBotTask CreatePvPRotationTask(IBotContext botContext);
    }
}
