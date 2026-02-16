using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Retribution paladin rotation. Seal + Judge + auto-attack.
    /// Self-heals when low.
    /// </summary>
    public class PaladinRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 0f;

        public bool Pull(LocalPlayer player, WoWUnit target) => false;

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var manaPct = player.MaxMana > 0 ? (float)player.Mana / player.MaxMana * 100 : 100;
            var hpPct = GetHealthPct(player);

            // Lay on Hands emergency (once per hour)
            if (hpPct < 15 && TryCast(player, Spellbook.LayOnHands))
                return;

            // Divine Protection emergency
            if (hpPct < 25 && TryCast(player, Spellbook.DivineProtection))
                return;

            // Hammer of Justice to stun and heal
            if (hpPct < 40 && TryCast(player, Spellbook.HammerOfJustice))
                return;

            // Self-heal when low
            if (hpPct < 40 && !player.IsCasting && TryCast(player, Spellbook.HolyLight))
                return;

            // Consecration for AOE
            if (aggressorCount >= 2 && manaPct > 40 && TryCast(player, Spellbook.Consecration))
                return;

            // Judgement on cooldown
            if (TryCast(player, Spellbook.Judgement))
                return;

            // Maintain seal
            if (!player.HasBuff(Spellbook.SealOfCommand) && !player.HasBuff(Spellbook.SealOfRighteousness))
            {
                if (player.KnowsSpell(Spellbook.SealOfCommand))
                    Cast(Spellbook.SealOfCommand);
                else if (player.KnowsSpell(Spellbook.SealOfRighteousness))
                    Cast(Spellbook.SealOfRighteousness);
                return;
            }

            // Exorcism vs undead/demon (instant with Art of War proc in later patches, cast time in vanilla)
            if (TryCast(player, Spellbook.Exorcism))
                return;

            // Holy Shield for prot paladins
            if (TryCast(player, Spellbook.HolyShield))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Aura
            if (!player.HasBuff(Spellbook.RetributionAura) && !player.HasBuff(Spellbook.DevotionAura) && !player.HasBuff(Spellbook.SanctityAura))
            {
                if (player.KnowsSpell(Spellbook.SanctityAura))
                    Cast(Spellbook.SanctityAura);
                else if (player.KnowsSpell(Spellbook.RetributionAura))
                    Cast(Spellbook.RetributionAura);
                else if (player.KnowsSpell(Spellbook.DevotionAura))
                    Cast(Spellbook.DevotionAura);
            }

            // Blessing
            if (!player.HasBuff(Spellbook.BlessingOfMight) && !player.HasBuff(Spellbook.BlessingOfKings))
            {
                if (player.KnowsSpell(Spellbook.BlessingOfMight))
                    Cast(Spellbook.BlessingOfMight);
            }

            // Seal before combat
            if (!player.HasBuff(Spellbook.SealOfCommand) && !player.HasBuff(Spellbook.SealOfRighteousness))
            {
                if (player.KnowsSpell(Spellbook.SealOfCommand))
                    Cast(Spellbook.SealOfCommand);
                else if (player.KnowsSpell(Spellbook.SealOfRighteousness))
                    Cast(Spellbook.SealOfRighteousness);
            }
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
