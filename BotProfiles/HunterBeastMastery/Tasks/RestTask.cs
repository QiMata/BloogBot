using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            // Keep pet summoned and healthy
            if (ObjectManager.Pet == null && ObjectManager.IsSpellReady(CallPet))
                ObjectManager.CastSpell(CallPet);
            if (ObjectManager.Pet != null && ObjectManager.Pet.HealthPercent < 40 && ObjectManager.IsSpellReady(MendPet))
                ObjectManager.CastSpell(MendPet);

            if (ObjectManager.Player.HealthPercent >= 95 ||
                (ObjectManager.Player.HealthPercent >= 80 && !ObjectManager.Player.IsEating) ||
                ObjectManager.Player.IsInCombat ||
                ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid))
            {
                Wait.RemoveAll();
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsChanneling)
                return;

            // Eat food if health is low
            if (!ObjectManager.Player.IsEating && ObjectManager.Player.HealthPercent < 80 && Wait.For("EatFoodDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var foodItem = ConsumableData.FindBestFood(ObjectManager);
                foodItem?.Use();
            }

            // Drink if mana is low
            if (!ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60 && Wait.For("DrinkDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var drinkItem = ConsumableData.FindBestDrink(ObjectManager);
                drinkItem?.Use();
            }
        }
    }
}
