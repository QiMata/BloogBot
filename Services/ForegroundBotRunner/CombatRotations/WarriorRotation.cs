using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Arms/Fury warrior rotation. Prioritizes stance-appropriate abilities.
    /// Works from level 1 with only auto-attack, scales up as abilities are learned.
    /// </summary>
    public class WarriorRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 0f;

        public bool Pull(LocalPlayer player, WoWUnit target) => false;

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var rage = player.Rage;
            var targetHpPct = target.MaxHealth > 0 ? (float)target.Health / target.MaxHealth * 100 : 100;

            // Execute at low HP (requires Battle Stance or Berserker Stance)
            if (targetHpPct <= 20 && rage >= 15 && TryCast(player, Spellbook.Execute))
                return;

            // Overpower when available (Battle Stance, dodge proc)
            if (rage >= 5 && TryCast(player, Spellbook.Overpower))
                return;

            // Mortal Strike (Arms 31pt talent)
            if (rage >= 30 && TryCast(player, Spellbook.MortalStrike))
                return;

            // Bloodthirst (Fury 31pt talent)
            if (rage >= 30 && TryCast(player, Spellbook.Bloodthirst))
                return;

            // Whirlwind (Fury, costs 25 rage)
            if (rage >= 25 && aggressorCount >= 2 && TryCast(player, Spellbook.Whirlwind))
                return;

            // Sweeping Strikes for AOE
            if (aggressorCount >= 2 && rage >= 30 && TryCast(player, Spellbook.SweepingStrikes))
                return;

            // Rend if target doesn't have it (DoT, good for leveling)
            if (rage >= 10 && !target.HasDebuff(Spellbook.Rend) && TryCast(player, Spellbook.Rend))
                return;

            // Thunder Clap for AOE slow
            if (aggressorCount >= 2 && rage >= 20 && TryCast(player, Spellbook.ThunderClap))
                return;

            // Heroic Strike as rage dump
            if (rage >= 15 && TryCast(player, Spellbook.HeroicStrike))
                return;

            // Battle Shout if not active
            if (rage >= 10 && !player.HasBuff(Spellbook.BattleShout) && TryCast(player, Spellbook.BattleShout))
                return;

            // Bloodrage for free rage (only if HP is decent)
            if (rage < 20 && GetHealthPct(player) > 50 && TryCast(player, Spellbook.Bloodrage))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Ensure Battle Stance
            if (player.CurrentStance == "None" && player.KnowsSpell(Spellbook.BattleStance))
            {
                Cast(Spellbook.BattleStance);
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
