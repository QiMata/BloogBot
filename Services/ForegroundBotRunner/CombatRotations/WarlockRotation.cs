using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Affliction warlock rotation. DoT-based with Life Tap for mana.
    /// Relies on pet (Voidwalker) for tanking.
    /// </summary>
    public class WarlockRotation : ICombatRotation
    {
        public float DesiredRange => 28f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            if (TryCast(player, Spellbook.ShadowBolt)) return true;
            if (TryCast(player, Spellbook.Immolate)) return true;
            return false;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var mana = player.Mana;
            var manaPct = player.MaxMana > 0 ? (float)mana / player.MaxMana * 100 : 100;
            var hpPct = GetHealthPct(player);

            // Life Tap if low mana but decent HP
            if (manaPct < 20 && hpPct > 50 && TryCast(player, Spellbook.LifeTap))
                return;

            // Fear if low HP and being hit
            if (hpPct < 25 && target.TargetGuid == player.Guid && TryCast(player, Spellbook.Fear))
                return;

            // Death Coil emergency
            if (hpPct < 20 && TryCast(player, Spellbook.DeathCoil))
                return;

            // Corruption if not on target
            if (!target.HasDebuff(Spellbook.Corruption) && TryCast(player, Spellbook.Corruption))
                return;

            // Curse of Agony if not on target
            if (!target.HasDebuff(Spellbook.CurseOfAgony) && TryCast(player, Spellbook.CurseOfAgony))
                return;

            // Siphon Life if known
            if (!target.HasDebuff(Spellbook.SiphonLife) && TryCast(player, Spellbook.SiphonLife))
                return;

            // Immolate if no other DoTs to apply
            if (!target.HasDebuff(Spellbook.Immolate) && TryCast(player, Spellbook.Immolate))
                return;

            // Drain Soul if target is low (for soul shard)
            var targetHpPct = target.MaxHealth > 0 ? (float)target.Health / target.MaxHealth * 100 : 100;
            if (targetHpPct < 25 && TryCast(player, Spellbook.DrainSoul))
                return;

            // Shadow Bolt as filler
            if (!player.IsCasting && TryCast(player, Spellbook.ShadowBolt))
                return;
        }

        public void Buff(LocalPlayer player)
        {
            // Demon Armor / Demon Skin
            if (!player.HasBuff(Spellbook.DemonArmor) && !player.HasBuff(Spellbook.DemonSkin))
            {
                if (player.KnowsSpell(Spellbook.DemonArmor))
                    Cast(Spellbook.DemonArmor);
                else if (player.KnowsSpell(Spellbook.DemonSkin))
                    Cast(Spellbook.DemonSkin);
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
