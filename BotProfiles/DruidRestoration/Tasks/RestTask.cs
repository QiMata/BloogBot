using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Handles resting logic for the restoration druid.
    /// </summary>
    internal class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            if (ObjectManager.Player.IsInCombat)
            {
                Wait.RemoveAll();
                ObjectManager.Player.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                return;
            }

            if (HealthOk && ManaOk)
            {
                Wait.RemoveAll();
                ObjectManager.Player.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            if (ObjectManager.Player.HealthPercent < 60 && Wait.For("HealTouch", 5000, true))
            {
                ObjectManager.Player.CastSpell(HealingTouch);
                return;
            }

            if (ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation) && Wait.For("Rejuvenation", 5000, true))
            {
                ObjectManager.Player.CastSpell(Rejuvenation);
            }
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent >= 90;

        private bool ManaOk => (ObjectManager.Player.Level < 6 && ObjectManager.Player.ManaPercent > 50) ||
                               ObjectManager.Player.ManaPercent >= 90 ||
                               (ObjectManager.Player.ManaPercent >= 65 && !ObjectManager.Player.IsDrinking);
    }
}
