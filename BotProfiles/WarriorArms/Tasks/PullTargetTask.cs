using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;

namespace WarriorArms.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        public PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsInCombat)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget < 25 && distanceToTarget > 8 && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady("Charge") && ObjectManager.Player.InLosWith(ObjectManager.GetTarget(ObjectManager.Player)))
                ObjectManager.CastSpell("Charge");

            if (distanceToTarget < 3)
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
