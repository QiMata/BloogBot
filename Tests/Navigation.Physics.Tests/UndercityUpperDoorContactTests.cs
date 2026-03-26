using System;
using System.Linq;
using Navigation.Physics.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class UndercityUpperDoorContactTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    private sealed class Frame15Scenario
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
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame15_QueryIncludesSignedElevatorFloorContact()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrame15Scenario();
        var contacts = QueryFrame15Contacts(scenario);
        var supportContact = FindElevatorSupportContact(contacts, scenario.WorldPosition.Z);

        _output.WriteLine(
            $"support inst=0x{supportContact.InstanceId:X8} point={supportContact.Point} normal={supportContact.Normal} raw={supportContact.RawNormal} walk={supportContact.Walkable}");

        Assert.Equal(0u, supportContact.Walkable);
        Assert.True(supportContact.Normal.Z < -0.99f,
            $"Expected the signed support face to point down, got normal {supportContact.Normal}.");
        Assert.True(MathF.Abs(supportContact.Point.Z - scenario.WorldPosition.Z) < 0.35f,
            $"Expected the support face to sit at the transport deck height {scenario.WorldPosition.Z:F3}, got {supportContact.Point.Z:F3}.");
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame15_ElevatorFloorUsesStatefulCheckWalkablePath()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrame15Scenario();
        var contacts = QueryFrame15Contacts(scenario);
        var supportContact = FindElevatorSupportContact(contacts, scenario.WorldPosition.Z);
        var triangle = supportContact.ToTriangle();
        var contactNormal = supportContact.Normal;
        var worldPosition = scenario.WorldPosition;

        bool walkableWithoutState = EvaluateWoWCheckWalkable(
            in triangle,
            in contactNormal,
            in worldPosition,
            scenario.Radius,
            scenario.Height,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: false,
            out bool walkableStateWithoutState,
            out bool groundedWallFlagAfterWithoutState);

        bool walkableWithState = EvaluateWoWCheckWalkable(
            in triangle,
            in contactNormal,
            in worldPosition,
            scenario.Radius,
            scenario.Height,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: true,
            out bool walkableStateWithState,
            out bool groundedWallFlagAfterWithState);

        _output.WriteLine(
            $"support state=false => walk={walkableWithoutState} state={walkableStateWithoutState} after={groundedWallFlagAfterWithoutState}");
        _output.WriteLine(
            $"support state=true  => walk={walkableWithState} state={walkableStateWithState} after={groundedWallFlagAfterWithState}");

        Assert.False(walkableWithoutState);
        Assert.False(walkableStateWithoutState);
        Assert.False(groundedWallFlagAfterWithoutState);

        Assert.True(walkableWithState);
        Assert.False(walkableStateWithState);
        Assert.True(groundedWallFlagAfterWithState);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_CurrentPositionReorientationFindsOpposingStatefulBlocker()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        var contacts = QueryFrame15Contacts(scenario);
        var candidate = contacts
            .Select(contact => AnalyzeContactSelection(contact, scenario))
            .Where(info =>
                info.HorizontalMagnitude > 0.99f &&
                !info.WalkableWithoutState &&
                info.WalkableWithState &&
                info.RawOpposeScore <= 1e-5f &&
                info.ReorientedOpposeScore > 0.9f)
            .OrderBy(info => MathF.Abs(info.Contact.Point.Z - scenario.WorldPosition.Z))
            .FirstOrDefault();

        Assert.True(candidate is not null,
            "Expected frame-16 merged query to contain a statefully walkable horizontal blocker that only becomes opposing after orienting the contact normal against the current collision position.");

        _output.WriteLine(
            $"frame16 reoriented blocker inst=0x{candidate!.Contact.InstanceId:X8} point={candidate.Contact.Point} normal={candidate.Contact.Normal} " +
            $"rawOppose={candidate.RawOpposeScore:F4} reorientedOppose={candidate.ReorientedOpposeScore:F4} " +
            $"walk0={candidate.WalkableWithoutState} walk1={candidate.WalkableWithState}");
    }

    private Frame15Scenario LoadFrame15Scenario()
    {
        return LoadFrameScenario(15);
    }

    private Frame15Scenario LoadFrameScenario(int frameIndex)
    {
        var recording = RecordingTestHelpers.LoadByFilename(Recordings.PacketBackedUndercityElevatorUp, _output);
        RecordingTestHelpers.TryPreloadMap(recording.MapId, _output);

        var currentFrame = recording.Frames[frameIndex];
        var nextFrame = recording.Frames[frameIndex + 1];

        Assert.True(currentFrame.TransportGuid != 0 && currentFrame.TransportGuid == nextFrame.TransportGuid,
            $"Expected frame {frameIndex} to be a continuous on-transport step.");

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

        return new Frame15Scenario
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
        };
    }

    private TerrainAabbContact[] QueryFrame15Contacts(Frame15Scenario scenario)
    {
        ClearAllDynamicObjects();
        try
        {
            foreach (var go in scenario.DynamicObjects)
            {
                RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation);
            }

            var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
            var contacts = new TerrainAabbContact[128];
            int count = QueryTerrainAABBContacts(scenario.Recording.MapId, in boxMin, in boxMax, contacts, contacts.Length);
            Assert.True(count > 0, "Expected frame-15 merged query to return terrain contacts.");

            _output.WriteLine($"queryMin={boxMin} queryMax={boxMax} count={count}");
            return contacts.Take(count).ToArray();
        }
        finally
        {
            ClearAllDynamicObjects();
        }
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
            $"Expected a signed downward support contact near z={supportZ:F3} in the merged frame-15 query.");
        return supportContact;
    }

    private static ContactSelectionInfo AnalyzeContactSelection(TerrainAabbContact contact, Frame15Scenario scenario)
    {
        var moveDir = new Vector3(
            MathF.Cos(scenario.CurrentFrame.Facing),
            MathF.Sin(scenario.CurrentFrame.Facing),
            0.0f);
        var triangle = contact.ToTriangle();
        var contactNormal = contact.Normal;
        var worldPosition = scenario.WorldPosition;

        float rawOppose = ComputeOpposeScore(contact.Normal, moveDir);

        var reorientedNormal = contact.Normal;
        var toCurrentPosition = new Vector3(
            scenario.WorldPosition.X - contact.Point.X,
            scenario.WorldPosition.Y - contact.Point.Y,
            0.0f);
        if (toCurrentPosition.LengthSquared() > 1e-6f)
        {
            var horizontal = new Vector3(reorientedNormal.X, reorientedNormal.Y, 0.0f);
            if (Vector3.Dot(horizontal, toCurrentPosition) < 0.0f)
                reorientedNormal = new Vector3(-reorientedNormal.X, -reorientedNormal.Y, -reorientedNormal.Z);
        }

        bool walkableWithoutState = EvaluateWoWCheckWalkable(
            in triangle,
            in contactNormal,
            in worldPosition,
            scenario.Radius,
            scenario.Height,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: false,
            out _,
            out _);

        bool walkableWithState = EvaluateWoWCheckWalkable(
            in triangle,
            in contactNormal,
            in worldPosition,
            scenario.Radius,
            scenario.Height,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: true,
            out _,
            out _);

        return new ContactSelectionInfo
        {
            Contact = contact,
            HorizontalMagnitude = MathF.Sqrt((contact.Normal.X * contact.Normal.X) + (contact.Normal.Y * contact.Normal.Y)),
            RawOpposeScore = rawOppose,
            ReorientedOpposeScore = ComputeOpposeScore(reorientedNormal, moveDir),
            WalkableWithoutState = walkableWithoutState,
            WalkableWithState = walkableWithState,
        };
    }

    private static float ComputeOpposeScore(Vector3 normal, Vector3 moveDir)
    {
        var horizontal = new Vector3(normal.X, normal.Y, 0.0f);
        float horizontalMag = horizontal.Length();
        if (horizontalMag <= 1e-6f)
            return 0.0f;

        horizontal = new Vector3(horizontal.X / horizontalMag, horizontal.Y / horizontalMag, 0.0f);
        return MathF.Max(0.0f, -Vector3.Dot(moveDir, horizontal));
    }

    private static (Vector3 boxMin, Vector3 boxMax) BuildMergedQueryBox(Frame15Scenario scenario)
    {
        GetPhysicsConstants(out _, out _, out float stepHeight, out _, out float walkableMinNormalZ);

        const float skin = 0.333333f;
        float tan50 = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (walkableMinNormalZ * walkableMinNormalZ))) / walkableMinNormalZ;
        float sqrt2 = MathF.Sqrt(2.0f);
        float speed = scenario.CurrentFrame.CurrentSpeed > 0.0f ? scenario.CurrentFrame.CurrentSpeed : scenario.CurrentFrame.RunSpeed;
        float speedDt = speed * scenario.DeltaTime;
        float slopeLimit = MathF.Max(scenario.Radius * tan50, skin + (1.0f / 720.0f));
        float sweepDist = slopeLimit + speedDt;
        float dirX = MathF.Cos(scenario.CurrentFrame.Facing);
        float dirY = MathF.Sin(scenario.CurrentFrame.Facing);

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

    private static (float x, float y, float z) TransportLocalToWorld(RecordedFrame frame, RecordedGameObject transport)
    {
        float cosO = MathF.Cos(transport.Facing);
        float sinO = MathF.Sin(transport.Facing);
        float wx = frame.Position.X * cosO - frame.Position.Y * sinO + transport.Position.X;
        float wy = frame.Position.X * sinO + frame.Position.Y * cosO + transport.Position.Y;
        float wz = frame.Position.Z + transport.Position.Z;
        return (wx, wy, wz);
    }

    private sealed class ContactSelectionInfo
    {
        public required TerrainAabbContact Contact { get; init; }
        public required float HorizontalMagnitude { get; init; }
        public required float RawOpposeScore { get; init; }
        public required float ReorientedOpposeScore { get; init; }
        public required bool WalkableWithoutState { get; init; }
        public required bool WalkableWithState { get; init; }
    }
}
