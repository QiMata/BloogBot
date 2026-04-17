using System;
using System.IO;
using System.Text.Json;
using ForegroundBotRunner.Diagnostics;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundAckCorpusRecorderTests
{
    private const string RecordingArtifactsEnvVar = "WWOW_ENABLE_RECORDING_ARTIFACTS";

    [Fact]
    public void Start_RecordForceSpeedAck_WritesFixtureJsonWithParsedSnapshot()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.OutputDirEnvVar, tempDir);
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundAckCorpusRecorder(loggerFactory);

            recorder.Start();

            var rawBytes = BuildForceSpeedAckPacket(
                Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK,
                guid: 0x1122334455667788ul,
                counter: 73u,
                clientTimeMs: 4567u,
                speed: 9.5f);

            PacketLogger.RecordOutboundPacket((ushort)Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK, rawBytes);

            var opcodeDir = Path.Combine(tempDir, nameof(Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK));
            var fixturePath = Assert.Single(Directory.GetFiles(opcodeDir, "*.json"));
            using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
            var root = document.RootElement;

            Assert.Equal("CMSG_FORCE_RUN_SPEED_CHANGE_ACK", root.GetProperty("OpcodeName").GetString());
            Assert.Equal(Convert.ToHexString(rawBytes), root.GetProperty("PacketBytesHex").GetString());
            Assert.Equal(73u, root.GetProperty("Counter").GetUInt32());
            Assert.Equal(4567u, root.GetProperty("ClientTimeMs").GetUInt32());
            Assert.Equal(9.5f, root.GetProperty("Speed").GetSingle());

            var movement = root.GetProperty("Movement");
            Assert.Equal((uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING), movement.GetProperty("MovementFlags").GetUInt32());
            Assert.Equal(12.5f, movement.GetProperty("X").GetSingle());
            Assert.Equal(-8.25f, movement.GetProperty("Y").GetSingle());
            Assert.Equal(101.75f, movement.GetProperty("Z").GetSingle());
            Assert.Equal(1.75f, movement.GetProperty("Facing").GetSingle());
            Assert.Equal(77u, movement.GetProperty("FallTimeMs").GetUInt32());
            Assert.Equal(0.25f, movement.GetProperty("SwimPitch").GetSingle());
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Start_RecordWorldportAck_WritesFixtureJsonWithoutPayloadFields()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.OutputDirEnvVar, tempDir);
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundAckCorpusRecorder(loggerFactory);

            recorder.Start();

            var rawBytes = BitConverter.GetBytes((uint)Opcode.MSG_MOVE_WORLDPORT_ACK);
            PacketLogger.RecordOutboundPacket((ushort)Opcode.MSG_MOVE_WORLDPORT_ACK, rawBytes);

            var opcodeDir = Path.Combine(tempDir, nameof(Opcode.MSG_MOVE_WORLDPORT_ACK));
            var fixturePath = Assert.Single(Directory.GetFiles(opcodeDir, "*.json"));
            using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
            var root = document.RootElement;

            Assert.Equal("MSG_MOVE_WORLDPORT_ACK", root.GetProperty("OpcodeName").GetString());
            Assert.Equal(Convert.ToHexString(rawBytes), root.GetProperty("PacketBytesHex").GetString());
            Assert.True(root.GetProperty("Counter").ValueKind is JsonValueKind.Null);
            Assert.True(root.GetProperty("Movement").ValueKind is JsonValueKind.Null);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Theory]
    [InlineData(Opcode.MSG_MOVE_SET_RAW_POSITION_ACK)]
    [InlineData(Opcode.CMSG_MOVE_FLIGHT_ACK)]
    public void Start_RecordNonWoWExeAckCandidates_DoesNotWriteFixture(Opcode opcode)
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundAckCorpusRecorder.OutputDirEnvVar, tempDir);
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundAckCorpusRecorder(loggerFactory);

            recorder.Start();

            PacketLogger.RecordOutboundPacket((ushort)opcode, BitConverter.GetBytes((uint)opcode));

            var opcodeDir = Path.Combine(tempDir, opcode.ToString());
            Assert.False(Directory.Exists(opcodeDir));
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static byte[] BuildForceSpeedAckPacket(Opcode opcode, ulong guid, uint counter, uint clientTimeMs, float speed)
    {
        var player = new WoWLocalPlayer(new HighGuid(guid))
        {
            Position = new Position(12.5f, -8.25f, 101.75f),
            Facing = 1.75f,
            MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING,
            SwimPitch = 0.25f,
            FallTime = 77
        };

        var movementInfo = MovementPacketHandler.BuildMovementInfoBuffer(player, clientTimeMs, (uint)player.FallTime);
        var payload = new byte[8 + 4 + movementInfo.Length + 4];
        BitConverter.GetBytes(player.Guid).CopyTo(payload, 0);
        BitConverter.GetBytes(counter).CopyTo(payload, 8);
        movementInfo.CopyTo(payload, 12);
        BitConverter.GetBytes(speed).CopyTo(payload, payload.Length - 4);

        var rawBytes = new byte[4 + payload.Length];
        BitConverter.GetBytes((uint)opcode).CopyTo(rawBytes, 0);
        payload.CopyTo(rawBytes, 4);
        return rawBytes;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WWoW", "ack-corpus-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
