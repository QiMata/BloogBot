using GameData.Core.Enums;
using GameData.Core.Models;
using Navigation.Physics.Tests;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Models;

public class WoWUnitExtrapolationTests
{
    [Fact]
    public void GetExtrapolatedPosition_BackwardUsesRunBackSpeed()
    {
        var unit = CreateUnit(
            new Position(10f, 20f, 30f),
            MovementFlags.MOVEFLAG_BACKWARD,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(5.5f, predicted.X, 3);
        Assert.Equal(20f, predicted.Y, 3);
        Assert.Equal(30f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_StrafeRightUsesPerpendicularBasis()
    {
        var unit = CreateUnit(
            new Position(0f, 0f, 0f),
            MovementFlags.MOVEFLAG_STRAFE_RIGHT,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(0f, predicted.X, 3);
        Assert.Equal(-7f, predicted.Y, 3);
        Assert.Equal(0f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_ForwardStrafeAppliesDiagonalDamping()
    {
        var unit = CreateUnit(
            new Position(0f, 0f, 0f),
            MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT,
            facing: MathF.PI / 2f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(-4.9497f, predicted.X, 3);
        Assert.Equal(4.9497f, predicted.Y, 3);
        Assert.Equal(0f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_SpeedBelowJitterThreshold_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(30f, 40f, 50f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 2.9f,
            runBackSpeed: 2.9f,
            timestampMs: 1000);

        unit.Position = new Position(31f, 41f, 51f);

        var predicted = unit.GetExtrapolatedPosition(1500);

        Assert.Equal(31f, predicted.X, 3);
        Assert.Equal(41f, predicted.Y, 3);
        Assert.Equal(51f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_SpeedAboveTeleportThreshold_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(30f, 40f, 50f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 60.5f,
            runBackSpeed: 60.5f,
            timestampMs: 1000);

        unit.Position = new Position(31f, 41f, 51f);

        var predicted = unit.GetExtrapolatedPosition(1500);

        Assert.Equal(31f, predicted.X, 3);
        Assert.Equal(41f, predicted.Y, 3);
        Assert.Equal(51f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_StaleUpdate_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(10f, 20f, 30f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        unit.Position = new Position(12f, 22f, 32f);

        var predicted = unit.GetExtrapolatedPosition(2601);

        Assert.Equal(12f, predicted.X, 3);
        Assert.Equal(22f, predicted.Y, 3);
        Assert.Equal(32f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_RecordedUndercityNpcWalk_BelowJitterThreshold_ReturnsCurrentPosition()
    {
        const string recordingName = "Dralrahgra_Undercity_2026-03-06_11-04-19";
        const ulong unitGuid = 17379391038426224136ul; // Tawny Grisette
        const int startFrame = 506;
        const int calibrationFrame = 512;
        const int targetFrame = 536;

        var recording = RecordingLoader.LoadFromFile(RecordingLoader.FindRecordingByFilename(recordingName));
        var start = GetRecordedUnit(recording, startFrame, unitGuid);
        var calibration = GetRecordedUnit(recording, calibrationFrame, unitGuid);
        var target = GetRecordedUnit(recording, targetFrame, unitGuid);

        var directionalFlags = ToDirectionalMovementFlags(start.MovementFlags);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, directionalFlags);

        float inferredSpeed = Distance2D(start.Position, calibration.Position) /
            SecondsBetween(recording, startFrame, calibrationFrame);

        var unit = CreateUnit(
            ToPosition(start.Position),
            directionalFlags,
            facing: start.Facing,
            runSpeed: inferredSpeed,
            runBackSpeed: inferredSpeed,
            timestampMs: (uint)recording.Frames[startFrame].FrameTimestamp);

        var predicted = unit.GetExtrapolatedPosition((uint)recording.Frames[targetFrame].FrameTimestamp);
        var startPosition = ToPosition(start.Position);
        var targetPosition = ToPosition(target.Position);

        Assert.True(inferredSpeed < 3.0f, $"Fixture speed {inferredSpeed:F3}y/s should stay below the jitter threshold.");
        Assert.Equal(startPosition.X, predicted.X, 3);
        Assert.Equal(startPosition.Y, predicted.Y, 3);
        Assert.Equal(startPosition.Z, predicted.Z, 3);
        Assert.True(
            Distance2D(startPosition, targetPosition) > 1.0f,
            "Fixture should show visible real-world drift so the jitter guard is meaningfully exercised.");
    }

    [Fact]
    public void GetExtrapolatedPosition_RecordedBlackrockRunner_MatchesObservedPosition()
    {
        const string recordingName = "Dralrahgra_Blackrock_Spire_2026-02-08_12-04-53";
        const ulong unitGuid = 17379391114628341262ul; // Rage Talon Dragonspawn
        const int startFrame = 3;
        const int calibrationFrame = 9;
        const int targetFrame = 15;

        var recording = RecordingLoader.LoadFromFile(RecordingLoader.FindRecordingByFilename(recordingName));
        var start = GetRecordedUnit(recording, startFrame, unitGuid);
        var calibration = GetRecordedUnit(recording, calibrationFrame, unitGuid);
        var target = GetRecordedUnit(recording, targetFrame, unitGuid);

        var directionalFlags = ToDirectionalMovementFlags(start.MovementFlags);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, directionalFlags);

        float inferredSpeed = Distance2D(start.Position, calibration.Position) /
            SecondsBetween(recording, startFrame, calibrationFrame);

        var unit = CreateUnit(
            ToPosition(start.Position),
            directionalFlags,
            facing: start.Facing,
            runSpeed: inferredSpeed,
            runBackSpeed: inferredSpeed,
            timestampMs: (uint)recording.Frames[startFrame].FrameTimestamp);

        var predicted = unit.GetExtrapolatedPosition((uint)recording.Frames[targetFrame].FrameTimestamp);
        var targetPosition = ToPosition(target.Position);

        Assert.True(
            Distance2D(predicted, targetPosition) < 0.02f,
            $"Recorded fast-run replay drifted {Distance2D(predicted, targetPosition):F4}y in XY.");
    }

    private static WoWUnit CreateUnit(
        Position position,
        MovementFlags movementFlags,
        float facing,
        float runSpeed,
        float runBackSpeed,
        uint timestampMs)
    {
        return new WoWUnit(new HighGuid(1))
        {
            Position = position,
            RunSpeed = runSpeed,
            RunBackSpeed = runBackSpeed,
            ExtrapolationBasePosition = new Position(position.X, position.Y, position.Z),
            ExtrapolationFlags = movementFlags,
            ExtrapolationFacing = facing,
            ExtrapolationTimeMs = timestampMs,
        };
    }

    private static RecordedUnit GetRecordedUnit(MovementRecording recording, int frameIndex, ulong guid)
    {
        foreach (var unit in recording.Frames[frameIndex].NearbyUnits)
        {
            if (unit.Guid == guid)
                return unit;
        }

        throw new InvalidOperationException($"Unit 0x{guid:X} missing at frame {frameIndex}.");
    }

    private static MovementFlags ToDirectionalMovementFlags(uint rawMovementFlags)
    {
        const MovementFlags directionalMask =
            MovementFlags.MOVEFLAG_FORWARD |
            MovementFlags.MOVEFLAG_BACKWARD |
            MovementFlags.MOVEFLAG_STRAFE_LEFT |
            MovementFlags.MOVEFLAG_STRAFE_RIGHT;

        return (MovementFlags)rawMovementFlags & directionalMask;
    }

    private static float SecondsBetween(MovementRecording recording, int startFrame, int endFrame)
        => (recording.Frames[endFrame].FrameTimestamp - recording.Frames[startFrame].FrameTimestamp) * 0.001f;

    private static float Distance2D(RecordedPosition start, RecordedPosition end)
        => Distance2D(ToPosition(start), ToPosition(end));

    private static float Distance2D(Position start, Position end)
        => MathF.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));

    private static Position ToPosition(RecordedPosition position)
        => new(position.X, position.Y, position.Z);
}
