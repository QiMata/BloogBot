using System;
using GameData.Core.Models;
using Xunit;

namespace BotRunner.Tests.Combat;

/// <summary>
/// Unit tests for CombatDistance — vanilla WoW 1.12.1 melee range, leeway,
/// interaction distance, spell range, and facing/arc calculations.
/// </summary>
public class CombatDistanceTests
{
    // ── Melee Range Formula ─────────────────────────────────────

    [Fact]
    public void GetMeleeAttackRange_DefaultPlayers_ReturnsExpected()
    {
        // Two players with default 1.5 combat reach:
        // 1.5 + 1.5 + 4/3 = 4.333
        var range = CombatDistance.GetMeleeAttackRange(1.5f, 1.5f);
        Assert.InRange(range, 4.33f, 4.34f);
    }

    [Fact]
    public void GetMeleeAttackRange_WithLeeway_AddsTwo()
    {
        // Same as above but with leeway: 4.333 + 2.0 = 6.333
        var range = CombatDistance.GetMeleeAttackRange(1.5f, 1.5f, bothMoving: true);
        Assert.InRange(range, 6.33f, 6.34f);
    }

    [Fact]
    public void GetMeleeAttackRange_SmallCreature_NominalFloor()
    {
        // Two tiny creatures with 0.1 combat reach:
        // 0.1 + 0.1 + 1.333 = 1.533 < NOMINAL (1.667)
        // Should clamp to NOMINAL
        var range = CombatDistance.GetMeleeAttackRange(0.1f, 0.1f);
        Assert.Equal(CombatDistance.NOMINAL_MELEE_RANGE, range);
    }

    [Fact]
    public void GetMeleeAttackRange_LargeCreature_ExceedsNominal()
    {
        // Tauren (combat reach ~2.0) vs Devilsaur (combat reach ~5.0):
        // 2.0 + 5.0 + 1.333 = 8.333
        var range = CombatDistance.GetMeleeAttackRange(2.0f, 5.0f);
        Assert.InRange(range, 8.33f, 8.34f);
    }

    [Fact]
    public void GetMeleeAttackRange_LargeCreatureWithLeeway_AddsTwo()
    {
        var range = CombatDistance.GetMeleeAttackRange(2.0f, 5.0f, bothMoving: true);
        Assert.InRange(range, 10.33f, 10.34f);
    }

    [Fact]
    public void GetMeleeAttackRange_ZeroCombatReach_ClampsToNominal()
    {
        // 0 + 0 + 1.333 = 1.333 < NOMINAL (1.667)
        var range = CombatDistance.GetMeleeAttackRange(0f, 0f);
        Assert.Equal(CombatDistance.NOMINAL_MELEE_RANGE, range);
    }

    // ── Interaction Distance ────────────────────────────────────

    [Fact]
    public void GetInteractionDistance_DefaultRadius_Returns5Plus()
    {
        // 5.0 + 0.306 = 5.306
        var dist = CombatDistance.GetInteractionDistance(0.306f);
        Assert.InRange(dist, 5.30f, 5.31f);
    }

    [Fact]
    public void GetInteractionDistance_LargeCreature_ScalesWithRadius()
    {
        // 5.0 + 3.0 = 8.0
        var dist = CombatDistance.GetInteractionDistance(3.0f);
        Assert.Equal(8.0f, dist);
    }

    [Fact]
    public void GetInteractionDistance_ZeroRadius_ReturnsBase()
    {
        var dist = CombatDistance.GetInteractionDistance(0f);
        Assert.Equal(CombatDistance.INTERACTION_DISTANCE, dist);
    }

    // ── Spell Range ─────────────────────────────────────────────

    [Fact]
    public void GetSpellRange_AddsAllRadii()
    {
        // 30y base + 0.306 attacker + 0.5 target = 30.806
        var range = CombatDistance.GetSpellRange(30f, 0.306f, 0.5f);
        Assert.InRange(range, 30.80f, 30.81f);
    }

    [Fact]
    public void GetSpellRange_ZeroRadii_ReturnsBaseRange()
    {
        var range = CombatDistance.GetSpellRange(30f, 0f, 0f);
        Assert.Equal(30f, range);
    }

    // ── IsMovingXZ ──────────────────────────────────────────────

    [Fact]
    public void IsMovingXZ_Forward_ReturnsTrue()
    {
        Assert.True(CombatDistance.IsMovingXZ(0x01)); // FORWARD
    }

    [Fact]
    public void IsMovingXZ_Backward_ReturnsTrue()
    {
        Assert.True(CombatDistance.IsMovingXZ(0x02)); // BACKWARD
    }

    [Fact]
    public void IsMovingXZ_StrafeLeft_ReturnsTrue()
    {
        Assert.True(CombatDistance.IsMovingXZ(0x04)); // STRAFE_LEFT
    }

    [Fact]
    public void IsMovingXZ_StrafeRight_ReturnsTrue()
    {
        Assert.True(CombatDistance.IsMovingXZ(0x08)); // STRAFE_RIGHT
    }

    [Fact]
    public void IsMovingXZ_ForwardAndWalking_ReturnsFalse()
    {
        // Walking disables leeway even when moving forward
        Assert.False(CombatDistance.IsMovingXZ(0x01 | 0x100)); // FORWARD | WALK_MODE
    }

    [Fact]
    public void IsMovingXZ_StationaryJumping_ReturnsFalse()
    {
        // Jumping in place (no XZ movement) should NOT trigger leeway
        Assert.False(CombatDistance.IsMovingXZ(0x2000)); // JUMPING only
    }

    [Fact]
    public void IsMovingXZ_None_ReturnsFalse()
    {
        Assert.False(CombatDistance.IsMovingXZ(0));
    }

    [Fact]
    public void IsMovingXZ_TurningOnly_ReturnsFalse()
    {
        // Turning doesn't count as XZ translation
        Assert.False(CombatDistance.IsMovingXZ(0x10 | 0x20)); // TURN_LEFT | TURN_RIGHT
    }

    // ── IsFacing ────────────────────────────────────────────────

    [Fact]
    public void IsFacing_DirectlyAhead_ReturnsTrue()
    {
        // Attacker at origin, facing east (0 rad), target at (10, 0)
        Assert.True(CombatDistance.IsFacing(0, 0, 0, 10, 0));
    }

    [Fact]
    public void IsFacing_DirectlyBehind_ReturnsFalse()
    {
        // Attacker at origin, facing east (0 rad), target at (-10, 0)
        Assert.False(CombatDistance.IsFacing(0, 0, 0, -10, 0));
    }

    [Fact]
    public void IsFacing_45Degrees_ReturnsTrue()
    {
        // Attacker at origin, facing east (0 rad), target at (10, 10) = 45° left
        Assert.True(CombatDistance.IsFacing(0, 0, 0, 10, 10));
    }

    [Fact]
    public void IsFacing_89Degrees_ReturnsTrue()
    {
        // Just barely inside the 90° cone
        float angle = 89f * MathF.PI / 180f;
        Assert.True(CombatDistance.IsFacing(0, 0, 0, MathF.Cos(angle), MathF.Sin(angle)));
    }

    [Fact]
    public void IsFacing_91Degrees_ReturnsFalse()
    {
        // Just barely outside the 90° cone
        float angle = 91f * MathF.PI / 180f;
        Assert.False(CombatDistance.IsFacing(0, 0, 0, MathF.Cos(angle), MathF.Sin(angle)));
    }

    [Fact]
    public void IsFacing_FacingNorth_TargetNorth_ReturnsTrue()
    {
        // Facing north (PI/2), target north
        Assert.True(CombatDistance.IsFacing(0, 0, MathF.PI / 2, 0, 10));
    }

    [Fact]
    public void IsFacing_FacingNorth_TargetSouth_ReturnsFalse()
    {
        // Facing north (PI/2), target south
        Assert.False(CombatDistance.IsFacing(0, 0, MathF.PI / 2, 0, -10));
    }

    // ── IsBehind ────────────────────────────────────────────────

    [Fact]
    public void IsBehind_AttackerBehindTarget_ReturnsTrue()
    {
        // Target at origin facing east (0 rad), attacker behind at (-10, 0)
        Assert.True(CombatDistance.IsBehind(-10, 0, 0, 0, 0));
    }

    [Fact]
    public void IsBehind_AttackerInFront_ReturnsFalse()
    {
        // Target at origin facing east (0 rad), attacker in front at (10, 0)
        Assert.False(CombatDistance.IsBehind(10, 0, 0, 0, 0));
    }

    [Fact]
    public void IsBehind_AttackerAtSide_ReturnsFalse()
    {
        // Target at origin facing east, attacker at 45° — still in frontal arc
        Assert.False(CombatDistance.IsBehind(10, 10, 0, 0, 0));
    }

    [Fact]
    public void IsBehind_AttackerDiagonallyBehind_ReturnsTrue()
    {
        // Target facing east, attacker at (-10, -10) = behind and to the right
        Assert.True(CombatDistance.IsBehind(-10, -10, 0, 0, 0));
    }

    // ── Boundary conditions ─────────────────────────────────────

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.InRange(CombatDistance.BASE_MELEERANGE_OFFSET, 1.333f, 1.334f);
        Assert.Equal(2.0f, CombatDistance.MELEE_LEEWAY);
        Assert.InRange(CombatDistance.NOMINAL_MELEE_RANGE, 1.666f, 1.667f);
        Assert.Equal(5.0f, CombatDistance.INTERACTION_DISTANCE);
        Assert.Equal(1.5f, CombatDistance.DEFAULT_PLAYER_COMBAT_REACH);
        Assert.InRange(CombatDistance.DEFAULT_PLAYER_BOUNDING_RADIUS, 0.305f, 0.307f);
    }
}
