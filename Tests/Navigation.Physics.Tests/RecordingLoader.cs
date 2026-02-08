using System.Text.Json;
using System.Text.Json.Serialization;

namespace Navigation.Physics.Tests;

/// <summary>
/// Loads movement recording JSON files produced by MovementRecorder.
/// </summary>
public static class RecordingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static MovementRecording LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MovementRecording>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize recording from {path}");
    }

    /// <summary>
    /// Finds the recordings directory. Checks common locations.
    /// </summary>
    public static string GetRecordingsDirectory()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("WWOW_RECORDINGS_DIR"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot", "MovementRecordings"),
        };

        foreach (var dir in candidates)
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }

        throw new DirectoryNotFoundException(
            "Movement recordings directory not found. Set WWOW_RECORDINGS_DIR or place recordings in Documents/BloogBot/MovementRecordings/");
    }

    /// <summary>
    /// Finds the most recent recording matching a scenario name prefix (e.g., "01_flat_run_forward").
    /// </summary>
    public static string FindRecording(string scenarioPrefix)
    {
        var dir = GetRecordingsDirectory();
        var files = Directory.GetFiles(dir, "*.json")
            .Select(f => new { Path = f, Recording = TryLoadHeader(f) })
            .Where(f => f.Recording?.Description?.Contains(scenarioPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(f => f.Recording!.StartTimestampUtc)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found for scenario '{scenarioPrefix}' in {dir}");

        return files[0].Path;
    }

    /// <summary>
    /// Finds a recording by filename pattern (e.g., "Dralrahgra_Durotar_2026-02-07_19-29-21").
    /// </summary>
    public static string FindRecordingByFilename(string filenamePattern)
    {
        var dir = GetRecordingsDirectory();
        var files = Directory.GetFiles(dir, "*.json")
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .Contains(filenamePattern, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found matching '{filenamePattern}' in {dir}");

        return files[0];
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
}

public class MovementRecording
{
    [JsonPropertyName("characterName")]
    public string CharacterName { get; set; } = "";

    [JsonPropertyName("mapId")]
    public uint MapId { get; set; }

    [JsonPropertyName("zoneName")]
    public string ZoneName { get; set; } = "";

    [JsonPropertyName("startTimestampUtc")]
    public long StartTimestampUtc { get; set; }

    [JsonPropertyName("frameIntervalMs")]
    public uint FrameIntervalMs { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("race")]
    public uint Race { get; set; }

    [JsonPropertyName("raceName")]
    public string RaceName { get; set; } = "";

    [JsonPropertyName("gender")]
    public uint Gender { get; set; }

    [JsonPropertyName("genderName")]
    public string GenderName { get; set; } = "";

    [JsonPropertyName("frameCount")]
    public int FrameCount { get; set; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("frames")]
    public List<RecordedFrame> Frames { get; set; } = [];
}

public class RecordedFrame
{
    [JsonPropertyName("frameTimestamp")]
    public long FrameTimestamp { get; set; }

    [JsonPropertyName("movementFlags")]
    public uint MovementFlags { get; set; }

    [JsonPropertyName("position")]
    public RecordedPosition Position { get; set; } = new();

    [JsonPropertyName("facing")]
    public float Facing { get; set; }

    [JsonPropertyName("fallTime")]
    public uint FallTime { get; set; }

    [JsonPropertyName("walkSpeed")]
    public float WalkSpeed { get; set; }

    [JsonPropertyName("runSpeed")]
    public float RunSpeed { get; set; }

    [JsonPropertyName("runBackSpeed")]
    public float RunBackSpeed { get; set; }

    [JsonPropertyName("swimSpeed")]
    public float SwimSpeed { get; set; }

    [JsonPropertyName("swimBackSpeed")]
    public float SwimBackSpeed { get; set; }

    [JsonPropertyName("turnRate")]
    public float TurnRate { get; set; }

    [JsonPropertyName("jumpVerticalSpeed")]
    public float JumpVerticalSpeed { get; set; }

    [JsonPropertyName("jumpSinAngle")]
    public float JumpSinAngle { get; set; }

    [JsonPropertyName("jumpCosAngle")]
    public float JumpCosAngle { get; set; }

    [JsonPropertyName("jumpHorizontalSpeed")]
    public float JumpHorizontalSpeed { get; set; }

    [JsonPropertyName("swimPitch")]
    public float SwimPitch { get; set; }

    [JsonPropertyName("fallStartHeight")]
    public float FallStartHeight { get; set; }

    [JsonPropertyName("transportGuid")]
    public ulong TransportGuid { get; set; }

    [JsonPropertyName("transportOffsetX")]
    public float TransportOffsetX { get; set; }

    [JsonPropertyName("transportOffsetY")]
    public float TransportOffsetY { get; set; }

    [JsonPropertyName("transportOffsetZ")]
    public float TransportOffsetZ { get; set; }

    [JsonPropertyName("transportOrientation")]
    public float TransportOrientation { get; set; }

    [JsonPropertyName("currentSpeed")]
    public float CurrentSpeed { get; set; }

    [JsonPropertyName("fallingSpeed")]
    public float FallingSpeed { get; set; }

    // Player spline data (populated during flight paths or spline-related movement)
    [JsonPropertyName("splineFlags")]
    public uint SplineFlags { get; set; }

    [JsonPropertyName("splineTimePassed")]
    public int SplineTimePassed { get; set; }

    [JsonPropertyName("splineDuration")]
    public uint SplineDuration { get; set; }

    [JsonPropertyName("splineId")]
    public uint SplineId { get; set; }

    [JsonPropertyName("splineFinalPoint")]
    public RecordedPosition? SplineFinalPoint { get; set; }

    [JsonPropertyName("splineFinalDestination")]
    public RecordedPosition? SplineFinalDestination { get; set; }

    [JsonPropertyName("splineNodes")]
    public List<RecordedPosition>? SplineNodes { get; set; }

    [JsonPropertyName("nearbyGameObjects")]
    public List<RecordedGameObject> NearbyGameObjects { get; set; } = [];

    [JsonPropertyName("nearbyUnits")]
    public List<RecordedUnit> NearbyUnits { get; set; } = [];
}

public class RecordedGameObject
{
    [JsonPropertyName("guid")]
    public ulong Guid { get; set; }

    [JsonPropertyName("entry")]
    public uint Entry { get; set; }

    [JsonPropertyName("displayId")]
    public uint DisplayId { get; set; }

    [JsonPropertyName("gameObjectType")]
    public uint GameObjectType { get; set; }

    [JsonPropertyName("flags")]
    public uint Flags { get; set; }

    [JsonPropertyName("goState")]
    public uint GoState { get; set; }

    [JsonPropertyName("position")]
    public RecordedPosition Position { get; set; } = new();

    [JsonPropertyName("facing")]
    public float Facing { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("distanceToPlayer")]
    public float DistanceToPlayer { get; set; }
}

public class RecordedUnit
{
    [JsonPropertyName("guid")]
    public ulong Guid { get; set; }

    [JsonPropertyName("entry")]
    public uint Entry { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("position")]
    public RecordedPosition Position { get; set; } = new();

    [JsonPropertyName("facing")]
    public float Facing { get; set; }

    [JsonPropertyName("movementFlags")]
    public uint MovementFlags { get; set; }

    [JsonPropertyName("health")]
    public uint Health { get; set; }

    [JsonPropertyName("maxHealth")]
    public uint MaxHealth { get; set; }

    [JsonPropertyName("level")]
    public uint Level { get; set; }

    [JsonPropertyName("unitFlags")]
    public uint UnitFlags { get; set; }

    [JsonPropertyName("distanceToPlayer")]
    public float DistanceToPlayer { get; set; }

    [JsonPropertyName("boundingRadius")]
    public float BoundingRadius { get; set; }

    [JsonPropertyName("combatReach")]
    public float CombatReach { get; set; }

    [JsonPropertyName("npcFlags")]
    public uint NpcFlags { get; set; }

    [JsonPropertyName("targetGuid")]
    public ulong TargetGuid { get; set; }

    [JsonPropertyName("isPlayer")]
    public bool IsPlayer { get; set; }

    // Spline data for nearby units (NPCs with patrol paths, etc.)
    [JsonPropertyName("hasSpline")]
    public bool HasSpline { get; set; }

    [JsonPropertyName("splineFlags")]
    public uint SplineFlags { get; set; }

    [JsonPropertyName("splineTimePassed")]
    public int SplineTimePassed { get; set; }

    [JsonPropertyName("splineDuration")]
    public uint SplineDuration { get; set; }

    [JsonPropertyName("splineFinalDestination")]
    public RecordedPosition? SplineFinalDestination { get; set; }

    [JsonPropertyName("splineNodeCount")]
    public uint SplineNodeCount { get; set; }
}

public class RecordedPosition
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
}
