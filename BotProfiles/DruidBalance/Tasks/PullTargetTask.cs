using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;

namespace DruidBalance.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private const string Wrath = "Wrath";
        private const string Starfire = "Starfire";
        private const string MoonkinForm = "Moonkin Form";
        private readonly int range;
        private readonly string pullingSpell;

        internal PullTargetTask(IBotContext botContext) : base(botContext)
        {
            if (ObjectManager.Player.Level <= 19)
                range = 28;
            else if (ObjectManager.Player.Level == 20)
                range = 31;
            else
                range = 34;

            if (ObjectManager.IsSpellReady(Starfire))
                pullingSpell = Starfire;
            else
                pullingSpell = Wrath;
        }

        public void Update()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther || (ObjectManager.Aggressors.Any() && !ObjectManager.Aggressors.Any(a => a.Guid == ObjectManager.GetTarget(ObjectManager.Player).Guid)))
            {
                Wait.RemoveAll();
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsCasting)
                return;

            if (ObjectManager.IsSpellReady(MoonkinForm) && !ObjectManager.Player.HasBuff(MoonkinForm))
            {
                ObjectManager.CastSpell(MoonkinForm);
            }

            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < range && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(pullingSpell) && ObjectManager.Player.InLosWith(ObjectManager.GetTarget(ObjectManager.Player)))
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (Wait.For("BalanceDruidPullDelay", 100))
                {
                    if (!ObjectManager.Player.IsInCombat)
                        ObjectManager.CastSpell(pullingSpell);

                    if (ObjectManager.Player.IsCasting || ObjectManager.Player.IsInCombat)
                    {
                        ObjectManager.StopAllMovement();
                        Wait.RemoveAll();
                        BotTasks.Pop();
                        BotTasks.Push(new PvERotationTask(BotContext));
                    }
                }
                return;
            }

            NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
        }
    }
}
