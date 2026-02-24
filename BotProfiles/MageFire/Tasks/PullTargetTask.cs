using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFire.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private readonly string pullingSpell;

        internal PullTargetTask(IBotContext botContext) : base(botContext)
        {
            if (ObjectManager.IsSpellReady(Frostbolt))
                pullingSpell = Frostbolt;
            else
                pullingSpell = Fireball;
        }

        public void Update()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget <= 28)
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (ObjectManager.IsSpellReady(pullingSpell) && Wait.For("FireMagePull", 500))
                {
                    ObjectManager.StopAllMovement();
                    Wait.RemoveAll();
                    ObjectManager.CastSpell(pullingSpell);
                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                    return;
                }
            }
            else
            {
                NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
            }
        }
    }
}
