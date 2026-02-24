using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private readonly string pullingSpell;

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
            if (distanceToTarget < 27 && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(pullingSpell))
            {
                if (ObjectManager.Player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
                    ObjectManager.StopAllMovement();

                if (Wait.For("WarlockAfflictionPullDelay", 250))
                {
                    ObjectManager.StopAllMovement();

                    // Send pet to attack (Voidwalker tanks, Imp adds DPS)
                    ObjectManager.Pet?.Attack();

                    ObjectManager.CastSpell(pullingSpell);

                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(BotContext));
                }

                return;
            }

            NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
        }
    }
}
