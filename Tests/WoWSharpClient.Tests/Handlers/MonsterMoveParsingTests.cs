using System.IO;
using System.Reflection;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Handlers;

public class MonsterMoveParsingTests
{
    [Fact]
    public void ParseMonsterMove_SetsServerStartTimeOnMovementUpdateAndSplineBlock()
    {
        const uint splineStartTime = 123456u;
        const float facingAngle = 1.25f;

        byte[] body = BuildMonsterMoveBody(writer =>
        {
            writer.Write(10f);
            writer.Write(20f);
            writer.Write(30f);
            writer.Write(splineStartTime);
            writer.Write((byte)SplineType.FacingAngle);
            writer.Write(facingAngle);
            writer.Write((uint)SplineFlags.Runmode);
            writer.Write(2000u);
            writer.Write(1u);
            writer.Write(40f);
            writer.Write(50f);
            writer.Write(60f);
        });

        var parsed = ParseMonsterMove(body);

        Assert.Equal(10f, parsed.X, 3);
        Assert.Equal(20f, parsed.Y, 3);
        Assert.Equal(30f, parsed.Z, 3);
        Assert.Equal(splineStartTime, parsed.LastUpdated);
        Assert.NotNull(parsed.MovementBlockUpdate);
        Assert.Equal(splineStartTime, parsed.MovementBlockUpdate!.SplineStartTime);
        Assert.Equal(SplineType.FacingAngle, parsed.MovementBlockUpdate.SplineType);
        Assert.Equal(facingAngle, parsed.MovementBlockUpdate.FacingAngle, 3);
        Assert.Equal(2000u, parsed.MovementBlockUpdate.SplineTimestamp);
        Assert.Single(parsed.MovementBlockUpdate.SplinePoints);
        Assert.Equal(40f, parsed.MovementBlockUpdate.SplinePoints[0].X, 3);
        Assert.Equal(50f, parsed.MovementBlockUpdate.SplinePoints[0].Y, 3);
        Assert.Equal(60f, parsed.MovementBlockUpdate.SplinePoints[0].Z, 3);
    }

    [Fact]
    public void ParseMonsterMove_LinearPackedOffsets_ReconstructsWaypointsInForwardOrder()
    {
        var parsed = ParseMonsterMove(BuildLinearMonsterMoveBody(
            start: new Position(0f, 0f, 0f),
            splineStartTime: 5000u,
            durationMs: 1800u,
            points:
            [
                new Position(10f, 0f, 0f),
                new Position(20f, 10f, 0f),
                new Position(30f, 15f, 5f),
            ]));

        Assert.NotNull(parsed.MovementBlockUpdate);
        Assert.Equal(3, parsed.MovementBlockUpdate!.SplinePoints.Count);
        Assert.Equal(10f, parsed.MovementBlockUpdate.SplinePoints[0].X, 3);
        Assert.Equal(20f, parsed.MovementBlockUpdate.SplinePoints[1].X, 3);
        Assert.Equal(10f, parsed.MovementBlockUpdate.SplinePoints[1].Y, 3);
        Assert.Equal(30f, parsed.MovementBlockUpdate.SplinePoints[2].X, 3);
        Assert.Equal(15f, parsed.MovementBlockUpdate.SplinePoints[2].Y, 3);
        Assert.Equal(5f, parsed.MovementBlockUpdate.SplinePoints[2].Z, 3);
    }

    [Fact]
    public void ParseMonsterMove_CyclicCatmullRomEnterCycle_NormalizesManagedClosingLoop()
    {
        var start = new Position(0f, 0f, 0f);
        var parsed = ParseMonsterMove(BuildCyclicCatmullRomMonsterMoveBody(
            start,
            splineStartTime: 9000u,
            durationMs: 3000u,
            cyclePoints:
            [
                start,
                new Position(10f, 0f, 0f),
                new Position(10f, 10f, 0f),
            ]));

        Assert.NotNull(parsed.MovementBlockUpdate);
        Assert.Equal(3, parsed.MovementBlockUpdate!.SplinePoints.Count);

        Assert.Equal(10f, parsed.MovementBlockUpdate.SplinePoints[0].X, 3);
        Assert.Equal(0f, parsed.MovementBlockUpdate.SplinePoints[0].Y, 3);
        Assert.Equal(10f, parsed.MovementBlockUpdate.SplinePoints[1].X, 3);
        Assert.Equal(10f, parsed.MovementBlockUpdate.SplinePoints[1].Y, 3);
        Assert.Equal(0f, parsed.MovementBlockUpdate.SplinePoints[2].X, 3);
        Assert.Equal(0f, parsed.MovementBlockUpdate.SplinePoints[2].Y, 3);
    }

    private static MovementInfoUpdate ParseMonsterMove(byte[] body)
    {
        using var stream = new MemoryStream(body);
        using var reader = new BinaryReader(stream);
        var method = typeof(MovementHandler).GetMethod(
            "ParseMonsterMove",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (MovementInfoUpdate)method!.Invoke(null, [reader])!;
    }

    private static byte[] BuildLinearMonsterMoveBody(
        Position start,
        uint splineStartTime,
        uint durationMs,
        IReadOnlyList<Position> points)
    {
        Assert.NotEmpty(points);

        return BuildMonsterMoveBody(writer =>
        {
            writer.Write(start.X);
            writer.Write(start.Y);
            writer.Write(start.Z);
            writer.Write(splineStartTime);
            writer.Write((byte)SplineType.Normal);
            writer.Write((uint)SplineFlags.Runmode);
            writer.Write(durationMs);
            writer.Write((uint)points.Count);

            Position destination = points[^1];
            writer.Write(destination.X);
            writer.Write(destination.Y);
            writer.Write(destination.Z);

            for (int i = 0; i < points.Count - 1; i++)
                writer.Write(PackMonsterMoveOffset(destination - points[i]));
        });
    }

    private static byte[] BuildCyclicCatmullRomMonsterMoveBody(
        Position start,
        uint splineStartTime,
        uint durationMs,
        IReadOnlyList<Position> cyclePoints)
    {
        Assert.NotEmpty(cyclePoints);

        return BuildMonsterMoveBody(writer =>
        {
            writer.Write(start.X);
            writer.Write(start.Y);
            writer.Write(start.Z);
            writer.Write(splineStartTime);
            writer.Write((byte)SplineType.Normal);
            writer.Write((uint)(SplineFlags.Runmode | SplineFlags.Flying | SplineFlags.Cyclic | SplineFlags.EnterCycle));
            writer.Write(durationMs);
            writer.Write((uint)(cyclePoints.Count + 1));

            // VMaNGOS prepends a fake start node for cyclic Catmull-Rom packets.
            writer.Write(start.X);
            writer.Write(start.Y);
            writer.Write(start.Z);

            foreach (var point in cyclePoints)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }
        });
    }

    private static byte[] BuildMonsterMoveBody(Action<BinaryWriter> writeBody)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writeBody(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static uint PackMonsterMoveOffset(Position offset)
    {
        uint packed = 0;
        packed |= (uint)((int)(offset.X / 0.25f) & 0x7FF);
        packed |= (uint)(((int)(offset.Y / 0.25f) & 0x7FF) << 11);
        packed |= (uint)(((int)(offset.Z / 0.25f) & 0x3FF) << 22);
        return packed;
    }
}
