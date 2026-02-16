using ForegroundBotRunner.Objects;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Fallback rotation that only auto-attacks. Used for unsupported classes.
    /// </summary>
    public class AutoAttackRotation : ICombatRotation
    {
        public float DesiredRange => 5f;
        public float PullRange => 0f;

        public bool Pull(LocalPlayer player, WoWUnit target) => false;

        public void Execute(LocalPlayer player, WoWUnit target, int aggressorCount)
        {
            // Auto-attack is handled by GrindBot via StartAutoAttack()
        }

        public void Buff(LocalPlayer player)
        {
        }
    }
}
