using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Applies restoration druid buffs when out of combat.
    /// </summary>
    internal class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if ((ObjectManager.Player.HasBuff(MarkOfTheWild) || !ObjectManager.Player.IsSpellReady(MarkOfTheWild)) &&
                (ObjectManager.Player.HasBuff(Thorns) || !ObjectManager.Player.IsSpellReady(Thorns)))
            {
                BotTasks.Pop();
                return;
            }

            if (!ObjectManager.Player.HasBuff(MarkOfTheWild))
                ObjectManager.Player.CastSpell(MarkOfTheWild);

            if (!ObjectManager.Player.HasBuff(Thorns))
                ObjectManager.Player.CastSpell(Thorns);
        }
    }
}
