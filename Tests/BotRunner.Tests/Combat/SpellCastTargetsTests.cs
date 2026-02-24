using GameData.Core.Models;
using System.IO;
using System.Text;

namespace BotRunner.Tests.Combat;

public class SpellCastTargetsTests
{
    private static byte[] BuildValidPayload(
        ulong unitGuid = 0, ulong goGuid = 0, ulong corpseGuid = 0,
        ulong itemGuid = 0, uint itemEntry = 0,
        float srcX = 0, float srcY = 0, float srcZ = 0,
        float destX = 0, float destY = 0, float destZ = 0,
        ushort targetMask = 0, string strTarget = "")
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(unitGuid);
        writer.Write(goGuid);
        writer.Write(corpseGuid);
        writer.Write(itemGuid);
        writer.Write(itemEntry);
        writer.Write(srcX);
        writer.Write(srcY);
        writer.Write(srcZ);
        writer.Write(destX);
        writer.Write(destY);
        writer.Write(destZ);
        writer.Write(targetMask);
        if (strTarget.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(strTarget);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
        return ms.ToArray();
    }

    [Fact]
    public void Read_ValidPayload_ParsesAllFields()
    {
        var data = BuildValidPayload(
            unitGuid: 42, goGuid: 100, corpseGuid: 200, itemGuid: 300,
            itemEntry: 5000, srcX: 1.5f, srcY: 2.5f, srcZ: 3.5f,
            destX: 10.0f, destY: 20.0f, destZ: 30.0f, targetMask: 0x0002);

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal(42UL, targets.UnitTargetGUID);
        Assert.Equal(100UL, targets.GOTargetGUID);
        Assert.Equal(200UL, targets.CorpseTargetGUID);
        Assert.Equal(300UL, targets.ItemTargetGUID);
        Assert.Equal(5000u, targets.ItemTargetEntry);
        Assert.Equal(1.5f, targets.SrcX);
        Assert.Equal(2.5f, targets.SrcY);
        Assert.Equal(3.5f, targets.SrcZ);
        Assert.Equal(10.0f, targets.DestX);
        Assert.Equal(20.0f, targets.DestY);
        Assert.Equal(30.0f, targets.DestZ);
        Assert.Equal(0x0002, targets.TargetMask);
    }

    [Fact]
    public void Read_ZeroPayload_AllFieldsZero()
    {
        var data = BuildValidPayload();

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal(0UL, targets.UnitTargetGUID);
        Assert.Equal(0UL, targets.GOTargetGUID);
        Assert.Equal(0UL, targets.CorpseTargetGUID);
        Assert.Equal(0UL, targets.ItemTargetGUID);
        Assert.Equal(0u, targets.ItemTargetEntry);
        Assert.Equal(0f, targets.SrcX);
        Assert.Equal(0f, targets.DestZ);
        Assert.Equal(0, targets.TargetMask);
        Assert.Equal(string.Empty, targets.StrTarget);
    }

    [Fact]
    public void Read_WithStringTarget_ParsesString()
    {
        var data = BuildValidPayload(strTarget: "Hello");

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal("Hello", targets.StrTarget);
    }

    [Fact]
    public void Read_NoStringData_EmptyString()
    {
        // Just the fixed-size fields, no trailing string byte
        var data = BuildValidPayload();

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal(string.Empty, targets.StrTarget);
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsEndOfStreamException()
    {
        // Only write partial data (16 bytes instead of required ~62)
        var data = new byte[16];

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));

        Assert.Throws<EndOfStreamException>(() => targets.Read(reader));
    }

    [Fact]
    public void Read_EmptyPayload_ThrowsEndOfStreamException()
    {
        var data = Array.Empty<byte>();

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));

        Assert.Throws<EndOfStreamException>(() => targets.Read(reader));
    }

    [Fact]
    public void Read_TruncatedString_ThrowsEndOfStreamException()
    {
        // Build a valid fixed payload, then add a string length byte claiming
        // 50 bytes but only provide 5
        var fixedData = BuildValidPayload();
        using var ms = new MemoryStream();
        ms.Write(fixedData);
        ms.WriteByte(50); // length prefix claims 50 bytes
        ms.Write(Encoding.UTF8.GetBytes("Hello")); // only 5 bytes
        var data = ms.ToArray();

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));

        Assert.Throws<EndOfStreamException>(() => targets.Read(reader));
    }

    [Fact]
    public void Read_MaxGuidValues()
    {
        var data = BuildValidPayload(
            unitGuid: ulong.MaxValue,
            goGuid: ulong.MaxValue,
            corpseGuid: ulong.MaxValue,
            itemGuid: ulong.MaxValue);

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal(ulong.MaxValue, targets.UnitTargetGUID);
        Assert.Equal(ulong.MaxValue, targets.GOTargetGUID);
        Assert.Equal(ulong.MaxValue, targets.CorpseTargetGUID);
        Assert.Equal(ulong.MaxValue, targets.ItemTargetGUID);
    }

    [Fact]
    public void Read_NegativeFloatCoordinates()
    {
        var data = BuildValidPayload(srcX: -100.5f, srcY: -200.5f, srcZ: -300.5f);

        var targets = new SpellCastTargets();
        using var reader = new BinaryReader(new MemoryStream(data));
        targets.Read(reader);

        Assert.Equal(-100.5f, targets.SrcX);
        Assert.Equal(-200.5f, targets.SrcY);
        Assert.Equal(-300.5f, targets.SrcZ);
    }

    [Fact]
    public void DefaultProperties_AreZeroOrNull()
    {
        var targets = new SpellCastTargets();

        Assert.Equal(0UL, targets.UnitTargetGUID);
        Assert.Equal(0UL, targets.GOTargetGUID);
        Assert.Equal(0UL, targets.CorpseTargetGUID);
        Assert.Equal(0UL, targets.ItemTargetGUID);
        Assert.Equal(0u, targets.ItemTargetEntry);
        Assert.Equal(0f, targets.SrcX);
        Assert.Equal(0f, targets.DestZ);
        Assert.Equal(0, targets.TargetMask);
        Assert.Null(targets.StrTarget);
    }
}
