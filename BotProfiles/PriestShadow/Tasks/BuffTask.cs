using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PriestShadow.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.PartyMembers.Any(x => x.HealthPercent < 70) && ObjectManager.Player.Mana >= ObjectManager.GetManaCost(LesserHeal))
            {
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            if (!ObjectManager.IsSpellReady(PowerWordFortitude) || ObjectManager.PartyMembers.All(x => x.HasBuff(PowerWordFortitude)))
            {
                BotTasks.Pop();
                return;
            }


            IWoWUnit woWUnit = ObjectManager.PartyMembers.First(x => !x.HasBuff(PowerWordFortitude));

            if (woWUnit.Position.DistanceTo(ObjectManager.Player.Position) > 15 || !ObjectManager.Player.InLosWith(woWUnit))
            {
                NavigateToward(woWUnit.Position);
            }

            ObjectManager.SetTarget(woWUnit.Guid);

            if (!woWUnit.HasBuff(PowerWordFortitude) && ObjectManager.IsSpellReady(PowerWordFortitude))
                ObjectManager.CastSpell(PowerWordFortitude);
        }
    }
}
