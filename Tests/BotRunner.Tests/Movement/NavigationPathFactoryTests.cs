using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using Pathfinding;
using System;
using System.Collections.Generic;

namespace BotRunner.Tests.Movement;

public class NavigationPathFactoryTests
{
    [Fact]
    public void Create_StandardPolicy_ThreadsPlayerCapabilitiesAndSmoothPreference()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(x => x.Race).Returns(Race.Troll);
        player.SetupGet(x => x.Gender).Returns(Gender.Female);

        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);

        var pathfinding = new CapturingPathfindingClient(_ => [new Position(5f, 0f, 0f), new Position(10f, 0f, 0f)]);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.Standard);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.NotEmpty(pathfinding.SmoothCalls);
        Assert.True(pathfinding.SmoothCalls[0]);
        Assert.Equal(Race.Troll, pathfinding.CapabilityCalls[0].Race);
        Assert.Equal(Gender.Female, pathfinding.CapabilityCalls[0].Gender);
    }

    [Fact]
    public void Create_CorpseRunPolicy_DisablesProbeHeuristicsAndUsesUnsmoothPaths()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(x => x.Race).Returns(Race.Orc);
        player.SetupGet(x => x.Gender).Returns(Gender.Male);

        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);

        var pathfinding = new CapturingPathfindingClient(_ => [new Position(5f, 0f, 0f), new Position(10f, 0f, 0f)]);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.CorpseRun);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.NotEmpty(pathfinding.SmoothCalls);
        Assert.False(pathfinding.SmoothCalls[0]);
    }

    [Fact]
    public void Create_LongTravelPolicy_UsesSmoothServicePathsWithoutProbeSkipping()
    {
        var settings = NavigationRoutePolicySettings.Resolve(NavigationRoutePolicy.LongTravel);

        Assert.False(settings.EnableProbeHeuristics);
        Assert.False(settings.EnableDynamicProbeSkipping);
        Assert.False(settings.StrictPathValidation);
        Assert.True(settings.RequireVerticalWaypointArrival);
        Assert.True(settings.PreferSmoothPath);
        Assert.False(settings.AllowAlternatePathMode);
        Assert.True(settings.ValidateLocalPhysicsSegments);
    }

    [Fact]
    public void Create_LongTravelPolicy_StopsMovementBeforePathCalculation()
    {
        var stopCount = 0;
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);
        objectManager
            .Setup(x => x.StopAllMovement())
            .Callback(() => stopCount++);

        var pathfinding = new CapturingPathfindingClient(_ =>
        {
            Assert.Equal(1, stopCount);
            return [new Position(5f, 0f, 0f), new Position(10f, 0f, 0f)];
        });
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        objectManager.Verify(x => x.StopAllMovement(), Times.Once);
    }

    [Fact]
    public void Create_LongTravelPolicy_KeepsSmoothDetourModeDuringRecovery()
    {
        var stuckGeneration = 0;
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(() => stuckGeneration);

        var smoothDetour = new[]
        {
            new Position(40f, 0f, 10f),
            new Position(60f, 0f, 5f),
        };
        var unsmoothedShortcut = new[]
        {
            new Position(60f, 0f, 5f),
        };

        var pathfinding = new CapturingPathfindingClient(smooth => smooth ? smoothDetour : unsmoothedShortcut);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 10f);
        var destination = new Position(60f, 0f, 5f);

        var initialWaypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var initialTrace = navPath.TraceSnapshot;

        stuckGeneration = 1;
        var recoveryWaypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var recoveryTrace = navPath.TraceSnapshot;

        Assert.NotNull(initialWaypoint);
        Assert.NotNull(recoveryWaypoint);
        Assert.Equal(NavigationTraceReason.MovementStuckRecovery, recoveryTrace.LastReplanReason);
        Assert.True(initialTrace.SmoothPath);
        Assert.True(recoveryTrace.SmoothPath);
        Assert.False(initialTrace.RouteDecision.AlternateEvaluated);
        Assert.False(recoveryTrace.RouteDecision.AlternateEvaluated);
        Assert.False(recoveryTrace.RouteDecision.AlternateSelected);
        Assert.Equal(SegmentAffordance.Drop, recoveryTrace.RouteDecision.MaxAffordance);
        Assert.Equal([true, true], pathfinding.SmoothCalls);
    }

    [Fact]
    public void Create_LongTravelPolicy_KeepsSmoothDetourModeDuringWallRecovery()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);

        var smoothDetour = new[]
        {
            new Position(40f, 0f, 10f),
            new Position(60f, 0f, 5f),
        };
        var unsmoothedShortcut = new[]
        {
            new Position(60f, 0f, 5f),
        };

        var pathfinding = new CapturingPathfindingClient(smooth => smooth ? smoothDetour : unsmoothedShortcut);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 10f);
        var destination = new Position(60f, 0f, 5f);

        navPath.CalculatePath(start, destination, mapId: 1, force: true, reason: NavigationTraceReason.WallStuck);
        var trace = navPath.TraceSnapshot;

        Assert.True(trace.SmoothPath);
        Assert.False(trace.RouteDecision.AlternateEvaluated);
        Assert.False(trace.RouteDecision.AlternateSelected);
        Assert.Equal(NavigationTraceReason.WallStuck, trace.LastReplanReason);
        Assert.Equal([true], pathfinding.SmoothCalls);
    }

    [Fact]
    public void Create_LongTravelPolicy_KeepsSmoothPathModeDuringDynamicOverlayReplan()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);

        var smoothRoute = new[]
        {
            new Position(10f, 0f, 0f),
            new Position(20f, 0f, 0f),
        };
        var rawCorridor = new[]
        {
            new Position(4f, 4f, 0f),
            new Position(8f, -4f, 0f),
            new Position(20f, 0f, 0f),
        };

        var pathfinding = new CapturingPathfindingClient(smooth => smooth ? smoothRoute : rawCorridor);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 0f);
        var destination = new Position(20f, 0f, 0f);

        navPath.CalculatePath(start, destination, mapId: 1, force: true, reason: NavigationTraceReason.DynamicBlockerObserved);
        var waypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.True(trace.SmoothPath);
        Assert.False(trace.RouteDecision.AlternateEvaluated);
        Assert.False(trace.RouteDecision.AlternateSelected);
        Assert.Equal(NavigationTraceReason.DynamicBlockerObserved, trace.LastReplanReason);
        Assert.Equal([true], pathfinding.SmoothCalls);
    }

    [Fact]
    public void Create_LongTravelPolicy_EvaluatesAlternateOnVerticalLayerMismatch()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);

        var smoothWrongLayer = new[]
        {
            new Position(10f, 0f, 0f),
            new Position(20f, 0f, -4f),
        };
        var alternateSameLayer = new[]
        {
            new Position(10f, 8f, 0f),
            new Position(20f, 0f, 0f),
        };

        var pathfinding = new CapturingPathfindingClient(smooth => smooth ? smoothWrongLayer : alternateSameLayer);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 0f);
        var destination = new Position(20f, 0f, 0f);

        navPath.CalculatePath(start, destination, mapId: 1, force: true, reason: NavigationTraceReason.VerticalLayerMismatch);
        var waypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.True(trace.RouteDecision.IsSupported);
        Assert.Equal(SegmentAffordance.Walk, trace.RouteDecision.MaxAffordance);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.Contains(pathfinding.SmoothCalls, smooth => !smooth);
    }

    [Fact]
    public void Create_LongTravelPolicy_EvaluatesAlternateWhenFirstLegLeavesCurrentLayer()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);

        var smoothWrongLayer = new[]
        {
            new Position(20f, 0f, 3.5f),
            new Position(40f, 0f, 8f),
        };
        var alternateSameLayer = new[]
        {
            new Position(10f, 10f, 9.5f),
            new Position(25f, 10f, 9f),
            new Position(40f, 0f, 8f),
        };

        var pathfinding = new CapturingPathfindingClient(smooth => smooth ? smoothWrongLayer : alternateSameLayer);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 10f);
        var destination = new Position(40f, 0f, 8f);

        navPath.CalculatePath(start, destination, mapId: 1, force: true, reason: NavigationTraceReason.VerticalLayerMismatch);
        var waypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.Equal(10f, waypoint.Y);
        Assert.True(trace.RouteDecision.IsSupported);
        Assert.Equal(SegmentAffordance.Walk, trace.RouteDecision.MaxAffordance);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.Contains(pathfinding.SmoothCalls, smooth => !smooth);
    }

    [Fact]
    public void Create_LongTravelPolicy_RejectsServiceStaticBlockedRecoveryPathAndUsesAlternate()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(0);

        var blockedCorridor = new[]
        {
            new Position(10f, 0f, 0f),
            new Position(20f, 0f, 0f),
        };
        var alternateCorridor = new[]
        {
            new Position(0f, 8f, 0f),
            new Position(20f, 8f, 0f),
        };

        var pathfinding = new CapturingPathfindingClient(
            smooth => smooth ? blockedCorridor : alternateCorridor,
            smooth => CapturingPathfindingClient.CreateRouteResult(
                smooth ? blockedCorridor : alternateCorridor,
                blockedSegmentIndex: smooth ? 0 : null,
                blockedReason: smooth ? "static_los" : "none"));
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);
        var start = new Position(0f, 0f, 0f);
        var destination = new Position(20f, 8f, 0f);

        navPath.CalculatePath(start, destination, mapId: 1, force: true, reason: NavigationTraceReason.MovementStuckRecovery);
        var waypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(0f, waypoint!.X);
        Assert.Equal(8f, waypoint.Y);
        Assert.False(trace.SmoothPath);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.Equal([true, false], pathfinding.SmoothCalls);
    }

    [Fact]
    public void Create_WithNullPlayer_UsesDefaultMovementCapabilities()
    {
        using var env = new CharacterEnvironmentScope();
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);

        var pathfinding = new CapturingPathfindingClient(_ => [new Position(5f, 0f, 0f), new Position(10f, 0f, 0f)]);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.Standard);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.NotEmpty(pathfinding.CapabilityCalls);
        Assert.Equal(0, (int)pathfinding.CapabilityCalls[0].Race);
        Assert.Equal(0, (int)pathfinding.CapabilityCalls[0].Gender);
    }

    [Fact]
    public void Create_WithUnhydratedPlayer_UsesConfiguredEnvironmentCapabilities()
    {
        using var env = new CharacterEnvironmentScope("Tauren", "Male");
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(x => x.Race).Returns(Race.None);
        player.SetupGet(x => x.Gender).Returns(Gender.None);

        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);

        var pathfinding = new CapturingPathfindingClient(_ => [new Position(5f, 0f, 0f), new Position(10f, 0f, 0f)]);
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.LongTravel);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.NotEmpty(pathfinding.CapabilityCalls);
        Assert.Equal(Race.Tauren, pathfinding.CapabilityCalls[0].Race);
        Assert.Equal(Gender.Male, pathfinding.CapabilityCalls[0].Gender);
        Assert.Equal(Race.Tauren, navPath.TraceSnapshot.Race);
        Assert.Equal(Gender.Male, navPath.TraceSnapshot.Gender);
        Assert.InRange(navPath.TraceSnapshot.CapsuleRadius, 0.974f, 1.1f);
        Assert.InRange(navPath.TraceSnapshot.CapsuleHeight, 2.62f, 2.8f);
    }

    [Fact]
    public void Create_WithoutExplicitStuckProvider_UsesObjectManagerGenerationForRepath()
    {
        using var env = new CharacterEnvironmentScope();
        var stuckGeneration = 0;
        var objectManager = new Mock<IObjectManager>();
        objectManager.SetupGet(x => x.Player).Returns((IWoWLocalPlayer?)null!);
        objectManager.SetupGet(x => x.GameObjects).Returns([]);
        objectManager.SetupGet(x => x.MovementStuckRecoveryGeneration).Returns(() => stuckGeneration);

        var callCount = 0;
        var pathfinding = new CapturingPathfindingClient(_ =>
        {
            callCount++;
            return callCount == 1
                ? [new Position(10f, 0f, 0f)]
                : [new Position(0f, 10f, 0f)];
        });
        var navPath = NavigationPathFactory.Create(pathfinding, objectManager.Object, NavigationRoutePolicy.Standard);
        var destination = new Position(20f, 20f, 0f);

        var firstWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        stuckGeneration = 1;

        var replannedWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(firstWaypoint);
        Assert.NotNull(replannedWaypoint);
        Assert.Equal(2, callCount);
        Assert.Equal(10f, firstWaypoint!.X);
        Assert.Equal(0f, firstWaypoint.Y);
        Assert.Equal(0f, replannedWaypoint!.X);
        Assert.Equal(10f, replannedWaypoint.Y);
        Assert.Equal(NavigationTraceReason.MovementStuckRecovery, navPath.TraceSnapshot.LastReplanReason);
    }

    private sealed class CapturingPathfindingClient(
        Func<bool, Position[]> responseFactory,
        Func<bool, PathfindingRouteResult>? routeResultFactory = null) : PathfindingClient
    {
        private readonly Func<bool, Position[]> _responseFactory = responseFactory;
        private readonly Func<bool, PathfindingRouteResult>? _routeResultFactory = routeResultFactory;
        public List<bool> SmoothCalls { get; } = [];
        public List<(Race Race, Gender Gender)> CapabilityCalls { get; } = [];
        public IReadOnlyList<DynamicObjectProto>? LastNearbyObjects { get; private set; }

        public static PathfindingRouteResult CreateRouteResult(
            Position[] corners,
            int? blockedSegmentIndex = null,
            string blockedReason = "none")
            => new(
                Corners: corners,
                Result: corners.Length > 0 ? "native_path" : "no_path",
                RawCornerCount: (uint)corners.Length,
                BlockedSegmentIndex: blockedSegmentIndex,
                BlockedReason: blockedReason,
                MaxAffordance: PathSegmentAffordance.Walk,
                PathSupported: corners.Length > 0 && blockedSegmentIndex is null,
                StepUpCount: 0,
                DropCount: 0,
                CliffCount: 0,
                VerticalCount: 0,
                TotalZGain: 0f,
                TotalZLoss: 0f,
                MaxSlopeAngleDeg: 0f,
                JumpGapCount: 0,
                SafeDropCount: 0,
                UnsafeDropCount: 0,
                BlockedCount: blockedSegmentIndex.HasValue ? 1u : 0u,
                MaxClimbHeight: 0f,
                MaxGapDistance: 0f,
                MaxDropHeight: 0f);

        public override PathfindingRouteResult GetPathResult(
            uint mapId,
            Position start,
            Position end,
            IReadOnlyList<DynamicObjectProto>? nearbyObjects,
            bool smoothPath = false,
            Race race = 0,
            Gender gender = 0)
        {
            SmoothCalls.Add(smoothPath);
            CapabilityCalls.Add((race, gender));
            LastNearbyObjects = nearbyObjects;
            if (_routeResultFactory is not null)
                return _routeResultFactory(smoothPath);

            return new PathfindingRouteResult(
                Corners: _responseFactory(smoothPath),
                Result: "ok",
                RawCornerCount: 0,
                BlockedSegmentIndex: null,
                BlockedReason: "none",
                MaxAffordance: PathSegmentAffordance.Walk,
                PathSupported: true,
                StepUpCount: 0,
                DropCount: 0,
                CliffCount: 0,
                VerticalCount: 0,
                TotalZGain: 0f,
                TotalZLoss: 0f,
                MaxSlopeAngleDeg: 0f,
                JumpGapCount: 0,
                SafeDropCount: 0,
                UnsafeDropCount: 0,
                BlockedCount: 0,
                MaxClimbHeight: 0f,
                MaxGapDistance: 0f,
                MaxDropHeight: 0f);
        }

        public override LocalSegmentSimulationResult SimulateLocalSegment(
            uint mapId,
            Position from,
            Position to,
            Race race = 0,
            Gender gender = 0,
            float maxDistance = 12.0f,
            float deltaTime = 0.05f)
            => LocalSegmentSimulationResult.CompatibleResult(to);

        public override (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(
            uint mapId,
            Position position,
            float searchRadius = 4.0f)
            => (true, position);

        public override (uint areaType, Position nearestPoint) FindNearestWalkablePoint(
            uint mapId,
            Position position,
            float searchRadius = 8.0f)
            => (1, position);
    }

    private sealed class CharacterEnvironmentScope : IDisposable
    {
        private readonly string? _previousRace = Environment.GetEnvironmentVariable("WWOW_CHARACTER_RACE");
        private readonly string? _previousGender = Environment.GetEnvironmentVariable("WWOW_CHARACTER_GENDER");

        public CharacterEnvironmentScope(string? race = null, string? gender = null)
        {
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_RACE", race);
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_GENDER", gender);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_RACE", _previousRace);
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_GENDER", _previousGender);
        }
    }
}
