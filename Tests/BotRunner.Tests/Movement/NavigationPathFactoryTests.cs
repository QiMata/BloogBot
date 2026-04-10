using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using Pathfinding;
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
    public void Create_WithNullPlayer_UsesDefaultMovementCapabilities()
    {
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

    private sealed class CapturingPathfindingClient(Func<bool, Position[]> responseFactory) : PathfindingClient
    {
        private readonly Func<bool, Position[]> _responseFactory = responseFactory;
        public List<bool> SmoothCalls { get; } = [];
        public List<(Race Race, Gender Gender)> CapabilityCalls { get; } = [];
        public IReadOnlyList<DynamicObjectProto>? LastNearbyObjects { get; private set; }

        public override Position[] GetPath(
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
            return _responseFactory(smoothPath);
        }
    }
}
