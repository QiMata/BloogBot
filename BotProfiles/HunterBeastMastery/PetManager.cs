using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using HunterBeastMastery.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery
{
    public class PetManagerState(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            if (!ObjectManager.IsSpellReady(CallPet) || ObjectManager.Pet != null)
            {
                ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            ObjectManager.CastSpell(CallPet);
        }
    }
}
