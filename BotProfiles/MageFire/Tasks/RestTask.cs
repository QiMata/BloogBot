using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace MageFire.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (InCombat)
            {
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

            if (ObjectManager.Player.IsChanneling)
                return;

            if (ObjectManager.Player.ManaPercent < 20 && ObjectManager.IsSpellReady(Evocation))
            {
                ObjectManager.CastSpell(Evocation);
                return;
            }

            if (!ObjectManager.Player.IsEating && ObjectManager.Player.HealthPercent < 80 && Wait.For("EatFoodDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var foodItem = ConsumableData.FindBestFood(ObjectManager);
                foodItem?.Use();
            }

            if (!ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 80 && Wait.For("DrinkDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var drinkItem = ConsumableData.FindBestDrink(ObjectManager);
                drinkItem?.Use();
            }
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent > 90;

        private bool ManaOk => (ObjectManager.Player.Level < 6 && ObjectManager.Player.ManaPercent > 60) || ObjectManager.Player.ManaPercent >= 90 || (ObjectManager.Player.ManaPercent >= 75 && !ObjectManager.Player.IsDrinking);

        private bool InCombat => ObjectManager.Player.IsInCombat || ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid);
    }
}
