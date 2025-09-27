using BotRunner;
using BotRunner.Clients;
using GameData.Core.Models;

namespace BotRunner.Tests
{
    public class BotRunnerServiceTests
    {
        [Fact]
        public void ResolveNextWaypoint_ReturnsNull_WhenNoWaypoints()
        {
            var pathfindingClient = new TestPathfindingClient(Array.Empty<Position>());
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, Origin, Origin), logMessages.Add);

            Assert.Null(result);
            Assert.Contains(logMessages, message => message.Contains("no waypoints", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ResolveNextWaypoint_UsesFirstWaypoint_WhenOnlyOneExists()
        {
            var waypoint = new Position(1, 2, 3);
            var pathfindingClient = new TestPathfindingClient(new[] { waypoint });
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, Origin, waypoint), logMessages.Add);

            Assert.Same(waypoint, result);
            Assert.Contains(logMessages, message => message.Contains("single waypoint", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ResolveNextWaypoint_UsesSecondWaypoint_WhenMultipleExist()
        {
            var waypoint0 = new Position(0, 0, 0);
            var waypoint1 = new Position(1, 1, 1);
            var pathfindingClient = new TestPathfindingClient(new[] { waypoint0, waypoint1 });
            var logMessages = new List<string>();

            var result = BotRunnerService.ResolveNextWaypoint(pathfindingClient.GetPath(0, waypoint0, waypoint1), logMessages.Add);

            Assert.Same(waypoint1, result);
            Assert.Empty(logMessages);
        }

        private static Position Origin { get; } = new(0, 0, 0);

        private sealed class TestPathfindingClient(Position[] path) : PathfindingClient
        {
            private readonly Position[] _path = path;

            public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false) => _path;
        }
    }
}
