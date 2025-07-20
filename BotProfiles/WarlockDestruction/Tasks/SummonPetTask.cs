using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
{
    internal class SummonPetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            ObjectManager.Player.DoEmote(Emote.EMOTE_STATE_STAND);

            if ((!ObjectManager.Player.IsSpellReady(SummonImp)
                && !ObjectManager.Player.IsSpellReady(SummonVoidwalker)
                && !ObjectManager.Player.IsSpellReady(SummonSuccubus)
                && !ObjectManager.Player.IsSpellReady(SummonFelhunter)
                && !ObjectManager.Player.IsSpellReady(SummonFelguard)) || ObjectManager.Pet != null)
            {
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(BotContext));
                return;
            }

            string spellToCast = SummonImp;

            if (ObjectManager.Aggressors.Count() > 1 || ObjectManager.Player.HealthPercent < 50)
            {
                if (ObjectManager.Player.IsSpellReady(SummonVoidwalker))
                    spellToCast = SummonVoidwalker;
            }
            else if (ObjectManager.Player.IsSpellReady(SummonSuccubus))
            {
                spellToCast = SummonSuccubus;
            }
            else if (ObjectManager.Player.IsSpellReady(SummonFelhunter))
            {
                spellToCast = SummonFelhunter;
            }

            if (ObjectManager.Player.IsSpellReady(spellToCast))
                ObjectManager.Player.CastSpell(spellToCast);
        }
    }
}
