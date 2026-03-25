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
    private int _nextIndex;

    private sealed record PacketTraceRow(
        int Index,
        long ElapsedMs,
        Opcode Opcode,
        int Size);

    public BackgroundPacketTraceRecorder(WoWClient wowClient, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(wowClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _wowClient = wowClient;
        _logger = loggerFactory.CreateLogger<BackgroundPacketTraceRecorder>();
        _wowClient.MovementOpcodeSent += HandleMovementOpcodeSent;
    }

    public void StartRecording(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            accountName = "unknown";

        lock (_lock)
        {
            StopRecordingInternal(writeFile: true);

            _accountName = accountName;
            _rows = [];
            _nextIndex = 0;
            _stopwatch = Stopwatch.StartNew();
            _isRecording = true;
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
            _wowClient.MovementOpcodeSent -= HandleMovementOpcodeSent;
        }
    }

    private void HandleMovementOpcodeSent(Opcode opcode, int size)
    {
        lock (_lock)
        {
            if (!_isRecording || _stopwatch == null || _rows == null)
                return;

            _rows.Add(new PacketTraceRow(
                Index: _nextIndex++,
                ElapsedMs: _stopwatch.ElapsedMilliseconds,
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

        if (!writeFile)
            return;

        Directory.CreateDirectory(RecordingDir);
        var filePath = Path.Combine(RecordingDir, $"packets_{accountName}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Index,ElapsedMs,Direction,Opcode,OpcodeHex,OpcodeName,Size,IsMovement");
        foreach (var row in rows)
        {
            sb.Append(row.Index).Append(',');
            sb.Append(row.ElapsedMs).Append(',');
            sb.Append("Send").Append(',');
            sb.Append((ushort)row.Opcode).Append(',');
            sb.Append($"0x{(ushort)row.Opcode:X4}").Append(',');
            sb.Append(row.Opcode).Append(',');
            sb.Append(row.Size.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine("1");
        }

        File.WriteAllText(filePath, sb.ToString());
        _logger.LogInformation("[DIAG] Background packet trace written to {Path} ({Count} packets)", filePath, rows.Count);
    }
}
