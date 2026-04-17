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

    public static IEnumerable<object[]> WorldportAckFixtures()
    {
        foreach (var fixture in LoadFixtures(Opcode.MSG_MOVE_WORLDPORT_ACK))
            yield return new object[] { fixture };
    }

    public static IEnumerable<object[]> ForceSpeedAckFixtures()
    {
        var opcodes = new[]
        {
            Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK,
            Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK,
            Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK,
            Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK
        };

        foreach (var opcode in opcodes)
        {
            foreach (var fixture in LoadFixtures(opcode))
                yield return new object[] { fixture };
        }
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

    [Theory]
    [MemberData(nameof(WorldportAckFixtures))]
    [Trait("Category", "AckParity")]
    public void WorldportAck_MatchesWoWExeBytes(AckCorpusFixture fixture)
    {
        var actualBytes = EncodeRawClientPacket((Opcode)(uint)fixture.Opcode, Array.Empty<byte>());
        var expectedBytes = Convert.FromHexString(fixture.PacketBytesHex);

        Assert.Equal(expectedBytes, actualBytes);
    }

    [Theory]
    [MemberData(nameof(ForceSpeedAckFixtures))]
    [Trait("Category", "AckParity")]
    public void ForceSpeedAck_MatchesWoWExeBytes(AckCorpusFixture fixture)
    {
        Assert.NotNull(fixture.Guid);
        Assert.NotNull(fixture.Counter);
        Assert.NotNull(fixture.ClientTimeMs);
        Assert.NotNull(fixture.Speed);
        Assert.NotNull(fixture.Movement);

        var player = BuildPlayerFromFixture(fixture);
        var payload = MovementPacketHandler.BuildForceSpeedChangeAck(
            player,
            fixture.Counter.Value,
            fixture.ClientTimeMs.Value,
            fixture.Speed.Value);

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

    private static WoWLocalPlayer BuildPlayerFromFixture(AckCorpusFixture fixture)
    {
        var movement = Assert.IsType<MovementSnapshot>(fixture.Movement);
        var player = new WoWLocalPlayer(new HighGuid(fixture.Guid!.Value))
        {
            Position = new Position(movement.X, movement.Y, movement.Z),
            Facing = movement.Facing,
            MovementFlags = (MovementFlags)movement.MovementFlags,
            FallTime = movement.FallTimeMs
        };

        if (movement.TransportGuid.HasValue)
        {
            player.TransportGuid = movement.TransportGuid.Value;
            player.TransportOffset = new Position(
                movement.TransportOffsetX ?? 0f,
                movement.TransportOffsetY ?? 0f,
                movement.TransportOffsetZ ?? 0f);
            player.TransportOrientation = movement.TransportOrientation ?? 0f;
        }

        if (movement.SwimPitch.HasValue)
            player.SwimPitch = movement.SwimPitch.Value;

        if (movement.JumpVerticalSpeed.HasValue)
            player.JumpVerticalSpeed = movement.JumpVerticalSpeed.Value;

        if (movement.JumpCosAngle.HasValue)
            player.JumpCosAngle = movement.JumpCosAngle.Value;

        if (movement.JumpSinAngle.HasValue)
            player.JumpSinAngle = movement.JumpSinAngle.Value;

        if (movement.JumpHorizontalSpeed.HasValue)
            player.JumpHorizontalSpeed = movement.JumpHorizontalSpeed.Value;

        if (movement.SplineElevation.HasValue)
            player.SplineElevation = movement.SplineElevation.Value;

        return player;
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
        public MovementSnapshot? Movement { get; init; }
    }

    public sealed class MovementSnapshot
    {
        public uint MovementFlags { get; init; }
        public uint ClientTimeMs { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
        public float Facing { get; init; }
        public ulong? TransportGuid { get; init; }
        public float? TransportOffsetX { get; init; }
        public float? TransportOffsetY { get; init; }
        public float? TransportOffsetZ { get; init; }
        public float? TransportOrientation { get; init; }
        public float? SwimPitch { get; init; }
        public uint FallTimeMs { get; init; }
        public float? JumpVerticalSpeed { get; init; }
        public float? JumpCosAngle { get; init; }
        public float? JumpSinAngle { get; init; }
        public float? JumpHorizontalSpeed { get; init; }
        public float? SplineElevation { get; init; }
    }
}
