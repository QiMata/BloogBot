using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Moves toward and pulls the selected target using Moonfire.
    /// </summary>
    public class PullTargetTask : BotTask, IBotTask
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
            if (distance < PullRange && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(Moonfire))
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

            NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
        }
    }
}
