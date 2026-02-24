using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private const string waitKey = "FrostMagePull";
        private readonly string pullingSpell;
        private readonly int range;

        internal PullTargetTask(IBotContext botContext) : base(botContext)
        {
            if (ObjectManager.IsSpellReady(Frostbolt))
                pullingSpell = Frostbolt;
            else
                pullingSpell = Fireball;

            range = 28 + (ObjectManager.GetTalentRank(3, 11) * 3);
        }

        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget <= range && ObjectManager.Player.InLosWith(ObjectManager.GetTarget(ObjectManager.Player)))
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (Wait.For(waitKey, 250))
                {
                    ObjectManager.StopAllMovement();
                    Wait.Remove(waitKey);

                    if (!ObjectManager.Player.IsInCombat)
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
