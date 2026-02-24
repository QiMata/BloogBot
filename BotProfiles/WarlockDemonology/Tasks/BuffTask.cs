using BotRunner.Interfaces;
using GameData.Core.Enums;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);

            if ((!ObjectManager.IsSpellReady(DemonSkin) || ObjectManager.Player.HasBuff(DemonSkin)) && (!ObjectManager.IsSpellReady(DemonArmor) || ObjectManager.Player.HasBuff(DemonArmor)))
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.IsSpellReady(DemonArmor))
                TryCastBuff(DemonArmor);
            else
                TryCastBuff(DemonSkin);
        }

        private void TryCastBuff(string name)
        {
            if (!ObjectManager.Player.HasBuff(name) && ObjectManager.IsSpellReady(name))
                ObjectManager.CastSpell(name);
        }
    }
}
