using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    internal class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if ((!ObjectManager.IsSpellReady(ArcaneIntellect) || ObjectManager.Player.HasBuff(ArcaneIntellect)) && (ObjectManager.Player.HasBuff(FrostArmor) || ObjectManager.Player.HasBuff(IceArmor) || ObjectManager.Player.HasBuff(MageArmor)) && (!ObjectManager.IsSpellReady(DampenMagic) || ObjectManager.Player.HasBuff(DampenMagic)))
            {
                BotTasks.Pop();
                BotTasks.Push(new ConjureItemsTask(BotContext));
                return;
            }

            TryCastSpell(ArcaneIntellect, castOnSelf: true);

            if (ObjectManager.IsSpellReady(MageArmor))
                TryCastSpell(MageArmor);
            else if (ObjectManager.IsSpellReady(IceArmor))
                TryCastSpell(IceArmor);
            else
                TryCastSpell(FrostArmor);

            TryCastSpell(DampenMagic, castOnSelf: true);
        }

        private void TryCastSpell(string name, bool castOnSelf = false)
        {
            if (!ObjectManager.Player.HasBuff(name) && ObjectManager.IsSpellReady(name) && ObjectManager.IsSpellReady(name))
            {
                //if (castOnSelf)
                //{
                //    Functions.LuaCall($"CastSpellByName(\"{name}\",1)");
                //}
                //else
                //    Functions.LuaCall($"CastSpellByName('{name}')");
            }
        }
    }
}
