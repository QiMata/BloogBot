using System;
using System.IO;
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

    private sealed class DelegateDisposable(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }

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
        var contacts = QueryFrameContacts(scenario);
        var supportContact = FindElevatorSupportContact(contacts, scenario.WorldPosition.Z);

        _output.WriteLine(
            $"support inst=0x{supportContact.InstanceId:X8} stype={supportContact.SourceType} point={supportContact.Point} normal={supportContact.Normal} raw={supportContact.RawNormal} walk={supportContact.Walkable}");

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
        var contacts = QueryFrameContacts(scenario);
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
    public void PacketBackedUndercityElevatorUp_Frame16_CurrentPositionReorientationFindsOpposingSelectedBlocker()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 native trace idx={trace.SelectedContactIndex} inst=0x{trace.SelectedInstanceId:X8} stype={trace.SelectedSourceType} point={trace.SelectedPoint} " +
            $"normal={trace.SelectedNormal} oriented={trace.OrientedNormal} rawOppose={trace.RawOpposeScore:F4} " +
            $"orientedOppose={trace.OrientedOpposeScore:F4} walk0={trace.WalkableWithoutState} walk1={trace.WalkableWithState} after={trace.GroundedWallStateAfter} " +
            $"iflags=0x{trace.SelectedInstanceFlags:X8} mflags=0x{trace.SelectedModelFlags:X8} resolved=0x{trace.SelectedResolvedModelFlags:X8} src={trace.SelectedMetadataSource} " +
            $"gflags=0x{trace.SelectedGroupFlags:X8} root={trace.SelectedRootId} group={trace.SelectedGroupId} gmatch={trace.SelectedGroupMatchFound}");

        Assert.True(traced,
            "Expected frame-16 grounded wall trace to select a contact from the production Navigation.dll path.");
        Assert.True(trace.QueryContactCount > 0);
        Assert.True(trace.CandidateCount > 0);
        Assert.True(trace.UsedPositionReorientation != 0u,
            "Expected the selected blocker to require current-position normal reorientation on frame 16.");
        Assert.True(trace.RawOpposeScore <= 1e-5f,
            $"Expected the raw selected-contact oppose score to be effectively zero before reorientation, got {trace.RawOpposeScore:F6}.");
        Assert.True(trace.OrientedOpposeScore > 0.9f,
            $"Expected the reoriented selected-contact oppose score to become strongly blocking, got {trace.OrientedOpposeScore:F6}.");
        Assert.True(trace.SelectedNormal.X > 0.99f && MathF.Abs(trace.SelectedNormal.Z) < 1e-3f,
            $"Expected a horizontal +X side contact before reorientation, got {trace.SelectedNormal}.");
        Assert.True(trace.OrientedNormal.X < -0.99f && MathF.Abs(trace.OrientedNormal.Z) < 1e-3f,
            $"Expected the oriented blocker normal to face back into motion, got {trace.OrientedNormal}.");
        Assert.Equal(0u, trace.WalkableWithoutState);
        Assert.Equal(0u, trace.WalkableWithState);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_SelectedContactCurrentlyCollapsesToParentWmoMetadata()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        string legacyScenePath = Path.Combine(GetTempScenesDirectory(scenario.Recording.MapId), $"{scenario.Recording.MapId}.scene");
        using var scenesDir = UseScenesDirectory(scenario.Recording.MapId, Path.GetDirectoryName(legacyScenePath)!);
        CreateLegacyLocalSceneCache(scenario, legacyScenePath);
        UnloadSceneCache(scenario.Recording.MapId);
        Assert.True(LoadSceneCache(scenario.Recording.MapId, legacyScenePath),
            $"Expected to load legacy v1 scene cache from {legacyScenePath}.");

        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 metadata inst=0x{trace.SelectedInstanceId:X8} stype={trace.SelectedSourceType} iflags=0x{trace.SelectedInstanceFlags:X8} " +
            $"mflags=0x{trace.SelectedModelFlags:X8} resolved=0x{trace.SelectedResolvedModelFlags:X8} src={trace.SelectedMetadataSource} " +
            $"gflags=0x{trace.SelectedGroupFlags:X8} root={trace.SelectedRootId} group={trace.SelectedGroupId} gmatch={trace.SelectedGroupMatchFound}");

        Assert.True(traced);
        Assert.NotEqual(0u, trace.SelectedInstanceId);
        Assert.Equal(0x00000004u, trace.SelectedInstanceFlags);
        Assert.Equal(trace.SelectedInstanceFlags, trace.SelectedModelFlags);
        Assert.Equal(0x00000004u, trace.SelectedResolvedModelFlags);
        Assert.Equal(1u, trace.SelectedMetadataSource);
        Assert.Equal(1150, trace.SelectedRootId);
        Assert.Equal(-1, trace.SelectedGroupId);
        Assert.Equal(0u, trace.SelectedGroupFlags);
        Assert.Equal(0u, trace.SelectedGroupMatchFound);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_EnsureMapLoaded_UpgradesLegacySceneCacheToMetadataBearingFormat()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        string tempScenesDir = GetTempScenesDirectory(scenario.Recording.MapId);
        string legacyScenePath = Path.Combine(tempScenesDir, $"{scenario.Recording.MapId}.scene");
        using var scenesDir = UseScenesDirectory(scenario.Recording.MapId, tempScenesDir);
        CreateLegacyLocalSceneCache(scenario, legacyScenePath);
        Assert.Equal(1u, ReadSceneCacheVersion(legacyScenePath));

        UnloadSceneCache(scenario.Recording.MapId);

        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 upgraded-autoload inst=0x{trace.SelectedInstanceId:X8} stype={trace.SelectedSourceType} " +
            $"iflags=0x{trace.SelectedInstanceFlags:X8} mflags=0x{trace.SelectedModelFlags:X8} " +
            $"resolved=0x{trace.SelectedResolvedModelFlags:X8} src={trace.SelectedMetadataSource} " +
            $"gflags=0x{trace.SelectedGroupFlags:X8} root={trace.SelectedRootId} group={trace.SelectedGroupId} gmatch={trace.SelectedGroupMatchFound} " +
            $"version={ReadSceneCacheVersion(legacyScenePath)}");

        Assert.True(traced);
        Assert.Equal(2u, ReadSceneCacheVersion(legacyScenePath));
        Assert.Equal(0u, trace.SelectedSourceType);
        Assert.Equal(0x00000004u, trace.SelectedInstanceFlags);
        Assert.Equal(0x00000004u, trace.SelectedModelFlags);
        Assert.Equal(0x00000004u, trace.SelectedResolvedModelFlags);
        Assert.Equal(2u, trace.SelectedMetadataSource);
        Assert.Equal(0x0000AA05u, trace.SelectedGroupFlags);
        Assert.Equal(1150, trace.SelectedRootId);
        Assert.Equal(3228, trace.SelectedGroupId);
        Assert.Equal(1u, trace.SelectedGroupMatchFound);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_NativeTraceCapturesHorizontalResolverBranchTransaction()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 branch={trace.BranchKind} merged={trace.MergedWallNormal} finalWall={trace.FinalWallNormal} " +
            $"horizontal={trace.HorizontalProjectedMove} branchMove={trace.BranchProjectedMove} finalMove={trace.FinalProjectedMove} " +
            $"blockedFraction={trace.BlockedFraction:F6}");

        Assert.True(traced);
        Assert.Equal(1u, trace.GroundedWallStateBefore);
        Assert.Equal((uint)GroundedWallBranchKind.Horizontal, trace.BranchKind);
        Assert.Equal(0u, trace.UsedWalkableSelectedContact);
        Assert.Equal(0u, trace.UsedNonWalkableVertical);
        Assert.True(trace.MergedWallNormal.X < -0.99f && MathF.Abs(trace.MergedWallNormal.Z) < 1e-3f,
            $"Expected the merged blocker normal to face back into motion, got {trace.MergedWallNormal}.");
        Assert.True(MathF.Abs(trace.BranchProjectedMove.Z) <= 1e-6f,
            $"Expected the horizontal branch to stay flat on frame 16, got branchMove={trace.BranchProjectedMove}.");
        Assert.True(MathF.Abs(trace.FinalProjectedMove.Z) <= 1e-6f,
            $"Expected the final resolved move to stay flat on frame 16, got finalMove={trace.FinalProjectedMove}.");
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_SelectedContactThresholdGateReportsProjectedPrismDecision()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 thresholdGate normalZ={trace.SelectedThresholdNormalZ:F6} thresholdPoint={trace.SelectedThresholdPoint} " +
            $"insideCurrent={trace.SelectedCurrentPositionInsidePrism} insideProjected={trace.SelectedProjectedPositionInsidePrism} " +
            $"sensitiveStd={trace.SelectedThresholdSensitiveStandard} sensitiveRelaxed={trace.SelectedThresholdSensitiveRelaxed} " +
            $"directStd={trace.SelectedWouldUseDirectPairStandard} directRelaxed={trace.SelectedWouldUseDirectPairRelaxed}");

        Assert.True(traced);
        Assert.True(MathF.Abs(trace.SelectedThresholdNormalZ) <= 1e-3f,
            $"Expected the frame-16 selected wall to stay effectively horizontal at the `0x633760` gate, got normalZ={trace.SelectedThresholdNormalZ:F6}.");
        Assert.Equal(1u, trace.SelectedThresholdSensitiveStandard);
        Assert.Equal(1u, trace.SelectedThresholdSensitiveRelaxed);
        Assert.Equal(0u, trace.SelectedCurrentPositionInsidePrism);
        Assert.Equal(0u, trace.SelectedProjectedPositionInsidePrism);
        Assert.Equal(0u, trace.SelectedWouldUseDirectPairStandard);
        Assert.Equal(0u, trace.SelectedWouldUseDirectPairRelaxed);
        Assert.Equal(trace.SelectedWouldUseDirectPairStandard, trace.SelectedWouldUseDirectPairRelaxed);
        Assert.Equal(worldPosition.X + requestedMove.X, trace.SelectedThresholdPoint.X, 4);
        Assert.Equal(worldPosition.Y + requestedMove.Y, trace.SelectedThresholdPoint.Y, 4);
        Assert.Equal(worldPosition.Z + requestedMove.Z, trace.SelectedThresholdPoint.Z, 4);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_MergedQueryContainsNoDirectPairCandidates()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        var contacts = QueryFrameContacts(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var projectedPosition = scenario.WorldPosition + requestedMove;

        var standardDirectCandidates = contacts
            .Where(contact =>
            {
                bool directPair = EvaluateWoWSelectedContactThresholdGate(
                    contact.ToTriangle(),
                    contact.Normal,
                    scenario.WorldPosition,
                    projectedPosition,
                    useStandardWalkableThreshold: true,
                    out _,
                    out _,
                    out _,
                    out _);
                return directPair;
            })
            .ToArray();

        var relaxedDirectCandidates = contacts
            .Where(contact =>
            {
                bool directPair = EvaluateWoWSelectedContactThresholdGate(
                    contact.ToTriangle(),
                    contact.Normal,
                    scenario.WorldPosition,
                    projectedPosition,
                    useStandardWalkableThreshold: false,
                    out _,
                    out _,
                    out _,
                    out _);
                return directPair;
            })
            .ToArray();

        _output.WriteLine($"frame16 directPair candidates: standard={standardDirectCandidates.Length} relaxed={relaxedDirectCandidates.Length}");
        Assert.NotEmpty(contacts);
        Assert.Empty(standardDirectCandidates);
        Assert.Empty(relaxedDirectCandidates);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame16_FreshSceneExtract_ReportsSelectedContactMetadata()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadFrameScenario(16);
        using var freshScene = UseFreshExtractedLocalSceneCache(scenario);
        string tempScenePath = GetTempLocalSceneCachePath(scenario.Recording.MapId);
        UnloadSceneCache(scenario.Recording.MapId);
        Assert.True(LoadSceneCache(scenario.Recording.MapId, tempScenePath),
            $"Expected to reload fresh scene cache from {tempScenePath}.");

        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        var requestedMove = BuildRequestedMove(scenario);
        var worldPosition = scenario.WorldPosition;

        bool traced = EvaluateGroundedWallSelection(
            scenario.Recording.MapId,
            in boxMin,
            in boxMax,
            in worldPosition,
            in requestedMove,
            scenario.Radius,
            scenario.Height,
            groundedWallFlagBefore: true,
            out GroundedWallSelectionTrace trace);

        _output.WriteLine(
            $"frame16 fresh-extract inst=0x{trace.SelectedInstanceId:X8} stype={trace.SelectedSourceType} " +
            $"iflags=0x{trace.SelectedInstanceFlags:X8} mflags=0x{trace.SelectedModelFlags:X8} " +
            $"resolved=0x{trace.SelectedResolvedModelFlags:X8} src={trace.SelectedMetadataSource} " +
            $"gflags=0x{trace.SelectedGroupFlags:X8} root={trace.SelectedRootId} group={trace.SelectedGroupId} gmatch={trace.SelectedGroupMatchFound}");

        Assert.True(traced);
        Assert.Equal(0u, trace.SelectedSourceType);
        Assert.Equal(0x00000004u, trace.SelectedInstanceFlags);
        Assert.Equal(0x00000004u, trace.SelectedModelFlags);
        Assert.Equal(0x00000004u, trace.SelectedResolvedModelFlags);
        Assert.Equal(2u, trace.SelectedMetadataSource);
        Assert.Equal(0x0000AA05u, trace.SelectedGroupFlags);
        Assert.Equal(1150, trace.SelectedRootId);
        Assert.Equal(3228, trace.SelectedGroupId);
        Assert.Equal(1u, trace.SelectedGroupMatchFound);
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

    private TerrainAabbContact[] QueryFrameContacts(Frame15Scenario scenario)
    {
        ClearAllDynamicObjects();
        try
        {
            foreach (var go in scenario.DynamicObjects)
            {
                RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation, go.GoState);
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

    private static Vector3 BuildRequestedMove(Frame15Scenario scenario)
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

    private IDisposable UseFreshExtractedLocalSceneCache(Frame15Scenario scenario)
    {
        string dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR") ?? string.Empty;
        string? originalScenePath = string.IsNullOrEmpty(dataDir)
            ? null
            : Path.Combine(dataDir, "scenes", $"{scenario.Recording.MapId}.scene");
        bool restoreOriginalScene = !string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath);

        string tempScenePath = GetTempLocalSceneCachePath(scenario.Recording.MapId);
        ExtractLocalSceneCache(scenario, tempScenePath);

        return new DelegateDisposable(() =>
        {
            UnloadSceneCache(scenario.Recording.MapId);
            if (restoreOriginalScene && originalScenePath is not null)
            {
                Assert.True(LoadSceneCache(scenario.Recording.MapId, originalScenePath),
                    $"Expected to restore scene cache from {originalScenePath}.");
            }
        });
    }

    private static string GetTempLocalSceneCachePath(uint mapId)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "wwow-scene-tests");
        return Path.Combine(tempDir, $"map{mapId}_undercity_local.scene");
    }

    private static string GetTempScenesDirectory(uint mapId)
    {
        return Path.Combine(Path.GetTempPath(), "wwow-scene-tests", $"map{mapId}_scenes");
    }

    private IDisposable UseScenesDirectory(uint mapId, string scenesDir)
    {
        string normalizedTempDir = EnsureTrailingDirectorySeparator(scenesDir);
        string dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR") ?? string.Empty;
        string originalScenesDir = string.IsNullOrEmpty(dataDir)
            ? EnsureTrailingDirectorySeparator("scenes")
            : EnsureTrailingDirectorySeparator(Path.Combine(dataDir, "scenes"));

        Directory.CreateDirectory(scenesDir);
        SetScenesDir(normalizedTempDir);

        return new DelegateDisposable(() =>
        {
            SetScenesDir(originalScenesDir);
            UnloadSceneCache(mapId);
        });
    }

    private void ExtractLocalSceneCache(Frame15Scenario scenario, string outPath)
    {
        var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
        const float extractPad = 30.0f;

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        UnloadSceneCache(scenario.Recording.MapId);
        bool extracted = ExtractSceneCache(
            scenario.Recording.MapId,
            outPath,
            boxMin.X - extractPad,
            boxMin.Y - extractPad,
            boxMax.X + extractPad,
            boxMax.Y + extractPad);

        Assert.True(extracted,
            $"Expected local scene extraction to succeed for map {scenario.Recording.MapId}.");
    }

    private void CreateLegacyLocalSceneCache(Frame15Scenario scenario, string outPath)
    {
        ExtractLocalSceneCache(scenario, outPath);
        WriteLegacySceneCacheVersion1(outPath);
    }

    private static void WriteLegacySceneCacheVersion1(string path)
    {
        const int headerSize = 64;
        const int sceneTriSize = 44;
        const int sceneTriMetadataSize = 28;
        const int liquidCellSize = 12;

        byte[] allBytes = File.ReadAllBytes(path);
        using var input = new MemoryStream(allBytes, writable: false);
        using var reader = new BinaryReader(input);

        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        reader.ReadUInt32(); // mapId
        uint triCount = reader.ReadUInt32();
        reader.ReadSingle(); // cellSize
        uint cellsX = reader.ReadUInt32();
        uint cellsY = reader.ReadUInt32();
        uint triIdxCount = reader.ReadUInt32();
        reader.ReadSingle(); // liquidCellSize
        uint liquidCellsX = reader.ReadUInt32();
        uint liquidCellsY = reader.ReadUInt32();

        Assert.Equal(0x454E4353u, magic);
        Assert.Equal(2u, version);

        int triangleBytes = checked((int)triCount * sceneTriSize);
        int triangleMetadataBytes = checked((int)triCount * sceneTriMetadataSize);
        int cellTotal = checked((int)(cellsX * cellsY));
        int spatialBytes = checked((cellTotal * sizeof(uint) * 2) + ((int)triIdxCount * sizeof(uint)));
        int liquidBytes = checked((2 * sizeof(float)) + ((int)(liquidCellsX * liquidCellsY) * liquidCellSize));
        int spatialOffset = headerSize + triangleBytes + triangleMetadataBytes;
        int expectedLength = checked(spatialOffset + spatialBytes + liquidBytes);

        Assert.Equal(expectedLength, allBytes.Length);

        byte[] header = allBytes[..headerSize].ToArray();
        BitConverter.GetBytes(1u).CopyTo(header, sizeof(uint));

        using var output = new MemoryStream(expectedLength - triangleMetadataBytes);
        using var writer = new BinaryWriter(output);
        writer.Write(header);
        writer.Write(allBytes, headerSize, triangleBytes);
        writer.Write(allBytes, spatialOffset, spatialBytes + liquidBytes);
        File.WriteAllBytes(path, output.ToArray());
    }

    private static uint ReadSceneCacheVersion(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        reader.ReadUInt32(); // magic
        return reader.ReadUInt32();
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
