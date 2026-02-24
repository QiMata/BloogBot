using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if ((ObjectManager.Player.HasBuff(MarkOfTheWild) || !ObjectManager.IsSpellReady(MarkOfTheWild)) &&
                (ObjectManager.Player.HasBuff(Thorns) || !ObjectManager.IsSpellReady(Thorns)) &&
                (ObjectManager.Player.HasBuff(OmenOfClarity) || !ObjectManager.IsSpellReady(OmenOfClarity)))
            {
                BotTasks.Pop();
                return;
            }

            if (!ObjectManager.Player.HasBuff(MarkOfTheWild))
            {
                if (ObjectManager.Player.HasBuff(MoonkinForm))
                {
                    ObjectManager.CastSpell(MoonkinForm);
                }

                ObjectManager.CastSpell(MarkOfTheWild);
            }

            ObjectManager.CastSpell(Thorns);
            ObjectManager.CastSpell(OmenOfClarity);
        }
    }
}
