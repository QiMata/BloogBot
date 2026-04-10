using System.IO;
using System.Reflection;
using GameData.Core.Enums;
using WoWSharpClient.Handlers;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests.Handlers;

public class CompressedMoveEntryParsingTests
{
    [Fact]
    public void ParseCompressedMove_UnknownOpcode_AdvancesToDeclaredEntryBoundary()
    {
        byte[] firstEntry = BuildCompressedEntry(
            opcode: (Opcode)0x7FFE,
            guid: 0x0102030405060708,
            payload: [0xDE, 0xAD, 0xBE, 0xEF]);

        byte[] secondEntry = BuildCompressedEntry(
            opcode: (Opcode)0x7FFD,
            guid: 0x1112131415161718,
            payload: [0x01, 0x02]);

        byte[] packet = [.. firstEntry, .. secondEntry];

        using var stream = new MemoryStream(packet);
        using var reader = new BinaryReader(stream);

        InvokeParseCompressedMove(reader);
        Assert.Equal(firstEntry.Length, stream.Position);

        InvokeParseCompressedMove(reader);
        Assert.Equal(packet.Length, stream.Position);
    }

    [Fact]
    public void ParseCompressedMove_TruncatedEntry_DoesNotThrow_AndConsumesRemainingBytes()
    {
        // Declared entry size (12) is larger than available bytes after the size field.
        byte[] truncated = [12, 0x34, 0x12, 0x01, 0xAA];

        using var stream = new MemoryStream(truncated);
        using var reader = new BinaryReader(stream);

        InvokeParseCompressedMove(reader);

        Assert.Equal(truncated.Length, stream.Position);
    }

    private static void InvokeParseCompressedMove(BinaryReader reader)
    {
        var method = typeof(MovementHandler).GetMethod(
            "ParseCompressedMove",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(null, [reader]);
    }

    private static byte[] BuildCompressedEntry(Opcode opcode, ulong guid, byte[] payload)
    {
        using var entryBodyStream = new MemoryStream();
        using (var entryWriter = new BinaryWriter(entryBodyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            entryWriter.Write((ushort)opcode);
            ReaderUtils.WritePackedGuid(entryWriter, guid);
            entryWriter.Write(payload);
            entryWriter.Flush();
        }

        byte[] body = entryBodyStream.ToArray();
        Assert.InRange(body.Length, 0, byte.MaxValue);

        using var packetStream = new MemoryStream();
        using var packetWriter = new BinaryWriter(packetStream);
        packetWriter.Write((byte)body.Length);
        packetWriter.Write(body);
        packetWriter.Flush();
        return packetStream.ToArray();
    }
}
