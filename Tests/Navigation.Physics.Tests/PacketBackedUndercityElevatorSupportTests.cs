using System;
using System.Linq;
using Navigation.Physics.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class PacketBackedUndercityElevatorSupportTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    private sealed class SupportQueryScenario
    {
        public required MovementRecording Recording { get; init; }
        public required RecordedFrame CurrentFrame { get; init; }
        public required RecordedFrame NextFrame { get; init; }
        public required RecordedGameObject Transport { get; init; }
        public required DynamicObjectInfo[] DynamicObjects { get; init; }
        public required Vector3 WorldPosition { get; init; }
        public required float Radius { get; init; }
        public required float Height { get; init; }
        public required float DeltaTime { get; init; }
        public required float WorldFacing { get; init; }
        public required int CurrentFrameIndex { get; init; }
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_ReplayLogsUpperArrivalSupportState()
    {
        var result = _fixture.ReplayCache.GetOrReplay(Recordings.PacketBackedUndercityElevatorUp, _output, _fixture.IsInitialized);
        if (result.FrameCount == 0)
            return;

        var transportWindow = result.FrameDetails
            .Where(frame => frame.IsOnTransport && frame.Frame >= 10 && frame.Frame <= 19)
            .OrderBy(frame => frame.Frame)
            .ToList();

        Assert.NotEmpty(transportWindow);

        foreach (var frame in transportWindow)
        {
            _output.WriteLine(
                $"frame={frame.Frame} simZ={frame.SimZ:F3} recZ={frame.RecZ:F3} err={frame.PosError:F3} " +
                $"support={frame.EngineStandingOnInstanceId} local=({frame.EngineStandingOnLocalX:F3}," +
                $"{frame.EngineStandingOnLocalY:F3},{frame.EngineStandingOnLocalZ:F3}) gw={frame.EngineGroundedWallState}");
        }

        var frame19 = transportWindow.FirstOrDefault(frame => frame.Frame == 19) ?? transportWindow.Last();
        Assert.Equal(19, frame19.Frame);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 19);
        ClearAllDynamicObjects();
        try
        {
            foreach (var go in scenario.DynamicObjects)
            {
                RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation, go.GoState);
            }

            var mergedContacts = QueryContacts(scenario.Recording.MapId, BuildMergedQueryBox(scenario));
            var finalSupportContacts = QueryContacts(scenario.Recording.MapId, BuildFinalSupportQueryBox(scenario));

            var mergedSupport = FindElevatorSupportContact(mergedContacts, scenario.WorldPosition.Z);
            var finalSupport = finalSupportContacts.FirstOrDefault(contact =>
                contact.InstanceId != 0 &&
                contact.Normal.Z < -0.99f &&
                MathF.Abs(contact.Point.Z - scenario.WorldPosition.Z) < 0.35f);
            var staticUpperDeck = finalSupportContacts
                .Where(contact => contact.InstanceId == 0 && MathF.Abs(contact.Point.Z - 39.945f) < 0.5f)
                .OrderBy(contact => MathF.Abs(contact.Point.Z - 39.945f))
                .FirstOrDefault();
            bool finalSupportWalkableWithoutState = false;
            bool finalSupportStateWithoutState = false;
            bool finalSupportFlagAfterWithoutState = false;
            bool finalSupportWalkableWithState = false;
            bool finalSupportStateWithState = false;
            bool finalSupportFlagAfterWithState = false;
            if (finalSupport.InstanceId != 0)
            {
                var finalTriangle = finalSupport.ToTriangle();
                var finalNormal = finalSupport.Normal;
                var worldPosition = scenario.WorldPosition;
                finalSupportWalkableWithoutState = EvaluateWoWCheckWalkable(
                    in finalTriangle,
                    in finalNormal,
                    in worldPosition,
                    scenario.Radius,
                    scenario.Height,
                    useStandardWalkableThreshold: true,
                    groundedWallFlagBefore: false,
                    out finalSupportStateWithoutState,
                    out finalSupportFlagAfterWithoutState);
                finalSupportWalkableWithState = EvaluateWoWCheckWalkable(
                    in finalTriangle,
                    in finalNormal,
                    in worldPosition,
                    scenario.Radius,
                    scenario.Height,
                    useStandardWalkableThreshold: true,
                    groundedWallFlagBefore: true,
                    out finalSupportStateWithState,
                    out finalSupportFlagAfterWithState);
            }

            _output.WriteLine(
                $"framePair={scenario.CurrentFrameIndex}->{scenario.CurrentFrameIndex + 1} startZ={scenario.WorldPosition.Z:F3} " +
                $"mergedCount={mergedContacts.Length} finalCount={finalSupportContacts.Length}");
            _output.WriteLine(
                $"mergedSupport inst=0x{mergedSupport.InstanceId:X8} point={mergedSupport.Point} normal={mergedSupport.Normal}");
            if (finalSupport.InstanceId != 0)
            {
                _output.WriteLine(
                    $"finalSupport inst=0x{finalSupport.InstanceId:X8} point={finalSupport.Point} normal={finalSupport.Normal}");
                _output.WriteLine(
                    $"finalSupport walk state=false => walk={finalSupportWalkableWithoutState} state={finalSupportStateWithoutState} after={finalSupportFlagAfterWithoutState}");
                _output.WriteLine(
                    $"finalSupport walk state=true  => walk={finalSupportWalkableWithState} state={finalSupportStateWithState} after={finalSupportFlagAfterWithState}");
            }
            if (staticUpperDeck.Point.Z != 0 || staticUpperDeck.InstanceId == 0)
            {
                _output.WriteLine(
                    $"finalStatic point={staticUpperDeck.Point} normal={staticUpperDeck.Normal} walk={staticUpperDeck.Walkable}");
            }

            Assert.True(mergedSupport.InstanceId != 0,
                "Expected the merged query to include the elevator deck support face on the failing upper-arrival step.");
            Assert.True(finalSupport.InstanceId != 0,
                "Expected the final support query to include a dynamic support face near the upper-arrival step.");
            Assert.False(finalSupportWalkableWithoutState,
                "Expected the final support face to stay non-walkable without the stateful path.");
            Assert.True(finalSupportWalkableWithState,
                "Expected the final support face to become walkable on the stateful CheckWalkable path.");
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 19);
        ClearAllDynamicObjects();
        try
        {
            foreach (var go in scenario.DynamicObjects)
            {
                RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation, go.GoState);
            }

            var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
            var requestedMove = BuildRequestedMove(scenario);
            var worldPosition = scenario.WorldPosition;

            bool tracedWithoutState = EvaluateGroundedWallSelection(
                scenario.Recording.MapId,
                in boxMin,
                in boxMax,
                in worldPosition,
                in requestedMove,
                scenario.Radius,
                scenario.Height,
                groundedWallFlagBefore: false,
                out GroundedWallSelectionTrace traceWithoutState);
            bool tracedWithState = EvaluateGroundedWallSelection(
                scenario.Recording.MapId,
                in boxMin,
                in boxMax,
                in worldPosition,
                in requestedMove,
                scenario.Radius,
                scenario.Height,
                groundedWallFlagBefore: true,
                out GroundedWallSelectionTrace traceWithState);

            _output.WriteLine(
                $"frame19 gw0 traced={tracedWithoutState} branch={traceWithoutState.BranchKind} " +
                $"before={traceWithoutState.GroundedWallStateBefore} after={traceWithoutState.GroundedWallStateAfter} " +
                $"selectedInst=0x{traceWithoutState.SelectedInstanceId:X8} selectedNormal={traceWithoutState.SelectedNormal} " +
                $"walk0={traceWithoutState.WalkableWithoutState} walk1={traceWithoutState.WalkableWithState} " +
                $"finalMove={traceWithoutState.FinalProjectedMove} blocked={traceWithoutState.BlockedFraction:F4}");
            _output.WriteLine(
                $"frame19 gw1 traced={tracedWithState} branch={traceWithState.BranchKind} " +
                $"before={traceWithState.GroundedWallStateBefore} after={traceWithState.GroundedWallStateAfter} " +
                $"selectedInst=0x{traceWithState.SelectedInstanceId:X8} selectedNormal={traceWithState.SelectedNormal} " +
                $"walk0={traceWithState.WalkableWithoutState} walk1={traceWithState.WalkableWithState} " +
                $"finalMove={traceWithState.FinalProjectedMove} blocked={traceWithState.BlockedFraction:F4}");

            Assert.True(tracedWithoutState, "Expected the frame-19 grounded-wall trace to resolve a contact set.");
            Assert.True(tracedWithState, "Expected the stateful frame-19 grounded-wall trace to resolve a contact set.");
            Assert.Equal(0u, traceWithoutState.GroundedWallStateAfter);
            Assert.Equal(1u, traceWithState.GroundedWallStateAfter);
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame19_LogsTransportRegistrationOrderAgainstSupportContacts()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 19);
        ClearAllDynamicObjects();
        try
        {
            foreach (var go in scenario.DynamicObjects)
            {
                RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation, go.GoState);
            }

            int transportIndex = Array.FindIndex(scenario.DynamicObjects, go => go.Guid == scenario.Transport.Guid);
            Assert.True(transportIndex >= 0, "Expected the active transport GUID to be part of the registered dynamic object set.");

            var mergedContacts = QueryContacts(scenario.Recording.MapId, BuildMergedQueryBox(scenario));
            var finalSupportContacts = QueryContacts(scenario.Recording.MapId, BuildFinalSupportQueryBox(scenario));
            var mergedSupport = FindElevatorSupportContact(mergedContacts, scenario.WorldPosition.Z);
            var finalSupport = finalSupportContacts.FirstOrDefault(contact =>
                contact.InstanceId != 0 &&
                contact.Normal.Z < -0.99f &&
                MathF.Abs(contact.Point.Z - scenario.WorldPosition.Z) < 0.35f);
            var dynamicRegistrationOrder = string.Join(", ",
                scenario.DynamicObjects.Select((go, index) =>
                    $"{index}:{go.Guid}:display={go.DisplayId}"));

            _output.WriteLine(
                $"frame19 transportGuid={scenario.Transport.Guid} transportIndex={transportIndex} " +
                $"freshProcessTransportInst~0x{(0x80000001u + (uint)transportIndex):X8}");
            _output.WriteLine($"frame19 dynamicOrder={dynamicRegistrationOrder}");
            _output.WriteLine(
                $"frame19 mergedInst=0x{mergedSupport.InstanceId:X8} mergedPoint={mergedSupport.Point} " +
                $"finalInst=0x{finalSupport.InstanceId:X8} finalPoint={finalSupport.Point}");

            Assert.NotEqual(0u, mergedSupport.InstanceId);
            Assert.NotEqual(0u, finalSupport.InstanceId);
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    private SupportQueryScenario LoadSupportQueryScenario(int currentFrameIndex)
    {
        var recording = RecordingTestHelpers.LoadByFilename(Recordings.PacketBackedUndercityElevatorUp, _output);
        RecordingTestHelpers.TryPreloadMap(recording.MapId, _output);

        var currentFrame = recording.Frames[currentFrameIndex];
        var nextFrame = recording.Frames[currentFrameIndex + 1];

        Assert.True(currentFrame.TransportGuid != 0 && currentFrame.TransportGuid == nextFrame.TransportGuid,
            $"Expected frame {currentFrameIndex} to be a continuous on-transport step.");

        var transport = nextFrame.NearbyGameObjects.FirstOrDefault(go => go.Guid == currentFrame.TransportGuid);
        Assert.NotNull(transport);

        var dynamicObjects = nextFrame.NearbyGameObjects
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
                GoState = go.GoState,
            })
            .ToArray();

        var (radius, height) = RecordingTestHelpers.GetCapsuleDimensions(recording, _output);
        var (worldX, worldY, worldZ) = TransportLocalToWorld(currentFrame, transport!);
        float dt = (nextFrame.FrameTimestamp - currentFrame.FrameTimestamp) / 1000.0f;

        return new SupportQueryScenario
        {
            Recording = recording,
            CurrentFrame = currentFrame,
            NextFrame = nextFrame,
            Transport = transport!,
            DynamicObjects = dynamicObjects,
            WorldPosition = new Vector3(worldX, worldY, worldZ),
            Radius = radius,
            Height = height,
            DeltaTime = dt,
            WorldFacing = currentFrame.Facing + transport!.Facing,
            CurrentFrameIndex = currentFrameIndex,
        };
    }

    private TerrainAabbContact[] QueryContacts(uint mapId, (Vector3 boxMin, Vector3 boxMax) bounds)
    {
        var contacts = new TerrainAabbContact[128];
        int count = QueryTerrainAABBContacts(mapId, in bounds.boxMin, in bounds.boxMax, contacts, contacts.Length);
        Assert.True(count > 0, "Expected transport support query to return contacts.");
        return contacts.Take(count).ToArray();
    }

    private static (Vector3 boxMin, Vector3 boxMax) BuildMergedQueryBox(SupportQueryScenario scenario)
    {
        GetPhysicsConstants(out _, out _, out float stepHeight, out _, out float walkableMinNormalZ);

        const float skin = 0.333333f;
        float tan50 = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (walkableMinNormalZ * walkableMinNormalZ))) / walkableMinNormalZ;
        float sqrt2 = MathF.Sqrt(2.0f);
        float speed = scenario.CurrentFrame.CurrentSpeed > 0.0f ? scenario.CurrentFrame.CurrentSpeed : scenario.CurrentFrame.RunSpeed;
        float speedDt = speed * scenario.DeltaTime;
        float slopeLimit = MathF.Max(scenario.Radius * tan50, skin + (1.0f / 720.0f));
        float sweepDist = slopeLimit + speedDt;
        float dirX = MathF.Cos(scenario.WorldFacing);
        float dirY = MathF.Sin(scenario.WorldFacing);

        float endX = scenario.WorldPosition.X + dirX * sweepDist;
        float endY = scenario.WorldPosition.Y + dirY * sweepDist;
        float stepUp = MathF.Min(2.0f * scenario.Radius, speedDt);
        float adjustedMaxZ = scenario.WorldPosition.Z + stepHeight + stepUp;
        float slopeDown = scenario.Radius + speedDt * tan50;
        float adjustedMinZ = adjustedMaxZ - slopeDown - stepHeight;
        float halfDist = speedDt * 0.5f;
        float contracted = skin * sqrt2;
        float halfX = scenario.WorldPosition.X + dirX * halfDist;
        float halfY = scenario.WorldPosition.Y + dirY * halfDist;

        Vector3 startMin = new(scenario.WorldPosition.X - skin, scenario.WorldPosition.Y - skin, adjustedMinZ);
        Vector3 startMax = new(scenario.WorldPosition.X + skin, scenario.WorldPosition.Y + skin, adjustedMaxZ);
        Vector3 endMin = new(endX - skin, endY - skin, adjustedMinZ);
        Vector3 endMax = new(endX + skin, endY + skin, adjustedMaxZ);
        Vector3 halfMin = new(halfX - contracted, halfY - contracted, adjustedMinZ);
        Vector3 halfMax = new(halfX + contracted, halfY + contracted, adjustedMaxZ);

        return (
            new Vector3(
                MathF.Min(startMin.X, MathF.Min(endMin.X, halfMin.X)),
                MathF.Min(startMin.Y, MathF.Min(endMin.Y, halfMin.Y)),
                MathF.Min(startMin.Z, MathF.Min(endMin.Z, halfMin.Z))),
            new Vector3(
                MathF.Max(startMax.X, MathF.Max(endMax.X, halfMax.X)),
                MathF.Max(startMax.Y, MathF.Max(endMax.Y, halfMax.Y)),
                MathF.Max(startMax.Z, MathF.Max(endMax.Z, halfMax.Z))));
    }

    private static (Vector3 boxMin, Vector3 boxMax) BuildFinalSupportQueryBox(SupportQueryScenario scenario)
    {
        GetPhysicsConstants(out _, out _, out float stepHeight, out _, out float walkableMinNormalZ);

        const float skin = 0.333333f;
        float tan50 = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (walkableMinNormalZ * walkableMinNormalZ))) / walkableMinNormalZ;
        float speed = scenario.CurrentFrame.CurrentSpeed > 0.0f ? scenario.CurrentFrame.CurrentSpeed : scenario.CurrentFrame.RunSpeed;
        float speedDt = speed * scenario.DeltaTime;
        float stepUp = MathF.Min(2.0f * scenario.Radius, speedDt);
        float adjustedMaxZ = scenario.WorldPosition.Z + stepHeight + stepUp;
        float slopeDown = scenario.Radius + speedDt * tan50;
        float adjustedMinZ = adjustedMaxZ - slopeDown - stepHeight;

        return (
            new Vector3(scenario.WorldPosition.X - skin, scenario.WorldPosition.Y - skin, adjustedMinZ),
            new Vector3(scenario.WorldPosition.X + skin, scenario.WorldPosition.Y + skin, adjustedMaxZ));
    }

    private static TerrainAabbContact FindElevatorSupportContact(TerrainAabbContact[] contacts, float supportZ)
    {
        var supportContact = contacts
            .Where(c => c.InstanceId != 0 &&
                        c.Normal.Z < -0.99f &&
                        MathF.Abs(c.Point.Z - supportZ) < 0.35f)
            .OrderBy(c => MathF.Abs(c.Point.Z - supportZ))
            .FirstOrDefault();

        Assert.True(supportContact.InstanceId != 0,
            $"Expected a signed downward support contact near z={supportZ:F3} in the contact query.");
        return supportContact;
    }

    private static Vector3 BuildRequestedMove(SupportQueryScenario scenario)
    {
        float speed = scenario.CurrentFrame.CurrentSpeed > 0.0f ? scenario.CurrentFrame.CurrentSpeed : scenario.CurrentFrame.RunSpeed;
        float moveDistance = speed * scenario.DeltaTime;
        return new Vector3(
            MathF.Cos(scenario.CurrentFrame.Facing) * moveDistance,
            MathF.Sin(scenario.CurrentFrame.Facing) * moveDistance,
            0.0f);
    }

    private static (float x, float y, float z) TransportLocalToWorld(RecordedFrame frame, RecordedGameObject transport)
    {
        float cosO = MathF.Cos(transport.Facing);
        float sinO = MathF.Sin(transport.Facing);
        float wx = frame.Position.X * cosO - frame.Position.Y * sinO + transport.Position.X;
        float wy = frame.Position.X * sinO + frame.Position.Y * cosO + transport.Position.Y;
        float wz = frame.Position.Z + transport.Position.Z;
        return (wx, wy, wz);
    }
}
