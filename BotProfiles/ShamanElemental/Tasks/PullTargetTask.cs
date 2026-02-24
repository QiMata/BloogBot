using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;

namespace ShamanElemental.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private const string LightningBolt = "Lightning Bolt";
        private readonly int stuckCount;
        private IWoWUnit target;

        internal PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.Aggressors.Any())
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(Container.ClassContainer.CreatePvERotationTask(BotContext));
                return;
            }

            if (ObjectManager.Hostiles.Any())
            {
                IWoWUnit potentialNewTarget = ObjectManager.Hostiles.First();

                if (potentialNewTarget != null && potentialNewTarget.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid)
                {
                    target = potentialNewTarget;
                    ObjectManager.SetTarget(potentialNewTarget.Guid);
                }
            }

            if (ObjectManager.Player.Position.DistanceTo(target.Position) < 30 && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(LightningBolt) && ObjectManager.Player.InLosWith(target))
            {
                ObjectManager.StopAllMovement();

                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
        }
    }
}
