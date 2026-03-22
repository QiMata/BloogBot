using System;

namespace GameData.Core.Models;

/// <summary>
/// Vanilla WoW 1.12.1 combat distance calculations.
/// Implements MaNGOS melee range formula: attacker.CombatReach + target.CombatReach + BASE_OFFSET + leeway.
/// </summary>
public static class CombatDistance
{
    /// <summary>Base melee range offset added to combined combat reach (4/3 yards).</summary>
    public const float BASE_MELEERANGE_OFFSET = 4.0f / 3.0f;

    /// <summary>
    /// Extra melee range when both attacker AND target are moving (XZ translation).
    /// Only applies when both have MOVEFLAG_MASK_XZ flags set and neither is walking.
    /// </summary>
    public const float MELEE_LEEWAY = 2.0f;

    /// <summary>Floor for melee range — prevents melee from requiring overlap.</summary>
    public const float NOMINAL_MELEE_RANGE = 5.0f / 3.0f;

    /// <summary>Base interaction distance (yards) added to target's bounding radius.</summary>
    public const float INTERACTION_DISTANCE = 5.0f;

    /// <summary>Default player combat reach (human male).</summary>
    public const float DEFAULT_PLAYER_COMBAT_REACH = 1.5f;

    /// <summary>Default creature combat reach.</summary>
    public const float DEFAULT_CREATURE_COMBAT_REACH = 1.5f;

    /// <summary>Default player bounding radius (human male).</summary>
    public const float DEFAULT_PLAYER_BOUNDING_RADIUS = 0.306f;

    /// <summary>
    /// Calculate melee attack range between two units.
    /// Formula: max(NOMINAL, attacker.CombatReach + target.CombatReach + BASE_OFFSET + leeway)
    /// </summary>
    /// <param name="attackerCombatReach">Attacker's UNIT_FIELD_COMBATREACH value.</param>
    /// <param name="targetCombatReach">Target's UNIT_FIELD_COMBATREACH value.</param>
    /// <param name="bothMoving">True if both units have MOVEFLAG_MASK_XZ flags set (not walking).</param>
    /// <returns>Maximum distance at which a melee attack can land.</returns>
    public static float GetMeleeAttackRange(float attackerCombatReach, float targetCombatReach, bool bothMoving = false)
    {
        float range = attackerCombatReach + targetCombatReach + BASE_MELEERANGE_OFFSET;

        if (bothMoving)
            range += MELEE_LEEWAY;

        return MathF.Max(NOMINAL_MELEE_RANGE, range);
    }

    /// <summary>
    /// Calculate interaction distance to a target (NPC, game object).
    /// Formula: INTERACTION_DISTANCE + target.BoundingRadius
    /// </summary>
    public static float GetInteractionDistance(float targetBoundingRadius)
    {
        return INTERACTION_DISTANCE + targetBoundingRadius;
    }

    /// <summary>
    /// Calculate spell range between two units, accounting for bounding radii.
    /// Formula: baseSpellRange + attacker.BoundingRadius + target.BoundingRadius
    /// </summary>
    public static float GetSpellRange(float baseSpellRange, float attackerBoundingRadius, float targetBoundingRadius)
    {
        return baseSpellRange + attackerBoundingRadius + targetBoundingRadius;
    }

    /// <summary>
    /// Check if a unit is moving in XZ plane (eligible for leeway).
    /// Uses MOVEFLAG_MASK_XZ: FORWARD | BACKWARD | STRAFE_LEFT | STRAFE_RIGHT.
    /// Walking units do NOT get leeway.
    /// </summary>
    public static bool IsMovingXZ(uint movementFlags)
    {
        const uint MASK_XZ = 0x0F; // FORWARD(1) | BACKWARD(2) | STRAFE_LEFT(4) | STRAFE_RIGHT(8)
        const uint WALK_MODE = 0x100; // MOVEFLAG_WALK_MODE

        return (movementFlags & MASK_XZ) != 0 && (movementFlags & WALK_MODE) == 0;
    }

    /// <summary>
    /// Check if the attacker is facing the target within the required arc.
    /// WoW uses a 180-degree (PI radians) frontal arc for melee attacks.
    /// </summary>
    /// <param name="attackerX">Attacker X position.</param>
    /// <param name="attackerY">Attacker Y position.</param>
    /// <param name="attackerFacing">Attacker facing angle in radians.</param>
    /// <param name="targetX">Target X position.</param>
    /// <param name="targetY">Target Y position.</param>
    /// <returns>True if the target is within the attacker's frontal arc.</returns>
    public static bool IsFacing(float attackerX, float attackerY, float attackerFacing, float targetX, float targetY)
    {
        float dx = targetX - attackerX;
        float dy = targetY - attackerY;
        float angleToTarget = MathF.Atan2(dy, dx);

        // Normalize the difference to [-PI, PI]
        float diff = NormalizeAngle(angleToTarget - attackerFacing);

        // Frontal arc is ±PI/2 (90 degrees each side = 180 degree cone)
        return MathF.Abs(diff) <= MathF.PI / 2.0f;
    }

    /// <summary>
    /// Check if target is behind the attacker (backstab arc).
    /// "Behind" = outside the 180-degree frontal arc of the TARGET.
    /// </summary>
    public static bool IsBehind(float attackerX, float attackerY, float targetX, float targetY, float targetFacing)
    {
        float dx = attackerX - targetX;
        float dy = attackerY - targetY;
        float angleFromTarget = MathF.Atan2(dy, dx);

        float diff = NormalizeAngle(angleFromTarget - targetFacing);

        // Behind = outside frontal 180° arc = angle difference > 90° from facing
        return MathF.Abs(diff) > MathF.PI / 2.0f;
    }

    /// <summary>
    /// Normalize angle to [-PI, PI] range.
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2.0f * MathF.PI;
        while (angle < -MathF.PI) angle += 2.0f * MathF.PI;
        return angle;
    }
}
