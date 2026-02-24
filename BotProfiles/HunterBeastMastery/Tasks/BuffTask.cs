using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {

        public void Update()
        {
            if (!ObjectManager.IsSpellReady(AspectOfTheHawk) || ObjectManager.Player.HasBuff(AspectOfTheHawk))
            {
                BotTasks.Pop();
                return;
            }

            ObjectManager.CastSpell(AspectOfTheHawk);
        }
    }
}
