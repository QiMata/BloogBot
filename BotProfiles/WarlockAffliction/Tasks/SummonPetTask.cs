using BotRunner.Interfaces;
using GameData.Core.Enums;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    public class SummonPetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {

        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);

            if ((!ObjectManager.IsSpellReady(SummonImp)
                && !ObjectManager.IsSpellReady(SummonVoidwalker)
                && !ObjectManager.IsSpellReady(SummonSuccubus)
                && !ObjectManager.IsSpellReady(SummonFelhunter)
                && !ObjectManager.IsSpellReady(SummonFelguard)) || ObjectManager.Pet != null)
            {
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            string spellToCast = SummonImp;

            bool needTankPet = ObjectManager.Aggressors.Count() > 1 || ObjectManager.Player.HealthPercent < 50;

            if (needTankPet && ObjectManager.IsSpellReady(SummonVoidwalker) && ObjectManager.IsSpellReady(FelDomination))
            {
                ObjectManager.CastSpell(FelDomination);
                spellToCast = SummonVoidwalker;
            }
            else if (ObjectManager.IsSpellReady(SummonSuccubus))
                spellToCast = SummonSuccubus;
            else if (ObjectManager.IsSpellReady(SummonFelhunter))
                spellToCast = SummonFelhunter;

            if (ObjectManager.IsSpellReady(spellToCast))
                ObjectManager.CastSpell(spellToCast);
        }
    }
}
