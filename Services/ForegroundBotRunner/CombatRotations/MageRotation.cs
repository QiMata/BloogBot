using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Frost mage rotation. Frostbolt spam with Frost Nova for emergency kiting.
    /// Conjures food/water during buff phase.
    /// </summary>
    public class MageRotation : ICombatRotation
    {
        public float DesiredRange => 28f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            if (TryCast(player, Spellbook.Frostbolt)) return true;
            if (TryCast(player, Spellbook.Fireball)) return true;
            return false;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var manaPct = player.MaxMana > 0 ? (float)player.Mana / player.MaxMana * 100 : 100;
            var hpPct = GetHealthPct(player);
            var distance = player.Position.DistanceTo(target.Position);

            // Counterspell to interrupt
            if (target.IsCasting && TryCast(player, Spellbook.Counterspell))
                return;

            // Frost Nova if mob is too close and we're ranged
            if (distance < 8 && hpPct < 80 && TryCast(player, Spellbook.FrostNova))
                return;

            // Ice Barrier for protection
            if (hpPct < 70 && TryCast(player, Spellbook.IceBarrier))
                return;

            // Mana Shield emergency
            if (hpPct < 30 && manaPct > 30 && TryCast(player, Spellbook.ManaShield))
                return;

            // Cold Snap to reset cooldowns in emergency
            if (hpPct < 20 && TryCast(player, Spellbook.ColdSnap))
                return;

            // Icy Veins for burst
            if (TryCast(player, Spellbook.IcyVeins))
                return;

            // Arcane Power + Presence of Mind for burst
            if (TryCast(player, Spellbook.ArcanePower))
                return;
            if (TryCast(player, Spellbook.PresenceOfMind))
                return;

            // Cone of Cold if multiple mobs close
            if (aggressorCount >= 2 && distance < 10 && TryCast(player, Spellbook.ConeOfCold))
                return;

            // Fire Blast for instant damage while moving
            if (player.IsMoving && TryCast(player, Spellbook.FireBlast))
                return;

            // Frostbolt as main nuke
            if (!player.IsCasting && TryCast(player, Spellbook.Frostbolt))
                return;

            // Fireball fallback if no Frostbolt
            if (!player.IsCasting && TryCast(player, Spellbook.Fireball))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Frost/Ice Armor
            if (!player.HasBuff(Spellbook.IceArmor) && !player.HasBuff(Spellbook.FrostArmor) && !player.HasBuff(Spellbook.MageArmor))
            {
                if (player.KnowsSpell(Spellbook.IceArmor))
                    Cast(Spellbook.IceArmor);
                else if (player.KnowsSpell(Spellbook.FrostArmor))
                    Cast(Spellbook.FrostArmor);
            }

            // Arcane Intellect
            if (!player.HasBuff(Spellbook.ArcaneIntellect) && player.KnowsSpell(Spellbook.ArcaneIntellect))
                Cast(Spellbook.ArcaneIntellect);

            // Dampen Magic
            if (!player.HasBuff(Spellbook.DampenMagic) && player.KnowsSpell(Spellbook.DampenMagic))
                Cast(Spellbook.DampenMagic);
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
