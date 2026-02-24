using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    public class HealTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {

        public void Update()
        {
            if (ObjectManager.Player.IsCasting) return;

            if (ObjectManager.Player.HealthPercent > 70 || (ObjectManager.Player.Mana < ObjectManager.GetManaCost(HealingTouch) && ObjectManager.Player.Mana < ObjectManager.GetManaCost(Rejuvenation)))
            {
                Wait.RemoveAll();
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.IsSpellReady(WarStomp) && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) <= 8)
                ObjectManager.CastSpell(WarStomp);

            TryCastSpell(MoonkinForm, ObjectManager.Player.HasBuff(MoonkinForm));

            TryCastSpell(Barkskin);

            TryCastSpell(Rejuvenation, !ObjectManager.Player.HasBuff(Rejuvenation));

            TryCastSpell(HealingTouch);
        }

        private void TryCastSpell(string name, bool condition = true)
        {
            if (ObjectManager.IsSpellReady(name) && condition)
                ObjectManager.CastSpell(name);
        }
    }
}
