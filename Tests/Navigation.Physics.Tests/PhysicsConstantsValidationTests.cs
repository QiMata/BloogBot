// PhysicsConstantsValidationTests.cs - Tests that validate C# constants match C++ values
// Ensures the managed test constants stay in sync with the native physics engine.

namespace Navigation.Physics.Tests;

using static NavigationInterop;

/// <summary>
/// Tests that validate the C# physics constants match the C++ implementation.
/// This ensures test assertions are based on the actual engine values.
/// </summary>
public class PhysicsConstantsValidationTests : IClassFixture<PhysicsEngineFixture>
{
    private readonly PhysicsEngineFixture _fixture;

    public PhysicsConstantsValidationTests(PhysicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PhysicsConstants_GravityMatchesCpp()
    {
        // Skip if native DLL not available
        if (!_fixture.IsInitialized)
        {
            return; // Skip test
        }

        // Act
        GetPhysicsConstants(out float gravity, out _, out _, out _, out _);

        // Assert
        Assert.True(MathF.Abs(gravity - PhysicsTestConstants.Gravity) < 0.01f,
            $"Gravity mismatch: C# has {PhysicsTestConstants.Gravity}, C++ has {gravity}");
    }

    [Fact]
    public void PhysicsConstants_JumpVelocityMatchesCpp()
    {
        if (!_fixture.IsInitialized) return;

        GetPhysicsConstants(out _, out float jumpVelocity, out _, out _, out _);

        Assert.True(MathF.Abs(jumpVelocity - PhysicsTestConstants.JumpVelocity) < 0.01f,
            $"JumpVelocity mismatch: C# has {PhysicsTestConstants.JumpVelocity}, C++ has {jumpVelocity}");
    }

    [Fact]
    public void PhysicsConstants_StepHeightMatchesCpp()
    {
        if (!_fixture.IsInitialized) return;

        GetPhysicsConstants(out _, out _, out float stepHeight, out _, out _);

        Assert.True(MathF.Abs(stepHeight - PhysicsTestConstants.StepHeight) < 0.01f,
            $"StepHeight mismatch: C# has {PhysicsTestConstants.StepHeight}, C++ has {stepHeight}");
    }

    [Fact]
    public void PhysicsConstants_StepDownHeightMatchesCpp()
    {
        if (!_fixture.IsInitialized) return;

        GetPhysicsConstants(out _, out _, out _, out float stepDownHeight, out _);

        Assert.True(MathF.Abs(stepDownHeight - PhysicsTestConstants.StepDownHeight) < 0.01f,
            $"StepDownHeight mismatch: C# has {PhysicsTestConstants.StepDownHeight}, C++ has {stepDownHeight}");
    }

    [Fact]
    public void PhysicsConstants_WalkableMinNormalZMatchesCpp()
    {
        if (!_fixture.IsInitialized) return;

        GetPhysicsConstants(out _, out _, out _, out _, out float walkableMinNormalZ);

        Assert.True(MathF.Abs(walkableMinNormalZ - PhysicsTestConstants.WalkableMinNormalZ) < 0.01f,
            $"WalkableMinNormalZ mismatch: C# has {PhysicsTestConstants.WalkableMinNormalZ}, C++ has {walkableMinNormalZ}");
    }

    [Fact]
    public void PhysicsConstants_AllValuesRetrieved()
    {
        if (!_fixture.IsInitialized) return;

        // Act
        GetPhysicsConstants(
            out float gravity,
            out float jumpVelocity,
            out float stepHeight,
            out float stepDownHeight,
            out float walkableMinNormalZ);

        // Assert: All values should be reasonable (not zero or invalid)
        Assert.True(gravity > 0, $"Gravity should be positive: {gravity}");
        Assert.True(jumpVelocity > 0, $"JumpVelocity should be positive: {jumpVelocity}");
        Assert.True(stepHeight > 0, $"StepHeight should be positive: {stepHeight}");
        Assert.True(stepDownHeight > 0, $"StepDownHeight should be positive: {stepDownHeight}");
        Assert.True(walkableMinNormalZ > 0 && walkableMinNormalZ < 1,
            $"WalkableMinNormalZ should be in (0,1): {walkableMinNormalZ}");
    }

    // ==========================================================================
    // DERIVED CONSTANT VALIDATION
    // ==========================================================================

    [Fact]
    public void PhysicsConstants_WalkableThresholdMatchesSlopeAngle()
    {
        // cos(60°) should equal WalkableMinNormalZ
        float cos60 = MathF.Cos(60.0f * MathF.PI / 180.0f);

        Assert.True(MathF.Abs(cos60 - PhysicsTestConstants.WalkableMinNormalZ) < 0.01f,
            $"WalkableMinNormalZ ({PhysicsTestConstants.WalkableMinNormalZ}) should equal cos(60°) ({cos60})");
    }

    [Fact]
    public void PhysicsConstants_MaxWalkableSlopeMatchesThreshold()
    {
        // arccos(WalkableMinNormalZ) should give MaxWalkableSlopeDegrees
        float angleFromThreshold = MathF.Acos(PhysicsTestConstants.WalkableMinNormalZ) * 180.0f / MathF.PI;

        Assert.True(MathF.Abs(angleFromThreshold - PhysicsTestConstants.MaxWalkableSlopeDegrees) < 0.1f,
            $"MaxWalkableSlopeDegrees ({PhysicsTestConstants.MaxWalkableSlopeDegrees}) should match " +
            $"arccos(WalkableMinNormalZ) ({angleFromThreshold})");
    }

    [Fact]
    public void PhysicsConstants_CapsuleDimensionsAreReasonable()
    {
        // A standard character should fit through a 2-yard wide doorway
        Assert.True(PhysicsTestConstants.DefaultCapsuleRadius * 2 < 2.0f,
            "Capsule diameter should be less than standard doorway width");

        // Character height should be reasonable (1.5 - 3 yards)
        Assert.True(PhysicsTestConstants.DefaultCapsuleHeight >= 1.5f &&
                   PhysicsTestConstants.DefaultCapsuleHeight <= 3.0f,
            $"Capsule height ({PhysicsTestConstants.DefaultCapsuleHeight}) should be reasonable");
    }

    [Fact]
    public void PhysicsConstants_StepHeightAllowsReasonableSteps()
    {
        // Step height should allow climbing typical stairs (~1-2 feet per step)
        // 1 foot ? 0.33 yards, so 2 feet ? 0.67 yards
        // WoW stairs can be taller, up to ~2 yards
        Assert.True(PhysicsTestConstants.StepHeight >= 1.0f,
            "StepHeight should allow climbing 1-yard steps");
        Assert.True(PhysicsTestConstants.StepHeight <= 3.0f,
            "StepHeight should not allow climbing walls");
    }

    [Fact]
    public void PhysicsConstants_JumpHeightIsReasonable()
    {
        // Maximum jump height = v?² / (2g)
        float maxJumpHeight = (PhysicsTestConstants.JumpVelocity * PhysicsTestConstants.JumpVelocity) /
                             (2.0f * PhysicsTestConstants.Gravity);

        // Should be able to jump over small obstacles but not buildings
        Assert.True(maxJumpHeight >= 1.0f, $"Jump should clear 1-yard obstacles, max height = {maxJumpHeight}");
        Assert.True(maxJumpHeight <= 5.0f, $"Jump shouldn't clear buildings, max height = {maxJumpHeight}");
    }
}
