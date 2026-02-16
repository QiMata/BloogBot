using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Enhancement shaman rotation. Melee with shocks and weapon enchants.
    /// Self-heals when low.
    /// </summary>
    public class ShamanRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            if (TryCast(player, Spellbook.LightningBolt)) return true;
            return false;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var manaPct = player.MaxMana > 0 ? (float)player.Mana / player.MaxMana * 100 : 100;
            var hpPct = GetHealthPct(player);

            // Self-heal if low
            if (hpPct < 35 && !player.IsCasting && TryCast(player, Spellbook.HealingWave))
                return;

            // Stormstrike (Enhancement talent)
            if (TryCast(player, Spellbook.Stormstrike))
                return;

            // Earth Shock to interrupt or as main damage
            if (target.IsCasting && TryCast(player, Spellbook.EarthShock))
                return;

            // Flame Shock if Earth Shock on CD
            if (!target.HasDebuff(Spellbook.FlameShock) && TryCast(player, Spellbook.FlameShock))
                return;

            // Earth Shock as filler
            if (manaPct > 30 && TryCast(player, Spellbook.EarthShock))
                return;

            // Lightning Bolt if far (pulling or kiting)
            var distance = player.Position.DistanceTo(target.Position);
            if (distance > 8 && !player.IsCasting && TryCast(player, Spellbook.LightningBolt))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Lightning Shield
            if (!player.HasBuff(Spellbook.LightningShield) && player.KnowsSpell(Spellbook.LightningShield))
                Cast(Spellbook.LightningShield);

            // Weapon enchant
            if (!player.MainhandIsEnchanted)
            {
                if (player.KnowsSpell(Spellbook.WindfuryWeapon))
                    Cast(Spellbook.WindfuryWeapon);
                else if (player.KnowsSpell(Spellbook.RockbiterWeapon))
                    Cast(Spellbook.RockbiterWeapon);
                else if (player.KnowsSpell(Spellbook.FlametongueWeapon))
                    Cast(Spellbook.FlametongueWeapon);
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
