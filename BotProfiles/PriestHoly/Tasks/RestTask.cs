using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace PriestHoly.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting) return;

            if (InCombat || (HealthOk && ManaOk))
            {
                Wait.RemoveAll();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            if (!ObjectManager.Player.IsDrinking && Wait.For("HealSelfDelay", 3500, true))
            {
                ObjectManager.StopAllMovement();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);

                if (ObjectManager.Player.HealthPercent < 50)
                {
                    if (ObjectManager.IsSpellReady(Heal))
                        ObjectManager.CastSpell(Heal);
                    else
                        ObjectManager.CastSpell(LesserHeal);
                }
                else if (ObjectManager.Player.HealthPercent < 70)
                    ObjectManager.CastSpell(LesserHeal, castOnSelf: true);
            }

            IWoWItem? drinkItem = ConsumableData.FindBestDrink(ObjectManager);
            if (ObjectManager.Player.Level >= 5 && drinkItem != null && !ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60)
                drinkItem.Use();
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent > 90;

        private bool ManaOk => (ObjectManager.Player.Level < 5 && ObjectManager.Player.ManaPercent > 50) || ObjectManager.Player.ManaPercent >= 90 || (ObjectManager.Player.ManaPercent >= 65 && !ObjectManager.Player.IsDrinking);

        private bool InCombat => ObjectManager.Player.IsInCombat || ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid);
    }
}
