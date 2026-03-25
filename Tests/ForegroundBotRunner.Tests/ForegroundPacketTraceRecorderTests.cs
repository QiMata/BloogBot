using ForegroundBotRunner.Diagnostics;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Xunit;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundPacketTraceRecorderTests
{
    private static readonly string RecordingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

    [Fact]
    public void StartStopRecording_WritesStableCsvWithCapturedPackets()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var recorder = new ForegroundPacketTraceRecorder(loggerFactory);
        var account = $"TRACE_{Guid.NewGuid():N}";
        var path = Path.Combine(RecordingDir, $"packets_{account}.csv");

        TryDelete(path);

        recorder.StartRecording(account);
        PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_HEARTBEAT, 18);
        PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_SET_FACING, 42);
        recorder.StopRecording(account);

        Assert.True(File.Exists(path));

        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length);
        Assert.Contains("OpcodeName", lines[0], StringComparison.Ordinal);
        Assert.Contains("MSG_MOVE_HEARTBEAT", lines[1], StringComparison.Ordinal);
        Assert.Contains("MSG_MOVE_SET_FACING", lines[2], StringComparison.Ordinal);
        Assert.EndsWith(",1", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",1", lines[2], StringComparison.Ordinal);

        TryDelete(path);
    }

    [Fact]
    public void RestartRecording_ReplacesPriorStableArtifact()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var recorder = new ForegroundPacketTraceRecorder(loggerFactory);
        var account = $"TRACE_{Guid.NewGuid():N}";
        var path = Path.Combine(RecordingDir, $"packets_{account}.csv");

        TryDelete(path);

        recorder.StartRecording(account);
        PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_HEARTBEAT, 18);
        recorder.StopRecording(account);

        recorder.StartRecording(account);
        PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_STOP, 27);
        recorder.StopRecording(account);

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("MSG_MOVE_STOP", lines[1], StringComparison.Ordinal);

        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
