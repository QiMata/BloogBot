using System;
using System.IO;

namespace BotRunner.Tests.LiveValidation;

public sealed class PacketTraceArtifactHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"wwow-packet-helper-{Guid.NewGuid():N}");

    public PacketTraceArtifactHelperTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void LoadPacketCsv_ParsesPacketRows()
    {
        var csvPath = Path.Combine(_tempDir, "packets_TESTBOT1.csv");
        File.WriteAllText(
            csvPath,
            """
            Index,ElapsedMs,Direction,Opcode,OpcodeHex,OpcodeName,Size,IsMovement
            0,125,Send,302,0x012E,CMSG_CAST_SPELL,18,0
            1,412,Recv,313,0x0139,MSG_CHANNEL_START,8,0
            2,995,Send,181,0x00B5,MSG_MOVE_START_FORWARD,17,1
            """);

        var rows = PacketTraceArtifactHelper.LoadPacketCsv(csvPath);

        Assert.Equal(3, rows.Count);
        Assert.Equal("CMSG_CAST_SPELL", rows[0].OpcodeName);
        Assert.Equal("Recv", rows[1].Direction);
        Assert.True(rows[2].IsMovement);
    }

    [Fact]
    public void LoadPacketCsv_SkipsMalformedRows()
    {
        var csvPath = Path.Combine(_tempDir, "packets_TESTBOT2.csv");
        File.WriteAllText(
            csvPath,
            """
            Index,ElapsedMs,Direction,Opcode,OpcodeHex,OpcodeName,Size,IsMovement
            nope
            0,250,Recv,306,0x0132,SMSG_SPELL_GO,45,0
            1,broken,Send,177,0x00B1,CMSG_GAMEOBJ_USE,8,0
            """);

        var rows = PacketTraceArtifactHelper.LoadPacketCsv(csvPath);

        var row = Assert.Single(rows);
        Assert.Equal("SMSG_SPELL_GO", row.OpcodeName);
        Assert.Equal(45, row.Size);
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
