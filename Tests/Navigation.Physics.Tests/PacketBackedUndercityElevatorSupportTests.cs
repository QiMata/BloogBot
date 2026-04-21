using System;
using System.Linq;
using System.Runtime.InteropServices;
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
            .Where(frame => frame.IsOnTransport && frame.Frame >= 6 && frame.Frame <= 20)
            .OrderBy(frame => frame.Frame)
            .ToList();

        Assert.NotEmpty(transportWindow);

        foreach (var frame in transportWindow)
        {
            _output.WriteLine(
                $"frame={frame.Frame} sim=({frame.SimX:F3},{frame.SimY:F3},{frame.SimZ:F3}) " +
                $"rec=({frame.RecX:F3},{frame.RecY:F3},{frame.RecZ:F3}) err={frame.PosError:F3} " +
                $"support={frame.EngineStandingOnInstanceId} local=({frame.EngineStandingOnLocalX:F3}," +
                $"{frame.EngineStandingOnLocalY:F3},{frame.EngineStandingOnLocalZ:F3}) gw={frame.EngineGroundedWallState}");
        }

        var frame19 = transportWindow.FirstOrDefault(frame => frame.Frame == 19) ?? transportWindow.Last();
        Assert.Equal(19, frame19.Frame);
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 10);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        ClearAllDynamicObjects();
        try
        {
            int transportIndex = Array.FindIndex(scenario.DynamicObjects, go => go.Guid == scenario.Transport.Guid);
            var finalSupportContacts = QueryContacts(scenario.Recording.MapId, BuildFinalSupportQueryBox(scenario));
            var mergedContacts = QueryContacts(scenario.Recording.MapId, BuildMergedQueryBox(scenario));
            var finalSupport = FindElevatorSupportContact(finalSupportContacts, scenario.WorldPosition.Z);
            var dynamicFinalContacts = finalSupportContacts
                .Where(contact => contact.InstanceId != 0)
                .Select(contact => $"0x{contact.InstanceId:X8}@z={contact.Point.Z:F3}/nz={contact.Normal.Z:F3}/walk={contact.Walkable}")
                .ToArray();
            var dynamicMergedContacts = mergedContacts
                .Where(contact => contact.InstanceId != 0)
                .Select(contact => $"0x{contact.InstanceId:X8}@z={contact.Point.Z:F3}/nz={contact.Normal.Z:F3}/walk={contact.Walkable}")
                .ToArray();

            var finalTriangle = finalSupport.ToTriangle();
            var finalNormal = finalSupport.Normal;
            var worldPosition = scenario.WorldPosition;
            bool walkableWithoutState = EvaluateWoWCheckWalkable(
                in finalTriangle,
                in finalNormal,
                in worldPosition,
                scenario.Radius,
                scenario.Height,
                useStandardWalkableThreshold: true,
                groundedWallFlagBefore: false,
                out bool walkableStateWithoutState,
                out bool groundedWallAfterWithoutState);
            bool walkableWithState = EvaluateWoWCheckWalkable(
                in finalTriangle,
                in finalNormal,
                in worldPosition,
                scenario.Radius,
                scenario.Height,
                useStandardWalkableThreshold: true,
                groundedWallFlagBefore: true,
                out bool walkableStateWithState,
                out bool groundedWallAfterWithState);

            var (boxMin, boxMax) = BuildMergedQueryBox(scenario);
            var requestedMove = BuildRequestedMove(scenario);
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
            var output = StepScenarioFrame(scenario, scenario.DynamicObjects, cleanedMoveFlags);
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} transportGuid={scenario.Transport.Guid} transportIndex={transportIndex} " +
                $"freshProcessTransportInst~0x{(0x80000001u + (uint)Math.Max(transportIndex, 0)):X8}");
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} finalDynamic=[{string.Join(", ", dynamicFinalContacts)}] " +
                $"mergedDynamic=[{string.Join(", ", dynamicMergedContacts)}]");
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} finalSupport inst=0x{finalSupport.InstanceId:X8} point={finalSupport.Point} " +
                $"walk0={walkableWithoutState} state0={walkableStateWithoutState} after0={groundedWallAfterWithoutState} " +
                $"walk1={walkableWithState} state1={walkableStateWithState} after1={groundedWallAfterWithState}");
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} gw0 traced={tracedWithoutState} branch={traceWithoutState.BranchKind} " +
                $"selectedInst=0x{traceWithoutState.SelectedInstanceId:X8} selectedPoint={traceWithoutState.SelectedPoint} " +
                $"selectedNormal={traceWithoutState.SelectedNormal} walk0={traceWithoutState.WalkableWithoutState} " +
                $"walk1={traceWithoutState.WalkableWithState} threshNz={traceWithoutState.SelectedThresholdNormalZ:F4} " +
                $"directStd={traceWithoutState.SelectedWouldUseDirectPairStandard} directRelax={traceWithoutState.SelectedWouldUseDirectPairRelaxed} " +
                $"finalMove={traceWithoutState.FinalProjectedMove} blocked={traceWithoutState.BlockedFraction:F4}");
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} gw1 traced={tracedWithState} branch={traceWithState.BranchKind} " +
                $"selectedInst=0x{traceWithState.SelectedInstanceId:X8} selectedPoint={traceWithState.SelectedPoint} " +
                $"selectedNormal={traceWithState.SelectedNormal} walk0={traceWithState.WalkableWithoutState} " +
                $"walk1={traceWithState.WalkableWithState} threshNz={traceWithState.SelectedThresholdNormalZ:F4} " +
                $"directStd={traceWithState.SelectedWouldUseDirectPairStandard} directRelax={traceWithState.SelectedWouldUseDirectPairRelaxed} " +
                $"finalMove={traceWithState.FinalProjectedMove} blocked={traceWithState.BlockedFraction:F4}");
            _output.WriteLine(
                $"frame={scenario.CurrentFrameIndex} world={scenario.WorldPosition} " +
                $"support={output.StandingOnInstanceId} local=({output.StandingOnLocalX:F3}," +
                $"{output.StandingOnLocalY:F3},{output.StandingOnLocalZ:F3}) outZ={output.Z:F3}");

            Assert.NotEqual(0u, finalSupport.InstanceId);
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_LowerTransportFrame_CurrentVsNextTransportRegistrationComparison()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 10);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;
        var currentObjects = BuildDynamicObjects(scenario.CurrentFrame);
        var nextObjects = BuildDynamicObjects(scenario.NextFrame);

        var currentOutput = StepScenarioFrame(scenario, currentObjects, cleanedMoveFlags);
        var nextOutput = StepScenarioFrame(scenario, nextObjects, cleanedMoveFlags);

        _output.WriteLine(
            $"frame={scenario.CurrentFrameIndex} currentDyn out=({currentOutput.X:F4}, {currentOutput.Y:F4}, {currentOutput.Z:F4}) " +
            $"support=0x{currentOutput.StandingOnInstanceId:X8} gw={currentOutput.GroundedWallState} wall={currentOutput.HitWall}");
        _output.WriteLine(
            $"frame={scenario.CurrentFrameIndex} nextDyn out=({nextOutput.X:F4}, {nextOutput.Y:F4}, {nextOutput.Z:F4}) " +
            $"support=0x{nextOutput.StandingOnInstanceId:X8} gw={nextOutput.GroundedWallState} wall={nextOutput.HitWall}");
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_LowerTransportFrame_ForcedGroundedWallState_LogsFullRuntimeDelta()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 10);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        var baselineOutput = StepScenarioFrame(scenario, scenario.DynamicObjects, cleanedMoveFlags);
        var forcedStateOutput = StepScenarioFrame(
            scenario,
            scenario.DynamicObjects,
            cleanedMoveFlags,
            groundedWallState: 1);

        _output.WriteLine(
            $"frame10 baseline out=({baselineOutput.X:F4}, {baselineOutput.Y:F4}, {baselineOutput.Z:F4}) " +
            $"support=0x{baselineOutput.StandingOnInstanceId:X8} gw={baselineOutput.GroundedWallState} wall={baselineOutput.HitWall}");
        _output.WriteLine(
            $"frame10 forcedGw out=({forcedStateOutput.X:F4}, {forcedStateOutput.Y:F4}, {forcedStateOutput.Z:F4}) " +
            $"support=0x{forcedStateOutput.StandingOnInstanceId:X8} gw={forcedStateOutput.GroundedWallState} wall={forcedStateOutput.HitWall}");

        Assert.True(float.IsFinite(baselineOutput.Z));
        Assert.True(float.IsFinite(forcedStateOutput.Z));
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame9_ForcedGroundedWallState_LogsFullRuntimeDelta()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 9);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        var baselineOutput = StepScenarioFrame(scenario, scenario.DynamicObjects, cleanedMoveFlags);
        var forcedStateOutput = StepScenarioFrame(
            scenario,
            scenario.DynamicObjects,
            cleanedMoveFlags,
            groundedWallState: 1);

        _output.WriteLine(
            $"frame9 baseline out=({baselineOutput.X:F4}, {baselineOutput.Y:F4}, {baselineOutput.Z:F4}) " +
            $"support=0x{baselineOutput.StandingOnInstanceId:X8} gw={baselineOutput.GroundedWallState} wall={baselineOutput.HitWall}");
        _output.WriteLine(
            $"frame9 forcedGw out=({forcedStateOutput.X:F4}, {forcedStateOutput.Y:F4}, {forcedStateOutput.Z:F4}) " +
            $"support=0x{forcedStateOutput.StandingOnInstanceId:X8} gw={forcedStateOutput.GroundedWallState} wall={forcedStateOutput.HitWall}");

        Assert.True(float.IsFinite(baselineOutput.Z));
        Assert.True(float.IsFinite(forcedStateOutput.Z));
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
    public void PacketBackedUndercityElevatorUp_Frame19_ForcedGroundedWallState_LogsFullRuntimeDelta()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 19);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

        var baselineOutput = StepScenarioFrame(scenario, scenario.DynamicObjects, cleanedMoveFlags);
        var forcedStateOutput = StepScenarioFrame(
            scenario,
            scenario.DynamicObjects,
            cleanedMoveFlags,
            groundedWallState: 1);

        _output.WriteLine(
            $"frame19 baseline out=({baselineOutput.X:F4}, {baselineOutput.Y:F4}, {baselineOutput.Z:F4}) " +
            $"support=0x{baselineOutput.StandingOnInstanceId:X8} gw={baselineOutput.GroundedWallState} wall={baselineOutput.HitWall}");
        _output.WriteLine(
            $"frame19 forcedGw out=({forcedStateOutput.X:F4}, {forcedStateOutput.Y:F4}, {forcedStateOutput.Z:F4}) " +
            $"support=0x{forcedStateOutput.StandingOnInstanceId:X8} gw={forcedStateOutput.GroundedWallState} wall={forcedStateOutput.HitWall}");

        Assert.True(float.IsFinite(baselineOutput.Z));
        Assert.True(float.IsFinite(forcedStateOutput.Z));
    }

    [Fact]
    public void PacketBackedUndercityElevatorUp_Frame20_UpperArrivalTraceShowsRemainingXYDriftSource()
    {
        if (!_fixture.IsInitialized)
            return;

        var scenario = LoadSupportQueryScenario(currentFrameIndex: 20, requireContinuousTransport: false);
        var cleanedMoveFlags = scenario.CurrentFrame.MovementFlags &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.TeleportToPlane &
            ~Navigation.Physics.Tests.Helpers.MoveFlags.SplineElevation;

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
            var finalSupport = FindElevatorSupportContact(finalSupportContacts, scenario.WorldPosition.Z);

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

            var baselineOutput = StepScenarioFrame(scenario, scenario.DynamicObjects, cleanedMoveFlags);
            var forcedStateOutput = StepScenarioFrame(
                scenario,
                scenario.DynamicObjects,
                cleanedMoveFlags,
                groundedWallState: 1);

            bool currentHasTransportGo = scenario.CurrentFrame.NearbyGameObjects.Any(go => go.Guid == scenario.CurrentFrame.TransportGuid);
            bool nextHasTransportGo = scenario.NextFrame.NearbyGameObjects.Any(go => go.Guid == scenario.CurrentFrame.TransportGuid);
            _output.WriteLine(
                $"frame20 raw currentTransportGuid={scenario.CurrentFrame.TransportGuid} nextTransportGuid={scenario.NextFrame.TransportGuid} " +
                $"currentGoCount={scenario.CurrentFrame.NearbyGameObjects.Count} nextGoCount={scenario.NextFrame.NearbyGameObjects.Count} " +
                $"currentHasTransportGo={currentHasTransportGo} nextHasTransportGo={nextHasTransportGo}");
            _output.WriteLine(
                $"frame20 mergedSupport inst=0x{mergedSupport.InstanceId:X8} point={mergedSupport.Point} normal={mergedSupport.Normal}");
            _output.WriteLine(
                $"frame20 finalSupport inst=0x{finalSupport.InstanceId:X8} point={finalSupport.Point} normal={finalSupport.Normal}");
            _output.WriteLine(
                $"frame20 gw0 traced={tracedWithoutState} branch={traceWithoutState.BranchKind} " +
                $"before={traceWithoutState.GroundedWallStateBefore} after={traceWithoutState.GroundedWallStateAfter} " +
                $"selectedInst=0x{traceWithoutState.SelectedInstanceId:X8} selectedPoint={traceWithoutState.SelectedPoint} " +
                $"selectedNormal={traceWithoutState.SelectedNormal} walk0={traceWithoutState.WalkableWithoutState} " +
                $"walk1={traceWithoutState.WalkableWithState} finalMove={traceWithoutState.FinalProjectedMove} " +
                $"blocked={traceWithoutState.BlockedFraction:F4}");
            _output.WriteLine(
                $"frame20 gw1 traced={tracedWithState} branch={traceWithState.BranchKind} " +
                $"before={traceWithState.GroundedWallStateBefore} after={traceWithState.GroundedWallStateAfter} " +
                $"selectedInst=0x{traceWithState.SelectedInstanceId:X8} selectedPoint={traceWithState.SelectedPoint} " +
                $"selectedNormal={traceWithState.SelectedNormal} walk0={traceWithState.WalkableWithoutState} " +
                $"walk1={traceWithState.WalkableWithState} finalMove={traceWithState.FinalProjectedMove} " +
                $"blocked={traceWithState.BlockedFraction:F4}");
            _output.WriteLine(
                $"frame20 baseline out=({baselineOutput.X:F4}, {baselineOutput.Y:F4}, {baselineOutput.Z:F4}) " +
                $"support=0x{baselineOutput.StandingOnInstanceId:X8} gw={baselineOutput.GroundedWallState} wall={baselineOutput.HitWall}");
            _output.WriteLine(
                $"frame20 forcedGw out=({forcedStateOutput.X:F4}, {forcedStateOutput.Y:F4}, {forcedStateOutput.Z:F4}) " +
                $"support=0x{forcedStateOutput.StandingOnInstanceId:X8} gw={forcedStateOutput.GroundedWallState} wall={forcedStateOutput.HitWall}");

            Assert.True(tracedWithoutState, "Expected the frame-20 grounded-wall trace to resolve a contact set.");
            Assert.True(tracedWithState, "Expected the stateful frame-20 grounded-wall trace to resolve a contact set.");
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

    private SupportQueryScenario LoadSupportQueryScenario(int currentFrameIndex, bool requireContinuousTransport = true)
    {
        var recording = RecordingTestHelpers.LoadByFilename(Recordings.PacketBackedUndercityElevatorUp, _output);
        RecordingTestHelpers.TryPreloadMap(recording.MapId, _output);

        var currentFrame = recording.Frames[currentFrameIndex];
        var nextFrame = recording.Frames[currentFrameIndex + 1];

        if (requireContinuousTransport)
        {
            Assert.True(currentFrame.TransportGuid != 0 && currentFrame.TransportGuid == nextFrame.TransportGuid,
                $"Expected frame {currentFrameIndex} to be a continuous on-transport step.");
        }
        else
        {
            Assert.True(currentFrame.TransportGuid != 0,
                $"Expected frame {currentFrameIndex} to be an on-transport step.");
        }

        var transportSourceFrame = currentFrame.NearbyGameObjects.Any(go => go.Guid == currentFrame.TransportGuid)
            ? currentFrame
            : nextFrame;
        var transport = transportSourceFrame.NearbyGameObjects.FirstOrDefault(go => go.Guid == currentFrame.TransportGuid);
        Assert.NotNull(transport);

        var dynamicObjects = transportSourceFrame.NearbyGameObjects
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

    private static DynamicObjectInfo[] BuildDynamicObjects(RecordedFrame frame)
    {
        if (frame.NearbyGameObjects == null || frame.NearbyGameObjects.Count == 0)
            return [];

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
                GoState = go.GoState,
            })
            .ToArray();
    }

    private PhysicsOutput StepScenarioFrame(
        SupportQueryScenario scenario,
        DynamicObjectInfo[] dynamicObjects,
        uint cleanedMoveFlags,
        uint groundedWallState = 0)
    {
        ClearAllDynamicObjects();
        GCHandle dynHandle = default;
        try
        {
            if (dynamicObjects.Length > 0)
            {
                dynHandle = GCHandle.Alloc(dynamicObjects, GCHandleType.Pinned);
                foreach (var go in dynamicObjects)
                {
                    RegisterDynamicObject(go.Guid, 0, go.DisplayId, scenario.Recording.MapId, go.Scale);
                    UpdateDynamicObjectPosition(go.Guid, go.X, go.Y, go.Z, go.Orientation, go.GoState);
                }
            }

            var input = new PhysicsInput
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
                PrevGroundZ = scenario.WorldPosition.Z,
                PrevGroundNx = 0,
                PrevGroundNy = 0,
                PrevGroundNz = 1,
                NearbyObjects = dynHandle.IsAllocated ? dynHandle.AddrOfPinnedObject() : IntPtr.Zero,
                NearbyObjectCount = dynamicObjects.Length,
                MapId = scenario.Recording.MapId,
                DeltaTime = scenario.DeltaTime,
                FrameCounter = (uint)scenario.CurrentFrameIndex,
                GroundedWallState = groundedWallState,
            };

            return StepPhysicsV2(ref input);
        }
        finally
        {
            if (dynHandle.IsAllocated)
                dynHandle.Free();
            ClearAllDynamicObjects();
        }
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
