using BloogBot.Tests.WoWSimulation.Core;
using FluentAssertions;
using Xunit;

namespace BloogBot.Tests.WoWSimulation;

public class MockMangosServerTests
{
    [Fact]
    public async Task SendCommand_GetPlayerPosition_ReturnsValidPosition()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer 
        { 
            Id = 1, 
            Name = "TestPlayer",
            Position = new Position3D(100, 200, 0)
        };
        server.AddPlayer(player);

        // Act
        var position = await server.SendCommand<Position3D>("GetPlayerPosition");

        // Assert
        position.X.Should().Be(100);
        position.Y.Should().Be(200);
        position.Z.Should().Be(0);
    }

    [Fact]
    public async Task SendCommand_GetNearbyObjects_ReturnsObjectsInRange()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer 
        { 
            Id = 1, 
            Position = new Position3D(100, 100, 0)
        };
        server.AddPlayer(player);

        // Act
        var nearbyObjects = await server.SendCommand<List<MockGameObject>>("GetNearbyObjects", 100);

        // Assert
        nearbyObjects.Should().NotBeEmpty();
        nearbyObjects.Should().Contain(obj => obj.Name == "Training Dummy");
    }

    [Fact]
    public async Task SendCommand_MoveToPosition_TriggersMovementEvent()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer 
        { 
            Id = 1, 
            Position = new Position3D(0, 0, 0)
        };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        var targetPosition = new Position3D(50, 50, 0);

        // Act
        var result = await server.SendCommand<bool>("MoveToPosition", targetPosition);

        // Assert
        result.Should().BeTrue();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.PlayerMoved);
    }

    [Fact]
    public async Task SendCommand_InteractWithObject_TriggersInteractionEvent()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("InteractWithObject", 2); // Herb Node

        // Assert
        result.Should().BeTrue();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.ObjectInteraction);
    }

    [Fact]
    public async Task SendCommand_CastSpell_TriggersSpellCastEvent()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("CastSpell", "Fireball");

        // Assert
        result.Should().BeTrue();
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.SpellCast);
    }

    [Fact]
    public void EventHistory_TracksAllEvents()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        // Act
        _ = server.SendCommand<bool>("CastSpell", "Heal").Result;
        _ = server.SendCommand<bool>("InteractWithObject", 1).Result;

        var history = server.GetEventHistory();

        // Assert
        history.Should().HaveCount(2);
        history.Should().Contain(e => e.Type == EventType.SpellCast);
        history.Should().Contain(e => e.Type == EventType.ObjectInteraction);
    }

    [Fact]
    public void ClearEventHistory_RemovesAllEvents()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        _ = server.SendCommand<bool>("CastSpell", "Heal").Result;

        // Act
        server.ClearEventHistory();

        // Assert
        server.GetEventHistory().Should().BeEmpty();
    }
}
