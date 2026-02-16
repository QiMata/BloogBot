using ForegroundBotRunner.Objects;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Interface for class-specific combat spell rotations.
    /// Called once per GrindBot tick (~500ms) during the Combat phase.
    /// </summary>
    public interface ICombatRotation
    {
        /// <summary>
        /// Preferred combat range in yards. GrindBot will approach to this distance.
        /// Melee classes return ~5, ranged return ~25-30.
        /// </summary>
        float DesiredRange { get; }

        /// <summary>
        /// Pull range in yards. Returns 0 if class has no ranged pull.
        /// GrindBot will use Pull() when within this range but outside DesiredRange.
        /// </summary>
        float PullRange { get; }

        /// <summary>
        /// Cast a ranged opener / pull spell at the target.
        /// Called once when entering combat from MoveToTarget phase.
        /// Returns true if a pull spell was cast.
        /// </summary>
        bool Pull(LocalPlayer player, WoWUnit target);

        /// <summary>
        /// Execute one combat action (spell cast / ability use).
        /// Called every tick while in combat with a live target.
        /// Should cast at most ONE spell per call to avoid GCD conflicts.
        /// </summary>
        void Execute(LocalPlayer player, WoWUnit target, int aggressorCount);

        /// <summary>
        /// Apply self-buffs or pre-combat preparation.
        /// Called during Rest/FindTarget phases when out of combat.
        /// </summary>
        void Buff(LocalPlayer player);
    }
}
