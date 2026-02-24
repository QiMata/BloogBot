using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class PullTargetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        private bool _petSent;

        public void Update()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null || target.Health <= 0 || target.TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsInCombat || ObjectManager.Aggressors.Any())
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);

            if (distanceToTarget < 28)
            {
                ObjectManager.StopAllMovement();

                // Send pet first to establish aggro (BM pet is the main tank)
                if (!_petSent && ObjectManager.Pet != null && ObjectManager.Pet.Health > 0)
                {
                    ObjectManager.Pet.Attack();
                    _petSent = true;
                }

                if (ObjectManager.IsSpellReady(HuntersMark) && !target.HasDebuff(HuntersMark))
                    ObjectManager.CastSpell(HuntersMark);
                else
                    ObjectManager.StartRangedAttack();

                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            NavigateToward(target.Position);
        }
    }
}
