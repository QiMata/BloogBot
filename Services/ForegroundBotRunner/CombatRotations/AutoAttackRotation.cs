using ForegroundBotRunner.Objects;
using GameData.Core.Models;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Fallback rotation that only auto-attacks. Used for unsupported classes.
    /// </summary>
    public class AutoAttackRotation : ICombatRotation
    {
        public float DesiredRange => CombatDistance.GetMeleeAttackRange(CombatDistance.DEFAULT_PLAYER_COMBAT_REACH, CombatDistance.DEFAULT_CREATURE_COMBAT_REACH);
        public float PullRange => 0f;

        public bool Pull(LocalPlayer player, WoWUnit target) => false;

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            // Auto-attack is handled via StateManager StartMeleeAttack action
        }

        public void Buff(LocalPlayer player)
        {
        }
    }
}
