using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Feral druid rotation. Cat Form DPS with Bear Form emergency tanking.
    /// Falls back to caster if no forms are known (low level).
    /// </summary>
    public class DruidRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            // Pull with Moonfire in caster form before shifting
            if (TryCast(player, Spellbook.Moonfire)) return true;
            if (TryCast(player, Spellbook.Wrath)) return true;
            return false;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var hpPct = GetHealthPct(player);
            var shapeshiftForm = player.CurrentShapeshiftForm;

            // Emergency: shift to caster and heal if very low
            if (hpPct < 25 && shapeshiftForm != Spellbook.HumanForm)
            {
                // Cancel form to cast healing
                Functions.LuaCall("CancelShapeshiftForm()");
                return;
            }

            // Heal in caster form
            if (hpPct < 30 && shapeshiftForm == Spellbook.HumanForm && !player.IsCasting)
            {
                if (TryCast(player, Spellbook.Rejuvenation) && !player.HasBuff(Spellbook.Rejuvenation))
                    return;
                if (TryCast(player, Spellbook.HealingTouch))
                    return;
            }

            // Barkskin if low in caster form
            if (hpPct < 50 && shapeshiftForm == Spellbook.HumanForm && TryCast(player, Spellbook.Barkskin))
                return;

            // Cat Form combat
            if (shapeshiftForm == Spellbook.CatForm)
            {
                ExecuteCatForm(player, target);
                return;
            }

            // Bear Form combat
            if (shapeshiftForm == Spellbook.BearForm)
            {
                ExecuteBearForm(player, target, aggressorCount);
                return;
            }

            // Caster form: try to enter Cat or Bear
            if (player.KnowsSpell(Spellbook.CatForm) && hpPct > 30)
            {
                Cast(Spellbook.CatForm);
                return;
            }
            if (player.KnowsSpell(Spellbook.BearForm) && hpPct > 30)
            {
                Cast(Spellbook.BearForm);
                return;
            }

            // Low level caster rotation
            if (!target.HasDebuff(Spellbook.Moonfire) && TryCast(player, Spellbook.Moonfire))
                return;
            if (!player.IsCasting && TryCast(player, Spellbook.Wrath))
                return;
        }

        private void ExecuteCatForm(LocalPlayer player, WoWUnit target)
        {
            var energy = player.Energy;
            // Using Lua for combo points since it works in forms
            var comboPoints = player.ComboPoints;
            var targetHpPct = target.MaxHealth > 0 ? (float)target.Health / target.MaxHealth * 100 : 100;

            // Tiger's Fury for burst
            if (energy < 30 && TryCast(player, Spellbook.TigersFury))
                return;

            // Ferocious Bite at 5 cp
            if (comboPoints >= 5 && energy >= 35 && TryCast(player, Spellbook.FerociousBite))
                return;

            // Rip at 5 cp if target has lots of HP
            if (comboPoints >= 5 && targetHpPct > 50 && !target.HasDebuff(Spellbook.Rip) && energy >= 30 && TryCast(player, Spellbook.Rip))
                return;

            // Rake if not on target
            if (!target.HasDebuff(Spellbook.Rake) && energy >= 40 && TryCast(player, Spellbook.Rake))
                return;

            // Claw to build combo points
            if (energy >= 45 && TryCast(player, Spellbook.Claw))
                return;
        }

        private void ExecuteBearForm(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var rage = player.Rage;

            // Enrage for rage generation
            if (rage < 20 && TryCast(player, Spellbook.Enrage))
                return;

            // Demoralizing Roar for AOE threat
            if (aggressorCount >= 2 && rage >= 10 && TryCast(player, Spellbook.DemoralizingRoar))
                return;

            // Maul as rage dump
            if (rage >= 15 && TryCast(player, Spellbook.Maul))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Mark of the Wild
            if (!player.HasBuff(Spellbook.MarkOfTheWild) && player.KnowsSpell(Spellbook.MarkOfTheWild))
                Cast(Spellbook.MarkOfTheWild);

            // Thorns
            if (!player.HasBuff(Spellbook.Thorns) && player.KnowsSpell(Spellbook.Thorns))
                Cast(Spellbook.Thorns);

            // Omen of Clarity
            if (!player.HasBuff(Spellbook.OmenOfClarity) && player.KnowsSpell(Spellbook.OmenOfClarity))
                Cast(Spellbook.OmenOfClarity);
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
