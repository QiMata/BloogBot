using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
{
    internal class PullTargetTask : BotTask, IBotTask
    {
        private readonly string pullingSpell;
        private Position currentWaypoint;

        internal PullTargetTask(IBotContext botContext) : base(botContext)
        {
            if (ObjectManager.IsSpellReady(CurseOfAgony))
                pullingSpell = CurseOfAgony;
            else
                pullingSpell = ShadowBolt;
        }

        public void Update()
        {

            if (ObjectManager.Pet == null && (ObjectManager.IsSpellReady(SummonImp) || ObjectManager.IsSpellReady(SummonVoidwalker)))
            {
                ObjectManager.StopAllMovement();
                BotTasks.Push(new SummonPetTask(BotContext));
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget < 27 && ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(pullingSpell))
            {
                if (ObjectManager.Player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
                    ObjectManager.StopAllMovement();

                if (Wait.For("WarlockAfflictionPullDelay", 250))
                {
                    ObjectManager.StopAllMovement();
                    ObjectManager.CastSpell(pullingSpell);

                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                }

                return;
            }

            Position[] nextWaypoint = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.GetTarget(ObjectManager.Player).Position, true);
            if (nextWaypoint.Length > 1)
            {
                currentWaypoint = nextWaypoint[1];
            }
            else
            {
                BotTasks.Pop();
                return;
            }

            ObjectManager.MoveToward(currentWaypoint);
        }
    }
}
