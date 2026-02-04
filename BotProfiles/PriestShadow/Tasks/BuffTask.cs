using BotRunner.Interfaces;
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
                Position[] locations = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, woWUnit.Position, true);

                if (locations.Length > 1)
                {
                    ObjectManager.MoveToward(locations[1]);
                }
                else
                {
                    ObjectManager.StopAllMovement();
                    BotTasks.Pop();
                    return;
                }
            }

            ObjectManager.SetTarget(woWUnit.Guid);

            if (!woWUnit.HasBuff(PowerWordFortitude) && ObjectManager.IsSpellReady(PowerWordFortitude))
                ObjectManager.CastSpell(PowerWordFortitude);
        }
    }
}
