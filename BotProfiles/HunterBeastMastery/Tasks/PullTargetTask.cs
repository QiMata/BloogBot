using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace HunterBeastMastery.Tasks
{
    internal class PullTargetTask : BotTask, IBotTask
    {

        internal PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.Hostiles.Any())
            {
                IWoWUnit potentialNewTarget = ObjectManager.Hostiles.First();

                if (potentialNewTarget != null && potentialNewTarget.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid)
                {
                    ObjectManager.SetTarget(potentialNewTarget.TargetGuid);
                }
            }

            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 28)
            {
                ObjectManager.StopAllMovement();
                ObjectManager.StartRangedAttack();

                BotTasks.Pop();
                BotTasks.Push(Container.CreatePvERotationTask(BotContext));
                return;
            } else
            {
                Position[] nextWaypoint = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.GetTarget(ObjectManager.Player).Position, true);
                ObjectManager.MoveToward(nextWaypoint[1]);
            }
        }
    }
}
