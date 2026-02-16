using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Replays recorded movement data through the C++ PhysicsEngine (StepV2)
/// and tracks deviation between simulated and recorded positions.
///
/// Dynamic game objects (elevators, doors) are passed as part of PhysicsInput
/// each frame via the nearbyObjects array. The engine auto-registers models
/// on first encounter by displayId and updates positions per-frame.
///
/// Transport handling:
/// - On-transport frames pass transport-local coordinates directly with
///   TransportGuid set. The engine finds the matching transport in nearbyObjects
///   and transforms transport-local → world internally.
/// - BOARD transition (TransportGuid 0→nonzero): engine state reset, frame skipped.
/// - LEAVE transition (TransportGuid nonzero→0): engine state reset, frame skipped.
/// - Engine output is always in world coordinates.
/// - The TELEPORT_TO_PLANE (0x08000000) and SPLINE_ELEVATION (0x04000000) flags are stripped from moveFlags.
/// </summary>
public static class ReplayEngine
{
    private const uint TELEPORT_TO_PLANE = 0x08000000;
    private const uint SPLINE_ELEVATION = 0x04000000;

    /// <summary>
    /// Replays a full recording through PhysicsStepV2 and returns calibration results.
    /// Dynamic game objects from the recording are passed per-frame via PhysicsInput.
    /// </summary>
    public static CalibrationResult Replay(MovementRecording recording, string recordingName = "")
    {
        var result = new CalibrationResult();
        var frames = recording.Frames;

        if (frames.Count < 2) return result;

        var (capsuleRadius, capsuleHeight) = RecordingTestHelpers.GetCapsuleDimensions(recording);

        ReplayFrames(recording, frames, capsuleRadius, capsuleHeight, result, recordingName);

        return result;
    }

    /// <summary>
    /// Find a transport game object by GUID in the frame's nearbyGameObjects.
    /// </summary>
    private static RecordedGameObject? FindTransportGO(
        RecordedFrame frame, ulong transportGuid)
    {
        if (frame.NearbyGameObjects == null) return null;
        foreach (var go in frame.NearbyGameObjects)
        {
            if (go.Guid == transportGuid)
                return go;
        }
        return null;
    }

    /// <summary>
    /// Transform transport-local coordinates to world coordinates using the
    /// transport game object's recorded world position and orientation.
    /// </summary>
    private static (float x, float y, float z) TransportLocalToWorld(
        RecordedFrame frame, RecordedGameObject transport)
    {
        float cosO = MathF.Cos(transport.Facing);
        float sinO = MathF.Sin(transport.Facing);

        // Position fields are transport-local when TransportGuid != 0
        float lx = frame.Position.X;
        float ly = frame.Position.Y;
        float lz = frame.Position.Z;

        // Rotate local position by transport orientation, then translate
        float wx = lx * cosO - ly * sinO + transport.Position.X;
        float wy = lx * sinO + ly * cosO + transport.Position.Y;
        float wz = lz + transport.Position.Z;

        return (wx, wy, wz);
    }

    /// <summary>
    /// Build DynamicObjectInfo array from a frame's nearby game objects.
    /// </summary>
    private static DynamicObjectInfo[] BuildDynamicObjects(RecordedFrame frame)
    {
        if (frame.NearbyGameObjects == null || frame.NearbyGameObjects.Count == 0)
            return Array.Empty<DynamicObjectInfo>();

        return frame.NearbyGameObjects
            .Where(go => go.DisplayId != 0)
            .Select(go => new DynamicObjectInfo
            {
                Guid = go.Guid,
                DisplayId = go.DisplayId,
                X = go.Position.X,
                Y = go.Position.Y,
                Z = go.Position.Z,
                Orientation = go.Facing,
                Scale = go.Scale > 0 ? go.Scale : 1.0f,
                GoState = go.GoState
            })
            .ToArray();
    }

    /// <summary>
    /// Detect one-frame position spikes in the recording data.
    /// A spike at frame i is defined as: position[i] deviates from both neighbors
    /// by more than the threshold while neighbors agree with each other.
    /// Detects both Z-only spikes and full 3D position spikes (XY jitter at transitions).
    /// Returns a HashSet of frame indices that are spike frames.
    /// </summary>
    private static HashSet<int> DetectZSpikes(List<RecordedFrame> frames)
    {
        var spikeFrames = new HashSet<int>();
        for (int i = 1; i < frames.Count - 1; i++)
        {
            // Skip transport frames (transport-local coords aren't comparable)
            if (frames[i].TransportGuid != 0) continue;

            float prevZ = frames[i - 1].Position.Z;
            float currZ = frames[i].Position.Z;
            float nextZ = frames[i + 1].Position.Z;

            float deltaPrev = MathF.Abs(currZ - prevZ);
            float deltaNext = MathF.Abs(currZ - nextZ);
            float neighborDelta = MathF.Abs(nextZ - prevZ);

            // Z spike: current deviates from both neighbors by >0.8y, but neighbors agree within 0.3y
            if (deltaPrev > 0.8f && deltaNext > 0.8f && neighborDelta < 0.3f)
            {
                spikeFrames.Add(i);
                continue;
            }

            // 3D position spike: XY+Z combined deviation from neighbors > 0.5y
            // while neighbors are close to each other. Catches position jitter at
            // jump transitions where Z spike alone doesn't trigger (e.g. XY teleport).
            var prev = frames[i - 1].Position;
            var curr = frames[i].Position;
            var next = frames[i + 1].Position;

            float dist3dPrev = MathF.Sqrt(
                (curr.X - prev.X) * (curr.X - prev.X) +
                (curr.Y - prev.Y) * (curr.Y - prev.Y) +
                (curr.Z - prev.Z) * (curr.Z - prev.Z));
            float dist3dNext = MathF.Sqrt(
                (curr.X - next.X) * (curr.X - next.X) +
                (curr.Y - next.Y) * (curr.Y - next.Y) +
                (curr.Z - next.Z) * (curr.Z - next.Z));
            float neighborDist3d = MathF.Sqrt(
                (next.X - prev.X) * (next.X - prev.X) +
                (next.Y - prev.Y) * (next.Y - prev.Y) +
                (next.Z - prev.Z) * (next.Z - prev.Z));

            // 3D spike: current deviates from both neighbors by >0.5y, neighbors within 0.3y
            if (dist3dPrev > 0.5f && dist3dNext > 0.5f && neighborDist3d < 0.3f)
            {
                spikeFrames.Add(i);
            }
        }
        return spikeFrames;
    }

    private static void ReplayFrames(
        MovementRecording recording,
        List<RecordedFrame> frames,
        float capsuleRadius, float capsuleHeight,
        CalibrationResult result,
        string recordingName = "")
    {
        var prevOutput = new PhysicsOutput();
        float prevGroundZ = frames[0].Position.Z;
        int fallStartFrameIndex = -1;

        // Pre-detect one-frame Z spike artifacts in the recording data
        var zSpikeFrames = DetectZSpikes(frames);

        for (int i = 0; i < frames.Count - 1; i++)
        {
            var currentFrame = frames[i];
            var nextFrame = frames[i + 1];
            float dt = (nextFrame.FrameTimestamp - currentFrame.FrameTimestamp) / 1000.0f;

            if (dt <= 0) continue;

            // =================================================================
            // Transport transition detection
            // =================================================================
            bool currentOnTransport = currentFrame.TransportGuid != 0;
            bool nextOnTransport = nextFrame.TransportGuid != 0;

            // BOARD transition: world coords → transport-local coords
            if (!currentOnTransport && nextOnTransport)
            {
                fallStartFrameIndex = -1;
                prevOutput = new PhysicsOutput();
                var transport = FindTransportGO(nextFrame, nextFrame.TransportGuid)
                             ?? FindTransportGO(currentFrame, nextFrame.TransportGuid);
                if (transport != null)
                {
                    var (wx, wy, wz) = TransportLocalToWorld(nextFrame, transport);
                    prevGroundZ = wz;
                }
                else
                {
                    prevGroundZ = nextFrame.Position.Z;
                }
                result.AddSkippedTransportTransition(i);
                continue;
            }

            // LEAVE transition: transport-local coords → world coords
            if (currentOnTransport && !nextOnTransport)
            {
                fallStartFrameIndex = -1;
                prevOutput = new PhysicsOutput();
                prevGroundZ = nextFrame.Position.Z;
                result.AddSkippedTransportTransition(i);
                continue;
            }

            // =================================================================
            // Resolve world-space positions for comparison and velocity estimation
            // =================================================================
            float worldX, worldY, worldZ;
            float nextWorldX, nextWorldY, nextWorldZ;
            bool onTransportFrame = false;

            if (currentOnTransport)
            {
                var transport = FindTransportGO(currentFrame, currentFrame.TransportGuid);
                if (transport == null)
                {
                    // No GO data for this transport — skip the frame
                    result.AddSkippedTransportFrame(i);
                    continue;
                }

                (worldX, worldY, worldZ) = TransportLocalToWorld(currentFrame, transport);

                var nextTransport = FindTransportGO(nextFrame, nextFrame.TransportGuid)
                                 ?? transport;
                (nextWorldX, nextWorldY, nextWorldZ) = TransportLocalToWorld(nextFrame, nextTransport);
                onTransportFrame = true;
            }
            else
            {
                worldX = currentFrame.Position.X;
                worldY = currentFrame.Position.Y;
                worldZ = currentFrame.Position.Z;
                nextWorldX = nextFrame.Position.X;
                nextWorldY = nextFrame.Position.Y;
                nextWorldZ = nextFrame.Position.Z;
            }

            // =================================================================
            // Teleport detection (using world coords)
            // =================================================================
            float tdx = nextWorldX - worldX;
            float tdy = nextWorldY - worldY;
            float tdz = nextWorldZ - worldZ;
            if (tdx * tdx + tdy * tdy + tdz * tdz > 50f * 50f)
            {
                fallStartFrameIndex = -1;
                prevOutput = new PhysicsOutput();
                prevGroundZ = nextWorldZ;
                result.AddSkippedTeleport(i);
                continue;
            }

            // Compute fall duration from frame timestamps
            bool isFalling = (currentFrame.MovementFlags & 0x6000) != 0;
            bool wasAirborne = fallStartFrameIndex >= 0;
            uint fallTimeMs = 0;
            if (isFalling)
            {
                if (fallStartFrameIndex < 0)
                    fallStartFrameIndex = i;
                fallTimeMs = (uint)(currentFrame.FrameTimestamp - frames[fallStartFrameIndex].FrameTimestamp);
            }
            else
            {
                fallStartFrameIndex = -1;
            }

            // Strip TELEPORT_TO_PLANE from moveFlags
            uint cleanedMoveFlags = currentFrame.MovementFlags & ~TELEPORT_TO_PLANE & ~SPLINE_ELEVATION;

            // Build nearby objects array for this frame
            var dynObjects = BuildDynamicObjects(currentFrame);
            GCHandle dynHandle = default;

            try
            {
                var input = new PhysicsInput
                {
                    MoveFlags = cleanedMoveFlags,
                    // Pass raw coordinates: transport-local if on transport, world if not.
                    // Engine transforms to world internally when TransportGuid != 0.
                    X = currentFrame.Position.X,
                    Y = currentFrame.Position.Y,
                    Z = currentFrame.Position.Z,
                    Orientation = currentFrame.Facing,
                    Pitch = currentFrame.SwimPitch,
                    Vx = 0, Vy = 0, Vz = 0,
                    WalkSpeed = currentFrame.WalkSpeed,
                    RunSpeed = currentFrame.RunSpeed,
                    RunBackSpeed = currentFrame.RunBackSpeed,
                    SwimSpeed = currentFrame.SwimSpeed,
                    SwimBackSpeed = currentFrame.SwimBackSpeed,
                    FlightSpeed = 0,
                    TurnSpeed = currentFrame.TurnRate,
                    // Set transport GUID so the engine can transform coords internally
                    TransportGuid = currentFrame.TransportGuid,
                    TransportX = 0, TransportY = 0, TransportZ = 0, TransportO = 0,
                    FallTime = fallTimeMs,
                    Height = capsuleHeight,
                    Radius = capsuleRadius,
                    PrevGroundZ = prevGroundZ,
                    PrevGroundNx = prevOutput.GroundNx,
                    PrevGroundNy = prevOutput.GroundNy,
                    PrevGroundNz = prevOutput.GroundNz != 0 ? prevOutput.GroundNz : 1.0f,
                    PendingDepenX = 0,
                    PendingDepenY = 0,
                    PendingDepenZ = 0,
                    StandingOnInstanceId = prevOutput.StandingOnInstanceId,
                    StandingOnLocalX = prevOutput.StandingOnLocalX,
                    StandingOnLocalY = prevOutput.StandingOnLocalY,
                    StandingOnLocalZ = prevOutput.StandingOnLocalZ,
                    NearbyObjects = IntPtr.Zero,
                    NearbyObjectCount = 0,
                    MapId = recording.MapId,
                    DeltaTime = dt,
                    FrameCounter = (uint)i,
                };

                // Pin and set nearby objects
                if (dynObjects.Length > 0)
                {
                    dynHandle = GCHandle.Alloc(dynObjects, GCHandleType.Pinned);
                    input.NearbyObjects = dynHandle.AddrOfPinnedObject();
                    input.NearbyObjectCount = dynObjects.Length;
                }

                // Velocity: for ALL airborne frames, derive velocity from recorded
                // position deltas. The TRUST_INPUT_VELOCITY flag tells the engine
                // to use these Vx/Vy values as-is instead of recalculating from
                // moveFlags + orientation (which accumulates error over 50+ frames).
                bool isAirborne = (currentFrame.MovementFlags & 0x6000) != 0;
                if (isAirborne)
                {
                    const float GRAVITY = 19.2911f;
                    float deltaX = nextWorldX - worldX;
                    float deltaY = nextWorldY - worldY;
                    float deltaZ = nextWorldZ - worldZ;
                    input.Vx = deltaX / dt;
                    input.Vy = deltaY / dt;
                    // Remove gravity contribution from vertical delta to get initial Vz
                    input.Vz = deltaZ / dt + 0.5f * GRAVITY * dt;
                    input.PhysicsFlags = PHYSICS_FLAG_TRUST_INPUT_VELOCITY;

                    if (!wasAirborne)
                        input.FallTime = 1;
                }

                var output = StepPhysicsV2(ref input);
                prevOutput = output;
                prevGroundZ = output.GroundZ;

                float dx = output.X - nextWorldX;
                float dy = output.Y - nextWorldY;
                float dz = output.Z - nextWorldZ;
                float posError = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                float horizError = MathF.Sqrt(dx * dx + dy * dy);
                float vertError = MathF.Abs(dz);

                bool isSwimming = (currentFrame.MovementFlags & 0x00200000) != 0;
                uint nextCleanFlags = nextFrame.MovementFlags & ~TELEPORT_TO_PLANE & ~SPLINE_ELEVATION;
                bool flagsChanged = cleanedMoveFlags != nextCleanFlags;

                // Classify transition type
                var transition = ClassifyTransition(cleanedMoveFlags, nextCleanFlags);

                // Detect surface step: ground-to-ground Z change > threshold.
                // Walking off a ramp/ledge causes a sudden Z change that the engine
                // can't predict (single-frame surface transition). Classify these like
                // other transitions so they're excluded from steady-state metrics.
                if (transition == TransitionType.None && !isAirborne &&
                    (nextCleanFlags & 0x6000) == 0) // next frame also grounded
                {
                    float groundZDelta = MathF.Abs(nextWorldZ - worldZ);
                    if (groundZDelta > 0.5f)
                    {
                        transition = TransitionType.SurfaceStep;
                        flagsChanged = true; // Exclude from SS
                    }
                }

                if (onTransportFrame)
                    result.AddSimulatedTransportFrame();

                // Detect recording artifacts
                // A frame is an artifact if the current or next frame is a Z spike
                bool isArtifact = zSpikeFrames.Contains(i) || zSpikeFrames.Contains(i + 1);

                // Detect SPLINE_ELEVATION transitions (raw flags only)
                bool splineElevCurrent = (currentFrame.MovementFlags & SPLINE_ELEVATION) != 0;
                bool splineElevNext = (nextFrame.MovementFlags & SPLINE_ELEVATION) != 0;
                bool isSplineElevTransition = splineElevCurrent != splineElevNext;

                result.AddFrame(new CalibrationResult.FrameDetail
                {
                    Frame = i,
                    PosError = posError,
                    HorizError = horizError,
                    VertError = vertError,
                    SimX = output.X, SimY = output.Y, SimZ = output.Z,
                    RecX = nextWorldX, RecY = nextWorldY, RecZ = nextWorldZ,
                    MoveFlags = cleanedMoveFlags,
                    NextMoveFlags = nextCleanFlags,
                    RawMoveFlags = currentFrame.MovementFlags,
                    RawNextMoveFlags = nextFrame.MovementFlags,
                    Dt = dt,
                    Orientation = currentFrame.Facing,
                    RecordedSpeed = currentFrame.CurrentSpeed,
                    EngineGroundZ = output.GroundZ,
                    EngineVx = output.Vx, EngineVy = output.Vy, EngineVz = output.Vz,
                    InputVx = input.Vx, InputVy = input.Vy, InputVz = input.Vz,
                    IsSwimming = isSwimming,
                    IsAirborne = isAirborne,
                    IsFlagTransition = flagsChanged,
                    SwimPitch = currentFrame.SwimPitch,
                    IsOnTransport = onTransportFrame,
                    Transition = transition,
                    RecordingName = recordingName,
                    IsRecordingArtifact = isArtifact,
                    IsSplineElevationTransition = isSplineElevTransition,
                });
            }
            finally
            {
                if (dynHandle.IsAllocated) dynHandle.Free();
            }
        }
    }

    // Movement flag constants for transition classification
    private const uint MASK_AIRBORNE = 0x6000;       // JUMPING | FALLING_FAR
    private const uint MASK_SWIMMING = 0x00200000;
    private const uint MASK_WALKING  = 0x00000100;
    private const uint MASK_DIRECTION = 0x000000FF;   // Forward, Backward, Strafe, Turn, Pitch

    /// <summary>
    /// Classify the type of movement state transition between current and next frame flags.
    /// </summary>
    private static TransitionType ClassifyTransition(uint currentFlags, uint nextFlags)
    {
        if (currentFlags == nextFlags)
            return TransitionType.None;

        bool wasAirborne = (currentFlags & MASK_AIRBORNE) != 0;
        bool isAirborne = (nextFlags & MASK_AIRBORNE) != 0;
        bool wasSwimming = (currentFlags & MASK_SWIMMING) != 0;
        bool isSwimming = (nextFlags & MASK_SWIMMING) != 0;
        bool wasWalking = (currentFlags & MASK_WALKING) != 0;
        bool isWalking = (nextFlags & MASK_WALKING) != 0;

        // Air transitions
        if (!wasAirborne && isAirborne) return TransitionType.JumpStart;
        if (wasAirborne && !isAirborne) return TransitionType.Landing;

        // Water transitions
        if (!wasSwimming && isSwimming) return TransitionType.WaterEntry;
        if (wasSwimming && !isSwimming) return TransitionType.WaterExit;

        // Walk/run speed change
        if (wasWalking != isWalking) return TransitionType.SpeedChange;

        // Direction change (different directional flags but same movement mode)
        if ((currentFlags & MASK_DIRECTION) != (nextFlags & MASK_DIRECTION))
            return TransitionType.DirectionChange;

        return TransitionType.Other;
    }
}
