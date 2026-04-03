using BotRunner;
using BotRunner.Interfaces;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ForegroundBotRunner.Diagnostics;

/// <summary>
/// Writes a stable foreground packet sidecar for live parity tests.
/// Bound to BotRunnerService's Start/StopPhysicsRecording lifecycle.
/// </summary>
public sealed class ForegroundPacketTraceRecorder : IDiagnosticPacketTraceRecorder, IDisposable
{
    private static readonly string RecordingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

    private readonly ILogger<ForegroundPacketTraceRecorder> _logger;
    private readonly object _lock = new();
    private readonly Action<PacketDirection, ushort, int> _packetHandler;

    private Stopwatch? _stopwatch;
    private List<PacketTraceRow>? _rows;
    private string? _accountName;
    private bool _isRecording;
    private bool _writeArtifactOnStop;
    private int _nextIndex;

    private sealed record PacketTraceRow(
        int Index,
        long ElapsedMs,
        PacketDirection Direction,
        ushort Opcode,
        int Size);

    public ForegroundPacketTraceRecorder(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<ForegroundPacketTraceRecorder>();
        _packetHandler = HandlePacketCaptured;
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
                _logger.LogInformation("[DIAG] Foreground packet trace skipped; set {EnvVar}=1 to enable artifact recording",
                    RecordingArtifactsFeature.EnvironmentVariableName);
                return;
            }

            _accountName = accountName;
            _rows = [];
            _nextIndex = 0;
            _stopwatch = Stopwatch.StartNew();
            _isRecording = true;
            _writeArtifactOnStop = true;
            PacketLogger.OnPacketCaptured += _packetHandler;
        }

        _logger.LogInformation("[DIAG] Foreground packet trace recording STARTED for {Account}", accountName);
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
        }
    }

    private void HandlePacketCaptured(PacketDirection direction, ushort opcode, int size)
    {
        lock (_lock)
        {
            if (!_isRecording || _stopwatch == null || _rows == null)
                return;

            _rows.Add(new PacketTraceRow(
                Index: _nextIndex++,
                ElapsedMs: _stopwatch.ElapsedMilliseconds,
                Direction: direction,
                Opcode: opcode,
                Size: size));
        }
    }

    private void StopRecordingInternal(bool writeFile)
    {
        if (!_isRecording)
            return;

        PacketLogger.OnPacketCaptured -= _packetHandler;

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
            string opcodeName = ResolveOpcodeName(row.Opcode);
            sb.Append(row.Index).Append(',');
            sb.Append(row.ElapsedMs).Append(',');
            sb.Append(row.Direction == PacketDirection.Send ? "Send" : "Recv").Append(',');
            sb.Append(row.Opcode).Append(',');
            sb.Append($"0x{row.Opcode:X4}").Append(',');
            sb.Append(opcodeName).Append(',');
            sb.Append(row.Size.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(IsMovementOpcode(opcodeName) ? "1" : "0");
        }

        File.WriteAllText(filePath, sb.ToString());
        _logger.LogInformation("[DIAG] Foreground packet trace written to {Path} ({Count} packets)", filePath, rows.Count);
    }

    private static string ResolveOpcodeName(ushort opcode)
    {
        string name = ((Opcode)(uint)opcode).ToString();
        return name.Length > 0 && char.IsDigit(name[0])
            ? string.Empty
            : name;
    }

    private static bool IsMovementOpcode(string opcodeName)
    {
        if (string.IsNullOrWhiteSpace(opcodeName))
            return false;

        return opcodeName.Contains("MOVE", StringComparison.Ordinal);
    }
}
