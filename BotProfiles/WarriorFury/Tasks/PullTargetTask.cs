using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace WarriorFury.Tasks
{
    public class PullTargetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        private bool _threwRanged;
        private int _waitTicks;

        public void Update()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null || target.TappedByOther)
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

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);
            bool isDungeon = ObjectManager.Player.MapId != 0 && ObjectManager.Player.MapId != 1;

            if (isDungeon)
            {
                if (!_threwRanged && distanceToTarget < 30 && distanceToTarget > 5)
                {
                    ObjectManager.StopAllMovement();
                    if (ObjectManager.IsSpellReady("Throw"))
                    {
                        ObjectManager.CastSpell("Throw");
                        _threwRanged = true;
                        return;
                    }
                }

                if (_threwRanged)
                {
                    _waitTicks++;
                    ObjectManager.StopAllMovement();
                    if (_waitTicks > 30 || distanceToTarget < 5)
                    {
                        BotTasks.Pop();
                        BotTasks.Push(new PvERotationTask(BotContext));
                        return;
                    }
                    return;
                }

                if (distanceToTarget > 25)
                {
                    NavigateToward(target.Position);
                    return;
                }

                if (distanceToTarget < 5)
                {
                    ObjectManager.StopAllMovement();
                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                    return;
                }

                NavigateToward(target.Position);
            }
            else
            {
                if (distanceToTarget < 25 && distanceToTarget > 8 && !ObjectManager.Player.IsCasting
                    && ObjectManager.IsSpellReady("Charge") && ObjectManager.Player.InLosWith(target))
                    ObjectManager.CastSpell(Charge);

                if (distanceToTarget < 3)
                {
                    ObjectManager.StopAllMovement();
                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                    return;
                }

                NavigateToward(target.Position);
            }
        }
    }
}
