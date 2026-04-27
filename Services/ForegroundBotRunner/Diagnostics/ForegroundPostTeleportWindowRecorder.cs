using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using BotRunner;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner.Diagnostics;

/// <summary>
/// Captures the WoW.exe outbound + inbound packet stream during the window
/// that immediately follows an <c>MSG_MOVE_TELEPORT</c> arrival. The captured
/// fixture is the binary-parity oracle for whether BG bot's post-teleport
/// suppression in <see cref="WoWSharpClient.Movement.MovementController"/> is
/// dropping packets that WoW.exe actually emits.
///
/// Triggered when a <c>Recv</c> packet with opcode <see cref="Opcode.MSG_MOVE_TELEPORT"/>
/// (0xC5) is observed via <see cref="PacketLogger.OnPacketCapturedDetailed"/>.
/// Records every subsequent packet for <see cref="WindowDurationMs"/> milliseconds
/// (default 2500) and writes a JSON fixture into
/// <c>Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/</c>.
///
/// Gated on <c>WWOW_ENABLE_RECORDING_ARTIFACTS=1</c> AND
/// <c>WWOW_CAPTURE_POST_TELEPORT_WINDOW=1</c>; honors the same output-path
/// resolution conventions as <see cref="ForegroundAckCorpusRecorder"/> via
/// the optional <c>WWOW_POST_TELEPORT_WINDOW_OUTPUT</c> override.
/// </summary>
public sealed class ForegroundPostTeleportWindowRecorder : IDisposable
{
    public const string EnableEnvVar = "WWOW_CAPTURE_POST_TELEPORT_WINDOW";
    public const string OutputDirEnvVar = "WWOW_POST_TELEPORT_WINDOW_OUTPUT";
    public const string WindowDurationEnvVar = "WWOW_POST_TELEPORT_WINDOW_MS";

    private const int DefaultWindowDurationMs = 2500;
    private const int MaxPacketsPerWindow = 256;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<ForegroundPostTeleportWindowRecorder> _logger;
    private readonly object _lock = new();
    private readonly Action<PacketCapture> _packetHandler;

    private bool _started;
    private string? _outputDirectory;
    private int _windowDurationMs = DefaultWindowDurationMs;

    private List<PacketEntry>? _windowPackets;
    private DateTime _windowStartUtc;
    private Timer? _closeTimer;

    public ForegroundPostTeleportWindowRecorder(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<ForegroundPostTeleportWindowRecorder>();
        _packetHandler = HandlePacketCaptured;
    }

    public static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

    public int WindowDurationMs => _windowDurationMs;

    public void Start()
    {
        if (!RecordingArtifactsFeature.IsEnabled() || !IsEnabled())
            return;

        lock (_lock)
        {
            if (_started)
                return;

            _outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(_outputDirectory);
            _windowDurationMs = ResolveWindowDurationMs();
            PacketLogger.OnPacketCapturedDetailed += _packetHandler;
            _started = true;
        }

        _logger.LogInformation(
            "[POST-TELEPORT-WINDOW] Recording enabled -> {OutputDirectory} (window={WindowMs}ms)",
            _outputDirectory,
            _windowDurationMs);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_started)
                return;

            PacketLogger.OnPacketCapturedDetailed -= _packetHandler;
            _started = false;

            if (_windowPackets is { Count: > 0 })
            {
                FlushWindowLocked("disposed");
            }

            _closeTimer?.Dispose();
            _closeTimer = null;
        }
    }

    private void HandlePacketCaptured(PacketCapture capture)
    {
        var opcode = (Opcode)(uint)capture.Opcode;
        var isTeleportTrigger =
            capture.Direction == PacketDirection.Recv && opcode == Opcode.MSG_MOVE_TELEPORT;

        lock (_lock)
        {
            if (!_started || string.IsNullOrWhiteSpace(_outputDirectory))
                return;

            if (_windowPackets is null)
            {
                if (!isTeleportTrigger)
                    return;

                StartWindowLocked(capture);
                return;
            }

            // Already recording — flush if overdue, otherwise append.
            if ((capture.TimestampUtc - _windowStartUtc).TotalMilliseconds > _windowDurationMs)
            {
                FlushWindowLocked("duration-elapsed");
                if (isTeleportTrigger)
                    StartWindowLocked(capture);
                return;
            }

            AppendPacketLocked(capture);
            if (_windowPackets!.Count >= MaxPacketsPerWindow)
                FlushWindowLocked("max-packets");
        }
    }

    private void StartWindowLocked(PacketCapture trigger)
    {
        _windowPackets = new List<PacketEntry>(capacity: 32);
        _windowStartUtc = trigger.TimestampUtc;
        AppendPacketLocked(trigger);

        _closeTimer?.Dispose();
        _closeTimer = new Timer(OnCloseTimerFired, null, _windowDurationMs + 250, Timeout.Infinite);
    }

    private void AppendPacketLocked(PacketCapture capture)
    {
        var opcode = (Opcode)(uint)capture.Opcode;
        var entry = new PacketEntry
        {
            DeltaMs = (int)Math.Max(0, (capture.TimestampUtc - _windowStartUtc).TotalMilliseconds),
            Direction = capture.Direction.ToString(),
            Opcode = capture.Opcode,
            OpcodeName = opcode.ToString(),
            Size = capture.Size,
            PayloadHex = capture.RawBytes is { Length: > 0 }
                ? Convert.ToHexString(capture.RawBytes)
                : string.Empty
        };

        _windowPackets!.Add(entry);
    }

    private void OnCloseTimerFired(object? state)
    {
        lock (_lock)
        {
            if (!_started || _windowPackets is null)
                return;

            FlushWindowLocked("timer");
        }
    }

    private void FlushWindowLocked(string reason)
    {
        if (_windowPackets is null || _outputDirectory is null)
        {
            _windowPackets = null;
            return;
        }

        try
        {
            var trigger = _windowPackets[0];
            var capturedAtUtc = _windowStartUtc;
            var entry = new WindowFixture
            {
                CapturedAtUtc = capturedAtUtc,
                CaptureScenario = "post_teleport_packet_window",
                Source = "WoW.exe (FG) NetClient::Send 0x005379A0 / NetClient::ProcessMessage 0x00537AA0",
                CloseReason = reason,
                WindowDurationMs = _windowDurationMs,
                Trigger = trigger,
                Packets = _windowPackets!
            };

            Directory.CreateDirectory(_outputDirectory);
            var fileName = string.Create(CultureInfo.InvariantCulture,
                $"foreground_{capturedAtUtc:yyyyMMdd_HHmmss_fff}.json");
            var path = Path.Combine(_outputDirectory, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(entry, JsonOptions));

            _logger.LogInformation(
                "[POST-TELEPORT-WINDOW] Wrote fixture -> {Path} (packets={Count}, reason={Reason})",
                path,
                _windowPackets.Count,
                reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[POST-TELEPORT-WINDOW] Failed to flush window (packets={Count}, reason={Reason})",
                _windowPackets?.Count ?? 0, reason);
        }
        finally
        {
            _windowPackets = null;
            _closeTimer?.Dispose();
            _closeTimer = null;
        }
    }

    private static int ResolveWindowDurationMs()
    {
        var raw = Environment.GetEnvironmentVariable(WindowDurationEnvVar);
        if (!string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 100
            && parsed <= 10000)
        {
            return parsed;
        }

        return DefaultWindowDurationMs;
    }

    private static string ResolveOutputDirectory()
    {
        var explicitPath = Environment.GetEnvironmentVariable(OutputDirEnvVar);
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var repoRoot = Environment.GetEnvironmentVariable("WWOW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(repoRoot) && File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
        {
            return Path.Combine(
                Path.GetFullPath(repoRoot),
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "post_teleport_packet_window");
        }

        return Path.Combine(FgLogPaths.LogsDir, "post_teleport_packet_window");
    }

    private sealed class WindowFixture
    {
        public int SchemaVersion { get; init; } = 1;
        public DateTime CapturedAtUtc { get; init; }
        public string CaptureScenario { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string CloseReason { get; init; } = string.Empty;
        public int WindowDurationMs { get; init; }
        public PacketEntry? Trigger { get; init; }
        public List<PacketEntry> Packets { get; init; } = new();
    }

    private sealed class PacketEntry
    {
        public int DeltaMs { get; init; }
        public string Direction { get; init; } = string.Empty;
        public ushort Opcode { get; init; }
        public string OpcodeName { get; init; } = string.Empty;
        public int Size { get; init; }
        public string PayloadHex { get; init; } = string.Empty;
    }
}
