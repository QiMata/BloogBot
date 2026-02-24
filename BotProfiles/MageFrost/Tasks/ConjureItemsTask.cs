using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    public class ConjureItemsTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        private IWoWItem foodItem;
        private IWoWItem drinkItem;

        public void Update()
        {
            //foodItem = Inventory.GetAllItems()
            //    .FirstOrDefault(i => i.Info.Name == container.BotSettings.Food);

            //drinkItem = Inventory.GetAllItems()
            //    .FirstOrDefault(i => i.Info.Name == container.BotSettings.Drink);

            if (ObjectManager.Player.IsCasting)
                return;

            //ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);

            if (ObjectManager.Player.ManaPercent < 20)
            {
                BotTasks.Pop();
                BotTasks.Push(new RestTask(botContext));
                return;
            }

            if (ObjectManager.CountFreeSlots(false) == 0 || (foodItem != null || !ObjectManager.IsSpellReady(ConjureFood)) && (drinkItem != null || !ObjectManager.IsSpellReady(ConjureWater)))
            {
                BotTasks.Pop();

                if (ObjectManager.Player.ManaPercent <= 70)
                    BotTasks.Push(new RestTask(botContext));

                return;
            }

            uint foodCount = foodItem == null ? 0 : ObjectManager.GetItemCount(foodItem.ItemId);
            if (foodItem == null || foodCount <= 2)
                TryCastSpell(ConjureFood);

            uint drinkCount = drinkItem == null ? 0 : ObjectManager.GetItemCount(drinkItem.ItemId);
            if (drinkItem == null || drinkCount <= 2)
                TryCastSpell(ConjureWater);
        }

        private void TryCastSpell(string name)
        {
            if (ObjectManager.IsSpellReady(name) && ObjectManager.Player.IsCasting)
                ObjectManager.CastSpell(name);
        }
    }
}
