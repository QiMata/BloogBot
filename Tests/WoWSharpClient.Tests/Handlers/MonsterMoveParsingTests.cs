using System.IO;
using System.Reflection;
using GameData.Core.Enums;
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

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
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
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        var method = typeof(MovementHandler).GetMethod(
            "ParseMonsterMove",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var parsed = (MovementInfoUpdate)method!.Invoke(null, [reader])!;

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
}
