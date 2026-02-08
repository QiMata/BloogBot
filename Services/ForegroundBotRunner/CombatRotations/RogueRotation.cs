using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Combat rogue rotation. Sinister Strike to build combo points, finishers at 5cp.
    /// </summary>
    public class RogueRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 0f;

        public bool Pull(LocalPlayer player, WoWUnit target) => false;

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var energy = player.Energy;
            var comboPoints = player.ComboPoints;
            var targetHpPct = target.MaxHealth > 0 ? (float)target.Health / target.MaxHealth * 100 : 100;

            // Kick to interrupt casting
            if (target.IsCasting && energy >= 25 && TryCast(player, Spellbook.Kick))
                return;

            // Evasion if taking heavy damage
            if (GetHealthPct(player) < 30 && TryCast(player, Spellbook.Evasion))
                return;

            // Blade Flurry for AOE
            if (aggressorCount >= 2 && TryCast(player, Spellbook.BladeFlurry))
                return;

            // Adrenaline Rush for burst
            if (aggressorCount >= 2 && TryCast(player, Spellbook.AdrenalineRush))
                return;

            // Riposte when available (parry proc)
            if (player.CanRiposte && energy >= 10 && TryCast(player, Spellbook.Riposte))
                return;

            // Slice and Dice at 2+ cp if not active
            if (comboPoints >= 2 && !player.HasBuff(Spellbook.SliceAndDice) && energy >= 25 && TryCast(player, Spellbook.SliceAndDice))
                return;

            // Eviscerate at 5 cp (or 3+ if target is low)
            if ((comboPoints >= 5 || (comboPoints >= 3 && targetHpPct < 30)) && energy >= 35 && TryCast(player, Spellbook.Eviscerate))
                return;

            // Sinister Strike to build combo points
            if (energy >= 45 && TryCast(player, Spellbook.SinisterStrike))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Rogues don't have self-buffs to maintain while grinding
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
