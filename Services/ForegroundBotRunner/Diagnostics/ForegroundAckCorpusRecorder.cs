using System;
using System.IO;
using System.Text.Json;
using BotRunner;
using ForegroundBotRunner.Mem.Hooks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Parsers;

namespace ForegroundBotRunner.Diagnostics;

/// <summary>
/// Captures outbound WoW.exe ACK packets with raw bytes and a packet-derived snapshot
/// so parity fixtures can be versioned under the repo test corpus.
/// </summary>
public sealed class ForegroundAckCorpusRecorder : IDisposable
{
    public const string EnableEnvVar = "WWOW_CAPTURE_ACK_CORPUS";
    public const string OutputDirEnvVar = "WWOW_ACK_CORPUS_OUTPUT";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<ForegroundAckCorpusRecorder> _logger;
    private readonly object _lock = new();
    private readonly Action<PacketCapture> _packetHandler;

    private bool _started;
    private int _nextSequence;
    private string? _outputDirectory;

    public ForegroundAckCorpusRecorder(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<ForegroundAckCorpusRecorder>();
        _packetHandler = HandlePacketCaptured;
    }

    public static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

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
            PacketLogger.OnPacketCapturedDetailed += _packetHandler;
            _started = true;
        }

        _logger.LogInformation("[ACK-CORPUS] Recording enabled -> {OutputDirectory}", _outputDirectory);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_started)
                return;

            PacketLogger.OnPacketCapturedDetailed -= _packetHandler;
            _started = false;
        }
    }

    private void HandlePacketCaptured(PacketCapture capture)
    {
        if (capture.Direction != PacketDirection.Send || capture.RawBytes == null || capture.RawBytes.Length < 4)
            return;

        var opcode = (Opcode)(uint)capture.Opcode;
        if (!IsAckOpcode(opcode))
            return;

        AckCorpusEntry entry;
        try
        {
            entry = BuildEntry(capture, opcode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ACK-CORPUS] Failed to parse outbound {Opcode} ({OpcodeHex})", opcode, capture.Opcode);
            return;
        }

        lock (_lock)
        {
            if (!_started || string.IsNullOrWhiteSpace(_outputDirectory))
                return;

            var opcodeDirectory = Path.Combine(_outputDirectory, entry.OpcodeName);
            Directory.CreateDirectory(opcodeDirectory);

            var fileName = $"{capture.TimestampUtc:yyyyMMdd_HHmmss_fff}_{_nextSequence++:D4}.json";
            var path = Path.Combine(opcodeDirectory, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(entry, JsonOptions));
            _logger.LogInformation("[ACK-CORPUS] Wrote {OpcodeName} fixture -> {Path}", entry.OpcodeName, path);
        }
    }

    private static AckCorpusEntry BuildEntry(PacketCapture capture, Opcode opcode)
    {
        var rawBytes = capture.RawBytes ?? Array.Empty<byte>();
        var entry = new AckCorpusEntry
        {
            CapturedAtUtc = capture.TimestampUtc,
            Source = "WoW.exe NetClient::Send 0x005379A0",
            Opcode = capture.Opcode,
            OpcodeName = opcode.ToString(),
            PacketSize = capture.Size,
            PacketBytesHex = Convert.ToHexString(rawBytes)
        };

        if (opcode == Opcode.MSG_MOVE_WORLDPORT_ACK)
            return entry;

        entry.Guid = BitConverter.ToUInt64(rawBytes, 4);
        entry.Counter = BitConverter.ToUInt32(rawBytes, 12);

        if (opcode == Opcode.MSG_MOVE_TELEPORT_ACK)
        {
            entry.ClientTimeMs = BitConverter.ToUInt32(rawBytes, 16);
            return entry;
        }

        if (IsSpeedAckOpcode(opcode))
        {
            entry.Movement = ParseMovementSnapshot(rawBytes.AsSpan(16, rawBytes.Length - 20));
            entry.ClientTimeMs = entry.Movement?.ClientTimeMs;
            entry.Speed = BitConverter.ToSingle(rawBytes, rawBytes.Length - 4);
            return entry;
        }

        if (IsMovementFlagToggleAckOpcode(opcode))
        {
            entry.Movement = ParseMovementSnapshot(rawBytes.AsSpan(16, rawBytes.Length - 20));
            entry.ClientTimeMs = entry.Movement?.ClientTimeMs;
            entry.ToggleValue = BitConverter.ToSingle(rawBytes, rawBytes.Length - 4);
            return entry;
        }

        entry.Movement = ParseMovementSnapshot(rawBytes.AsSpan(16));
        entry.ClientTimeMs = entry.Movement?.ClientTimeMs;
        return entry;
    }

    private static MovementSnapshot ParseMovementSnapshot(ReadOnlySpan<byte> rawMovementBytes)
    {
        using var ms = new MemoryStream(rawMovementBytes.ToArray(), writable: false);
        using var reader = new BinaryReader(ms);
        var movement = MovementPacketHandler.ParseMovementInfo(reader);

        return new MovementSnapshot
        {
            MovementFlags = (uint)movement.MovementFlags,
            ClientTimeMs = movement.LastUpdated,
            X = movement.X,
            Y = movement.Y,
            Z = movement.Z,
            Facing = movement.Facing,
            TransportGuid = movement.TransportGuid,
            TransportOffsetX = movement.TransportOffset?.X,
            TransportOffsetY = movement.TransportOffset?.Y,
            TransportOffsetZ = movement.TransportOffset?.Z,
            TransportOrientation = movement.TransportOrientation,
            SwimPitch = movement.SwimPitch,
            FallTimeMs = movement.FallTime,
            JumpVerticalSpeed = movement.JumpVerticalSpeed,
            JumpCosAngle = movement.JumpCosAngle,
            JumpSinAngle = movement.JumpSinAngle,
            JumpHorizontalSpeed = movement.JumpHorizontalSpeed,
            SplineElevation = movement.SplineElevation
        };
    }

    private static bool IsAckOpcode(Opcode opcode)
        => opcode is Opcode.MSG_MOVE_TELEPORT_ACK
            or Opcode.MSG_MOVE_WORLDPORT_ACK
            or Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK
            or Opcode.CMSG_FORCE_MOVE_ROOT_ACK
            or Opcode.CMSG_FORCE_MOVE_UNROOT_ACK
            or Opcode.CMSG_MOVE_WATER_WALK_ACK
            or Opcode.CMSG_MOVE_HOVER_ACK
            or Opcode.CMSG_MOVE_FEATHER_FALL_ACK
            or Opcode.CMSG_MOVE_KNOCK_BACK_ACK
            or Opcode.MSG_MOVE_SET_RAW_POSITION_ACK
            or Opcode.CMSG_MOVE_FLIGHT_ACK;

    private static bool IsSpeedAckOpcode(Opcode opcode)
        => opcode is Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK
            or Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK;

    private static bool IsMovementFlagToggleAckOpcode(Opcode opcode)
        => opcode is Opcode.CMSG_MOVE_WATER_WALK_ACK
            or Opcode.CMSG_MOVE_HOVER_ACK
            or Opcode.CMSG_MOVE_FEATHER_FALL_ACK;

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
                "ack_golden_corpus");
        }

        return Path.Combine(FgLogPaths.LogsDir, "ack_golden_corpus");
    }

    private sealed class AckCorpusEntry
    {
        public int SchemaVersion { get; init; } = 1;
        public DateTime CapturedAtUtc { get; init; }
        public string Source { get; init; } = string.Empty;
        public ushort Opcode { get; init; }
        public string OpcodeName { get; init; } = string.Empty;
        public int PacketSize { get; init; }
        public string PacketBytesHex { get; init; } = string.Empty;
        public ulong? Guid { get; set; }
        public uint? Counter { get; set; }
        public uint? ClientTimeMs { get; set; }
        public float? Speed { get; set; }
        public float? ToggleValue { get; set; }
        public MovementSnapshot? Movement { get; set; }
    }

    private sealed class MovementSnapshot
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
