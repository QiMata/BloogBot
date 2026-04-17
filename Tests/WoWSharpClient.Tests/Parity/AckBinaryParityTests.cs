using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using Xunit;

namespace WoWSharpClient.Tests.Parity;

public sealed class AckBinaryParityTests
{
    private static readonly string CorpusRoot = Path.Combine(
        ResolveRepoRoot(),
        "Tests",
        "WoWSharpClient.Tests",
        "Fixtures",
        "ack_golden_corpus");

    public static IEnumerable<object[]> TeleportAckFixtures()
    {
        foreach (var fixture in LoadFixtures(Opcode.MSG_MOVE_TELEPORT_ACK))
            yield return new object[] { fixture };
    }

    [Theory]
    [MemberData(nameof(TeleportAckFixtures))]
    [Trait("Category", "AckParity")]
    public void TeleportAck_MatchesWoWExeBytes(AckCorpusFixture fixture)
    {
        Assert.NotNull(fixture.Guid);
        Assert.NotNull(fixture.Counter);
        Assert.NotNull(fixture.ClientTimeMs);

        var player = new WoWLocalPlayer(new HighGuid(fixture.Guid.Value));
        var payload = MovementPacketHandler.BuildMoveTeleportAckPayload(
            player,
            fixture.Counter.Value,
            fixture.ClientTimeMs.Value);

        var actualBytes = EncodeRawClientPacket((Opcode)(uint)fixture.Opcode, payload);
        var expectedBytes = Convert.FromHexString(fixture.PacketBytesHex);

        Assert.Equal(expectedBytes, actualBytes);
    }

    private static IEnumerable<AckCorpusFixture> LoadFixtures(Opcode opcode)
    {
        var opcodeDirectory = Path.Combine(CorpusRoot, opcode.ToString());
        if (!Directory.Exists(opcodeDirectory))
            yield break;

        foreach (var path in Directory.GetFiles(opcodeDirectory, "*.json"))
        {
            var fixture = JsonSerializer.Deserialize<AckCorpusFixture>(File.ReadAllText(path));
            if (fixture != null)
                yield return fixture;
        }
    }

    private static byte[] EncodeRawClientPacket(Opcode opcode, byte[] payload)
    {
        var packet = new byte[4 + payload.Length];
        BitConverter.GetBytes((uint)opcode).CopyTo(packet, 0);
        payload.CopyTo(packet, 4);
        return packet;
    }

    private static string ResolveRepoRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("WWOW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && File.Exists(Path.Combine(envRoot, "WestworldOfWarcraft.sln")))
            return Path.GetFullPath(envRoot);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WestworldOfWarcraft.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to resolve repo root for ack corpus fixtures.");
    }

    public sealed class AckCorpusFixture
    {
        public int SchemaVersion { get; init; }
        public DateTime CapturedAtUtc { get; init; }
        public string Source { get; init; } = string.Empty;
        public ushort Opcode { get; init; }
        public string OpcodeName { get; init; } = string.Empty;
        public int PacketSize { get; init; }
        public string PacketBytesHex { get; init; } = string.Empty;
        public ulong? Guid { get; init; }
        public uint? Counter { get; init; }
        public uint? ClientTimeMs { get; init; }
        public float? Speed { get; init; }
    }
}
