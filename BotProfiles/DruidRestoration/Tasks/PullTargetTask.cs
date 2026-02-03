using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Moves toward and pulls the selected target using Moonfire.
    /// </summary>
    internal class PullTargetTask : BotTask, IBotTask
    {
        private const int PullRange = 30;

        internal PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther ||
                (ObjectManager.Aggressors.Any() && !ObjectManager.Aggressors.Any(a => a.Guid == ObjectManager.GetTarget(ObjectManager.Player).Guid)))
            {
                ObjectManager.StopAllMovement();
                Wait.RemoveAll();
                BotTasks.Pop();
                return;
            }

            float distance = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distance < PullRange && ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(Moonfire))
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (Wait.For("RestoDruidPull", 100))
                {
                    if (!ObjectManager.Player.IsInCombat)
                        ObjectManager.CastSpell(Moonfire);

                    if (ObjectManager.Player.IsCasting || ObjectManager.Player.IsInCombat)
                    {
                        ObjectManager.StopAllMovement();
                        Wait.RemoveAll();
                        BotTasks.Pop();
                        BotTasks.Push(new PvERotationTask(BotContext));
                    }
                }
                return;
            }

            Position[] path = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.GetTarget(ObjectManager.Player).Position, true);
            if (path.Length > 0)
                ObjectManager.MoveToward(path[0]);
        }
    }
}
