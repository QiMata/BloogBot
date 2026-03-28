using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverResweepBookkeepingTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverResweepBookkeeping_UsableHorizontalResultNormalizesDirectionAndConsumesBudget()
    {
        uint finalize = EvaluateWoWGroundedDriverResweepBookkeeping(
            direction: new Vector3(1.0f, 0.0f, 0.0f),
            sweepScalar: 4.0f,
            correction: new Vector3(0.0f, 2.0f, 0.0f),
            horizontalBudgetBefore: 10.0f,
            out GroundedDriverResweepBookkeepingTrace trace);

        float magnitude = MathF.Sqrt(20.0f);

        Assert.Equal(0u, finalize);
        Assert.Equal(1u, trace.NormalizedDirection);
        Assert.Equal(1u, trace.WroteHorizontalPair);
        Assert.Equal(1u, trace.NormalizedHorizontalPair);
        Assert.Equal(0u, trace.FinalizeFlag);
        Assert.Equal(4.0f, trace.OutputCombinedMove.X, 6);
        Assert.Equal(2.0f, trace.OutputCombinedMove.Y, 6);
        Assert.Equal(0.0f, trace.OutputCombinedMove.Z, 6);
        Assert.Equal(magnitude, trace.OutputSweepDistance, 6);
        Assert.Equal(4.0f / magnitude, trace.OutputDirection.X, 6);
        Assert.Equal(2.0f / magnitude, trace.OutputDirection.Y, 6);
        Assert.Equal(0.0f, trace.OutputDirection.Z, 6);
        Assert.Equal(4.0f / magnitude, trace.OutputHorizontalX, 6);
        Assert.Equal(2.0f / magnitude, trace.OutputHorizontalY, 6);
        Assert.Equal(10.0f - magnitude, trace.OutputHorizontalBudget, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverResweepBookkeeping_VerticalCorrectionKeepsForwardBudgetRemainder()
    {
        uint finalize = EvaluateWoWGroundedDriverResweepBookkeeping(
            direction: new Vector3(1.0f, 0.0f, 0.0f),
            sweepScalar: 3.0f,
            correction: new Vector3(0.0f, 0.0f, 1.5f),
            horizontalBudgetBefore: 8.0f,
            out GroundedDriverResweepBookkeepingTrace trace);

        float magnitude = MathF.Sqrt(11.25f);

        Assert.Equal(0u, finalize);
        Assert.Equal(1u, trace.NormalizedDirection);
        Assert.Equal(1u, trace.WroteHorizontalPair);
        Assert.Equal(1u, trace.NormalizedHorizontalPair);
        Assert.Equal(3.0f, trace.OutputCombinedMove.X, 6);
        Assert.Equal(0.0f, trace.OutputCombinedMove.Y, 6);
        Assert.Equal(1.5f, trace.OutputCombinedMove.Z, 6);
        Assert.Equal(magnitude, trace.OutputSweepDistance, 6);
        Assert.Equal(3.0f / magnitude, trace.OutputDirection.X, 6);
        Assert.Equal(0.0f, trace.OutputDirection.Y, 6);
        Assert.Equal(1.5f / magnitude, trace.OutputDirection.Z, 6);
        Assert.Equal(1.0f, trace.OutputHorizontalX, 6);
        Assert.Equal(0.0f, trace.OutputHorizontalY, 6);
        Assert.Equal(5.0f, trace.OutputHorizontalBudget, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverResweepBookkeeping_TinyCombinedMagnitudeFinalizesImmediately()
    {
        uint finalize = EvaluateWoWGroundedDriverResweepBookkeeping(
            direction: new Vector3(1.0f, 0.0f, 0.0f),
            sweepScalar: 1.0f,
            correction: new Vector3(-0.9995f, 0.0f, 0.0f),
            horizontalBudgetBefore: 9.0f,
            out GroundedDriverResweepBookkeepingTrace trace);

        Assert.Equal(1u, finalize);
        Assert.Equal(1u, trace.FinalizeFlag);
        Assert.Equal(1u, trace.TinyMagnitudeFinalize);
        Assert.Equal(0u, trace.HorizontalBudgetFinalize);
        Assert.Equal(0u, trace.WroteHorizontalPair);
        Assert.Equal(0.0005f, trace.OutputCombinedMove.X, 6);
        Assert.Equal(0.0005f, trace.OutputSweepDistance, 6);
        Assert.Equal(9.0f, trace.OutputHorizontalBudget, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverResweepBookkeeping_ExhaustedHorizontalBudgetSetsFinalizeFlag()
    {
        uint finalize = EvaluateWoWGroundedDriverResweepBookkeeping(
            direction: new Vector3(1.0f, 0.0f, 0.0f),
            sweepScalar: 3.0f,
            correction: new Vector3(1.0f, 0.0f, 0.0f),
            horizontalBudgetBefore: 4.0f,
            out GroundedDriverResweepBookkeepingTrace trace);

        Assert.Equal(1u, finalize);
        Assert.Equal(1u, trace.FinalizeFlag);
        Assert.Equal(0u, trace.TinyMagnitudeFinalize);
        Assert.Equal(1u, trace.HorizontalBudgetFinalize);
        Assert.Equal(1u, trace.WroteHorizontalPair);
        Assert.Equal(4.0f, trace.OutputCombinedXYMagnitude, 6);
        Assert.Equal(0.0f, trace.OutputHorizontalBudget, 6);
    }
}
