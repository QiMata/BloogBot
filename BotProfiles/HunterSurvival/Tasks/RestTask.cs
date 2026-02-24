using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
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

            if (!ObjectManager.Player.IsEating && ObjectManager.Player.HealthPercent < 80 && Wait.For("EatFoodDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                ConsumableData.FindBestFood(ObjectManager)?.Use();
            }

            if (!ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60 && Wait.For("DrinkDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                ConsumableData.FindBestDrink(ObjectManager)?.Use();
            }
        }
    }
}
