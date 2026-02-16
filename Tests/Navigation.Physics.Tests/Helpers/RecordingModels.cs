using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Navigation.Physics.Tests;

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

    [JsonPropertyName("systemTick")]
    public uint SystemTick { get; set; }

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

    [JsonPropertyName("scale")]
    public float Scale { get; set; } = 1.0f;

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
