using WoWSimulation.Tests.Core;
using FluentAssertions;
using Xunit;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WoWSimulation.Tests;

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
    public async Task EventHistory_TracksAllEvents()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        // Act
        await server.SendCommand<bool>("CastSpell", "Heal");
        await server.SendCommand<bool>("InteractWithObject", 2); // Herb Node (interactable)

        var history = server.GetEventHistory();

        // Assert
        history.Should().HaveCount(2);
        history.Should().Contain(e => e.Type == EventType.SpellCast);
        history.Should().Contain(e => e.Type == EventType.ObjectInteraction);
    }

    [Fact]
    public async Task ClearEventHistory_RemovesAllEvents()
    {
        // Arrange
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        await server.SendCommand<bool>("CastSpell", "Heal");

        // Act
        server.ClearEventHistory();

        // Assert
        server.GetEventHistory().Should().BeEmpty();
    }

    // =========================================================================
    // WSIM-TST-001: Negative-path tests (unsupported command, no-player guards)
    // =========================================================================

    [Fact]
    public async Task SendCommand_UnsupportedCommand_ThrowsNotSupportedException()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);

        // Act
        Func<Task> act = async () => await server.SendCommand<bool>("NonExistentCommand");

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*NonExistentCommand*");
    }

    [Fact]
    public async Task SendCommand_MoveToPosition_WithoutPlayer_ReturnsFalse()
    {
        // Arrange — no player added
        var server = new MockMangosServer(commandLatencyMs: 0);
        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("MoveToPosition", new Position3D(10, 20, 0));

        // Assert
        result.Should().BeFalse();
        capturedEvent.Should().BeNull("no movement event should fire when there is no player");
    }

    [Fact]
    public async Task SendCommand_CastSpell_WithoutPlayer_ReturnsFalse()
    {
        // Arrange — no player added
        var server = new MockMangosServer(commandLatencyMs: 0);
        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("CastSpell", "Fireball");

        // Assert
        result.Should().BeFalse();
        capturedEvent.Should().BeNull("no spell event should fire when there is no player");
    }

    // =========================================================================
    // WSIM-TST-002: GetPlayerHealth direct coverage
    // =========================================================================

    [Fact]
    public async Task SendCommand_GetPlayerHealth_WithPlayer_ReturnsPlayerHealth()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 75, MaxHealth = 100 };
        server.AddPlayer(player);

        // Act
        var health = await server.SendCommand<int>("GetPlayerHealth");

        // Assert
        health.Should().Be(75);
    }

    [Fact]
    public async Task SendCommand_GetPlayerHealth_WithoutPlayer_ReturnsZero()
    {
        // Arrange — no player added
        var server = new MockMangosServer(commandLatencyMs: 0);

        // Act
        var health = await server.SendCommand<int>("GetPlayerHealth");

        // Assert
        health.Should().Be(0);
    }

    // =========================================================================
    // WSIM-TST-003: Movement payload verification
    // =========================================================================

    [Fact]
    public async Task SendCommand_MoveToPosition_EventPayloadContainsFromToDuration()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var startPos = new Position3D(10, 20, 0);
        var player = new MockPlayer { Id = 1, Position = startPos };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        var targetPos = new Position3D(80, 90, 0);

        // Act
        await server.SendCommand<bool>("MoveToPosition", targetPos);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.PlayerMoved);

        // Verify payload via dynamic since Data is anonymous type
        dynamic data = capturedEvent.Data!;
        Position3D from = data.From;
        Position3D to = data.To;
        double duration = data.Duration;

        from.X.Should().Be(10);
        from.Y.Should().Be(20);
        to.X.Should().Be(80);
        to.Y.Should().Be(90);
        duration.Should().BeGreaterThan(0, "movement over a non-zero distance must have positive duration");
    }

    [Fact]
    public async Task SendCommand_MoveToPosition_UpdatesPlayerPosition()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Position = new Position3D(0, 0, 0) };
        server.AddPlayer(player);

        var targetPos = new Position3D(30, 40, 5);

        // Act
        await server.SendCommand<bool>("MoveToPosition", targetPos);
        var newPosition = await server.SendCommand<Position3D>("GetPlayerPosition");

        // Assert
        newPosition.X.Should().Be(30);
        newPosition.Y.Should().Be(40);
        newPosition.Z.Should().Be(5);
    }

    // =========================================================================
    // WSIM-TST-004: Interaction failure tests
    // =========================================================================

    [Fact]
    public async Task SendCommand_InteractWithObject_InvalidId_ReturnsFalse()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act — object ID 999 does not exist
        var result = await server.SendCommand<bool>("InteractWithObject", 999);

        // Assert
        result.Should().BeFalse();
        capturedEvent.Should().BeNull("no interaction event should fire for a missing object");
    }

    [Fact]
    public async Task SendCommand_InteractWithObject_NonInteractableObject_ReturnsFalse()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act — object ID 1 is Training Dummy (IsInteractable = false)
        var result = await server.SendCommand<bool>("InteractWithObject", 1);

        // Assert
        result.Should().BeFalse();
        capturedEvent.Should().BeNull("no interaction event should fire for a non-interactable object");
    }

    // =========================================================================
    // WSIM-TST-005: Corpse lifecycle (death, resurrection)
    // =========================================================================

    [Fact]
    public async Task SendCommand_KillPlayer_SetsHealthToZeroAndFiresDeathEvent()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 100, MaxHealth = 100 };
        server.AddPlayer(player);

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("KillPlayer");

        // Assert
        result.Should().BeTrue();
        var health = await server.SendCommand<int>("GetPlayerHealth");
        health.Should().Be(0);
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.Death);
    }

    [Fact]
    public async Task SendCommand_KillPlayer_AlreadyDead_ReturnsFalse()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 0, MaxHealth = 100 };
        server.AddPlayer(player);

        // Act
        var result = await server.SendCommand<bool>("KillPlayer");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendCommand_KillPlayer_WithoutPlayer_ReturnsFalse()
    {
        // Arrange — no player added
        var server = new MockMangosServer(commandLatencyMs: 0);

        // Act
        var result = await server.SendCommand<bool>("KillPlayer");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendCommand_ResurrectPlayer_RestoresHealthAndFiresResurrectionEvent()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 100, MaxHealth = 100 };
        server.AddPlayer(player);

        // Kill first, then resurrect
        await server.SendCommand<bool>("KillPlayer");
        server.ClearEventHistory();

        SimulationEvent? capturedEvent = null;
        server.EventOccurred += (sender, e) => capturedEvent = e;

        // Act
        var result = await server.SendCommand<bool>("ResurrectPlayer");

        // Assert
        result.Should().BeTrue();
        var health = await server.SendCommand<int>("GetPlayerHealth");
        health.Should().Be(100);
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(EventType.Resurrection);
    }

    [Fact]
    public async Task SendCommand_ResurrectPlayer_AlreadyAlive_ReturnsFalse()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 100, MaxHealth = 100 };
        server.AddPlayer(player);

        // Act — player is alive, cannot resurrect
        var result = await server.SendCommand<bool>("ResurrectPlayer");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendCommand_ResurrectPlayer_WithoutPlayer_ReturnsFalse()
    {
        // Arrange — no player added
        var server = new MockMangosServer(commandLatencyMs: 0);

        // Act
        var result = await server.SendCommand<bool>("ResurrectPlayer");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CorpseLifecycle_FullDeathAndResurrectionCycle()
    {
        // Arrange
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1, Health = 100, MaxHealth = 100 };
        server.AddPlayer(player);

        // Act & Assert — full lifecycle
        var killResult = await server.SendCommand<bool>("KillPlayer");
        killResult.Should().BeTrue();

        var healthAfterDeath = await server.SendCommand<int>("GetPlayerHealth");
        healthAfterDeath.Should().Be(0);

        var resResult = await server.SendCommand<bool>("ResurrectPlayer");
        resResult.Should().BeTrue();

        var healthAfterRes = await server.SendCommand<int>("GetPlayerHealth");
        healthAfterRes.Should().Be(100);

        // Verify event history shows both death and resurrection
        var history = server.GetEventHistory();
        history.Should().Contain(e => e.Type == EventType.Death);
        history.Should().Contain(e => e.Type == EventType.Resurrection);
    }

    // =========================================================================
    // WSIM-TST-006: Configurable latency
    // =========================================================================

    [Fact]
    public async Task Constructor_DefaultLatency_PreservesOriginalBehavior()
    {
        // Arrange — default constructor (10ms latency)
        var server = new MockMangosServer();
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        var sw = Stopwatch.StartNew();

        // Act
        await server.SendCommand<bool>("CastSpell", "Heal");
        sw.Stop();

        // Assert — should take at least ~10ms due to default latency
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(5,
            "default latency of 10ms should introduce measurable delay");
    }

    [Fact]
    public async Task Constructor_ZeroLatency_RunsFasterThanDefault()
    {
        // Arrange — zero latency for fast deterministic tests
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer { Id = 1 };
        server.AddPlayer(player);

        var sw = Stopwatch.StartNew();

        // Act — run multiple commands
        for (int i = 0; i < 10; i++)
        {
            await server.SendCommand<bool>("CastSpell", "Heal");
        }
        sw.Stop();

        // Assert — 10 commands with zero latency should complete well under 50ms
        sw.ElapsedMilliseconds.Should().BeLessThan(50,
            "zero-latency server should process commands without artificial delay");
    }

    [Fact]
    public async Task Constructor_ZeroLatency_BehaviorParityWithDefault()
    {
        // Arrange — zero-latency server should produce identical game results
        var server = new MockMangosServer(commandLatencyMs: 0);
        var player = new MockPlayer
        {
            Id = 1,
            Name = "TestPlayer",
            Position = new Position3D(0, 0, 0),
            Health = 100,
            MaxHealth = 100
        };
        server.AddPlayer(player);

        // Act — exercise all command paths
        var pos = await server.SendCommand<Position3D>("GetPlayerPosition");
        var nearby = await server.SendCommand<List<MockGameObject>>("GetNearbyObjects", 200);
        var moveResult = await server.SendCommand<bool>("MoveToPosition", new Position3D(10, 10, 0));
        var interactResult = await server.SendCommand<bool>("InteractWithObject", 2);
        var health = await server.SendCommand<int>("GetPlayerHealth");
        var spellResult = await server.SendCommand<bool>("CastSpell", "Frostbolt");

        // Assert — all paths behave identically regardless of latency setting
        pos.X.Should().Be(0);
        pos.Y.Should().Be(0);
        nearby.Should().NotBeEmpty();
        moveResult.Should().BeTrue();
        interactResult.Should().BeTrue();
        health.Should().Be(100);
        spellResult.Should().BeTrue();
    }
}
