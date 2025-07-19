using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace MageFire.Tasks
{
    internal class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        private readonly PvERotationTask pveRotation;

        public PvPRotationTask(IBotContext botContext) : base(botContext)
        {
            pveRotation = new PvERotationTask(botContext);
        }

        public void Update() => pveRotation.Update();

        public override void PerformCombatRotation() => pveRotation.PerformCombatRotation();
    }
}
