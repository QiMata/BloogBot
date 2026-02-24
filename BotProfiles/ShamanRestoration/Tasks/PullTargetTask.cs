using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanRestoration.Tasks
{
    public class PullTargetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null || target.Health <= 0 || target.TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsInCombat || ObjectManager.Aggressors.Any(a => a.Guid != target.Guid))
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);

            if (distanceToTarget < 27)
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (!ObjectManager.Player.IsCasting && Wait.For("RestoShamanPullDelay", 100))
                {
                    Wait.Remove("RestoShamanPullDelay");
                    ObjectManager.CastSpell(LightningBolt);

                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                }
                return;
            }

            NavigateToward(target.Position);
        }
    }
}
