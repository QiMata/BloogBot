using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;

namespace WarriorArms.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private bool _threwRanged;
        private int _waitTicks;

        public PullTargetTask(IBotContext botContext) : base(botContext) { }

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

            // Dungeon pulling: use ranged throw to pull mob back to the group.
            // Overworld: use Charge to gap-close aggressively.
            bool isDungeon = ObjectManager.Player.MapId != 0 && ObjectManager.Player.MapId != 1;

            if (isDungeon)
            {
                // DUNGEON PULL: throw at range, then wait for mob to come to us
                if (!_threwRanged && distanceToTarget < 30 && distanceToTarget > 5)
                {
                    ObjectManager.StopAllMovement();
                    // Try Throw (thrown weapon ability) — all warriors have this
                    if (ObjectManager.IsSpellReady("Throw"))
                    {
                        ObjectManager.CastSpell("Throw");
                        _threwRanged = true;
                        return;
                    }
                    // Fallback: just walk into range and let aggro happen naturally
                }

                if (_threwRanged)
                {
                    // Wait for mob to come to us (up to ~3 seconds)
                    _waitTicks++;
                    ObjectManager.StopAllMovement();
                    if (_waitTicks > 30 || distanceToTarget < 5)
                    {
                        // Mob arrived or timeout — engage
                        BotTasks.Pop();
                        BotTasks.Push(new PvERotationTask(BotContext));
                        return;
                    }
                    return;
                }

                // If not yet in throw range, navigate toward target
                if (distanceToTarget > 25)
                {
                    NavigateToward(target.Position);
                    return;
                }

                // Close enough for melee — engage
                if (distanceToTarget < 5)
                {
                    ObjectManager.StopAllMovement();
                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                    return;
                }

                // In range 5-30 but throw wasn't ready — just walk in
                NavigateToward(target.Position);
            }
            else
            {
                // OVERWORLD PULL: aggressive Charge gap-close
                if (distanceToTarget < 25 && distanceToTarget > 8 && !ObjectManager.Player.IsCasting
                    && ObjectManager.IsSpellReady("Charge")
                    && ObjectManager.Player.InLosWith(target))
                    ObjectManager.CastSpell("Charge");

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
