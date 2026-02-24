using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinRetribution.Tasks
{
    public class HealTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting) return;

            if (ObjectManager.Player.HealthPercent > 70 || ObjectManager.Player.Mana < ObjectManager.GetManaCost(HolyLight))
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.Mana > ObjectManager.GetManaCost(DivineProtection) && ObjectManager.IsSpellReady(DivineProtection))
                ObjectManager.CastSpell(DivineProtection);

            if (ObjectManager.Player.Mana > ObjectManager.GetManaCost(HolyLight) && ObjectManager.IsSpellReady(HolyLight))
                ObjectManager.CastSpell(HolyLight, castOnSelf: true);
        }
    }
}
