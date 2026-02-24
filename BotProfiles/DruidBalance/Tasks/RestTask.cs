using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            if (ObjectManager.Player.IsInCombat)
            {
                Wait.RemoveAll();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                return;
            }

            if (HealthOk && ManaOk)
            {
                Wait.RemoveAll();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            if (ObjectManager.Player.HealthPercent < 60 && !ObjectManager.Player.HasBuff(Regrowth) && Wait.For("SelfHealDelay", 5000, true))
            {
                if (!ObjectManager.Player.HasBuff(MoonkinForm) && ObjectManager.IsSpellReady(MoonkinForm))
                    ObjectManager.CastSpell(MoonkinForm);

                ObjectManager.CastSpell(Regrowth);
            }

            if (ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation) && !ObjectManager.Player.HasBuff(Regrowth) && Wait.For("SelfHealDelay", 5000, true))
            {
                if (!ObjectManager.Player.HasBuff(MoonkinForm) && ObjectManager.IsSpellReady(MoonkinForm))
                    ObjectManager.CastSpell(MoonkinForm);

                ObjectManager.CastSpell(Rejuvenation);
            }

            // Use best available drink from inventory
            IWoWItem? drinkItem = ConsumableData.FindBestDrink(ObjectManager);

            if (ObjectManager.Player.Level >= 6 && drinkItem != null && !ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60)
                drinkItem.Use();
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent >= 81;

        private bool ManaOk => (ObjectManager.Player.Level < 6 && ObjectManager.Player.ManaPercent > 50) || ObjectManager.Player.ManaPercent >= 90 || (ObjectManager.Player.ManaPercent >= 65 && !ObjectManager.Player.IsDrinking);
    }
}
