using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using BotRunner;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace BackgroundBotRunner.Diagnostics;

/// <summary>
/// BG-side counterpart to <c>ForegroundPostTeleportWindowRecorder</c>. Captures
/// the BackgroundBotRunner's outbound + inbound packet stream during the window
/// that immediately follows an inbound MSG_MOVE_TELEPORT(_ACK), so the same
/// post-teleport parity oracle exists for BG drift detection (Stream 2D in
/// <c>docs/handoff_session_bg_movement_parity_followup_v8.md</c>).
///
/// Triggered when an inbound teleport-direction packet is observed via
/// <see cref="WoWClient.PacketReceived"/>. Records every subsequent packet for
/// <see cref="WindowDurationMs"/> milliseconds (default 2500) and writes a JSON
/// fixture into <c>Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/</c>.
///
/// Schema matches <c>PostTeleportWindowFixture</c> consumed by
/// <c>Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs</c>
/// so a single loader can deserialize either FG or BG baselines. PayloadHex is
/// left empty because the BG event surface only exposes (opcode, size) — the
/// fixture's purpose is opcode shape + timing parity, not byte-for-byte payload.
///
/// Gated on <c>WWOW_ENABLE_RECORDING_ARTIFACTS=1</c> AND
/// <c>WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1</c>; honors the optional
/// <c>WWOW_BG_POST_TELEPORT_OUTPUT</c> override and falls back to
/// <c>$WWOW_REPO_ROOT/Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window</c>.
/// </summary>
public sealed class BackgroundPostTeleportWindowRecorder : IDisposable
{
    public const string EnableEnvVar = "WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW";
    public const string OutputDirEnvVar = "WWOW_BG_POST_TELEPORT_OUTPUT";
    public const string WindowDurationEnvVar = "WWOW_BG_POST_TELEPORT_WINDOW_MS";

    private const int DefaultWindowDurationMs = 2500;
    private const int MaxPacketsPerWindow = 256;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly WoWClient _wowClient;
    private readonly ILogger<BackgroundPostTeleportWindowRecorder> _logger;
    private readonly object _lock = new();
    private readonly Action<Opcode, int> _packetSentHandler;
    private readonly Action<Opcode, int> _packetReceivedHandler;

    private bool _started;
    private string? _outputDirectory;
    private int _windowDurationMs = DefaultWindowDurationMs;

    private List<PacketEntry>? _windowPackets;
    private DateTime _windowStartUtc;
    private Timer? _closeTimer;

    public BackgroundPostTeleportWindowRecorder(WoWClient wowClient, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(wowClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _wowClient = wowClient;
        _logger = loggerFactory.CreateLogger<BackgroundPostTeleportWindowRecorder>();
        _packetSentHandler = (op, size) => HandlePacket(PacketDirection.Send, op, size);
        _packetReceivedHandler = (op, size) => HandlePacket(PacketDirection.Recv, op, size);
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

            _wowClient.PacketSent += _packetSentHandler;
            _wowClient.PacketReceived += _packetReceivedHandler;
            _started = true;
        }

        _logger.LogInformation(
            "[BG-POST-TELEPORT-WINDOW] Recording enabled -> {OutputDirectory} (window={WindowMs}ms)",
            _outputDirectory,
            _windowDurationMs);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_started)
                return;

            _wowClient.PacketSent -= _packetSentHandler;
            _wowClient.PacketReceived -= _packetReceivedHandler;
            _started = false;

            if (_windowPackets is { Count: > 0 })
            {
                FlushWindowLocked("disposed");
            }

            _closeTimer?.Dispose();
            _closeTimer = null;
        }
    }

    private void HandlePacket(PacketDirection direction, Opcode opcode, int size)
    {
        var capturedAt = DateTime.UtcNow;
        var isTeleportTrigger = IsInboundTeleportTrigger(direction, opcode);

        lock (_lock)
        {
            if (!_started || string.IsNullOrWhiteSpace(_outputDirectory))
                return;

            if (_windowPackets is null)
            {
                if (!isTeleportTrigger)
                    return;

                StartWindowLocked(capturedAt, direction, opcode, size);
                return;
            }

            if ((capturedAt - _windowStartUtc).TotalMilliseconds > _windowDurationMs)
            {
                FlushWindowLocked("duration-elapsed");
                if (isTeleportTrigger)
                    StartWindowLocked(capturedAt, direction, opcode, size);
                return;
            }

            AppendPacketLocked(capturedAt, direction, opcode, size);
            if (_windowPackets!.Count >= MaxPacketsPerWindow)
                FlushWindowLocked("max-packets");
        }
    }

    private void StartWindowLocked(DateTime startUtc, PacketDirection direction, Opcode opcode, int size)
    {
        _windowPackets = new List<PacketEntry>(capacity: 32);
        _windowStartUtc = startUtc;
        AppendPacketLocked(startUtc, direction, opcode, size);

        _closeTimer?.Dispose();
        _closeTimer = new Timer(OnCloseTimerFired, null, _windowDurationMs + 250, Timeout.Infinite);
    }

    private void AppendPacketLocked(DateTime capturedAt, PacketDirection direction, Opcode opcode, int size)
    {
        var entry = new PacketEntry
        {
            DeltaMs = (int)Math.Max(0, (capturedAt - _windowStartUtc).TotalMilliseconds),
            Direction = direction == PacketDirection.Send ? "Send" : "Recv",
            Opcode = (ushort)opcode,
            OpcodeName = opcode.ToString(),
            Size = size,
            PayloadHex = string.Empty,
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
                Source = "BG (BackgroundBotRunner) WoWClient.PacketSent / WoWClient.PacketReceived",
                CloseReason = reason,
                WindowDurationMs = _windowDurationMs,
                Trigger = trigger,
                Packets = _windowPackets!,
            };

            Directory.CreateDirectory(_outputDirectory);
            var fileName = string.Create(CultureInfo.InvariantCulture,
                $"background_{capturedAtUtc:yyyyMMdd_HHmmss_fff}.json");
            var path = Path.Combine(_outputDirectory, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(entry, JsonOptions));

            _logger.LogInformation(
                "[BG-POST-TELEPORT-WINDOW] Wrote fixture -> {Path} (packets={Count}, reason={Reason})",
                path,
                _windowPackets.Count,
                reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[BG-POST-TELEPORT-WINDOW] Failed to flush window (packets={Count}, reason={Reason})",
                _windowPackets?.Count ?? 0, reason);
        }
        finally
        {
            _windowPackets = null;
            _closeTimer?.Dispose();
            _closeTimer = null;
        }
    }

    private static bool IsInboundTeleportTrigger(PacketDirection direction, Opcode opcode)
        => direction == PacketDirection.Recv
            && opcode is Opcode.MSG_MOVE_TELEPORT or Opcode.MSG_MOVE_TELEPORT_ACK;

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
        if (!string.IsNullOrWhiteSpace(repoRoot)
            && File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
        {
            return Path.Combine(
                Path.GetFullPath(repoRoot),
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "post_teleport_packet_window");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WWoW",
            "post_teleport_packet_window");
    }

    private enum PacketDirection
    {
        Send,
        Recv,
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
