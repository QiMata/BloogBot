using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Shadow priest rotation. DoTs + Mind Blast + wand.
    /// Self-heals and shields when low.
    /// </summary>
    public class PriestRotation : ICombatRotation
    {
        public float DesiredRange => 28f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            if (TryCast(player, Spellbook.MindBlast)) return true;
            if (TryCast(player, Spellbook.ShadowWordPain)) return true;
            if (TryCast(player, Spellbook.Smite)) return true;
            return false;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var manaPct = player.MaxMana > 0 ? (float)player.Mana / player.MaxMana * 100 : 100;
            var hpPct = GetHealthPct(player);

            // Emergency: Power Word: Shield if low HP and no Weakened Soul
            if (hpPct < 40 && !player.HasDebuff(Spellbook.WeakenedSoul) && TryCast(player, Spellbook.PowerWordShield))
                return;

            // Psychic Scream if multiple mobs and low HP
            if (hpPct < 30 && aggressorCount >= 2 && TryCast(player, Spellbook.PsychicScream))
                return;

            // Fade to drop threat
            if (hpPct < 50 && aggressorCount >= 2 && TryCast(player, Spellbook.Fade))
                return;

            // Self-heal if really low
            if (hpPct < 35 && !player.IsCasting)
            {
                if (TryCast(player, Spellbook.Heal))
                    return;
                if (TryCast(player, Spellbook.LesserHeal))
                    return;
            }

            // Shadow Word: Pain if not on target
            if (!target.HasDebuff(Spellbook.ShadowWordPain) && TryCast(player, Spellbook.ShadowWordPain))
                return;

            // Vampiric Embrace for self-healing
            if (!target.HasDebuff(Spellbook.VampiricEmbrace) && TryCast(player, Spellbook.VampiricEmbrace))
                return;

            // Mind Blast on cooldown
            if (!player.IsCasting && TryCast(player, Spellbook.MindBlast))
                return;

            // Mind Flay as filler
            if (!player.IsCasting && manaPct > 20 && TryCast(player, Spellbook.MindFlay))
                return;

            // Wand if low on mana
            if (!player.IsCasting && manaPct <= 20)
            {
                Functions.LuaCall(Spellbook.WandLuaScript);
                return;
            }

            // Smite as fallback
            if (!player.IsCasting && TryCast(player, Spellbook.Smite))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Power Word: Fortitude
            if (!player.HasBuff(Spellbook.PowerWordFortitude) && player.KnowsSpell(Spellbook.PowerWordFortitude))
                Cast(Spellbook.PowerWordFortitude);

            // Inner Fire
            if (!player.HasBuff(Spellbook.InnerFire) && player.KnowsSpell(Spellbook.InnerFire))
                Cast(Spellbook.InnerFire);

            // Shadow Protection
            if (!player.HasBuff(Spellbook.ShadowProtection) && player.KnowsSpell(Spellbook.ShadowProtection))
                Cast(Spellbook.ShadowProtection);

            // Shadowform
            if (!player.HasBuff(Spellbook.ShadowForm) && player.KnowsSpell(Spellbook.ShadowForm))
                Cast(Spellbook.ShadowForm);

            // Power Word: Shield before pull
            if (!player.HasDebuff(Spellbook.WeakenedSoul) && !player.HasBuff(Spellbook.PowerWordShield) && player.KnowsSpell(Spellbook.PowerWordShield))
                Cast(Spellbook.PowerWordShield);
        }

        private static bool TryCast(LocalPlayer player, string spellName)
        {
            try
            {
                if (!player.KnowsSpell(spellName)) return false;
                if (!player.IsSpellReady(spellName)) return false;
                Cast(spellName);
                return true;
            }
            catch { return false; }
        }

        private static void Cast(string spellName)
        {
            Functions.LuaCall($"CastSpellByName('{spellName}')");
        }

        private static float GetHealthPct(LocalPlayer player)
        {
            return player.MaxHealth > 0 ? (float)player.Health / player.MaxHealth * 100 : 100;
        }
    }
}
