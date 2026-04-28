using GameData.Core.Enums;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace BotRunner;

public static class PostTeleportWindowTriggerClassifier
{
    public const string TransportEntriesEnvVar = "WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES";
    public const string TransportEntryEnvVar = "WWOW_TRANSPORT_PACKET_WINDOW_ENTRY";

    private const uint DefaultOrgrimmarUndercityZeppelinEntry = 164871;

    public static string? ResolveTriggerScenario(
        bool isSend,
        bool isReceive,
        Opcode opcode,
        ReadOnlySpan<byte> packetBytes,
        bool packetIncludesOpcodePrefix)
    {
        if (isSend && opcode == Opcode.MSG_MOVE_WORLDPORT_ACK)
            return "worldport_ack_packet_window";

        if (!isReceive)
            return null;

        return opcode switch
        {
            Opcode.MSG_MOVE_TELEPORT
                or Opcode.MSG_MOVE_TELEPORT_ACK
                or Opcode.SMSG_NEW_WORLD
                or Opcode.SMSG_TRANSFER_PENDING => "post_teleport_packet_window",
            Opcode.SMSG_MOVE_KNOCK_BACK => "knockback_packet_window",
            Opcode.SMSG_MONSTER_MOVE_TRANSPORT => "transport_packet_window",
            Opcode.SMSG_MONSTER_MOVE when MonsterMoveTargetsConfiguredTransport(
                ExtractPayload(packetBytes, opcode, packetIncludesOpcodePrefix)) => "transport_packet_window",
            Opcode.SMSG_UPDATE_OBJECT or Opcode.SMSG_COMPRESSED_UPDATE_OBJECT
                when ObjectUpdateMentionsConfiguredTransport(
                    opcode,
                    ExtractPayload(packetBytes, opcode, packetIncludesOpcodePrefix)) => "transport_packet_window",
            _ => null,
        };
    }

    private static ReadOnlySpan<byte> ExtractPayload(
        ReadOnlySpan<byte> packetBytes,
        Opcode opcode,
        bool packetIncludesOpcodePrefix)
    {
        if (!packetIncludesOpcodePrefix || packetBytes.IsEmpty)
            return packetBytes;

        if (packetBytes.Length >= 2 && BitConverter.ToUInt16(packetBytes[..2]) == (ushort)opcode)
            return packetBytes[2..];

        if (packetBytes.Length >= 4 && BitConverter.ToUInt32(packetBytes[..4]) == (uint)opcode)
            return packetBytes[4..];

        return packetBytes;
    }

    private static bool MonsterMoveTargetsConfiguredTransport(ReadOnlySpan<byte> payload)
    {
        if (!TryReadPackedGuid(payload, out var guid, out _))
            return false;

        var entry = (uint)((guid >> 24) & 0xFFFFFF);
        foreach (var configuredEntry in ResolveTransportEntries())
        {
            if (entry == configuredEntry)
                return true;
        }

        return false;
    }

    private static bool ObjectUpdateMentionsConfiguredTransport(Opcode opcode, ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
            return false;

        if (opcode == Opcode.SMSG_COMPRESSED_UPDATE_OBJECT)
        {
            if (payload.Length <= 4)
                return false;

            var decompressed = TryDecompress(payload[4..]);
            return decompressed is not null && MentionsConfiguredTransportEntry(decompressed);
        }

        return MentionsConfiguredTransportEntry(payload);
    }

    private static bool MentionsConfiguredTransportEntry(ReadOnlySpan<byte> payload)
    {
        Span<byte> needle = stackalloc byte[4];
        foreach (var entry in ResolveTransportEntries())
        {
            BitConverter.TryWriteBytes(needle, entry);
            if (payload.IndexOf(needle) >= 0)
                return true;
        }

        return false;
    }

    private static byte[]? TryDecompress(ReadOnlySpan<byte> compressedPayload)
    {
        try
        {
            using var input = new MemoryStream(compressedPayload.ToArray());
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryReadPackedGuid(ReadOnlySpan<byte> payload, out ulong guid, out int bytesRead)
    {
        guid = 0;
        bytesRead = 0;

        if (payload.IsEmpty)
            return false;

        byte mask = payload[0];
        bytesRead = 1;

        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) == 0)
                continue;

            if (bytesRead >= payload.Length)
                return false;

            guid |= (ulong)payload[bytesRead] << (i * 8);
            bytesRead++;
        }

        return guid != 0;
    }

    private static uint[] ResolveTransportEntries()
    {
        var raw = Environment.GetEnvironmentVariable(TransportEntriesEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
            raw = Environment.GetEnvironmentVariable(TransportEntryEnvVar);

        if (string.IsNullOrWhiteSpace(raw))
            return [DefaultOrgrimmarUndercityZeppelinEntry];

        var entries = raw.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var parsed = new uint[entries.Length];
        var count = 0;

        foreach (var entry in entries)
        {
            if (uint.TryParse(entry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                parsed[count++] = value;
        }

        if (count == 0)
            return [DefaultOrgrimmarUndercityZeppelinEntry];

        Array.Resize(ref parsed, count);
        return parsed;
    }
}
