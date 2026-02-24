using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PriestDiscipline.Tasks
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

            if (ObjectManager.Player.IsInCombat || ObjectManager.Aggressors.Any())
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

                // Pre-shield before pulling
                if (ObjectManager.IsSpellReady(PowerWordShield) && !ObjectManager.Player.HasDebuff(WeakenedSoul)
                    && !ObjectManager.Player.HasBuff(PowerWordShield))
                {
                    ObjectManager.CastSpell(PowerWordShield, castOnSelf: true);
                    return;
                }

                if (!ObjectManager.Player.IsCasting && Wait.For("DiscPriestPullDelay", 250))
                {
                    Wait.Remove("DiscPriestPullDelay");

                    if (ObjectManager.IsSpellReady(HolyFire))
                        ObjectManager.CastSpell(HolyFire);
                    else
                        ObjectManager.CastSpell(Smite);

                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                }
                return;
            }

            NavigateToward(target.Position);
        }
    }
}
