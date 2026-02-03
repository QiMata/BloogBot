using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinHoly.Tasks
{
    internal class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (!ObjectManager.IsSpellReady(BlessingOfMight) ||
                ObjectManager.Player.HasBuff(BlessingOfMight) ||
                ObjectManager.Player.HasBuff(BlessingOfKings) ||
                ObjectManager.Player.HasBuff(BlessingOfSanctuary))
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.IsSpellReady(BlessingOfMight) &&
                !ObjectManager.IsSpellReady(BlessingOfKings) &&
                !ObjectManager.IsSpellReady(BlessingOfSanctuary))
                TryCastSpell(BlessingOfMight);

            if (ObjectManager.IsSpellReady(BlessingOfKings) &&
                !ObjectManager.IsSpellReady(BlessingOfSanctuary))
                TryCastSpell(BlessingOfKings);

            if (ObjectManager.IsSpellReady(BlessingOfSanctuary))
                TryCastSpell(BlessingOfSanctuary);
        }

        private void TryCastSpell(string name)
        {
            if (!ObjectManager.Player.HasBuff(name) &&
                ObjectManager.IsSpellReady(name) &&
                ObjectManager.Player.Mana > ObjectManager.GetManaCost(name))
            {
                ObjectManager.CastSpell(name, castOnSelf: true);
            }
        }
    }
}

