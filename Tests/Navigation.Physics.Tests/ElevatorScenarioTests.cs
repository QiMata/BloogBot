using BotRunner.Movement;
using GameData.Core.Models;
using Navigation.Physics.Tests.Helpers;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using static BotRunner.Movement.TransportData;

namespace Navigation.Physics.Tests;

/// <summary>
/// Elevator scenario tests validating:
/// - TransportData correctly identifies Undercity elevators from position data
/// - TransportWaitingLogic handles full elevator ride cycles
/// - NavigationPath transport integration detects elevator crossings
/// - DetectElevatorCrossing identifies large Z-delta paths near elevator shafts
/// </summary>
public class ElevatorScenarioTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // Undercity elevator coordinates from recording data
    private static readonly Position UCUpperWest = new(1544.24f, 240.77f, 55.40f);
    private static readonly Position UCLowerWest = new(1544.24f, 240.77f, -43.0f);
    private static readonly Position UCUpperNorth = new(1596.15f, 291.80f, 55.40f);
    private static readonly Position UCLowerNorth = new(1596.15f, 291.80f, -43.0f);

    // =====================================================================
    // TRANSPORT DATA — ELEVATOR DETECTION
    // =====================================================================

    [Fact]
    public void TransportData_FindsUndercityElevator_NearUpperWestShaft()
    {
        var result = TransportData.FindNearestTransport(0, UCUpperWest, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        Assert.Equal(20655u, result.GameObjectEntry);
        _output.WriteLine($"Found: {result.Name} (entry {result.GameObjectEntry})");
    }

    [Fact]
    public void TransportData_FindsUndercityElevator_NearLowerWestShaft()
    {
        var result = TransportData.FindNearestTransport(0, UCLowerWest, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        _output.WriteLine($"Found: {result.Name} at lower level");
    }

    [Fact]
    public void TransportData_FindsNorthShaft_Separately()
    {
        var result = TransportData.FindNearestTransport(0, UCUpperNorth, maxDistance: 20f);

        Assert.NotNull(result);
        Assert.Contains("North", result.Name);
        _output.WriteLine($"Found: {result.Name}");
    }

    [Fact]
    public void TransportData_DoesNotFindElevator_InOrgrimmar()
    {
        var orgPos = new Position(1630f, -4373f, 31f);
        var result = TransportData.FindNearestTransport(1, orgPos, maxDistance: 20f);

        Assert.Null(result);
    }

    [Fact]
    public void TransportData_GetDestinationStop_FromUpper_ReturnsLower()
    {
        var dest = TransportData.GetDestinationStop(UndercityElevatorWest, UCUpperWest);

        Assert.NotNull(dest);
        Assert.Contains("Lower", dest.Name);
        Assert.True(dest.WaitPosition.Z < 0, "Lower stop should have negative Z");
        _output.WriteLine($"Destination: {dest.Name} at Z={dest.WaitPosition.Z:F1}");
    }

    [Fact]
    public void TransportData_GetDestinationStop_FromLower_ReturnsUpper()
    {
        var dest = TransportData.GetDestinationStop(UndercityElevatorWest, UCLowerWest);

        Assert.NotNull(dest);
        Assert.Contains("Upper", dest.Name);
        Assert.True(dest.WaitPosition.Z > 0, "Upper stop should have positive Z");
    }

    // =====================================================================
    // ELEVATOR CROSSING DETECTION
    // =====================================================================

    [Fact]
    public void DetectElevatorCrossing_UCUpperToLower_FindsElevator()
    {
        var result = TransportData.DetectElevatorCrossing(
            mapId: 0, from: UCUpperWest, to: UCLowerWest, minZDelta: 30f);

        Assert.NotNull(result);
        Assert.Equal(TransportType.Elevator, result.Type);
        _output.WriteLine($"Detected: {result.Name}, Z range={result.VerticalRange:F0}y");
    }

    [Fact]
    public void DetectElevatorCrossing_FlatPath_ReturnsNull()
    {
        var from = new Position(1544f, 241f, 55f);
        var to = new Position(1600f, 241f, 52f); // Only 3y drop

        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);
        Assert.Null(result);
    }

    [Fact]
    public void DetectElevatorCrossing_LargeZDeltaFarFromElevator_ReturnsNull()
    {
        // Large Z delta but nowhere near an elevator
        var from = new Position(0f, 0f, 100f);
        var to = new Position(0f, 0f, -50f);

        var result = TransportData.DetectElevatorCrossing(0, from, to, minZDelta: 30f);
        Assert.Null(result);
    }

    // =====================================================================
    // WAITING LOGIC — FULL ELEVATOR CYCLE
    // =====================================================================

    [Fact]
    public void WaitingLogic_FullElevatorRideDown_AllPhases()
    {
        var boardStop = UndercityElevatorWest.Stops[0]; // Upper
        var exitStop = UndercityElevatorWest.Stops[1];  // Lower
        var logic = new TransportWaitingLogic(UndercityElevatorWest, boardStop, exitStop);

        // Phase 1: Approaching (far from stop)
        var farPos = new Position(1560f, 260f, 55f);
        var target = logic.Update(farPos, 0, null, 0.5f);
        Assert.Equal(TransportPhase.Approaching, logic.CurrentPhase);
        Assert.NotNull(target);
        _output.WriteLine($"Phase 1 (Approaching): target=({target.X:F1}, {target.Y:F1})");

        // Phase 2: Arrive at stop → WaitingForArrival
        var atStop = new Position(1544f, 241f, 55f);
        target = logic.Update(atStop, 0, null, 0.5f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        _output.WriteLine($"Phase 2 (WaitingForArrival)");

        // Phase 3: Elevator arrives at upper stop → Boarding
        var elevatorAtUpper = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 55f }
        };
        target = logic.Update(atStop, 0, elevatorAtUpper, 0.5f);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);
        _output.WriteLine($"Phase 3 (Boarding): elevator at Z=55");

        // Phase 4: Step on → Riding
        target = logic.Update(atStop, currentTransportGuid: 42, null, 0.5f);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        _output.WriteLine($"Phase 4 (Riding)");

        // Phase 5: Riding — midway
        var elevatorMidway = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 10f }
        };
        target = logic.Update(new Position(1544f, 241f, 10f), 42, elevatorMidway, 5f);
        Assert.Equal(TransportPhase.Riding, logic.CurrentPhase);
        Assert.Null(target); // Don't move while riding
        _output.WriteLine($"Phase 5 (Riding midway): Z≈10");

        // Phase 6: Arrive at lower stop → Disembarking
        var elevatorAtLower = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = -43f }
        };
        target = logic.Update(new Position(1544f, 241f, -43f), 42, elevatorAtLower, 5f);
        Assert.Equal(TransportPhase.Disembarking, logic.CurrentPhase);
        _output.WriteLine($"Phase 6 (Disembarking): at lower stop");

        // Phase 7: Step off → Complete
        target = logic.Update(new Position(1544f, 241f, -43f), 0, null, 0.5f);
        Assert.Equal(TransportPhase.Complete, logic.CurrentPhase);
        _output.WriteLine($"Phase 7 (Complete): ride finished");
    }

    [Fact]
    public void WaitingLogic_ElevatorLeavesBeforeBoarding_ReturnsToWaiting()
    {
        var boardStop = UndercityElevatorWest.Stops[0];
        var exitStop = UndercityElevatorWest.Stops[1];
        var logic = new TransportWaitingLogic(UndercityElevatorWest, boardStop, exitStop);

        // Get to WaitingForArrival
        logic.Update(new Position(1544f, 241f, 55f), 0, null, 0.1f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);

        // Elevator arrives → Boarding
        var elevatorAtUpper = new List<DynamicObjectProto>
        {
            new() { DisplayId = 455, X = 1544f, Y = 241f, Z = 55f }
        };
        logic.Update(new Position(1544f, 241f, 55f), 0, elevatorAtUpper, 0.1f);
        Assert.Equal(TransportPhase.Boarding, logic.CurrentPhase);

        // Boarding timeout (couldn't get on in time) → back to WaitingForArrival
        logic.Update(new Position(1544f, 241f, 55f), 0, null, 11f);
        Assert.Equal(TransportPhase.WaitingForArrival, logic.CurrentPhase);
        _output.WriteLine("Elevator left, returned to WaitingForArrival");
    }

    // =====================================================================
    // NAVIGATION PATH — TRANSPORT INTEGRATION
    // =====================================================================

    [Fact]
    public void NavigationPath_CheckTransportNeeded_UCUpperToLower_ReturnsTrue()
    {
        var path = new NavigationPath(null);

        bool needed = path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);

        Assert.True(needed);
        Assert.True(path.IsRidingTransport);
        Assert.NotNull(path.ActiveTransportRide);
        Assert.Equal(TransportPhase.Approaching, path.ActiveTransportRide.CurrentPhase);
        _output.WriteLine("NavigationPath detected elevator crossing");
    }

    [Fact]
    public void NavigationPath_CheckTransportNeeded_FlatPath_ReturnsFalse()
    {
        var path = new NavigationPath(null);
        var flat1 = new Position(1630f, -4373f, 31f);
        var flat2 = new Position(1650f, -4360f, 31f);

        bool needed = path.CheckTransportNeeded(flat1, flat2, mapId: 1);

        Assert.False(needed);
        Assert.False(path.IsRidingTransport);
    }

    [Fact]
    public void NavigationPath_CancelTransportRide_ClearsState()
    {
        var path = new NavigationPath(null);
        path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);
        Assert.True(path.IsRidingTransport);

        path.CancelTransportRide();

        Assert.False(path.IsRidingTransport);
        Assert.Null(path.ActiveTransportRide);
    }

    [Fact]
    public void NavigationPath_GetTransportTarget_DelegatesCorrectly()
    {
        var path = new NavigationPath(null);
        path.CheckTransportNeeded(UCUpperWest, UCLowerWest, mapId: 0);

        // Should return a position to move toward (the boarding stop)
        var target = path.GetTransportTarget(
            new Position(1560f, 260f, 55f), 0, null, 1f / 60f);

        Assert.NotNull(target);
        _output.WriteLine($"Transport target: ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
    }
}

[Collection("PhysicsEngine")]
public class ElevatorPhysicsParityTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [StructLayout(LayoutKind.Sequential)]
    private struct SweepSceneHit
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool Hit;
        public float Distance;
        public float Time;
        public float PenetrationDepth;
        public NavigationInterop.Vector3 Normal;
        public NavigationInterop.Vector3 Point;
        public int TriIndex;
        public NavigationInterop.Vector3 Barycentric;
        public uint InstanceId;
        [MarshalAs(UnmanagedType.I1)]
        public bool StartPenetrating;
        [MarshalAs(UnmanagedType.I1)]
        public bool NormalFlipped;
        public byte FeatureType;
        public uint PhysMaterialId;
        public float StaticFriction;
        public float DynamicFriction;
        public float Restitution;
        public byte CapsuleRegion;
    }

    private sealed class TransportSupportScenario
    {
        public required MovementRecording Recording { get; init; }
        public required RecordedFrame CurrentFrame { get; init; }
        public required RecordedFrame NextFrame { get; init; }
        public required RecordedGameObject Transport { get; init; }
        public required NavigationInterop.DynamicObjectInfo[] DynamicObjects { get; init; }
        public required int FrameIndex { get; init; }
        public required float Radius { get; init; }
        public required float Height { get; init; }
        public required float WorldX { get; init; }
        public required float WorldY { get; init; }
        public required float WorldZ { get; init; }
        public required float DeltaTime { get; init; }
    }

    [DllImport("Navigation.dll", EntryPoint = "SweepCapsule", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SweepCapsuleForDiagnostics(
        uint mapId,
        in NavigationInterop.Capsule capsule,
        in NavigationInterop.Vector3 direction,
        float distance,
        [Out] SweepSceneHit[] hits,
        int maxHits,
        in NavigationInterop.Vector3 playerForward);

    private static (float x, float y, float z) TransportLocalToWorld(RecordedFrame frame, RecordedGameObject transport)
    {
        float cosO = MathF.Cos(transport.Facing);
        float sinO = MathF.Sin(transport.Facing);
        float wx = frame.Position.X * cosO - frame.Position.Y * sinO + transport.Position.X;
        float wy = frame.Position.X * sinO + frame.Position.Y * cosO + transport.Position.Y;
        float wz = frame.Position.Z + transport.Position.Z;
        return (wx, wy, wz);
    }

    private TransportSupportScenario LoadUndercityTransportSupportScenario()
    {
        var recording = RecordingTestHelpers.LoadByFilename(Recordings.UndercityElevatorV2, _output);
        RecordingTestHelpers.TryPreloadMap(recording.MapId, _output);

        int frameIndex = -1;
        RecordedFrame? currentFrame = null;
        RecordedFrame? nextFrame = null;
        RecordedGameObject? nextTransport = null;
        for (int i = 0; i < recording.Frames.Count - 1; i++)
        {
            var current = recording.Frames[i];
            var next = recording.Frames[i + 1];
            if (current.TransportGuid == 0 || current.TransportGuid != next.TransportGuid)
                continue;
            if ((current.MovementFlags & 0x6000) != 0)
                continue;

            nextTransport = next.NearbyGameObjects.FirstOrDefault(go => go.Guid == current.TransportGuid);
            if (nextTransport == null)
                continue;

            frameIndex = i;
            currentFrame = current;
            nextFrame = next;
            break;
        }

        Assert.True(frameIndex >= 0 && currentFrame != null && nextFrame != null && nextTransport != null,
            "Expected a grounded on-transport frame with dynamic GO data in UndercityElevatorV2.");

        var dynamicObjects = nextFrame!.NearbyGameObjects
            .Where(go => go.DisplayId != 0)
            .Select(go => new NavigationInterop.DynamicObjectInfo
            {
                Guid = go.Guid,
                DisplayId = go.DisplayId,
                X = go.Position.X,
                Y = go.Position.Y,
                Z = go.Position.Z,
                Orientation = go.Facing,
                Scale = go.Scale > 0 ? go.Scale : 1.0f,
                GoState = go.GoState,
            })
            .ToArray();

        var (radius, height) = RecordingTestHelpers.GetCapsuleDimensions(recording, _output);
        var (worldX, worldY, worldZ) = TransportLocalToWorld(currentFrame!, nextTransport!);
        float dt = (nextFrame.FrameTimestamp - currentFrame.FrameTimestamp) / 1000.0f;

        return new TransportSupportScenario
        {
            Recording = recording,
            CurrentFrame = currentFrame,
            NextFrame = nextFrame,
            Transport = nextTransport,
            DynamicObjects = dynamicObjects,
            FrameIndex = frameIndex,
            Radius = radius,
            Height = height,
            WorldX = worldX,
            WorldY = worldY,
            WorldZ = worldZ,
            DeltaTime = dt,
        };
    }

    [Fact]
    [Trait("Category", "MovementParity")]
    [Trait("ParityScenario", "Transport")]
    public void UndercityElevatorReplay_TransportAverageStaysWithinParityTarget()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.UndercityElevatorV2, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0)
            return;

        var transportStats = result.OnTransportStats();
        _output.WriteLine($"Undercity elevator transport avg={transportStats.avg:F4} p99={transportStats.p99:F4} max={transportStats.max:F4}");

        Assert.True(transportStats.count > 0, "Expected on-transport frames in the Undercity elevator replay.");
        Assert.True(transportStats.avg < 0.15f,
            $"Undercity elevator transport avg {transportStats.avg:F4}y exceeds the 0.15y parity target.");
    }

    [Fact]
    public void UndercityElevatorTransportFrame_ReportsDynamicSupportToken()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadUndercityTransportSupportScenario();
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        NavigationInterop.ClearAllDynamicObjects();
        GCHandle dynHandle = default;
        try
        {
            dynHandle = GCHandle.Alloc(scenario.DynamicObjects, GCHandleType.Pinned);

            var input = new NavigationInterop.PhysicsInput
            {
                MoveFlags = cleanedMoveFlags,
                X = scenario.CurrentFrame.Position.X,
                Y = scenario.CurrentFrame.Position.Y,
                Z = scenario.CurrentFrame.Position.Z,
                Orientation = scenario.CurrentFrame.Facing,
                Pitch = scenario.CurrentFrame.SwimPitch,
                Vx = 0,
                Vy = 0,
                Vz = 0,
                WalkSpeed = scenario.CurrentFrame.WalkSpeed,
                RunSpeed = scenario.CurrentFrame.RunSpeed,
                RunBackSpeed = scenario.CurrentFrame.RunBackSpeed,
                SwimSpeed = scenario.CurrentFrame.SwimSpeed,
                SwimBackSpeed = scenario.CurrentFrame.SwimBackSpeed,
                FlightSpeed = 0,
                TurnSpeed = scenario.CurrentFrame.TurnRate,
                TransportGuid = scenario.CurrentFrame.TransportGuid,
                FallTime = scenario.CurrentFrame.FallTime,
                Height = scenario.Height,
                Radius = scenario.Radius,
                PrevGroundZ = scenario.WorldZ,
                PrevGroundNx = 0,
                PrevGroundNy = 0,
                PrevGroundNz = 1,
                NearbyObjects = dynHandle.AddrOfPinnedObject(),
                NearbyObjectCount = scenario.DynamicObjects.Length,
                MapId = scenario.Recording.MapId,
                DeltaTime = scenario.DeltaTime,
                FrameCounter = (uint)scenario.FrameIndex,
            };

            var output = NavigationInterop.StepPhysicsV2(ref input);
            _output.WriteLine(
                $"frame={scenario.FrameIndex} world=({scenario.WorldX:F3},{scenario.WorldY:F3},{scenario.WorldZ:F3}) " +
                $"support={output.StandingOnInstanceId} local=({output.StandingOnLocalX:F3}," +
                $"{output.StandingOnLocalY:F3},{output.StandingOnLocalZ:F3}) outZ={output.Z:F3}");

            Assert.NotEqual(0u, output.StandingOnInstanceId);
            Assert.True(float.IsFinite(output.StandingOnLocalX));
            Assert.True(float.IsFinite(output.StandingOnLocalY));
            Assert.True(float.IsFinite(output.StandingOnLocalZ));
        }
        finally
        {
            if (dynHandle.IsAllocated)
                dynHandle.Free();
            NavigationInterop.ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadUndercityTransportSupportScenario();
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        NavigationInterop.ClearAllDynamicObjects();
        GCHandle dynHandle = default;
        try
        {
            dynHandle = GCHandle.Alloc(scenario.DynamicObjects, GCHandleType.Pinned);

            var input = new NavigationInterop.PhysicsInput
            {
                MoveFlags = cleanedMoveFlags,
                X = scenario.CurrentFrame.Position.X,
                Y = scenario.CurrentFrame.Position.Y,
                Z = scenario.CurrentFrame.Position.Z,
                Orientation = scenario.CurrentFrame.Facing,
                Pitch = scenario.CurrentFrame.SwimPitch,
                WalkSpeed = scenario.CurrentFrame.WalkSpeed,
                RunSpeed = scenario.CurrentFrame.RunSpeed,
                RunBackSpeed = scenario.CurrentFrame.RunBackSpeed,
                SwimSpeed = scenario.CurrentFrame.SwimSpeed,
                SwimBackSpeed = scenario.CurrentFrame.SwimBackSpeed,
                TurnSpeed = scenario.CurrentFrame.TurnRate,
                TransportGuid = scenario.CurrentFrame.TransportGuid,
                FallTime = scenario.CurrentFrame.FallTime,
                Height = scenario.Height,
                Radius = scenario.Radius,
                PrevGroundZ = scenario.WorldZ,
                PrevGroundNz = 1,
                NearbyObjects = dynHandle.AddrOfPinnedObject(),
                NearbyObjectCount = scenario.DynamicObjects.Length,
                MapId = scenario.Recording.MapId,
                DeltaTime = scenario.DeltaTime,
                FrameCounter = (uint)scenario.FrameIndex,
            };

            var output = NavigationInterop.StepPhysicsV2(ref input);
            Assert.NotEqual(0u, output.StandingOnInstanceId);

            var capsule = NavigationInterop.Capsule.FromFeetPosition(
                scenario.WorldX,
                scenario.WorldY,
                scenario.WorldZ - 0.10f,
                scenario.Radius,
                scenario.Height);
            var hits = new SweepSceneHit[32];
            var direction = new NavigationInterop.Vector3(1, 0, 0);
            float worldFacing = scenario.CurrentFrame.Facing + scenario.Transport.Facing;
            var playerForward = new NavigationInterop.Vector3(MathF.Cos(worldFacing), MathF.Sin(worldFacing), 0);
            int hitCount = SweepCapsuleForDiagnostics(
                scenario.Recording.MapId,
                in capsule,
                in direction,
                0.0f,
                hits,
                hits.Length,
                in playerForward);

            var dynamicIds = hits
                .Take(hitCount)
                .Where(hit => (hit.InstanceId & 0x80000000u) != 0)
                .Select(hit => hit.InstanceId)
                .Distinct()
                .ToArray();

            _output.WriteLine(
                $"frame={scenario.FrameIndex} support={output.StandingOnInstanceId} " +
                $"capsuleHits={hitCount} dynamicIds=[{string.Join(", ", dynamicIds)}]");

            Assert.True(hitCount > 0, "Expected overlap hits from SweepCapsule on the elevator support frame.");
            Assert.Single(dynamicIds);
            Assert.Equal(output.StandingOnInstanceId, dynamicIds[0]);
        }
        finally
        {
            if (dynHandle.IsAllocated)
                dynHandle.Free();
            NavigationInterop.ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void OrgrimmarZeppelinReplay_SkipsInFlightFrames_WithoutDynamicObjectData()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.OrgrimmarZeppelin, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0)
            return;

        int simulatedTransportFrames = result.FrameDetails.Count(f => f.IsOnTransport);
        _output.WriteLine(
            $"Orgrimmar zeppelin transport transitions={result.TransportTransitionCount} " +
            $"simulated={simulatedTransportFrames} skipped={result.TransportFrameCount}");

        Assert.True(result.TransportTransitionCount > 0,
            "Expected the replay harness to detect an Orgrimmar transport transition.");
        Assert.True(result.TransportFrameCount > 0,
            "Expected in-flight transport frames to be skipped when no dynamic transport object data is recorded.");
        Assert.Equal(0, simulatedTransportFrames);
    }
}
