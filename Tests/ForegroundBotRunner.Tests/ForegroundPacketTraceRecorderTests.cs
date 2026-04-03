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
    private const string RecordingArtifactsEnvVar = "WWOW_ENABLE_RECORDING_ARTIFACTS";

    private static readonly string RecordingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

    [Fact]
    public void StartStopRecording_WritesStableCsvWithCapturedPackets()
    {
        using var envScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
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
        using var envScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, "1");
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

    [Fact]
    public void StartStopRecording_DoesNotWriteCsvWhenFeatureFlagDisabled()
    {
        using var envScope = new EnvironmentVariableScope(RecordingArtifactsEnvVar, null);
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var recorder = new ForegroundPacketTraceRecorder(loggerFactory);
        var account = $"TRACE_{Guid.NewGuid():N}";
        var path = Path.Combine(RecordingDir, $"packets_{account}.csv");

        TryDelete(path);

        recorder.StartRecording(account);
        PacketLogger.RecordInboundPacket((ushort)Opcode.MSG_MOVE_HEARTBEAT, 18);
        recorder.StopRecording(account);

        Assert.False(File.Exists(path));
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
