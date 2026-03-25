using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameData.Core.Enums;

namespace Navigation.Physics.Tests;

/// <summary>
/// Loads movement recording JSON files produced by MovementRecorder.
/// Model classes are defined in Helpers/RecordingModels.cs.
/// </summary>
public static class RecordingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public sealed class RecordingFileEntry
    {
        public required string Name { get; init; }
        public string? JsonPath { get; init; }
        public string? ProtoPath { get; init; }

        public string PreferredPath
        {
            get
            {
                if (!string.IsNullOrEmpty(ProtoPath))
                    return ProtoPath;
                if (!string.IsNullOrEmpty(JsonPath))
                    return JsonPath;
                throw new InvalidOperationException($"Recording '{Name}' has no usable path.");
            }
        }

        public DateTime LastWriteTimeUtc
        {
            get
            {
                var jsonWrite = !string.IsNullOrEmpty(JsonPath) && File.Exists(JsonPath)
                    ? File.GetLastWriteTimeUtc(JsonPath)
                    : DateTime.MinValue;
                var protoWrite = !string.IsNullOrEmpty(ProtoPath) && File.Exists(ProtoPath)
                    ? File.GetLastWriteTimeUtc(ProtoPath)
                    : DateTime.MinValue;
                return protoWrite >= jsonWrite ? protoWrite : jsonWrite;
            }
        }
    }

    public static MovementRecording LoadFromFile(string path)
    {
        string resolvedPath = ResolvePreferredRecordingPath(path);
        if (string.Equals(Path.GetExtension(resolvedPath), ".bin", StringComparison.OrdinalIgnoreCase))
            return LoadFromProtoFile(resolvedPath);

        return LoadFromJsonFile(resolvedPath);
    }

    /// <summary>
    /// Finds the recordings directory. Checks common locations.
    /// </summary>
    public static string GetRecordingsDirectory()
    {
        // Priority 1: Walk up from test assembly to find repo Recordings dir
        var asmDir = Path.GetDirectoryName(typeof(RecordingLoader).Assembly.Location);
        if (!string.IsNullOrEmpty(asmDir))
        {
            var dir = new DirectoryInfo(asmDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Tests", "Navigation.Physics.Tests", "Recordings");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        // Priority 2: Build output copy
        var buildCandidate = Path.Combine(AppContext.BaseDirectory, "Recordings");
        if (Directory.Exists(buildCandidate))
            return buildCandidate;

        // Priority 3: Env var
        var envDir = Environment.GetEnvironmentVariable("WWOW_RECORDINGS_DIR");
        if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
            return envDir;

        // Priority 4: Legacy Documents location
        var docsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot", "MovementRecordings");
        if (Directory.Exists(docsDir))
            return docsDir;

        throw new DirectoryNotFoundException(
            "Movement recordings directory not found. Place recordings in Tests/Navigation.Physics.Tests/Recordings/ or set WWOW_RECORDINGS_DIR.");
    }

    /// <summary>
    /// Returns logical recording entries from the recordings directory, preferring protobuf sidecars
    /// when they exist and are at least as new as the JSON source.
    /// </summary>
    public static List<RecordingFileEntry> GetRecordingFiles()
    {
        var dir = GetRecordingsDirectory();
        return GetRecordingFiles(dir);
    }

    /// <summary>
    /// Finds the most recent recording matching a scenario name prefix (e.g., "01_flat_run_forward").
    /// </summary>
    public static string FindRecording(string scenarioPrefix)
    {
        var files = GetRecordingFiles()
            .Select(entry => new { Entry = entry, Recording = TryLoadHeader(entry.PreferredPath) })
            .Where(f => f.Recording?.Description?.Contains(scenarioPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(f => f.Recording!.StartTimestampUtc)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found for scenario '{scenarioPrefix}' in {GetRecordingsDirectory()}");

        return files[0].Entry.PreferredPath;
    }

    /// <summary>
    /// Finds a recording by filename pattern (e.g., "Dralrahgra_Durotar_2026-02-07_19-29-21").
    /// </summary>
    public static string FindRecordingByFilename(string filenamePattern)
    {
        var files = GetRecordingFiles()
            .Where(f => f.Name.Contains(filenamePattern, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found matching '{filenamePattern}' in {GetRecordingsDirectory()}");

        return files[0].PreferredPath;
    }

    /// <summary>
    /// Writes or refreshes a protobuf sidecar next to a JSON recording.
    /// Returns the output .bin path.
    /// </summary>
    public static string WriteProtoCompanion(string jsonPath, bool overwrite = false)
    {
        if (!string.Equals(Path.GetExtension(jsonPath), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("WriteProtoCompanion expects a .json path.", nameof(jsonPath));
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("Recording JSON not found.", jsonPath);

        string protoPath = Path.ChangeExtension(jsonPath, ".bin");
        if (File.Exists(protoPath) &&
            !overwrite &&
            File.GetLastWriteTimeUtc(protoPath) >= File.GetLastWriteTimeUtc(jsonPath))
            return protoPath;

        var recording = LoadFromJsonFile(jsonPath);
        var protoRecording = ToProtoRecording(recording);
        using var stream = File.Create(protoPath);
        using var output = new global::Google.Protobuf.CodedOutputStream(stream);
        protoRecording.WriteTo(output);
        output.Flush();
        return protoPath;
    }

    private static MovementRecording? TryLoadHeader(string path)
    {
        try
        {
            return LoadFromFile(path);
        }
        catch
        {
            return null;
        }
    }

    private static List<RecordingFileEntry> GetRecordingFiles(string dir)
    {
        var entries = new Dictionary<string, RecordingFileEntry>(StringComparer.OrdinalIgnoreCase);

        void Upsert(string path, bool isProto)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            entries.TryGetValue(name, out var existing);
            var jsonPath = existing?.JsonPath;
            var protoPath = existing?.ProtoPath;
            if (isProto)
                protoPath = path;
            else
                jsonPath = path;

            bool preferProto = !string.IsNullOrEmpty(protoPath) &&
                (!string.IsNullOrEmpty(jsonPath)
                    ? File.GetLastWriteTimeUtc(protoPath) >= File.GetLastWriteTimeUtc(jsonPath)
                    : true);

            entries[name] = new RecordingFileEntry
            {
                Name = name,
                JsonPath = jsonPath,
                ProtoPath = preferProto ? protoPath : null,
            };

            if (!preferProto && existing?.ProtoPath != null)
            {
                entries[name] = new RecordingFileEntry
                {
                    Name = name,
                    JsonPath = jsonPath,
                    ProtoPath = null,
                };
            }
        }

        foreach (var file in Directory.GetFiles(dir, "*.json"))
            Upsert(file, isProto: false);
        foreach (var file in Directory.GetFiles(dir, "*.bin"))
            Upsert(file, isProto: true);

        return entries.Values
            .Where(entry => !string.IsNullOrEmpty(entry.JsonPath) || !string.IsNullOrEmpty(entry.ProtoPath))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolvePreferredRecordingPath(string path)
    {
        string extension = Path.GetExtension(path);
        if (string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            return path;

        if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            string protoPath = Path.ChangeExtension(path, ".bin");
            if (File.Exists(protoPath) && File.GetLastWriteTimeUtc(protoPath) >= File.GetLastWriteTimeUtc(path))
                return protoPath;
            return path;
        }

        if (File.Exists(path))
            return path;

        string jsonPath = Path.ChangeExtension(path, ".json");
        string binPath = Path.ChangeExtension(path, ".bin");
        if (File.Exists(binPath))
            return binPath;
        if (File.Exists(jsonPath))
            return jsonPath;

        return path;
    }

    private static MovementRecording LoadFromJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        var recording = JsonSerializer.Deserialize<MovementRecording>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize recording from {path}");

        NormalizeDerivedFields(recording);
        TryHydrateProtoCompanion(path, recording);
        return recording;
    }

    private static MovementRecording LoadFromProtoFile(string path)
    {
        var proto = LoadProtoRecording(path);
        var recording = FromProtoRecording(proto);
        NormalizeDerivedFields(recording);
        return recording;
    }

    private static global::Game.MovementRecording LoadProtoRecording(string path)
    {
        using var stream = File.OpenRead(path);
        return global::Game.MovementRecording.Parser.ParseFrom(stream);
    }

    private static MovementRecording FromProtoRecording(global::Game.MovementRecording proto)
    {
        var recording = new MovementRecording
        {
            CharacterName = proto.CharacterName ?? "",
            MapId = proto.MapId,
            ZoneName = proto.ZoneName ?? "",
            StartTimestampUtc = ClampToLong(proto.StartTimestampUtc),
            FrameIntervalMs = proto.FrameIntervalMs,
            Description = proto.Description ?? "",
            Race = proto.Race,
            Gender = proto.Gender,
            Frames = proto.Frames.Select(frame => new RecordedFrame
            {
                FrameTimestamp = ClampToLong(frame.FrameTimestamp),
                MovementFlags = frame.MovementFlags,
                Position = ToRecordedPosition(frame.Position),
                Facing = frame.Facing,
                FallTime = frame.FallTime,
                WalkSpeed = frame.WalkSpeed,
                RunSpeed = frame.RunSpeed,
                RunBackSpeed = frame.RunBackSpeed,
                SwimSpeed = frame.SwimSpeed,
                SwimBackSpeed = frame.SwimBackSpeed,
                TurnRate = frame.TurnRate,
                JumpVerticalSpeed = frame.JumpVerticalSpeed,
                JumpSinAngle = frame.JumpSinAngle,
                JumpCosAngle = frame.JumpCosAngle,
                JumpHorizontalSpeed = frame.JumpHorizontalSpeed,
                SwimPitch = frame.SwimPitch,
                FallStartHeight = frame.FallStartHeight,
                TransportGuid = frame.TransportGuid,
                TransportOffsetX = frame.TransportOffsetX,
                TransportOffsetY = frame.TransportOffsetY,
                TransportOffsetZ = frame.TransportOffsetZ,
                TransportOrientation = frame.TransportOrientation,
                CurrentSpeed = frame.CurrentSpeed,
                FallingSpeed = frame.FallingSpeed,
                SplineFlags = frame.SplineFlags,
                SplineTimePassed = frame.SplineTimePassed,
                SplineDuration = ClampToUInt(frame.SplineDuration),
                SplineId = frame.SplineId,
                SplineFinalPoint = frame.SplineFinalPoint != null ? ToRecordedPosition(frame.SplineFinalPoint) : null,
                SplineFinalDestination = frame.SplineFinalDestination != null ? ToRecordedPosition(frame.SplineFinalDestination) : null,
                SplineNodes = frame.SplineNodes.Select(ToRecordedPosition).ToList(),
                SystemTick = frame.SystemTick,
                NearbyGameObjects = frame.NearbyGameObjects.Select(go => new RecordedGameObject
                {
                    Guid = go.Guid,
                    Entry = go.Entry,
                    DisplayId = go.DisplayId,
                    GameObjectType = go.GameObjectType,
                    Flags = go.Flags,
                    GoState = go.GoState,
                    Position = ToRecordedPosition(go.Position),
                    Facing = go.Facing,
                    Name = go.Name ?? "",
                    Scale = go.Scale,
                    DistanceToPlayer = go.DistanceToPlayer,
                    AnimProgress = go.AnimProgress,
                }).ToList(),
                NearbyUnits = frame.NearbyUnits.Select(unit => new RecordedUnit
                {
                    Guid = unit.Guid,
                    Entry = unit.Entry,
                    Name = unit.Name ?? "",
                    Position = ToRecordedPosition(unit.Position),
                    Facing = unit.Facing,
                    MovementFlags = unit.MovementFlags,
                    Health = unit.Health,
                    MaxHealth = unit.MaxHealth,
                    Level = unit.Level,
                    UnitFlags = unit.UnitFlags,
                    DistanceToPlayer = unit.DistanceToPlayer,
                    BoundingRadius = unit.BoundingRadius,
                    CombatReach = unit.CombatReach,
                    NpcFlags = unit.NpcFlags,
                    TargetGuid = unit.TargetGuid,
                    IsPlayer = unit.IsPlayer,
                    HasSpline = unit.HasSpline,
                    SplineFlags = unit.SplineFlags,
                    SplineTimePassed = unit.SplineTimePassed,
                    SplineDuration = ClampToUInt(unit.SplineDuration),
                    SplineFinalDestination = unit.SplineFinalDestination != null
                        ? ToRecordedPosition(unit.SplineFinalDestination)
                        : null,
                    SplineNodeCount = unit.SplineNodeCount,
                }).ToList(),
            }).ToList(),
            Packets = proto.Packets.Select(packet => new RecordedPacketEvent
            {
                TimestampMs = packet.TimestampMs,
                Opcode = (ushort)Math.Clamp((int)packet.Opcode, 0, ushort.MaxValue),
                OpcodeHex = $"0x{packet.Opcode:X4}",
                IsOutbound = packet.IsOutbound,
            }).ToList(),
        };

        return recording;
    }

    private static global::Game.MovementRecording ToProtoRecording(MovementRecording recording)
    {
        var proto = new global::Game.MovementRecording
        {
            CharacterName = recording.CharacterName ?? "",
            MapId = recording.MapId,
            ZoneName = recording.ZoneName ?? "",
            StartTimestampUtc = recording.StartTimestampUtc < 0 ? 0UL : (ulong)recording.StartTimestampUtc,
            FrameIntervalMs = recording.FrameIntervalMs,
            Description = recording.Description ?? "",
            Race = recording.Race,
            Gender = recording.Gender,
        };

        foreach (var packet in recording.Packets)
        {
            proto.Packets.Add(new global::Game.PacketEvent
            {
                TimestampMs = packet.TimestampMs,
                Opcode = packet.Opcode,
                IsOutbound = packet.IsOutbound,
            });
        }

        foreach (var frame in recording.Frames)
        {
            var protoFrame = new global::Game.MovementData
            {
                MovementFlags = frame.MovementFlags,
                FallTime = frame.FallTime,
                JumpVerticalSpeed = frame.JumpVerticalSpeed,
                JumpSinAngle = frame.JumpSinAngle,
                JumpCosAngle = frame.JumpCosAngle,
                JumpHorizontalSpeed = frame.JumpHorizontalSpeed,
                SwimPitch = frame.SwimPitch,
                WalkSpeed = frame.WalkSpeed,
                RunSpeed = frame.RunSpeed,
                RunBackSpeed = frame.RunBackSpeed,
                SwimSpeed = frame.SwimSpeed,
                SwimBackSpeed = frame.SwimBackSpeed,
                TurnRate = frame.TurnRate,
                Position = ToProtoPosition(frame.Position),
                Facing = frame.Facing,
                FrameTimestamp = frame.FrameTimestamp < 0 ? 0UL : (ulong)frame.FrameTimestamp,
                TransportGuid = frame.TransportGuid,
                TransportOffsetX = frame.TransportOffsetX,
                TransportOffsetY = frame.TransportOffsetY,
                TransportOffsetZ = frame.TransportOffsetZ,
                TransportOrientation = frame.TransportOrientation,
                FallStartHeight = frame.FallStartHeight,
                CurrentSpeed = frame.CurrentSpeed,
                FallingSpeed = frame.FallingSpeed,
                SplineFlags = frame.SplineFlags,
                SplineTimePassed = frame.SplineTimePassed,
                SplineDuration = ClampToInt32(frame.SplineDuration),
                SplineId = frame.SplineId,
                SystemTick = frame.SystemTick,
            };

            if (frame.SplineFinalPoint != null)
                protoFrame.SplineFinalPoint = ToProtoPosition(frame.SplineFinalPoint);
            if (frame.SplineFinalDestination != null)
                protoFrame.SplineFinalDestination = ToProtoPosition(frame.SplineFinalDestination);

            if (frame.SplineNodes != null)
            {
                protoFrame.SplineNodes.AddRange(frame.SplineNodes.Select(ToProtoPosition));
            }

            foreach (var go in frame.NearbyGameObjects)
            {
                protoFrame.NearbyGameObjects.Add(new global::Game.GameObjectSnapshot
                {
                    Guid = go.Guid,
                    Entry = go.Entry,
                    DisplayId = go.DisplayId,
                    GameObjectType = go.GameObjectType,
                    Flags = go.Flags,
                    GoState = go.GoState,
                    Position = ToProtoPosition(go.Position),
                    Facing = go.Facing,
                    Name = go.Name ?? "",
                    Scale = go.Scale,
                    DistanceToPlayer = go.DistanceToPlayer,
                    AnimProgress = go.AnimProgress,
                });
            }

            foreach (var unit in frame.NearbyUnits)
            {
                var protoUnit = new global::Game.UnitSnapshot
                {
                    Guid = unit.Guid,
                    Entry = unit.Entry,
                    Name = unit.Name ?? "",
                    Position = ToProtoPosition(unit.Position),
                    Facing = unit.Facing,
                    MovementFlags = unit.MovementFlags,
                    Health = unit.Health,
                    MaxHealth = unit.MaxHealth,
                    Level = unit.Level,
                    UnitFlags = unit.UnitFlags,
                    DistanceToPlayer = unit.DistanceToPlayer,
                    BoundingRadius = unit.BoundingRadius,
                    CombatReach = unit.CombatReach,
                    NpcFlags = unit.NpcFlags,
                    TargetGuid = unit.TargetGuid,
                    IsPlayer = unit.IsPlayer,
                    HasSpline = unit.HasSpline,
                    SplineFlags = unit.SplineFlags,
                    SplineTimePassed = unit.SplineTimePassed,
                    SplineDuration = ClampToInt32(unit.SplineDuration),
                    SplineNodeCount = unit.SplineNodeCount,
                };

                if (unit.SplineFinalDestination != null)
                    protoUnit.SplineFinalDestination = ToProtoPosition(unit.SplineFinalDestination);

                protoFrame.NearbyUnits.Add(protoUnit);
            }

            proto.Frames.Add(protoFrame);
        }

        return proto;
    }

    private static RecordedPosition ToRecordedPosition(global::Game.Position? position)
        => new()
        {
            X = position?.X ?? 0.0f,
            Y = position?.Y ?? 0.0f,
            Z = position?.Z ?? 0.0f,
        };

    private static global::Game.Position ToProtoPosition(RecordedPosition position)
        => new()
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z,
        };

    private static long ClampToLong(ulong value)
        => value > long.MaxValue ? long.MaxValue : (long)value;

    private static uint ClampToUInt(int value)
        => value <= 0 ? 0U : (uint)value;

    private static int ClampToInt32(uint value)
        => value > int.MaxValue ? int.MaxValue : (int)value;

    private static void NormalizeDerivedFields(MovementRecording recording)
    {
        recording.FrameCount = recording.Frames.Count;
        recording.PacketCount = recording.Packets.Count;
        recording.DurationMs = recording.Frames.Count > 0
            ? (int)Math.Clamp(recording.Frames[^1].FrameTimestamp, 0L, int.MaxValue)
            : 0;
        if (string.IsNullOrWhiteSpace(recording.RaceName))
        {
            recording.RaceName = Enum.IsDefined(typeof(Race), (int)recording.Race)
                ? ((Race)recording.Race).ToString()
                : "Unknown";
        }
        if (string.IsNullOrWhiteSpace(recording.GenderName))
        {
            recording.GenderName = recording.Gender <= byte.MaxValue &&
                Enum.IsDefined(typeof(Gender), (byte)recording.Gender)
                ? ((Gender)(byte)recording.Gender).ToString()
                : "Unknown";
        }
    }

    // Best-effort: newer recordings keep packet logs only in the protobuf sidecar.
    private static void TryHydrateProtoCompanion(string jsonPath, MovementRecording recording)
    {
        string protoPath = Path.ChangeExtension(jsonPath, ".bin");
        if (!File.Exists(protoPath))
            return;

        try
        {
            var protoRecording = LoadProtoRecording(protoPath);
            if (protoRecording == null)
                return;

            if (recording.Race == 0 && protoRecording.Race != 0)
                recording.Race = protoRecording.Race;
            if (recording.Gender == 0 && protoRecording.Gender != 0)
                recording.Gender = protoRecording.Gender;

            if (recording.Packets.Count == 0 && protoRecording.Packets.Count > 0)
            {
                recording.Packets = protoRecording.Packets
                    .Select(packet => new RecordedPacketEvent
                    {
                        TimestampMs = packet.TimestampMs,
                        Opcode = (ushort)Math.Clamp((int)packet.Opcode, 0, ushort.MaxValue),
                        OpcodeHex = $"0x{packet.Opcode:X4}",
                        IsOutbound = packet.IsOutbound,
                    })
                    .ToList();
            }
        }
        catch
        {
            // Keep JSON loading resilient when the optional protobuf sidecar is absent or stale.
        }
    }
}
