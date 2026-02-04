using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace DruidFeral.Tasks
{
    internal class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        private const string MarkOfTheWild = "Mark of the Wild";
        private const string Thorns = "Thorns";

        public void Update()
        {
            if ((ObjectManager.Player.HasBuff(MarkOfTheWild) || !ObjectManager.IsSpellReady(MarkOfTheWild)) && (ObjectManager.Player.HasBuff(Thorns) || !ObjectManager.IsSpellReady(Thorns)))
            {
                BotTasks.Pop();
                return;
            }
            
            ObjectManager.CastSpell(MarkOfTheWild);
            ObjectManager.CastSpell(Thorns);
        }
    }
}
