using BotRunner;
using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Movement;

public class GoToArrivalTests
{
    [Fact]
    public void HasReachedGoToTarget_WhenWithinHorizontalToleranceDespiteZMismatch_ReturnsTrue()
    {
        var current = new Position(-461.793f, -4761.793f, 32.411f);
        var target = new Position(-460f, -4760f, 38f);

        var reached = BotRunner.SequenceBuilders.MovementSequenceBuilder.HasReachedGoToTarget(current, target, tolerance: 3f);

        Assert.True(reached);
    }

    [Fact]
    public void HasReachedGoToTarget_WhenOutsideHorizontalTolerance_ReturnsFalse()
    {
        var current = new Position(-464.5f, -4764.5f, 32.411f);
        var target = new Position(-460f, -4760f, 38f);

        var reached = BotRunner.SequenceBuilders.MovementSequenceBuilder.HasReachedGoToTarget(current, target, tolerance: 3f);

        Assert.False(reached);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotRecalculateExhaustedPath_WhenOnlyDestinationZDiffers()
    {
        var pathfindingCalls = 0;
        var pathfinding = new Mock<PathfindingClient>();
        pathfinding
            .Setup(client => client.GetPath(
                It.IsAny<uint>(),
                It.IsAny<Position>(),
                It.IsAny<Position>(),
                It.IsAny<IReadOnlyList<Pathfinding.DynamicObjectProto>?>(),
                It.IsAny<bool>(),
                It.IsAny<GameData.Core.Enums.Race>(),
                It.IsAny<GameData.Core.Enums.Gender>()))
            .Returns<uint, Position, Position, IReadOnlyList<Pathfinding.DynamicObjectProto>?, bool, GameData.Core.Enums.Race, GameData.Core.Enums.Gender>((_, _, destination, _, _, _, _) =>
            {
                pathfindingCalls++;
                return [destination];
            });
        pathfinding
            .Setup(client => client.IsInLineOfSight(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>()))
            .Returns(true);

        var navPath = new NavigationPath(pathfinding.Object, () => 0, enableProbeHeuristics: false);
        var destination = new Position(10f, 0f, 38f);

        var firstWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 32f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        var secondWaypoint = navPath.GetNextWaypoint(
            new Position(10f, 0f, 32f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(firstWaypoint);
        Assert.Null(secondWaypoint);
        Assert.Equal(1, pathfindingCalls);
    }
}
