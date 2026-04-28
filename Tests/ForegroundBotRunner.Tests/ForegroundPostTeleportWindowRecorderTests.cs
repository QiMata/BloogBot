using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ForegroundBotRunner.Diagnostics;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundPostTeleportWindowRecorderTests
{
    private const string RecordingArtifactsEnvVar = "WWOW_ENABLE_RECORDING_ARTIFACTS";

    [Theory]
    [InlineData(Opcode.MSG_MOVE_TELEPORT)]
    [InlineData(Opcode.MSG_MOVE_TELEPORT_ACK)]
    public void Recorder_TriggeredByInboundTeleport_CapturesSubsequentPacketsUntilWindowElapses(Opcode triggerOpcode)
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.OutputDirEnvVar, tempDir);
            // Tight window so the test is fast; the recorder still flushes on Dispose.
            using var windowScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.WindowDurationEnvVar, "200");
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundPostTeleportWindowRecorder(loggerFactory);

            recorder.Start();

            // Pre-trigger noise — must be ignored.
            PacketLogger.RecordOutboundPacket((ushort)Opcode.MSG_MOVE_HEARTBEAT, [0x01, 0x02, 0x03]);

            // Trigger: inbound teleport (either of the two bidirectional MSG_* opcodes).
            PacketLogger.RecordInboundPacket((ushort)triggerOpcode, size: 32);

            // Window content: outbound ACK + outbound heartbeat.
            PacketLogger.RecordOutboundPacket(
                (ushort)Opcode.MSG_MOVE_TELEPORT_ACK,
                BuildOpcodeBytes(Opcode.MSG_MOVE_TELEPORT_ACK, payloadLength: 16));
            PacketLogger.RecordOutboundPacket(
                (ushort)Opcode.MSG_MOVE_HEARTBEAT,
                BuildOpcodeBytes(Opcode.MSG_MOVE_HEARTBEAT, payloadLength: 36));

            // Wait for the window to flush via timer.
            var fixturePath = WaitForFixture(tempDir, TimeSpan.FromSeconds(2));
            Assert.NotNull(fixturePath);

            using var document = JsonDocument.Parse(File.ReadAllText(fixturePath!));
            var root = document.RootElement;

            Assert.Equal("post_teleport_packet_window", root.GetProperty("CaptureScenario").GetString());
            Assert.Equal(200, root.GetProperty("WindowDurationMs").GetInt32());

            var trigger = root.GetProperty("Trigger");
            Assert.Equal(triggerOpcode.ToString(), trigger.GetProperty("OpcodeName").GetString());
            Assert.Equal("Recv", trigger.GetProperty("Direction").GetString());

            var packets = root.GetProperty("Packets").EnumerateArray().ToArray();
            Assert.Equal(3, packets.Length);
            Assert.Equal(triggerOpcode.ToString(), packets[0].GetProperty("OpcodeName").GetString());
            Assert.Equal("MSG_MOVE_TELEPORT_ACK", packets[1].GetProperty("OpcodeName").GetString());
            Assert.Equal("MSG_MOVE_HEARTBEAT", packets[2].GetProperty("OpcodeName").GetString());
            Assert.Equal("Recv", packets[0].GetProperty("Direction").GetString());
            Assert.Equal("Send", packets[1].GetProperty("Direction").GetString());
            Assert.Equal("Send", packets[2].GetProperty("Direction").GetString());
            Assert.True(packets[1].GetProperty("PayloadHex").GetString()!.Length > 0);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Recorder_OutboundTeleportAck_DoesNotTriggerWindow()
    {
        // A FG bot's outbound MSG_MOVE_TELEPORT_ACK (16-byte payload) is the
        // CLIENT-side ack of an inbound teleport, not a fresh trigger. It must
        // not open a recording window on its own — only inbound packets trigger.
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.OutputDirEnvVar, tempDir);
            using var windowScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.WindowDurationEnvVar, "200");
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundPostTeleportWindowRecorder(loggerFactory);

            recorder.Start();

            PacketLogger.RecordOutboundPacket(
                (ushort)Opcode.MSG_MOVE_TELEPORT_ACK,
                BuildOpcodeBytes(Opcode.MSG_MOVE_TELEPORT_ACK, payloadLength: 16));

            Thread.Sleep(400);

            Assert.False(Directory.EnumerateFiles(tempDir, "*.json").Any());
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Recorder_InboundMonsterMoveTransport_CapturesTransportScenario()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.OutputDirEnvVar, tempDir);
            using var windowScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.WindowDurationEnvVar, "200");
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundPostTeleportWindowRecorder(loggerFactory);

            recorder.Start();

            PacketLogger.RecordInboundPacket((ushort)Opcode.SMSG_MONSTER_MOVE_TRANSPORT, size: 48);
            PacketLogger.RecordOutboundPacket(
                (ushort)Opcode.MSG_MOVE_HEARTBEAT,
                BuildOpcodeBytes(Opcode.MSG_MOVE_HEARTBEAT, payloadLength: 36));

            var fixturePath = WaitForFixture(tempDir, TimeSpan.FromSeconds(2));
            Assert.NotNull(fixturePath);

            using var document = JsonDocument.Parse(File.ReadAllText(fixturePath!));
            var root = document.RootElement;

            Assert.Equal("transport_packet_window", root.GetProperty("CaptureScenario").GetString());
            var trigger = root.GetProperty("Trigger");
            Assert.Equal("SMSG_MONSTER_MOVE_TRANSPORT", trigger.GetProperty("OpcodeName").GetString());
            Assert.Equal("Recv", trigger.GetProperty("Direction").GetString());

            var packets = root.GetProperty("Packets").EnumerateArray().ToArray();
            Assert.Equal(2, packets.Length);
            Assert.Equal("SMSG_MONSTER_MOVE_TRANSPORT", packets[0].GetProperty("OpcodeName").GetString());
            Assert.Equal("MSG_MOVE_HEARTBEAT", packets[1].GetProperty("OpcodeName").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Recorder_WhenNotTriggered_DoesNotEmitFixture()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.EnableEnvVar, "1");
            using var outputScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.OutputDirEnvVar, tempDir);
            using var windowScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.WindowDurationEnvVar, "200");
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundPostTeleportWindowRecorder(loggerFactory);

            recorder.Start();

            PacketLogger.RecordOutboundPacket((ushort)Opcode.MSG_MOVE_HEARTBEAT, [0x10, 0x20]);
            PacketLogger.RecordInboundPacket((ushort)Opcode.SMSG_UPDATE_OBJECT, size: 8);

            Thread.Sleep(400); // Past the window even if a trigger had landed.

            Assert.False(Directory.EnumerateFiles(tempDir, "*.json").Any());
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void Recorder_WhenDisabled_DoesNotSubscribe()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            using var artifactsScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
            using var enableScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.EnableEnvVar, null);
            using var outputScope = new EnvironmentVariableScope(ForegroundPostTeleportWindowRecorder.OutputDirEnvVar, tempDir);
            using var loggerFactory = LoggerFactory.Create(_ => { });
            using var recorder = new ForegroundPostTeleportWindowRecorder(loggerFactory);

            recorder.Start();

            PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_TELEPORT, size: 32);
            PacketLogger.RecordOutboundPacket(
                (ushort)Opcode.MSG_MOVE_TELEPORT_ACK,
                BuildOpcodeBytes(Opcode.MSG_MOVE_TELEPORT_ACK, payloadLength: 16));

            Thread.Sleep(200);

            Assert.False(Directory.EnumerateFiles(tempDir, "*.json").Any());
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static string? WaitForFixture(string dir, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var files = Directory.EnumerateFiles(dir, "*.json").ToArray();
            if (files.Length > 0)
                return files[0];

            Thread.Sleep(25);
        }

        return null;
    }

    private static byte[] BuildOpcodeBytes(Opcode opcode, int payloadLength)
    {
        var buf = new byte[4 + payloadLength];
        BitConverter.GetBytes((uint)opcode).CopyTo(buf, 0);
        for (int i = 0; i < payloadLength; i++)
            buf[4 + i] = (byte)(i & 0xFF);
        return buf;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WWoW", "post-teleport-window-tests", Guid.NewGuid().ToString("N"));
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
