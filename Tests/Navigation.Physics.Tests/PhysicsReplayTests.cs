using Navigation.Physics.Tests.Helpers;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.Helpers.RecordingTestHelpers;

namespace Navigation.Physics.Tests;

/// <summary>
/// Frame-by-frame replay tests that feed each recorded frame through the C++ PhysicsEngine
/// (StepV2) and compare the engine's predicted next position against the actual recorded
/// next position.
///
/// Teleport frames (>50y position jump) are automatically skipped by the ReplayEngine.
///
/// These require Navigation.dll + map data. If not available, tests skip gracefully.
/// </summary>
public class PhysicsReplayTests : IClassFixture<PhysicsEngineFixture>
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PhysicsReplayTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ==========================================================================
    // GROUND MOVEMENT
    // ==========================================================================

    [Fact]
    public void FlatRunForward_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgFlatRunForward);
        AssertPrecision(result, Tolerances.GroundMovement, Tolerances.P99Ground);
    }

    [Fact]
    public void LongFlatRun_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.DurotarLongFlatRun);
        AssertPrecision(result, Tolerances.GroundMovement, Tolerances.P99Ground);
    }

    // ==========================================================================
    // JUMPS AND AIRBORNE
    // ==========================================================================

    [Fact]
    public void StandingJump_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgStandingJump);
        AssertPrecision(result, Tolerances.GroundMovement, Tolerances.P99Airborne);
    }

    [Fact]
    public void RunningJumps_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgRunningJumps);
        AssertPrecision(result, Tolerances.MixedMovement, Tolerances.P99Airborne);
    }

    // ==========================================================================
    // FREE-FALL
    // ==========================================================================

    [Fact]
    public void FallFromHeight_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.OrgFallFromHeight);
        AssertPrecision(result, Tolerances.Airborne, Tolerances.P99Airborne);
    }

    // ==========================================================================
    // MIXED MOVEMENT
    // ==========================================================================

    [Fact]
    public void ComplexMixed_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.DurotarMixedMovement);
        AssertPrecision(result, Tolerances.MixedMovement, Tolerances.P99Mixed);
    }

    [Fact]
    public void UndercityMixed_FrameByFrame_PositionMatchesRecording()
    {
        var result = ReplayAndAssert(Recordings.UndercityMixed);
        AssertPrecision(result, Tolerances.MixedMovement, Tolerances.P99Mixed);
    }

    // ==========================================================================
    // SWIMMING
    // ==========================================================================

    [Fact]
    public void SwimForward_FrameByFrame_PositionMatchesRecording()
    {
        var recording = TryLoadByFilename(Recordings.Swimming, _output);
        if (recording == null) { _output.WriteLine("SKIP: No swimming recording available"); return; }
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Physics engine not initialized"); return; }

        TryPreloadMap(recording.MapId, _output);
        var result = ReplayEngine.Replay(recording);
        LogCalibrationResult("swim_forward", result, _output);

        AssertPrecision(result, Tolerances.GroundMovement, Tolerances.P99Ground);
    }

    // ==========================================================================
    // PRIVATE HELPERS
    // ==========================================================================

    private CalibrationResult ReplayAndAssert(string recordingName)
    {
        if (!_fixture.IsInitialized) { _output.WriteLine("SKIP: Physics engine not initialized"); return new CalibrationResult(); }

        var recording = LoadByFilename(recordingName, _output);
        TryPreloadMap(recording.MapId, _output);

        var result = ReplayEngine.Replay(recording);
        LogCalibrationResult(recordingName, result, _output);
        return result;
    }

    private void AssertPrecision(CalibrationResult result, float avgTolerance, float p99Tolerance)
    {
        if (result.FrameCount == 0) return;

        Assert.True(result.AvgPositionError < avgTolerance,
            $"Avg position error {result.AvgPositionError:F4}y exceeds {avgTolerance}y tolerance");

        Assert.True(result.P99 < p99Tolerance,
            $"P99 position error {result.P99:F4}y exceeds {p99Tolerance}y tolerance " +
            $"(max={result.MaxPositionError:F4}y)");
    }
}
