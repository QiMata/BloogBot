using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;

namespace WarriorProtection.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
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

            // Use bandage first if health is very low (faster than food)
            if (ObjectManager.Player.HealthPercent < 60
                && !ObjectManager.Player.IsEating
                && !ObjectManager.Player.IsChanneling
                && !ObjectManager.Player.HasDebuff("Recently Bandaged")
                && Wait.For("UseBandageDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var bandage = ConsumableData.FindBestBandage(ObjectManager);
                if (bandage != null)
                {
                    bandage.Use();
                    return;
                }
            }

            if (!ObjectManager.Player.IsEating && !ObjectManager.Player.IsChanneling && Wait.For("EatFoodDelay", 3000, true))
            {
                ObjectManager.StopAllMovement();
                var foodItem = ConsumableData.FindBestFood(ObjectManager);
                foodItem?.Use();
            }
        }
    }
}
