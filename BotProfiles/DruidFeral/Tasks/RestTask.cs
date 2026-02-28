using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace DruidFeral.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            if (InCombat)
            {
                Wait.RemoveAll();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                return;
            }

            if (HealthOk && ManaOk)
            {
                if (ObjectManager.Player.HasBuff(BearForm) && Wait.For("BearFormDelay", 1000, true))
                    ObjectManager.CastSpell(BearForm);
                else if (ObjectManager.Player.HasBuff(CatForm) && Wait.For("CatFormDelay", 1000, true))
                    ObjectManager.CastSpell(CatForm);
                else
                {
                    Wait.RemoveAll();
                    ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                    BotTasks.Pop();
                    BotTasks.Push(new BuffTask(BotContext));
                }
                return;
            }

            if (ObjectManager.Player.CurrentShapeshiftForm == BearForm)
                ObjectManager.CastSpell(BearForm);

            if (ObjectManager.Player.CurrentShapeshiftForm == CatForm)
                ObjectManager.CastSpell(CatForm);

            if (ObjectManager.Player.HealthPercent < 60 && ObjectManager.Player.CurrentShapeshiftForm == HumanForm && !ObjectManager.Player.HasBuff(Regrowth) && Wait.For("SelfHealDelay", 5000, true))
                ObjectManager.CastSpell(Regrowth);

            if (ObjectManager.Player.HealthPercent < 80 && ObjectManager.Player.CurrentShapeshiftForm == HumanForm && !ObjectManager.Player.HasBuff(Rejuvenation) && !ObjectManager.Player.HasBuff(Regrowth) && Wait.For("SelfHealDelay", 5000, true))
                ObjectManager.CastSpell(Rejuvenation);

            // Use best available drink from inventory
            IWoWItem? drinkItem = ConsumableData.FindBestDrink(ObjectManager);

            if (ObjectManager.Player.Level > 8 && drinkItem != null && !ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60 && ObjectManager.Player.CurrentShapeshiftForm == HumanForm)
                drinkItem.Use();
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent >= 81;

        private bool ManaOk => (ObjectManager.Player.Level <= 8 && ObjectManager.Player.ManaPercent > 50) || ObjectManager.Player.ManaPercent >= 90 || (ObjectManager.Player.ManaPercent >= 65 && !ObjectManager.Player.IsDrinking);

        private bool InCombat => ObjectManager.Aggressors.Any();
    }
}
