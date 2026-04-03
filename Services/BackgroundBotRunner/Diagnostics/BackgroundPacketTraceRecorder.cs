using BotRunner;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using WoWSharpClient.Client;

namespace BackgroundBotRunner.Diagnostics;

/// <summary>
/// Writes a stable background packet sidecar for live parity tests.
/// Captures ALL sent (CMSG) and received (SMSG) opcodes via WoWClient events.
/// Bound to BotRunnerService's Start/StopPhysicsRecording lifecycle.
/// </summary>
public sealed class BackgroundPacketTraceRecorder : IDiagnosticPacketTraceRecorder, IDisposable
{
    private static readonly string RecordingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

    private readonly WoWClient _wowClient;
    private readonly ILogger<BackgroundPacketTraceRecorder> _logger;
    private readonly object _lock = new();

    private Stopwatch? _stopwatch;
    private List<PacketTraceRow>? _rows;
    private string? _accountName;
    private bool _isRecording;
    private bool _writeArtifactOnStop;
    private int _nextIndex;

    private sealed record PacketTraceRow(
        int Index,
        long ElapsedMs,
        string Direction,
        Opcode Opcode,
        int Size);

    public BackgroundPacketTraceRecorder(WoWClient wowClient, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(wowClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _wowClient = wowClient;
        _logger = loggerFactory.CreateLogger<BackgroundPacketTraceRecorder>();

        // Subscribe to ALL packets (sent + received), not just movement
        _wowClient.PacketSent += HandlePacketSent;
        _wowClient.PacketReceived += HandlePacketReceived;
    }

    public void StartRecording(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            accountName = "unknown";

        lock (_lock)
        {
            StopRecordingInternal(writeFile: true);

            if (!RecordingArtifactsFeature.IsEnabled())
            {
                _logger.LogInformation("[DIAG] Background packet trace skipped; set {EnvVar}=1 to enable artifact recording",
                    RecordingArtifactsFeature.EnvironmentVariableName);
                return;
            }

            _accountName = accountName;
            _rows = [];
            _nextIndex = 0;
            _stopwatch = Stopwatch.StartNew();
            _isRecording = true;
            _writeArtifactOnStop = true;
        }

        _logger.LogInformation("[DIAG] Background packet trace recording STARTED for {Account}", accountName);
    }

    public void StopRecording(string accountName)
    {
        lock (_lock)
        {
            StopRecordingInternal(writeFile: true);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopRecordingInternal(writeFile: false);
            _wowClient.PacketSent -= HandlePacketSent;
            _wowClient.PacketReceived -= HandlePacketReceived;
        }
    }

    private void HandlePacketSent(Opcode opcode, int size)
    {
        lock (_lock)
        {
            if (!_isRecording || _stopwatch == null || _rows == null)
                return;

            _rows.Add(new PacketTraceRow(
                Index: _nextIndex++,
                ElapsedMs: _stopwatch.ElapsedMilliseconds,
                Direction: "Send",
                Opcode: opcode,
                Size: size));
        }
    }

    private void HandlePacketReceived(Opcode opcode, int size)
    {
        lock (_lock)
        {
            if (!_isRecording || _stopwatch == null || _rows == null)
                return;

            _rows.Add(new PacketTraceRow(
                Index: _nextIndex++,
                ElapsedMs: _stopwatch.ElapsedMilliseconds,
                Direction: "Recv",
                Opcode: opcode,
                Size: size));
        }
    }

    private void StopRecordingInternal(bool writeFile)
    {
        if (!_isRecording)
            return;

        var rows = _rows ?? [];
        var accountName = string.IsNullOrWhiteSpace(_accountName) ? "unknown" : _accountName!;

        _isRecording = false;
        _stopwatch?.Stop();
        _stopwatch = null;
        _rows = null;
        _accountName = null;
        _nextIndex = 0;
        var shouldWriteArtifact = writeFile && _writeArtifactOnStop;
        _writeArtifactOnStop = false;

        if (!shouldWriteArtifact)
            return;

        Directory.CreateDirectory(RecordingDir);
        var filePath = Path.Combine(RecordingDir, $"packets_{accountName}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Index,ElapsedMs,Direction,Opcode,OpcodeHex,OpcodeName,Size,IsMovement");
        foreach (var row in rows)
        {
            string opcodeName = row.Opcode.ToString();
            bool isMovement = opcodeName.Contains("MOVE", StringComparison.Ordinal);

            sb.Append(row.Index).Append(',');
            sb.Append(row.ElapsedMs).Append(',');
            sb.Append(row.Direction).Append(',');
            sb.Append((ushort)row.Opcode).Append(',');
            sb.Append($"0x{(ushort)row.Opcode:X4}").Append(',');
            sb.Append(opcodeName).Append(',');
            sb.Append(row.Size.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(isMovement ? "1" : "0");
        }

        File.WriteAllText(filePath, sb.ToString());
        _logger.LogInformation("[DIAG] Background packet trace written to {Path} ({Count} packets)", filePath, rows.Count);
    }
}
