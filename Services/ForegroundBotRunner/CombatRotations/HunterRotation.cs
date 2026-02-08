using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Constants;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Beast Mastery hunter rotation. Ranged attacks with pet tanking.
    /// Switches to melee abilities when target is close.
    /// </summary>
    public class HunterRotation : ICombatRotation
    {
        public float DesiredRange => 25f;
        public float PullRange => 30f;

        public bool Pull(LocalPlayer player, WoWUnit target)
        {
            // Mark then shoot
            if (!target.HasDebuff(Spellbook.HuntersMark) && TryCast(player, Spellbook.HuntersMark)) return true;
            if (TryCast(player, Spellbook.ConcussiveShot)) return true;
            if (TryCast(player, Spellbook.ArcaneShot)) return true;
            // Auto Shot fallback
            Functions.LuaCall("AttackTarget()");
            return true;
        }

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            var mana = player.Mana;
            var distance = player.Position.DistanceTo(target.Position);

            // Melee range abilities (deadzone: 0-8 yards)
            if (distance < 8)
            {
                // Mongoose Bite (dodge proc)
                if (TryCast(player, Spellbook.MongooseBite))
                    return;

                // Raptor Strike
                if (mana >= 15 && TryCast(player, Spellbook.RaptorStrike))
                    return;

                // Wing Clip to slow and kite
                if (mana >= 40 && TryCast(player, Spellbook.WingClip))
                    return;

                return; // In deadzone, just melee
            }

            // Ranged abilities (8+ yards)

            // Hunter's Mark if not on target
            if (!target.HasDebuff(Spellbook.HuntersMark) && TryCast(player, Spellbook.HuntersMark))
                return;

            // Rapid Fire cooldown
            if (TryCast(player, Spellbook.RapidFire))
                return;

            // Serpent Sting if not on target
            if (!target.HasDebuff(Spellbook.SerpentSting) && mana >= 25 && TryCast(player, Spellbook.SerpentSting))
                return;

            // Multi-Shot for AOE
            if (aggressorCount >= 2 && mana >= 50 && TryCast(player, Spellbook.MultiShot))
                return;

            // Arcane Shot
            if (mana >= 25 && TryCast(player, Spellbook.ArcaneShot))
                return;

            // Concussive Shot to slow approaching mob
            if (distance < 15 && target.TargetGuid == player.Guid && TryCast(player, Spellbook.ConcussiveShot))
                return;

            // Auto Shot is handled by having a ranged weapon equipped
        }

        public void Buff(LocalPlayer player)
        {
            // Aspect of the Hawk
            if (!player.HasBuff(Spellbook.AspectOfTheHawk) && !player.HasBuff(Spellbook.AspectOfTheMonkey))
            {
                if (player.KnowsSpell(Spellbook.AspectOfTheHawk))
                    Cast(Spellbook.AspectOfTheHawk);
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
    }
}
