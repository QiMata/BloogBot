using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Applies restoration druid buffs when out of combat.
    /// </summary>
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if ((ObjectManager.Player.HasBuff(MarkOfTheWild) || !ObjectManager.IsSpellReady(MarkOfTheWild)) &&
                (ObjectManager.Player.HasBuff(Thorns) || !ObjectManager.IsSpellReady(Thorns)))
            {
                BotTasks.Pop();
                return;
            }

            if (!ObjectManager.Player.HasBuff(MarkOfTheWild))
                ObjectManager.CastSpell(MarkOfTheWild);

            if (!ObjectManager.Player.HasBuff(Thorns))
                ObjectManager.CastSpell(Thorns);
        }
    }
}
