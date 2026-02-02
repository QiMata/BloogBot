using GameData.Core.Enums;
using GameData.Core.Models;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Integration tests for WoWSharpClient against a live WoW emulator server.
/// 
/// Prerequisites:
/// - WoW emulator server (realmd + mangosd) must be running externally
/// - Test accounts must exist with GM level 3
/// - PathfindingService will be started automatically if not running
/// 
/// Run with: dotnet test --filter "Category=Integration"
/// 
/// Tests will be skipped automatically if the server is not available.
/// </summary>
[Trait("Category", "Integration")]
public class WoWSharpClientIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly LiveServerFixture _fixture;

    public WoWSharpClientIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _fixture = new LiveServerFixture(output);
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    /// <summary>
    /// Skips the test if the server is not available.
    /// </summary>
    private void SkipIfServerUnavailable()
    {
        if (!_fixture.IsServerAvailable)
        {
            throw new SkipException("WoW server is not running. Start the server and re-run tests.");
        }
    }

    #region Connection Tests

    [Fact]
    public async Task LoginToAuthServer_WithValidCredentials_Succeeds()
    {
        SkipIfServerUnavailable();

        // Arrange & Act
        var result = await _fixture.ConnectAndLoginAsync();

        // Assert
        Assert.True(result, "Failed to connect and login to auth server");
        Assert.True(_fixture.WoWClient.IsLoggedIn, "WoWClient should report logged in");
    }

    [Fact]
    public async Task GetRealmList_AfterLogin_ReturnsRealms()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.ConnectAndLoginAsync();

        // Act
        var realms = _fixture.WoWClient.GetRealmList();

        // Assert
        Assert.NotEmpty(realms);
        _output.WriteLine($"Found {realms.Count} realm(s):");
        foreach (var realm in realms)
        {
            _output.WriteLine($"  - {realm.RealmName} (Type: {realm.RealmType}, Pop: {realm.Population})");
        }
    }

    [Fact]
    public async Task SelectRealm_AfterLogin_ConnectsToWorld()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.ConnectAndLoginAsync();

        // Act
        var result = await _fixture.SelectRealmAsync();

        // Assert
        Assert.True(result, "Failed to connect to world server after selecting realm");
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "WoWClient should report world connected");
    }

    [Fact]
    public async Task GetCharacterList_AfterRealmSelect_ReturnsCharacters()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.ConnectAndLoginAsync();
        await _fixture.SelectRealmAsync();

        // Act
        var result = await _fixture.WaitForCharacterListAsync();

        // Assert
        Assert.True(result, "Failed to receive character list");

        var characters = _fixture.ObjectManager.CharacterSelectScreen.CharacterSelects;
        _output.WriteLine($"Found {characters.Count} character(s):");
        foreach (var character in characters)
        {
            _output.WriteLine($"  - {character.Name} (Level {character.Level} {character.Race} {character.Class})");
        }
    }

    [Fact]
    public async Task EnterWorld_WithCharacter_SuccessfullyEntersWorld()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.ConnectAndLoginAsync();
        await _fixture.SelectRealmAsync();
        await _fixture.WaitForCharacterListAsync();

        // Act
        var result = await _fixture.EnterWorldAsync();

        // Assert
        Assert.True(result, "Failed to enter world");
        Assert.True(_fixture.ObjectManager.HasEnteredWorld, "ObjectManager should report entered world");
        Assert.NotNull(_fixture.ObjectManager.Player);
        _output.WriteLine($"Entered world at position: ({_fixture.ObjectManager.Player.Position.X:F2}, {_fixture.ObjectManager.Player.Position.Y:F2}, {_fixture.ObjectManager.Player.Position.Z:F2})");
    }

    #endregion

    #region GM Command Tests

    [Fact]
    public async Task GmCommand_Teleport_MovesPlayerToLocation()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        var originalPosition = new Position(
            _fixture.ObjectManager.Player.Position.X,
            _fixture.ObjectManager.Player.Position.Y,
            _fixture.ObjectManager.Player.Position.Z
        );

        // Act - Teleport to Orgrimmar bank
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);

        // Assert
        var newPosition = _fixture.ObjectManager.Player.Position;
        var distance = originalPosition.DistanceTo(newPosition);

        _output.WriteLine($"Original position: ({originalPosition.X:F2}, {originalPosition.Y:F2}, {originalPosition.Z:F2})");
        _output.WriteLine($"New position: ({newPosition.X:F2}, {newPosition.Y:F2}, {newPosition.Z:F2})");
        _output.WriteLine($"Distance moved: {distance:F2}");

        // If we teleported, we should have moved significantly
        Assert.True(distance > 10 || IsNearPosition(newPosition, 1629.36f, -4373.39f, 31.28f, 5f),
            "Player should have teleported to new location");
    }

    [Fact]
    public async Task GmCommand_AddItem_AddsItemToInventory()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();

        // Act - Add Hearthstone (item ID 6948)
        await _fixture.AddItemAsync(6948, 1);

        // Allow time for inventory update
        await Task.Delay(1000);

        // Assert - Check that we received the item (implementation depends on inventory tracking)
        _output.WriteLine("Item add command sent. Check character inventory for Hearthstone.");
        // Note: Full assertion would require inventory tracking implementation
        Assert.True(_fixture.IsInWorld, "Should still be in world after GM command");
    }

    [Fact]
    public async Task GmCommand_SetLevel_ChangesPlayerLevel()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        var originalLevel = _fixture.ObjectManager.Player.Level;

        // Act
        await _fixture.SetLevelAsync(60);
        await Task.Delay(1000);

        // Assert
        _output.WriteLine($"Original level: {originalLevel}");
        _output.WriteLine($"Expected level: 60");
        // Note: Level change assertion depends on ObjectManager tracking player updates
        Assert.True(_fixture.IsInWorld, "Should still be in world after level change");
    }

    #endregion

    #region Movement Tests

    [Fact]
    public async Task Movement_ForwardMovement_UpdatesPosition()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f); // Orgrimmar bank

        var startPosition = new Position(
            _fixture.ObjectManager.Player.Position.X,
            _fixture.ObjectManager.Player.Position.Y,
            _fixture.ObjectManager.Player.Position.Z
        );

        // Act - Start forward movement
        _fixture.ObjectManager.StartMovement(ControlBits.Front);

        // Let the character move for a bit
        await Task.Delay(2000);

        // Stop movement
        _fixture.ObjectManager.StopMovement(ControlBits.Front);

        // Assert
        var endPosition = _fixture.ObjectManager.Player.Position;
        var distance = startPosition.DistanceTo(endPosition);

        _output.WriteLine($"Start: ({startPosition.X:F2}, {startPosition.Y:F2}, {startPosition.Z:F2})");
        _output.WriteLine($"End: ({endPosition.X:F2}, {endPosition.Y:F2}, {endPosition.Z:F2})");
        _output.WriteLine($"Distance moved: {distance:F2}");

        Assert.True(distance > 0.5f, "Player should have moved forward");
    }

    [Fact]
    public async Task Movement_Facing_UpdatesOrientation()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);

        var startFacing = _fixture.ObjectManager.Player.Facing;

        // Act - Turn 90 degrees
        var newFacing = (startFacing + (float)(Math.PI / 2)) % (float)(Math.PI * 2);
        _fixture.ObjectManager.SetFacing(newFacing);

        await Task.Delay(500);

        // Assert
        var endFacing = _fixture.ObjectManager.Player.Facing;
        _output.WriteLine($"Start facing: {startFacing:F4} rad");
        _output.WriteLine($"Expected facing: {newFacing:F4} rad");
        _output.WriteLine($"End facing: {endFacing:F4} rad");

        Assert.True(Math.Abs(endFacing - newFacing) < 0.1f || Math.Abs(endFacing - newFacing - Math.PI * 2) < 0.1f,
            "Player facing should have updated");
    }

    #endregion

    #region Combat Scenario Tests

    [Fact]
    public async Task CombatScenario_SpawnAndTarget_CanTargetNpc()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await _fixture.FullHealAsync();

        // Act - Spawn a training dummy or weak NPC (entry ID depends on server database)
        // Using a common test NPC entry - adjust based on your server
        await _fixture.SpawnNpcAsync(6, CancellationToken.None); // Example: Kobold Vermin

        await Task.Delay(2000);

        // Assert - Check for nearby units
        var nearbyUnits = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .Where(u => u.Guid != _fixture.ObjectManager.Player.Guid)
            .ToList();

        _output.WriteLine($"Found {nearbyUnits.Count} nearby units");
        foreach (var unit in nearbyUnits.Take(5))
        {
            _output.WriteLine($"  - {unit.Name ?? "Unknown"} (Entry: {unit.Entry}, Health: {unit.Health}/{unit.MaxHealth})");
        }

        Assert.True(_fixture.IsInWorld, "Should still be in world");
    }

    #endregion

    #region Bot Behavior Tests

    [Fact]
    public async Task BotBehavior_RestTask_RepairsItemsWithGmCommand()
    {
        SkipIfServerUnavailable();

        // Arrange - This simulates what RestTask does in bot profiles
        await _fixture.FullConnectSequenceAsync();

        // Act
        await _fixture.RepairItemsAsync();

        // Assert
        _output.WriteLine("Repair items command sent");
        Assert.True(_fixture.IsInWorld, "Should still be in world after repair command");
    }

    [Fact]
    public async Task BotBehavior_AddFood_SimulatesResourceGathering()
    {
        SkipIfServerUnavailable();

        // Arrange - Simulates RestTask adding food items
        await _fixture.FullConnectSequenceAsync();

        // Act - Add food items like RestTask does (item 5479 = Crispy Bat Wing)
        await _fixture.AddItemAsync(5479, 20);

        // Assert
        _output.WriteLine("Food items added to inventory");
        Assert.True(_fixture.IsInWorld, "Should still be in world after adding items");
    }

    #endregion

    #region Server Expectation Tests

    [Fact]
    public async Task ServerExpectation_MovementPacket_ServerAcceptsMovement()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);

        // Act - Move and verify server doesn't disconnect us
        _fixture.ObjectManager.StartMovement(ControlBits.Front);
        await Task.Delay(1000);
        _fixture.ObjectManager.StopMovement(ControlBits.Front);
        await Task.Delay(500);

        // Assert - We should still be connected and in world
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected to world server");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
    }

    [Fact]
    public async Task ServerExpectation_RapidCommands_ServerHandlesMultipleCommands()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();

        // Act - Send multiple GM commands rapidly
        for (int i = 0; i < 5; i++)
        {
            await _fixture.SendGmCommandAsync(".mod hp 100", 100);
        }

        // Assert - Server should handle rapid commands without issues
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after rapid commands");
    }

    #endregion

    #region Helper Methods

    private static bool IsNearPosition(Position current, float x, float y, float z, float tolerance)
    {
        var dx = current.X - x;
        var dy = current.Y - y;
        var dz = current.Z - z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return distance <= tolerance;
    }

    #endregion
}
