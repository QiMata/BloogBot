using BotRunner.Combat;
using BotRunner.Constants;
using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Movement;
using BotRunner.Tasks.Travel;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using Pathfinding;

namespace BotRunner.Tests.Travel;

public class TravelTaskTests
{
    [Fact]
    public void Update_GruntBaseDeckLipSlice_EmitsImmediatePlanAndWalkNavBoundaries()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var immediateDiagnostics = new List<string>();

        var playerPosition = new Position(1332.8f, -4633.4f, 24.0f);
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        const uint mapId = 1u;
        const ulong transportGuid = 0UL;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.Setup(p => p.GetFacingForPosition(It.IsAny<Position>())).Returns(0f);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);
        context.Setup(c => c.AddImmediateDiagnostic(It.IsAny<string>()))
            .Callback<string>(immediateDiagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] init enter", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] plan-route enter", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] plan-route exit", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] walk-nav enter leg=0", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] try-enter route=LongTravel", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] player-ready map=1 pos=(1332.8,-4633.4,24.0)", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] navpath-create enter", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] navpath-create exit policy=LongTravel created=True", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] waypoint-query enter map=1", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] walk-nav exit leg=0 nav=True", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("[TRAVEL_PLAN]", StringComparison.Ordinal));
        pathfinding.Verify(
            p => p.GetPathResult(
                mapId,
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()),
            Times.AtLeastOnce);
        objectManager.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Once);
    }

    [Fact]
    public void Update_SameMapLiteralFrezzaSlice_EmitsTravelPlanAndWalkNavDiagnostics()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var immediateDiagnostics = new List<string>();

        var playerPosition = new Position(1332.8f, -4633.4f, 24.0f);
        var literalFrezzaTarget = new Position(1331.1f, -4649.5f, 53.6f);
        const uint mapId = 1u;
        const ulong transportGuid = 0UL;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.Setup(p => p.GetFacingForPosition(It.IsAny<Position>())).Returns(0f);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);
        context.Setup(c => c.AddImmediateDiagnostic(It.IsAny<string>()))
            .Callback<string>(immediateDiagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            mapId,
            literalFrezzaTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] init enter", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] plan-route enter", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] walk-nav enter leg=0", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[NAV_EXEC] try-enter route=LongTravel", StringComparison.Ordinal));
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] walk-nav exit leg=0 nav=True", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("[TRAVEL_PLAN]", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("[TRAVEL_LEG] start index=0 type=Walk", StringComparison.Ordinal));
        objectManager.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Once);
    }

    [Fact]
    public void Update_TargetDirectlyAboveWithinHorizontalRadius_DoesNotCompleteWalkLegAtBase()
    {
        // Regression guard (2026-06-01): a plain (non-transport) walk leg whose
        // End is directly ABOVE the bot — within WalkLegArrivalRadius (15y)
        // horizontally but far below vertically — must NOT complete at the base.
        // OG zeppelin tower: literal Frezza (z=53.6) is ~14.8y 2D from this base
        // spot (z=24) yet ~30y above it. Before the WalkLegVerticalArrivalTolerance
        // gate, TryGetWalkLegArrival returned true here (2D<=15 && CanComplete=true),
        // falsely completing the leg at the base and dumping the bot into the
        // Standard-policy route-exhausted fallback that cannot drive the climb.
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var immediateDiagnostics = new List<string>();

        // 2D distance to Frezza = ~14.8y (<= 15y radius); vertical delta = ~29.6y.
        var playerPosition = new Position(1328.1f, -4635.0f, 24.0f);
        var literalFrezzaTarget = new Position(1331.1f, -4649.5f, 53.6f);
        const uint mapId = 1u;
        const ulong transportGuid = 0UL;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.Setup(p => p.GetFacingForPosition(It.IsAny<Position>())).Returns(0f);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);
        context.Setup(c => c.AddImmediateDiagnostic(It.IsAny<string>()))
            .Callback<string>(immediateDiagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            mapId,
            literalFrezzaTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        // Must NOT falsely arrive/complete at the base.
        Assert.DoesNotContain(diagnostics, message => message.Contains("reason=walk_arrived", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("[TRAVEL_COMPLETE]", StringComparison.Ordinal));
        // The task stays active and keeps navigating upward toward the climb.
        Assert.Same(task, taskStack.Peek());
        Assert.Contains(immediateDiagnostics, message => message.Contains("[TRAVEL_EXEC] walk-nav enter leg=0", StringComparison.Ordinal));
        objectManager.Verify(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()), Times.Once);
    }

    [Fact]
    public void Update_FlightPathLandingWithLingeringMountDisplay_CompletesFlightLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();

        var crossroads = new Position(-437.0f, -2596.0f, 96.0f);
        var orgrimmarFlightMaster = new Position(1677.0f, -4315.0f, 62.0f);
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = crossroads;
        var mapId = 1u;
        var mounted = false;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(() => mounted);

        var flightMaster = new Mock<IWoWUnit>(MockBehavior.Loose);
        flightMaster.SetupGet(u => u.Guid).Returns(0x25UL);
        flightMaster.SetupGet(u => u.Position).Returns(crossroads);
        flightMaster.SetupGet(u => u.Health).Returns(1u);
        flightMaster.SetupGet(u => u.NpcFlags).Returns(NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.Units).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        objectManager.Verify(
            o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()),
            Times.Once);

        playerPosition = orgrimmarFlightMaster;
        mounted = true;

        task.Update();
        task.Update();
        task.Update();

        Assert.Contains(diagnostics, message => message.Contains("[TRAVEL_FLIGHT] activated 25->23", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("flight_arrived", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_LiveOrgrimmarZeppelinApproachPosition_CompletesWalkLegAndStartsTransportLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();

        var crossroads = new Position(-437.0f, -2596.0f, 96.0f);
        var orgrimmarFlightMaster = new Position(1677.0f, -4315.0f, 62.0f);
        // The live Orgrimmar zeppelin handoff should complete at the front
        // boarding zone so the walk leg and boarding logic share the same
        // gangplank-side target.
        var liveApproachPosition = TransportData.ZeppelinUndercityOrgrimmar.Stops[0].NavigationPosition;
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = crossroads;
        var mapId = 1u;
        var mounted = false;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(() => mounted);

        var flightMaster = new Mock<IWoWUnit>(MockBehavior.Loose);
        flightMaster.SetupGet(u => u.Guid).Returns(0x25UL);
        flightMaster.SetupGet(u => u.Position).Returns(crossroads);
        flightMaster.SetupGet(u => u.Health).Returns(1u);
        flightMaster.SetupGet(u => u.NpcFlags).Returns(NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.Units).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        playerPosition = orgrimmarFlightMaster;
        mounted = true;
        task.Update();
        task.Update();
        task.Update();

        playerPosition = liveApproachPosition;
        mounted = false;
        task.Update();
        task.Update();

        Assert.Contains(diagnostics, message => message.Contains("complete index=1 reason=walk_arrived", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("start index=2 type=Zeppelin", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_LiveOrgrimmarZeppelinBoardingPosition_CompletesWalkLegAndStartsTransportLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();

        var crossroads = new Position(-437.0f, -2596.0f, 96.0f);
        var orgrimmarFlightMaster = new Position(1677.0f, -4315.0f, 62.0f);
        var liveBoardingPosition = new Position(1320.142944f, -4653.158691f, 53.891945f);
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = crossroads;
        var mapId = 1u;
        var mounted = false;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(() => mounted);

        var flightMaster = new Mock<IWoWUnit>(MockBehavior.Loose);
        flightMaster.SetupGet(u => u.Guid).Returns(0x25UL);
        flightMaster.SetupGet(u => u.Position).Returns(crossroads);
        flightMaster.SetupGet(u => u.Health).Returns(1u);
        flightMaster.SetupGet(u => u.NpcFlags).Returns(NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.Units).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        playerPosition = orgrimmarFlightMaster;
        mounted = true;
        task.Update();
        task.Update();
        task.Update();

        playerPosition = liveBoardingPosition;
        mounted = false;
        task.Update();
        task.Update();

        Assert.Contains(diagnostics, message => message.Contains("complete index=1 reason=walk_arrived", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("start index=2 type=Zeppelin", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_LowerOrgrimmarZeppelinTowerPosition_DoesNotCompleteWalkLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();

        var crossroads = new Position(-437.0f, -2596.0f, 96.0f);
        var orgrimmarFlightMaster = new Position(1677.0f, -4315.0f, 62.0f);
        var lowerTowerPosition = new Position(1340.9f, -4638.7f, 24.6f);
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = crossroads;
        var mapId = 1u;
        var mounted = false;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(() => mounted);

        var flightMaster = new Mock<IWoWUnit>(MockBehavior.Loose);
        flightMaster.SetupGet(u => u.Guid).Returns(0x25UL);
        flightMaster.SetupGet(u => u.Position).Returns(crossroads);
        flightMaster.SetupGet(u => u.Health).Returns(1u);
        flightMaster.SetupGet(u => u.NpcFlags).Returns(NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.Units).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        playerPosition = orgrimmarFlightMaster;
        mounted = true;
        task.Update();
        task.Update();
        task.Update();

        playerPosition = lowerTowerPosition;
        mounted = false;
        task.Update();
        task.Update();

        Assert.DoesNotContain(diagnostics, message => message.Contains("complete index=1 reason=walk_arrived", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("start index=2 type=Zeppelin", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_OrgrimmarZeppelinPillarPosition_DoesNotCompleteWalkLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();

        var crossroads = new Position(-437.0f, -2596.0f, 96.0f);
        var orgrimmarFlightMaster = new Position(1677.0f, -4315.0f, 62.0f);
        var pillarPosition = new Position(1341.0f, -4650.0f, 50.4f);
        var undercityTarget = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = crossroads;
        var mapId = 1u;
        var mounted = false;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(() => mounted);

        var flightMaster = new Mock<IWoWUnit>(MockBehavior.Loose);
        flightMaster.SetupGet(u => u.Guid).Returns(0x25UL);
        flightMaster.SetupGet(u => u.Position).Returns(crossroads);
        flightMaster.SetupGet(u => u.Health).Returns(1u);
        flightMaster.SetupGet(u => u.NpcFlags).Returns(NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.Units).Returns([flightMaster.Object]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.ActivateFlightAsync(0x25UL, 23u, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            0,
            undercityTarget,
            new TravelOptions
            {
                PlayerFaction = TravelFaction.Horde,
                DiscoveredFlightNodes = [25u, 23u]
            },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        playerPosition = orgrimmarFlightMaster;
        mounted = true;
        task.Update();
        task.Update();
        task.Update();

        playerPosition = pillarPosition;
        mounted = false;
        task.Update();
        task.Update();

        Assert.DoesNotContain(diagnostics, message => message.Contains("complete index=1 reason=walk_arrived", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("start index=2 type=Zeppelin", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_CrossMapZeppelinGuidDropBeforeDestination_KeepsTransportLeg()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var gameObjects = new List<IWoWGameObject>();

        var boardingStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[1];
        var target = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = boardingStop.WaitPosition;
        var mapId = boardingStop.MapId;
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(false);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns(() => gameObjects.Cast<IWoWObject>());
        objectManager.SetupGet(o => o.GameObjects).Returns(() => gameObjects);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            destinationStop.MapId,
            target,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 15f);
        taskStack.Push(task);

        task.Update();

        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            new Position(1318.1f, -4658.0f, 71.9f)).Object);
        task.Update();

        transportGuid = 0xABCUL;
        movementFlags = MovementFlags.MOVEFLAG_ONTRANSPORT;
        playerPosition = boardingStop.TransportBoardingOffset!;
        task.Update();

        gameObjects.Clear();
        mapId = destinationStop.MapId;
        playerPosition = new Position(2995.2f, 1739.2f, -2.1f);
        movementFlags = MovementFlags.MOVEFLAG_NONE;
        transportGuid = 0UL;
        task.Update();

        Assert.Contains(diagnostics, message => message.Contains("start index=0 type=Zeppelin", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("phase=Riding", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("transport_map_changed", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("complete index=0", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, message => message.Contains("start index=1 type=Walk", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_ScheduledZeppelinAtDock_DirectsToConfiguredBoardingPositionBeforeDeckOffset()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var gameObjects = new List<IWoWGameObject>();
        var pathRequests = new List<(Position Start, Position End)>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var boardingStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[1];
        var target = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = boardingStop.WaitPosition;
        var mapId = boardingStop.MapId;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        var movementFlags = MovementFlags.MOVEFLAG_NONE;
        var transportGuid = 0UL;
        player.SetupGet(p => p.MovementFlags).Returns(() => movementFlags);
        player.SetupGet(p => p.TransportGuid).Returns(() => transportGuid);
        player.SetupGet(p => p.IsMounted).Returns(false);
        player.Setup(p => p.GetFacingForPosition(It.IsAny<Position>()))
            .Returns((Position position) => FacingXY(playerPosition, position));

        var moveCalls = new List<(Position Position, float Facing)>();
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns(() => gameObjects.Cast<IWoWObject>());
        objectManager.SetupGet(o => o.GameObjects).Returns(() => gameObjects);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()))
            .Callback<Position, float>((position, facing) => moveCalls.Add((position, facing)));

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
            {
                pathRequests.Add((start, end));
                return CreateSupportedPathResult(start, end);
            });
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            destinationStop.MapId,
            target,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 15f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();

        var stagingPoint = boardingStop.BoardingPosition!;
        var preAttachmentPosition = new Position(
            stagingPoint.X + 3f,
            stagingPoint.Y,
            stagingPoint.Z);
        var transportFacing = -MathF.PI / 2f;
        var transportOrigin = new Position(1318.1f, -4658.0f, 71.9f);
        playerPosition = preAttachmentPosition;
        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            transportOrigin,
            transportFacing).Object);

        now = now.AddSeconds(1);
        task.Update();
        objectManager.Invocations.Clear();
        moveCalls.Clear();

        AdvanceTask(task, ref now, 8);
        Assert.True(
            moveCalls.Count > 0,
            "Expected pre-attachment boarding movement. "
            + $"pathRequests={FormatPathRequests(pathRequests)} "
            + $"trace={FormatTrace(task.GetNavigationTraceSnapshot())} "
            + $"diagnostics={string.Join(" | ", diagnostics)}");
        var firstMove = moveCalls[0];
        Assert.Equal(stagingPoint.X, firstMove.Position.X, 3);
        Assert.Equal(stagingPoint.Y, firstMove.Position.Y, 3);
        Assert.Equal(stagingPoint.Z, firstMove.Position.Z, 3);

        var updatedTransportOrigin = new Position(1319.1f, -4657.5f, 71.9f);
        gameObjects.Clear();
        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            updatedTransportOrigin,
            transportFacing).Object);
        objectManager.Invocations.Clear();
        moveCalls.Clear();

        now = now.AddSeconds(1);
        task.Update();

        Assert.NotEmpty(moveCalls);
        var updatedMove = moveCalls[0];
        Assert.Equal(stagingPoint.X, updatedMove.Position.X, 3);
        Assert.Equal(stagingPoint.Y, updatedMove.Position.Y, 3);
        Assert.Equal(stagingPoint.Z, updatedMove.Position.Z, 3);

        playerPosition = new Position(6.7f, 0.1f, -18.6f);
        transportGuid = 0x164871UL;
        movementFlags = MovementFlags.MOVEFLAG_ONTRANSPORT;
        objectManager.Invocations.Clear();
        moveCalls.Clear();

        now = now.AddSeconds(1);
        task.Update();

        Assert.NotEmpty(moveCalls);
        var deckMove = moveCalls[0];
        Assert.Equal(boardingStop.TransportBoardingOffset!.X, deckMove.Position.X, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Y, deckMove.Position.Y, 3);
        Assert.Equal(boardingStop.TransportBoardingOffset.Z, deckMove.Position.Z, 3);

        playerPosition = boardingStop.TransportBoardingOffset!;
        objectManager.Invocations.Clear();
        moveCalls.Clear();

        now = now.AddSeconds(1);
        task.Update();

        objectManager.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        Assert.Contains(diagnostics, message => message.Contains("phase=Boarding", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("phase=Riding", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_ScheduledZeppelinBoardingFromLiveMissPosition_UsesPathfindingToConfiguredBoardingPoint()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var gameObjects = new List<IWoWGameObject>();
        var pathRequests = new List<(Position Start, Position End)>();
        var moveCalls = new List<(Position Position, float Facing)>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var boardingStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[1];
        var target = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = boardingStop.BoardingPosition!;
        var mapId = boardingStop.MapId;
        var missedLiveBoardingPosition = new Position(1336.7f, -4658.3f, 49.3f);

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_NONE);
        player.SetupGet(p => p.TransportGuid).Returns(0UL);
        player.SetupGet(p => p.IsMounted).Returns(false);
        player.SetupGet(p => p.Race).Returns(Race.Tauren);
        player.SetupGet(p => p.Gender).Returns(Gender.Male);
        player.Setup(p => p.GetFacingForPosition(It.IsAny<Position>()))
            .Returns((Position position) => FacingXY(playerPosition, position));

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns(() => gameObjects.Cast<IWoWObject>());
        objectManager.SetupGet(o => o.GameObjects).Returns(() => gameObjects);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);
        objectManager
            .Setup(o => o.MoveToward(It.IsAny<Position>(), It.IsAny<float>()))
            .Callback<Position, float>((position, facing) => moveCalls.Add((position, facing)));

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
            {
                pathRequests.Add((start, end));
                return CreateSupportedPathResult(start, end);
            });
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            destinationStop.MapId,
            target,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 15f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();
        now = now.AddSeconds(1);
        task.Update();

        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            new Position(1318.1f, -4658.0f, 71.9f)).Object);
        playerPosition = missedLiveBoardingPosition;
        pathRequests.Clear();
        moveCalls.Clear();

        now = now.AddSeconds(1);
        task.Update();

        Assert.Contains(pathRequests, request =>
            DistanceXY(request.Start, missedLiveBoardingPosition) <= 0.1f
            && DistanceXY(request.End, boardingStop.BoardingPosition!) <= 0.1f
            && Math.Abs(request.End.Z - boardingStop.BoardingPosition!.Z) <= 0.1f);
        Assert.NotEmpty(moveCalls);
        Assert.Equal(boardingStop.BoardingPosition!.X, moveCalls[0].Position.X, 3);
        Assert.Equal(boardingStop.BoardingPosition.Y, moveCalls[0].Position.Y, 3);
        Assert.Equal(boardingStop.BoardingPosition.Z, moveCalls[0].Position.Z, 3);
        Assert.DoesNotContain(diagnostics, message =>
            message.Contains("[TRAVEL_TRANSPORT_MISSED_BOARDING]", StringComparison.Ordinal));

        diagnostics.Clear();
        AdvanceTask(task, ref now, 6);

        Assert.Contains(diagnostics, message =>
            message.Contains("[TRAVEL_TRANSPORT_BOARDING_STALL]", StringComparison.Ordinal)
            && message.Contains("replanned=", StringComparison.Ordinal));
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_ScheduledZeppelinLeavesBeforeBoarding_EmitsMissedBoardingDiagnostic()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var gameObjects = new List<IWoWGameObject>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var boardingStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[1];
        var target = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = boardingStop.WaitPosition;
        var mapId = boardingStop.MapId;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_NONE);
        player.SetupGet(p => p.TransportGuid).Returns(0UL);
        player.SetupGet(p => p.IsMounted).Returns(false);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns(() => gameObjects.Cast<IWoWObject>());
        objectManager.SetupGet(o => o.GameObjects).Returns(() => gameObjects);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            destinationStop.MapId,
            target,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 15f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();

        playerPosition = boardingStop.BoardingPosition!;
        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            new Position(1318.1f, -4658.0f, 71.9f)).Object);
        now = now.AddSeconds(1);
        task.Update();
        AdvanceTask(task, ref now, 8);

        gameObjects.Clear();
        objectManager.Invocations.Clear();
        for (var i = 0; i < 6; i++)
        {
            now = now.AddSeconds(2);
            task.Update();
        }

        Assert.Contains(diagnostics, message =>
            message.Contains("[TRAVEL_TRANSPORT_MISSED_BOARDING]", StringComparison.Ordinal)
            && message.Contains("type=Zeppelin", StringComparison.Ordinal));
        objectManager.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_ScheduledZeppelinFallsDuringBoarding_EmitsMissedBoardingDiagnostic()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var gameObjects = new List<IWoWGameObject>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var boardingStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[0];
        var destinationStop = TransportData.ZeppelinUndercityOrgrimmar.Stops[1];
        var target = new Position(1584.0f, 242.0f, -52.0f);
        var playerPosition = boardingStop.WaitPosition;
        var mapId = boardingStop.MapId;

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(() => mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_NONE);
        player.SetupGet(p => p.TransportGuid).Returns(0UL);
        player.SetupGet(p => p.IsMounted).Returns(false);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns(() => gameObjects.Cast<IWoWObject>());
        objectManager.SetupGet(o => o.GameObjects).Returns(() => gameObjects);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool _, Race _, Gender _) =>
                CreateSupportedPathResult(start, end));
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            destinationStop.MapId,
            target,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 15f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();

        playerPosition = boardingStop.BoardingPosition!;
        gameObjects.Add(CreateTransportGameObject(
            0x164871UL,
            TransportData.ZeppelinUndercityOrgrimmar.GameObjectEntry,
            TransportData.ZeppelinUndercityOrgrimmar.DisplayId,
            new Position(1318.1f, -4658.0f, 71.9f)).Object);
        now = now.AddSeconds(1);
        task.Update();
        AdvanceTask(task, ref now, 8);

        playerPosition = new Position(playerPosition.X, playerPosition.Y, playerPosition.Z - 20f);
        objectManager.Invocations.Clear();
        now = now.AddSeconds(1);
        task.Update();

        Assert.Contains(diagnostics, message =>
            message.Contains("[TRAVEL_TRANSPORT_MISSED_BOARDING]", StringComparison.Ordinal)
            && message.Contains("type=Zeppelin", StringComparison.Ordinal));
        objectManager.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_WalkLegNoProgress_ForcesStalledNavigationReplan()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var smoothPathCalls = new List<bool>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var playerPosition = new Position(0f, 0f, 0f);
        const uint mapId = 1u;
        var destination = new Position(100f, 0f, 0f);

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_NONE);
        player.SetupGet(p => p.TransportGuid).Returns(0UL);
        player.SetupGet(p => p.IsMounted).Returns(false);
        player.SetupGet(p => p.Race).Returns(Race.Tauren);
        player.SetupGet(p => p.Gender).Returns(Gender.Male);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([]);
        objectManager.SetupGet(o => o.GameObjects).Returns([]);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position start, Position end, IReadOnlyList<DynamicObjectProto>? _, bool smoothPath, Race _, Gender _) =>
            {
                smoothPathCalls.Add(smoothPath);
                return CreateSupportedPathResult(start, end);
            });
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            mapId,
            destination,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 5f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();
        now = now.AddSeconds(2);
        task.Update();
        now = now.AddSeconds(4);
        task.Update();

        Assert.True(smoothPathCalls.Count >= 2);
        Assert.All(smoothPathCalls, smoothPath => Assert.True(smoothPath));
        Assert.Contains(diagnostics, message =>
            message.Contains("[TRAVEL_WALK_STALL]", StringComparison.Ordinal)
            && message.Contains("replanned=True", StringComparison.Ordinal));
        objectManager.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        Assert.Same(task, taskStack.Peek());
    }

    [Fact]
    public void Update_WalkLegNoProgressWithNoWaypoint_ForcesRecoveryAttempt()
    {
        var taskStack = new Stack<IBotTask>();
        var diagnostics = new List<string>();
        var smoothPathCalls = new List<bool>();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var playerPosition = new Position(0f, 0f, 0f);
        const uint mapId = 1u;
        var destination = new Position(100f, 0f, 0f);

        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(p => p.MapId).Returns(mapId);
        player.SetupGet(p => p.Position).Returns(() => playerPosition);
        player.SetupGet(p => p.MovementFlags).Returns(MovementFlags.MOVEFLAG_NONE);
        player.SetupGet(p => p.TransportGuid).Returns(0UL);
        player.SetupGet(p => p.IsMounted).Returns(false);
        player.SetupGet(p => p.Race).Returns(Race.Tauren);
        player.SetupGet(p => p.Gender).Returns(Gender.Male);

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.Player).Returns(player.Object);
        objectManager.SetupGet(o => o.Objects).Returns([]);
        objectManager.SetupGet(o => o.GameObjects).Returns([]);
        objectManager.SetupGet(o => o.Units).Returns([]);
        objectManager.SetupGet(o => o.IsInFlight).Returns(false);

        var context = new Mock<IBotContext>(MockBehavior.Loose);
        context.SetupGet(c => c.ObjectManager).Returns(objectManager.Object);
        context.SetupGet(c => c.BotTasks).Returns(taskStack);
        context.SetupGet(c => c.Config).Returns(new BotBehaviorConfig());
        context.Setup(c => c.AddDiagnosticMessage(It.IsAny<string>()))
            .Callback<string>(diagnostics.Add);

        var pathfinding = new Mock<PathfindingClient>(MockBehavior.Loose);
        pathfinding
            .Setup(p => p.GetPathResult(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<Race>(),
                It.IsAny<Gender>()))
            .Returns((uint _, Position _, Position _, IReadOnlyList<DynamicObjectProto>? _, bool smoothPath, Race _, Gender _) =>
            {
                smoothPathCalls.Add(smoothPath);
                return PathfindingRouteResult.Empty;
            });
        var container = new Mock<IDependencyContainer>(MockBehavior.Loose);
        container.SetupGet(c => c.PathfindingClient).Returns(pathfinding.Object);
        context.SetupGet(c => c.Container).Returns(container.Object);

        var task = new TravelTask(
            context.Object,
            mapId,
            destination,
            new TravelOptions { PlayerFaction = TravelFaction.Horde },
            arrivalRadius: 5f,
            utcNowProvider: () => now);
        taskStack.Push(task);

        task.Update();
        now = now.AddSeconds(2);
        task.Update();
        now = now.AddSeconds(4);
        task.Update();

        Assert.Contains(smoothPathCalls, smoothPath => smoothPath);
        Assert.Contains(smoothPathCalls, smoothPath => !smoothPath);
        Assert.Contains(diagnostics, message =>
            message.Contains("[TRAVEL_WALK_STALL]", StringComparison.Ordinal)
            && message.Contains("nav=False", StringComparison.Ordinal)
            && message.Contains("replanned=False", StringComparison.Ordinal));
        objectManager.Verify(o => o.StopAllMovement(), Times.AtLeastOnce);
        Assert.Same(task, taskStack.Peek());
    }

    private static void AdvanceTask(TravelTask task, ref DateTime now, double seconds)
    {
        var remaining = seconds;
        while (remaining > 0)
        {
            var step = Math.Min(2.0, remaining);
            now = now.AddSeconds(step);
            task.Update();
            remaining -= step;
        }
    }

    private static string FormatPathRequests(IReadOnlyList<(Position Start, Position End)> pathRequests)
        => pathRequests.Count == 0
            ? "[]"
            : "["
                + string.Join(
                    " ",
                    pathRequests.Select(request =>
                        $"{FormatPosition(request.Start)}->{FormatPosition(request.End)}"))
                + "]";

    private static string FormatTrace(NavigationTraceSnapshot? trace)
        => trace == null
            ? "null"
            : $"reason={trace.LastReplanReason} resolution={trace.LastResolution} idx={trace.CurrentWaypointIndex} "
                + $"active={FormatPosition(trace.ActiveWaypoint)} planned={trace.PlannedWaypoints.Length}";

    private static string FormatPosition(Position? position)
        => position == null
            ? "none"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

    private static PathfindingRouteResult CreateSupportedPathResult(Position start, Position end)
        => new(
            Corners: [start, end],
            Result: "success",
            RawCornerCount: 2,
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

    private static float DistanceXY(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float FacingXY(Position from, Position to)
    {
        var facing = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        return facing < 0f ? facing + (MathF.PI * 2f) : facing;
    }

    private static Mock<IWoWGameObject> CreateTransportGameObject(
        ulong guid,
        uint entry,
        uint displayId,
        Position position,
        float facing = 0f)
    {
        var gameObject = new Mock<IWoWGameObject>();
        gameObject.SetupGet(x => x.Guid).Returns(guid);
        gameObject.SetupGet(x => x.Entry).Returns(entry);
        gameObject.SetupGet(x => x.TypeId).Returns((uint)GameObjectType.MapObjectTransport);
        gameObject.SetupGet(x => x.DisplayId).Returns(displayId);
        gameObject.SetupGet(x => x.Position).Returns(position);
        gameObject.SetupGet(x => x.Facing).Returns(facing);
        gameObject.SetupGet(x => x.ScaleX).Returns(1f);
        gameObject.SetupGet(x => x.GoState).Returns(GOState.Ready);
        return gameObject;
    }
}
