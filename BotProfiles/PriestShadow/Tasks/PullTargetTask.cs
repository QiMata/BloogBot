using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace PriestShadow.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {
        private readonly string pullingSpell;
        internal PullTargetTask(IBotContext botContext) : base(botContext)
        {
            if (ObjectManager.Player.HasBuff(ShadowForm))
                pullingSpell = MindBlast;
            else if (ObjectManager.IsSpellReady(HolyFire))
                pullingSpell = HolyFire;
            else
                pullingSpell = Smite;
        }

        public void Update()
        {
            if (ObjectManager.Hostiles.Any())
            {
                IWoWUnit potentialNewTarget = ObjectManager.Hostiles.First();

                if (potentialNewTarget != null && potentialNewTarget.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid)
                {
                    ObjectManager.SetTarget(potentialNewTarget.Guid);
                }
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget < 27)
            {
                if (ObjectManager.Player.IsMoving)
                    ObjectManager.StopAllMovement();

                if (!ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(pullingSpell))
                {
                    if (!ObjectManager.IsSpellReady(PowerWordShield) || ObjectManager.Player.HasBuff(PowerWordShield) || ObjectManager.Player.IsInCombat)
                    {
                        if (Wait.For("ShadowPriestPullDelay", 250))
                        {
                            ObjectManager.SetTarget(ObjectManager.GetTarget(ObjectManager.Player).Guid);
                            Wait.Remove("ShadowPriestPullDelay");

                            if (!ObjectManager.Player.IsInCombat)
                                ObjectManager.CastSpell(pullingSpell);

                            ObjectManager.StopAllMovement();
                            BotTasks.Pop();
                            BotTasks.Push(new PvERotationTask(BotContext));
                        }
                    }

                    if (ObjectManager.IsSpellReady(PowerWordShield) && !ObjectManager.Player.HasDebuff(WeakenedSoul) && !ObjectManager.Player.HasBuff(PowerWordShield))
                        ObjectManager.CastSpell(PowerWordShield, castOnSelf: true);

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
