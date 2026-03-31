using BotRunner.Clients;
using BotRunner.Interfaces;
using BotRunner.Tasks.Dungeoneering;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using BotRunner.Constants;
using Pathfinding;

namespace BotRunner.Tests.Combat;

/// <summary>
/// Unit tests for DungeoneeringTask — the coordinated dungeon crawling task.
/// Verifies: leader navigation, hostile pulling, follower behavior, wrong-map guard.
/// </summary>
public class DungeoneeringTaskTests : IDisposable
{
    private readonly Mock<IBotContext> _ctx;
    private readonly Mock<IObjectManager> _om;
    private readonly Mock<IWoWLocalPlayer> _player;
    private readonly Mock<IDependencyContainer> _container;
    private readonly Mock<IClassContainer> _classContainer;
    private readonly Mock<PathfindingClient> _pathfinding;
    private readonly Stack<IBotTask> _botTasks;

    private static readonly Position[] RfcWaypoints =
    [
        new(3f, -11f, -16f),
        new(-23f, -61f, -21f),
        new(-70f, -33f, -18f),
    ];

    public DungeoneeringTaskTests()
    {
        _ctx = new Mock<IBotContext>();
        _om = new Mock<IObjectManager>();
        _player = new Mock<IWoWLocalPlayer>();
        _container = new Mock<IDependencyContainer>();
        _classContainer = new Mock<IClassContainer>();
        _pathfinding = new Mock<PathfindingClient>();
        _botTasks = new Stack<IBotTask>();

        _pathfinding
            .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
            .Returns((uint mapId, Position start, Position end, bool smooth) =>
                new Position[] { start, end });

        _ctx.Setup(c => c.ObjectManager).Returns(_om.Object);
        _ctx.Setup(c => c.Config).Returns(new BotBehaviorConfig());
        _ctx.Setup(c => c.BotTasks).Returns(_botTasks);
        _ctx.Setup(c => c.Container).Returns(_container.Object);
        _container.Setup(c => c.PathfindingClient).Returns(_pathfinding.Object);
        _container.Setup(c => c.ClassContainer).Returns(_classContainer.Object);

        _player.Setup(p => p.Position).Returns(new Position(3f, -11f, -16f));
        _player.Setup(p => p.MapId).Returns(389u);
        _player.Setup(p => p.HealthPercent).Returns(100);
        _om.Setup(o => o.Player).Returns(_player.Object);
        _om.Setup(o => o.Hostiles).Returns(Enumerable.Empty<IWoWUnit>());
        _om.Setup(o => o.Aggressors).Returns(Enumerable.Empty<IWoWUnit>());
        _om.Setup(o => o.PartyMembers).Returns(Enumerable.Empty<IWoWPlayer>());
        _om.Setup(o => o.Units).Returns(Enumerable.Empty<IWoWUnit>());
    }

    public void Dispose() { }

    // =========================================================================
    // Leader behavior
    // =========================================================================

    [Fact]
    public void Leader_StartsInNavigateState()
    {
        var task = new DungeoneeringTask(_ctx.Object, isLeader: true, RfcWaypoints, targetMapId: 389);
        Assert.True(task.IsLeader);
    }

    [Fact]
    public void Leader_NavigatesToNextWaypoint_WhenNoHostiles()
    {
        // Place player at waypoint 0
        _player.Setup(p => p.Position).Returns(new Position(3f, -11f, -16f));

        var task = new DungeoneeringTask(_ctx.Object, isLeader: true, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        // First update should try to navigate (no hostiles, no aggressors)
        task.Update();

        // Verify navigation was attempted (StopAllMovement not called since we're moving)
        // The task should still be on the stack (not popped)
        Assert.Single(_botTasks);
    }

    [Fact]
    public void Leader_PushesPvERotation_WhenAggressorsExist()
    {
        var aggressor = CreateMockUnit(0x1234, new Position(-5f, -20f, -18f), health: 1000);
        _om.Setup(o => o.Aggressors).Returns(new[] { aggressor.Object });

        _classContainer.Setup(c => c.CreatePvERotationTask).Returns((IBotContext _) => Mock.Of<IBotTask>());

        var task = new DungeoneeringTask(_ctx.Object, isLeader: true, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        // Should have pushed PvERotation on top of DungeoneeringTask
        Assert.Equal(2, _botTasks.Count);
    }

    [Fact]
    public void Leader_PullsHostile_InLOS_WhenNoCombatCooldown()
    {
        var hostile = CreateMockUnit(0x5678, new Position(-10f, -30f, -18f), health: 2000);
        hostile.Setup(h => h.Name).Returns("Ragefire Trogg");
        _om.Setup(o => o.Hostiles).Returns(new[] { hostile.Object });
        _player.Setup(p => p.InLosWith(It.IsAny<IWoWUnit>())).Returns(true);

        _classContainer.Setup(c => c.CreatePullTargetTask).Returns((IBotContext _) => Mock.Of<IBotTask>());

        var task = new DungeoneeringTask(_ctx.Object, isLeader: true, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        // Should push PullTargetTask
        Assert.Equal(2, _botTasks.Count);
        _om.Verify(o => o.SetTarget(0x5678), Times.Once);
    }

    // =========================================================================
    // Follower behavior
    // =========================================================================

    [Fact]
    public void Follower_StartsInFollowLeaderState()
    {
        var task = new DungeoneeringTask(_ctx.Object, isLeader: false, RfcWaypoints, targetMapId: 389);
        Assert.False(task.IsLeader);
    }

    [Fact]
    public void Follower_FollowsPartyLeader_WhenFarAway()
    {
        // Leader is 30y away
        var leader = CreateMockPlayer(0xAAAA, new Position(-30f, -40f, -20f));
        _om.Setup(o => o.PartyLeader).Returns(leader.Object);

        var task = new DungeoneeringTask(_ctx.Object, isLeader: false, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        // Should still be on stack (navigating toward leader)
        Assert.Single(_botTasks);
    }

    [Fact]
    public void Follower_StopsMovement_WhenCloseToLeader()
    {
        // Leader is 5y away (within FollowStopDistance)
        var leader = CreateMockPlayer(0xAAAA, new Position(5f, -14f, -16f));
        _om.Setup(o => o.PartyLeader).Returns(leader.Object);

        var task = new DungeoneeringTask(_ctx.Object, isLeader: false, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        _om.Verify(o => o.StopAllMovement(), Times.Once);
    }

    [Fact]
    public void Follower_FightsAggressors_JustLikeLeader()
    {
        var aggressor = CreateMockUnit(0x9999, new Position(0f, -15f, -17f), health: 500);
        _om.Setup(o => o.Aggressors).Returns(new[] { aggressor.Object });

        _classContainer.Setup(c => c.CreatePvERotationTask).Returns((IBotContext _) => Mock.Of<IBotTask>());

        var task = new DungeoneeringTask(_ctx.Object, isLeader: false, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        // Follower also pushes PvERotation for aggressors
        Assert.Equal(2, _botTasks.Count);
    }

    // =========================================================================
    // Wrong-map guard
    // =========================================================================

    [Fact]
    public void Leader_StopsNavigation_WhenOnWrongMap()
    {
        _player.Setup(p => p.MapId).Returns(1u); // Kalimdor, not RFC (389)

        var task = new DungeoneeringTask(_ctx.Object, isLeader: true, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        _om.Verify(o => o.StopAllMovement(), Times.Once);
        Assert.Single(_botTasks); // Didn't pop or push anything
    }

    [Fact]
    public void Follower_StopsNavigation_WhenOnWrongMap()
    {
        _player.Setup(p => p.MapId).Returns(1u); // Kalimdor

        var task = new DungeoneeringTask(_ctx.Object, isLeader: false, RfcWaypoints, targetMapId: 389);
        _botTasks.Push(task);

        task.Update();

        _om.Verify(o => o.StopAllMovement(), Times.Once);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Mock<IWoWUnit> CreateMockUnit(ulong guid, Position pos, uint health)
    {
        var unit = new Mock<IWoWUnit>();
        unit.Setup(u => u.Guid).Returns(guid);
        unit.Setup(u => u.Position).Returns(pos);
        unit.Setup(u => u.Health).Returns(health);
        unit.Setup(u => u.HealthPercent).Returns(100);
        return unit;
    }

    private static Mock<IWoWPlayer> CreateMockPlayer(ulong guid, Position pos)
    {
        var player = new Mock<IWoWPlayer>();
        player.Setup(p => p.Guid).Returns(guid);
        player.Setup(p => p.Position).Returns(pos);
        player.Setup(p => p.Health).Returns(100);
        return player;
    }
}
