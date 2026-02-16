using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;

namespace WarriorArms.Tasks
{
    public class RestTask : BotTask, IBotTask
    {
        public RestTask(IBotContext botContext) : base(botContext)
        {
            ObjectManager.SetTarget(ObjectManager.Player.Guid);

            if (ObjectManager.GetTarget(ObjectManager.Player)?.Guid == ObjectManager.Player.Guid)
            {
                if (ObjectManager.GetEquippedItems().Any(x => x.DurabilityPercentage > 0 && x.DurabilityPercentage < 100))
                {
                    ObjectManager.SendChatMessage(".repairitems");
                }
            }
        }

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
        }
    }
}
