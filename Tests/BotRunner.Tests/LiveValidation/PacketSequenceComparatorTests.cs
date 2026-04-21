using System;
using System.IO;
using Tests.Infrastructure;

namespace BotRunner.Tests.LiveValidation;

public sealed class PacketSequenceComparatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"wwow-packet-seq-{Guid.NewGuid():N}");

    public PacketSequenceComparatorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ParseCsv_ParsesCurrentPacketTraceFormat()
    {
        var csvPath = Path.Combine(_tempDir, "packets_current.csv");
        File.WriteAllText(
            csvPath,
            """
            Index,ElapsedMs,Direction,Opcode,OpcodeHex,OpcodeName,Size,IsMovement
            0,125,Send,302,0x012E,CMSG_CAST_SPELL,18,0
            1,412,Recv,313,0x0139,MSG_CHANNEL_START,10,0
            """);

        var rows = PacketSequenceComparator.ParseCsv(csvPath);

        Assert.Equal(2, rows.Count);
        Assert.Equal("CMSG_CAST_SPELL", rows[0].Opcode);
        Assert.Equal(18, rows[0].Size);
        Assert.Equal(125d, rows[0].TimestampMs);
        Assert.Equal("MSG_CHANNEL_START", rows[1].Opcode);
    }

    [Fact]
    public void ParseCsv_ParsesLegacyThreeColumnFormat()
    {
        var csvPath = Path.Combine(_tempDir, "packets_legacy.csv");
        File.WriteAllText(
            csvPath,
            """
            Opcode,Size,TimestampMs
            CMSG_CAST_SPELL,18,125
            MSG_CHANNEL_START,10,412
            """);

        var rows = PacketSequenceComparator.ParseCsv(csvPath);

        Assert.Equal(2, rows.Count);
        Assert.Equal("CMSG_CAST_SPELL", rows[0].Opcode);
        Assert.Equal(10, rows[1].Size);
        Assert.Equal(412d, rows[1].TimestampMs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }
}
