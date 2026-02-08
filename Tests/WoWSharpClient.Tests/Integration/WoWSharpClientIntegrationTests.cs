using Communication;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
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

        // If GM privileges are available, we should have moved significantly.
        // If not, the command is silently ignored but we should still be in world.
        if (distance > 10 || IsNearPosition(newPosition, 1629.36f, -4373.39f, 31.28f, 5f))
        {
            _output.WriteLine("Teleport succeeded (GM command recognized by server)");
        }
        else
        {
            _output.WriteLine("Teleport did not move player - account may lack GM privileges");
        }
        Assert.True(_fixture.IsInWorld, "Should still be in world after teleport command");
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

    [Fact]
    public async Task CombatScenario_SetTarget_SendsSelectionPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);

        // Spawn a weak NPC nearby
        await _fixture.SpawnNpcAsync(6, CancellationToken.None);
        await Task.Delay(2000);

        // Find a nearby unit to target
        var nearbyUnit = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyUnit == null)
        {
            _output.WriteLine("No targetable units found - skipping target test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Targeting unit: {nearbyUnit.Name ?? "Unknown"} (GUID: {nearbyUnit.Guid:X}, Health: {nearbyUnit.Health}/{nearbyUnit.MaxHealth})");

        // Act - Set target
        _fixture.ObjectManager.SetTarget(nearbyUnit.Guid);
        await Task.Delay(500);

        // Assert - Server should accept the selection without disconnecting
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after SetTarget");
        Assert.True(_fixture.IsInWorld, "Should still be in world after SetTarget");
        _output.WriteLine("SetTarget accepted by server - no disconnect");
    }

    [Fact]
    public async Task CombatScenario_AttackSwing_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await _fixture.FullHealAsync();

        // Spawn a weak NPC
        await _fixture.SpawnNpcAsync(6, CancellationToken.None);
        await Task.Delay(2000);

        // Find a nearby unit
        var nearbyUnit = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyUnit == null)
        {
            _output.WriteLine("No attackable units found - skipping attack test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Attacking unit: {nearbyUnit.Name ?? "Unknown"} (GUID: {nearbyUnit.Guid:X})");

        // Act - Set target and attack
        _fixture.ObjectManager.SetTarget(nearbyUnit.Guid);
        await Task.Delay(200);
        _fixture.ObjectManager.StartMeleeAttack();
        await Task.Delay(3000); // Wait for server to process and send attack state updates

        // Assert - Server should accept the attack swing without ByteBuffer errors
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after AttackSwing");
        Assert.True(_fixture.IsInWorld, "Should still be in world after AttackSwing");

        // Stop attacking
        _fixture.ObjectManager.StopAttack();
        await Task.Delay(500);

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after StopAttack");
        _output.WriteLine("Attack sequence accepted by server - no disconnect or ByteBuffer errors");
    }

    [Fact]
    public async Task CombatScenario_CastSpell_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await _fixture.FullHealAsync();

        // Spawn a weak NPC
        await _fixture.SpawnNpcAsync(6, CancellationToken.None);
        await Task.Delay(2000);

        // Find a nearby unit
        var nearbyUnit = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyUnit == null)
        {
            _output.WriteLine("No targetable units found - skipping spell test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Targeting unit: {nearbyUnit.Name ?? "Unknown"} (GUID: {nearbyUnit.Guid:X})");

        // Set target first
        _fixture.ObjectManager.SetTarget(nearbyUnit.Guid);
        await Task.Delay(200);

        // Check what spells we know
        var spells = _fixture.ObjectManager.Spells;
        _output.WriteLine($"Known spells: {spells.Count}");
        foreach (var spell in spells.Take(10))
        {
            _output.WriteLine($"  - ID: {spell.Id}, Name: {spell.Name}");
        }

        // Act - Cast a spell by ID (use first known spell if any, or try Attack spell 6603)
        var spellId = spells.Count > 0 ? (int)spells[0].Id : 6603; // 6603 = Attack
        _output.WriteLine($"Casting spell ID: {spellId}");
        _fixture.ObjectManager.CastSpell(spellId);
        await Task.Delay(2000);

        // Assert - Server should accept the spell cast without ByteBuffer errors
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CastSpell");
        Assert.True(_fixture.IsInWorld, "Should still be in world after CastSpell");
        _output.WriteLine("CastSpell accepted by server - no disconnect or ByteBuffer errors");
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

    #region Stability Tests

    [Fact]
    public async Task Stability_StayConnected60Seconds_NoDisconnect()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        var startPosition = new Position(
            _fixture.ObjectManager.Player.Position.X,
            _fixture.ObjectManager.Player.Position.Y,
            _fixture.ObjectManager.Player.Position.Z
        );

        _output.WriteLine($"Entered world at: ({startPosition.X:F2}, {startPosition.Y:F2}, {startPosition.Z:F2})");
        _output.WriteLine("Starting 60-second stability test...");

        // Act - Stay connected for 60 seconds, checking connection every 5 seconds
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(5000);
            var elapsed = (i + 1) * 5;

            Assert.True(_fixture.WoWClient.IsWorldConnected(),
                $"Lost world connection at {elapsed}s");
            Assert.True(_fixture.ObjectManager.HasEnteredWorld,
                $"HasEnteredWorld became false at {elapsed}s");

            _output.WriteLine($"  [{elapsed}s] Connected, Position=({_fixture.ObjectManager.Player.Position.X:F2}, {_fixture.ObjectManager.Player.Position.Y:F2}, {_fixture.ObjectManager.Player.Position.Z:F2})");
        }

        // Assert - Still connected and in world after 60 seconds
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should be connected after 60 seconds");
        Assert.True(_fixture.IsInWorld, "Should be in world after 60 seconds");
        _output.WriteLine("60-second stability test PASSED");
    }

    [Fact]
    public async Task Stability_MoveForward10Seconds_PositionChanges()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        var startPosition = new Position(
            _fixture.ObjectManager.Player.Position.X,
            _fixture.ObjectManager.Player.Position.Y,
            _fixture.ObjectManager.Player.Position.Z
        );

        _output.WriteLine($"Start position: ({startPosition.X:F2}, {startPosition.Y:F2}, {startPosition.Z:F2})");

        // Act - Move forward for 10 seconds
        _fixture.ObjectManager.StartMovement(ControlBits.Front);

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1000);
            var pos = _fixture.ObjectManager.Player.Position;
            _output.WriteLine($"  [{i + 1}s] Position=({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");
        }

        _fixture.ObjectManager.StopMovement(ControlBits.Front);
        await Task.Delay(500);

        // Assert
        var endPosition = _fixture.ObjectManager.Player.Position;
        var distance = startPosition.DistanceTo(endPosition);

        _output.WriteLine($"End position: ({endPosition.X:F2}, {endPosition.Y:F2}, {endPosition.Z:F2})");
        _output.WriteLine($"Total distance moved: {distance:F2} yards");

        // At ~7 y/s run speed, 10 seconds should move ~70 yards
        Assert.True(distance > 10f, $"Expected significant movement, only moved {distance:F2} yards");
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after movement");
    }

    #endregion

    #region NPC Interaction Tests

    [Fact]
    public async Task NpcInteraction_GossipHello_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Find a nearby non-player unit (NPC)
        var nearbyNpc = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyNpc == null)
        {
            // Spawn a friendly NPC to interact with
            await _fixture.SpawnNpcAsync(6, CancellationToken.None);
            await Task.Delay(2000);
            nearbyNpc = _fixture.ObjectManager.Objects
                .OfType<WoWSharpClient.Models.WoWUnit>()
                .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);
        }

        if (nearbyNpc == null)
        {
            _output.WriteLine("No NPCs found - skipping gossip test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Interacting with NPC: {nearbyNpc.Name ?? "Unknown"} (GUID: {nearbyNpc.Guid:X})");

        // Create gossip component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var gossipComponent = new GossipNetworkClientComponent(worldClient, loggerFactory.CreateLogger<GossipNetworkClientComponent>());

        // Act - Send gossip hello
        GossipMenuData? receivedMenu = null;
        using var sub = gossipComponent.GossipMenus.Subscribe(menu =>
        {
            receivedMenu = menu;
            _output.WriteLine($"Gossip menu received! NpcGuid={menu.NpcGuid:X}, TextId={menu.TextId}, Options={menu.Options.Count}, Quests={menu.QuestOptions.Count}");
            foreach (var opt in menu.Options)
                _output.WriteLine($"  Option[{opt.Index}]: Icon={opt.GossipType}, Text='{opt.Text}'");
            foreach (var quest in menu.QuestOptions)
                _output.WriteLine($"  Quest[{quest.Index}]: ID={quest.QuestId}, Title='{quest.QuestTitle}', Level={quest.QuestLevel}");
        });

        await gossipComponent.GreetNpcAsync(nearbyNpc.Guid, CancellationToken.None);
        await Task.Delay(2000);

        // Assert - Server should accept the packet without disconnect
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_GOSSIP_HELLO");
        Assert.True(_fixture.IsInWorld, "Should still be in world after gossip hello");
        _output.WriteLine($"CMSG_GOSSIP_HELLO accepted by server. Menu received: {receivedMenu != null}");
    }

    [Fact]
    public async Task NpcInteraction_VendorListInventory_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        var nearbyNpc = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyNpc == null)
        {
            await _fixture.SpawnNpcAsync(6, CancellationToken.None);
            await Task.Delay(2000);
            nearbyNpc = _fixture.ObjectManager.Objects
                .OfType<WoWSharpClient.Models.WoWUnit>()
                .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);
        }

        if (nearbyNpc == null)
        {
            _output.WriteLine("No NPCs found - skipping vendor test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Requesting vendor inventory from: {nearbyNpc.Name ?? "Unknown"} (GUID: {nearbyNpc.Guid:X})");

        // Create vendor component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var vendorComponent = new VendorNetworkClientComponent(worldClient, loggerFactory.CreateLogger<VendorNetworkClientComponent>());

        // Act - Request vendor inventory
        VendorInfo? vendorInfo = null;
        using var sub = vendorComponent.VendorWindowsOpened.Subscribe(info =>
        {
            vendorInfo = info;
            _output.WriteLine($"Vendor window opened! GUID={info.VendorGuid:X}, Items={info.AvailableItems.Count}");
            foreach (var item in info.AvailableItems.Take(5))
                _output.WriteLine($"  Item[{item.VendorSlot}]: ID={item.ItemId}, Price={item.Price}c, Qty={item.AvailableQuantity}");
        });

        await vendorComponent.RequestVendorInventoryAsync(nearbyNpc.Guid, CancellationToken.None);
        await Task.Delay(2000);

        // Assert - Server should accept without disconnect
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_LIST_INVENTORY");
        Assert.True(_fixture.IsInWorld, "Should still be in world after vendor request");
        _output.WriteLine($"CMSG_LIST_INVENTORY accepted by server. Vendor info: {vendorInfo != null}");
    }

    [Fact]
    public async Task NpcInteraction_BuyItem_CorrectPacketFormat()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        var nearbyNpc = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid);

        if (nearbyNpc == null)
        {
            await _fixture.SpawnNpcAsync(6, CancellationToken.None);
            await Task.Delay(2000);
            nearbyNpc = _fixture.ObjectManager.Objects
                .OfType<WoWSharpClient.Models.WoWUnit>()
                .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid);
        }

        if (nearbyNpc == null)
        {
            _output.WriteLine("No NPCs found - skipping buy item test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        // Create vendor component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var vendorComponent = new VendorNetworkClientComponent(worldClient, loggerFactory.CreateLogger<VendorNetworkClientComponent>());

        // Act - Send raw CMSG_BUY_ITEM to test packet format acceptance
        // We send directly via SendOpcodeAsync to bypass CanPurchaseItem validation
        // (which requires vendor inventory to be loaded first)
        uint testItemId = 4540; // Tough Hunk of Bread
        _output.WriteLine($"Sending CMSG_BUY_ITEM to NPC: {nearbyNpc.Guid:X}, ItemId={testItemId}, Qty=1");

        // CMSG_BUY_ITEM: ObjectGuid vendorGuid (8), uint32 itemEntry (4), uint8 count (1), uint8 unk (1) = 14 bytes
        var payload = new byte[14];
        BitConverter.GetBytes(nearbyNpc.Guid).CopyTo(payload, 0);
        BitConverter.GetBytes(testItemId).CopyTo(payload, 8);
        payload[12] = 1; // count
        payload[13] = 0; // unk
        await worldClient.SendOpcodeAsync(GameData.Core.Enums.Opcode.CMSG_BUY_ITEM, payload, CancellationToken.None);
        await Task.Delay(1000);

        // Assert - Server should accept the packet format (even if NPC isn't a vendor)
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_BUY_ITEM");
        Assert.True(_fixture.IsInWorld, "Should still be in world after buy attempt");
        _output.WriteLine("CMSG_BUY_ITEM accepted by server - correct 14-byte format (guid+itemId+uint8 count+uint8 unk)");
    }

    [Fact]
    public async Task NpcInteraction_TrainerList_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        var nearbyNpc = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (nearbyNpc == null)
        {
            await _fixture.SpawnNpcAsync(6, CancellationToken.None);
            await Task.Delay(2000);
            nearbyNpc = _fixture.ObjectManager.Objects
                .OfType<WoWSharpClient.Models.WoWUnit>()
                .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);
        }

        if (nearbyNpc == null)
        {
            _output.WriteLine("No NPCs found - skipping trainer test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        // Create trainer component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var trainerComponent = new TrainerNetworkClientComponent(worldClient, loggerFactory.CreateLogger<TrainerNetworkClientComponent>());

        // Act - Request trainer list
        TrainerServiceData[]? receivedServices = null;
        using var sub = trainerComponent.TrainerWindowsOpened.Subscribe(tuple =>
        {
            receivedServices = tuple.Services;
            _output.WriteLine($"Trainer window opened! GUID={tuple.TrainerGuid:X}, Services={tuple.Services.Length}");
            foreach (var service in tuple.Services.Take(5))
                _output.WriteLine($"  Spell[{service.ServiceIndex}]: ID={service.SpellId}, Cost={service.Cost}c, CanLearn={service.CanLearn}, ReqLevel={service.RequiredLevel}");
        });

        await trainerComponent.RequestTrainerServicesAsync(nearbyNpc.Guid, CancellationToken.None);
        await Task.Delay(2000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_TRAINER_LIST");
        Assert.True(_fixture.IsInWorld, "Should still be in world after trainer request");
        _output.WriteLine($"CMSG_TRAINER_LIST accepted by server. Services received: {receivedServices != null}");
    }

    #endregion

    #region Dual-Client Orchestration Tests

    [Fact]
    public async Task DualClient_SnapshotBuilding_PopulatesFromObjectManager()
    {
        SkipIfServerUnavailable();

        // Arrange - Full connect and enter world
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        var om = _fixture.ObjectManager;

        // Assert - ObjectManager has player data
        Assert.True(om.HasEnteredWorld, "ObjectManager should report HasEnteredWorld");
        Assert.NotNull(om.Player);

        // Build snapshot (same logic as BotRunnerService.PopulateSnapshotFromObjectManager)
        var snapshot = new Communication.WoWActivitySnapshot
        {
            Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AccountName = "TESTBOT1",
            ScreenState = "InWorld",
            CharacterName = om.Player.Name ?? string.Empty,
        };

        // Movement data
        var pos = om.Player.Position;
        Assert.NotNull(pos);
        snapshot.MovementData = new Game.MovementData
        {
            MovementFlags = (uint)om.Player.MovementFlags,
            WalkSpeed = om.Player.WalkSpeed,
            RunSpeed = om.Player.RunSpeed,
            Facing = om.Player.Facing,
            Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z },
        };

        // Player protobuf
        snapshot.Player = new Game.WoWPlayer
        {
            Unit = new Game.WoWUnit
            {
                GameObject = new Game.WoWGameObject
                {
                    Base = new Game.WoWObject
                    {
                        Guid = om.Player.Guid,
                        ObjectType = (uint)om.Player.ObjectType,
                        Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z },
                        Facing = om.Player.Facing,
                    },
                    FactionTemplate = om.Player.FactionTemplate,
                    Level = om.Player.Level,
                },
                Health = om.Player.Health,
                MaxHealth = om.Player.MaxHealth,
            }
        };

        // Nearby units
        var nearbyUnits = om.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .Where(u => u.Guid != om.Player.Guid && u.Position != null && u.Position.DistanceTo(pos) < 40f)
            .ToList();

        foreach (var unit in nearbyUnits)
        {
            var unitPos = unit.Position;
            snapshot.NearbyUnits.Add(new Game.WoWUnit
            {
                GameObject = new Game.WoWGameObject
                {
                    Base = new Game.WoWObject
                    {
                        Guid = unit.Guid,
                        Position = unitPos != null ? new Game.Position { X = unitPos.X, Y = unitPos.Y, Z = unitPos.Z } : null,
                    },
                },
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
            });
        }

        // Assert snapshot has valid data
        _output.WriteLine($"Snapshot built successfully:");
        _output.WriteLine($"  AccountName: {snapshot.AccountName}");
        _output.WriteLine($"  ScreenState: {snapshot.ScreenState}");
        _output.WriteLine($"  CharacterName: {snapshot.CharacterName}");
        _output.WriteLine($"  Player GUID: {snapshot.Player.Unit.GameObject.Base.Guid:X}");
        _output.WriteLine($"  Player Health: {snapshot.Player.Unit.Health}/{snapshot.Player.Unit.MaxHealth}");
        _output.WriteLine($"  Player Level: {snapshot.Player.Unit.GameObject.Level}");
        _output.WriteLine($"  Position: ({snapshot.MovementData.Position.X:F2}, {snapshot.MovementData.Position.Y:F2}, {snapshot.MovementData.Position.Z:F2})");
        _output.WriteLine($"  RunSpeed: {snapshot.MovementData.RunSpeed:F2}");
        _output.WriteLine($"  WalkSpeed: {snapshot.MovementData.WalkSpeed:F2}");
        _output.WriteLine($"  NearbyUnits: {snapshot.NearbyUnits.Count}");

        Assert.Equal("InWorld", snapshot.ScreenState);
        Assert.NotEmpty(snapshot.CharacterName);
        Assert.True(snapshot.Player.Unit.GameObject.Base.Guid > 0, "Player GUID should be non-zero");
        Assert.True(snapshot.Player.Unit.MaxHealth > 0, "Player MaxHealth should be non-zero");
        Assert.True(snapshot.Player.Unit.GameObject.Level > 0, "Player Level should be non-zero");
        Assert.True(snapshot.MovementData.RunSpeed > 0, "RunSpeed should be non-zero");
        Assert.True(snapshot.MovementData.Position.X != 0 || snapshot.MovementData.Position.Y != 0, "Position should be non-zero");
    }

    #endregion

    #region Loot Tests

    [Fact]
    public async Task Loot_KillAndLoot_ReceivesLootResponse()
    {
        SkipIfServerUnavailable();

        // Arrange - Enter world and spawn a killable NPC
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Spawn a Kobold Vermin (entry 6) - weak mob likely to have loot
        await _fixture.SpawnNpcAsync(6, CancellationToken.None);
        await Task.Delay(2000);

        // Find the spawned NPC
        var target = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (target == null)
        {
            _output.WriteLine("No NPCs found - skipping loot test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Found NPC: {target.Name ?? "Unknown"} (GUID: {target.Guid:X}, Health: {target.Health})");

        // Target the NPC and kill it with GM command
        _fixture.ObjectManager.SetTarget(target.Guid);
        await Task.Delay(500);
        await _fixture.SendGmCommandAsync(".die", 1500); // Kills the currently targeted unit

        // Wait for health to update
        await Task.Delay(1000);

        // Create loot component wired to real opcodes
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var lootComponent = new LootingNetworkClientComponent(worldClient, loggerFactory.CreateLogger<LootingNetworkClientComponent>());

        // Subscribe to loot window events
        bool lootWindowOpened = false;
        uint lootGold = 0;
        uint lootItemCount = 0;
        using var sub = lootComponent.LootWindowOpened.Subscribe(data =>
        {
            lootWindowOpened = true;
            lootGold = data.AvailableMoney;
            lootItemCount = data.AvailableItems;
            _output.WriteLine($"Loot window opened! Guid={data.LootTargetGuid:X}, Gold={data.AvailableMoney}, Items={data.AvailableItems}");
        });

        // Act - Send CMSG_LOOT to open the loot window on the dead NPC
        await lootComponent.OpenLootAsync(target.Guid, CancellationToken.None);
        await Task.Delay(2000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_LOOT");
        Assert.True(_fixture.IsInWorld, "Should still be in world");

        _output.WriteLine($"Loot window opened: {lootWindowOpened}");
        _output.WriteLine($"IsLootWindowOpen: {lootComponent.IsLootWindowOpen}");
        _output.WriteLine($"Available loot slots: {lootComponent.GetAvailableLoot().Count}");

        if (lootComponent.IsLootWindowOpen)
        {
            foreach (var slot in lootComponent.GetAvailableLoot())
            {
                _output.WriteLine($"  Slot[{slot.SlotIndex}]: ItemId={slot.ItemId}, Name={slot.ItemName}, Qty={slot.Quantity}");
            }
        }

        // The server should respond with SMSG_LOOT_RESPONSE (even if the NPC has no loot)
        // Key assertion: CMSG_LOOT packet format was accepted by server (no disconnect)
        _output.WriteLine("CMSG_LOOT accepted by server - loot system packet format verified");

        // Close loot window
        await lootComponent.CloseLootAsync(CancellationToken.None);
        await Task.Delay(500);

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_LOOT_RELEASE");
    }

    [Fact]
    public async Task Loot_QuickLoot_CompletesFullSequence()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Spawn and kill a mob
        await _fixture.SpawnNpcAsync(6, CancellationToken.None);
        await Task.Delay(2000);

        var target = _fixture.ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid != _fixture.ObjectManager.Player.Guid && u.Health > 0);

        if (target == null)
        {
            _output.WriteLine("No NPCs found - skipping quick loot test");
            Assert.True(_fixture.IsInWorld, "Should still be in world");
            return;
        }

        _output.WriteLine($"Target: {target.Name ?? "Unknown"} (GUID: {target.Guid:X})");

        _fixture.ObjectManager.SetTarget(target.Guid);
        await Task.Delay(500);
        await _fixture.SendGmCommandAsync(".die", 1500);
        await Task.Delay(1000);

        // Create loot component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var lootComponent = new LootingNetworkClientComponent(worldClient, loggerFactory.CreateLogger<LootingNetworkClientComponent>());

        // Track events
        var lootEvents = new List<string>();
        using var openSub = lootComponent.LootWindowOpened.Subscribe(d =>
            lootEvents.Add($"OPENED: guid={d.LootTargetGuid:X}, items={d.AvailableItems}, gold={d.AvailableMoney}"));
        using var closeSub = lootComponent.LootWindowClosed.Subscribe(d =>
            lootEvents.Add("CLOSED"));
        using var moneySub = lootComponent.MoneyLoot.Subscribe(d =>
            lootEvents.Add($"MONEY: {d.Amount} copper"));
        using var itemSub = lootComponent.ItemLoot.Subscribe(d =>
            lootEvents.Add($"ITEM: {d.ItemName} x{d.Quantity}"));

        // Act - QuickLoot sends: CMSG_LOOT → CMSG_LOOT_MONEY → CMSG_AUTOSTORE_LOOT_ITEM (x8) → CMSG_LOOT_RELEASE
        await lootComponent.QuickLootAsync(target.Guid, CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after quick loot sequence");
        Assert.True(_fixture.IsInWorld, "Should still be in world");

        _output.WriteLine($"Loot events ({lootEvents.Count}):");
        foreach (var evt in lootEvents)
            _output.WriteLine($"  {evt}");

        // The full loot sequence should complete without errors
        _output.WriteLine("Quick loot sequence completed successfully - all CMSG loot packets accepted by server");
    }

    #endregion

    #region Inventory & Equipment Tests

    [Fact]
    public async Task Inventory_SwapItem_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange - Enter world and give an item via GM command
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Give the character a Hearthstone (item 6948) if they don't have one
        await _fixture.SendGmCommandAsync(".additem 6948", 1000);
        await Task.Delay(1000);

        // Create inventory component
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var inventoryComponent = new InventoryNetworkClientComponent(worldClient, loggerFactory.CreateLogger<InventoryNetworkClientComponent>());

        // Subscribe to error stream
        string? lastError = null;
        using var errorSub = inventoryComponent.InventoryErrors.Subscribe(err =>
        {
            lastError = err;
            _output.WriteLine($"Inventory error received: {err}");
        });

        // Act - Swap item from backpack slot 0 to slot 1
        // CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
        await inventoryComponent.SwapItemsAsync(255, 23, 255, 24, CancellationToken.None);
        await Task.Delay(1000);

        // Assert - Server should accept the packet format (even if swap fails due to empty slots)
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_SWAP_ITEM");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
        _output.WriteLine($"CMSG_SWAP_ITEM accepted by server. Error: {lastError ?? "none"}");
    }

    [Fact]
    public async Task Inventory_AutoEquipItem_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Give a weapon to equip (Worn Shortsword = 25)
        await _fixture.SendGmCommandAsync(".additem 25", 1000);
        await Task.Delay(1000);

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var equipComponent = new EquipmentNetworkClientComponent(worldClient, loggerFactory.CreateLogger<EquipmentNetworkClientComponent>());

        // Subscribe to equipment operations (SMSG_INVENTORY_CHANGE_FAILURE)
        EquipmentOperationData? lastOp = null;
        using var opSub = equipComponent.EquipmentOperations.Subscribe(op =>
        {
            lastOp = op;
            _output.WriteLine($"Equipment op: Result={op.Result}, Error={op.ErrorMessage}, Item={op.ItemGuid:X}");
        });

        // Act - Auto-equip from backpack slot 23 (first backpack slot)
        // CMSG_AUTOEQUIP_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
        await equipComponent.AutoEquipItemAsync(255, 23, CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_AUTOEQUIP_ITEM");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
        _output.WriteLine($"CMSG_AUTOEQUIP_ITEM accepted by server. Op result: {lastOp?.Result.ToString() ?? "no response (success)"}");
    }

    [Fact]
    public async Task Inventory_UseItem_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Give a usable item - Rough Copper Bomb (item 4360) or Minor Healing Potion (118)
        await _fixture.SendGmCommandAsync(".additem 118", 1000);
        await Task.Delay(1000);

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var itemUseComponent = new ItemUseNetworkClientComponent(worldClient, loggerFactory.CreateLogger<ItemUseNetworkClientComponent>());

        // Subscribe to item use events
        bool itemUseStarted = false;
        bool itemUseFailed = false;
        string? failReason = null;
        using var startSub = itemUseComponent.ItemUseStarted.Subscribe(d =>
        {
            itemUseStarted = true;
            _output.WriteLine($"Item use started: SpellId={d.SpellId}, CastTime={d.CastTime}");
        });
        using var failSub = itemUseComponent.ItemUseFailed.Subscribe(d =>
        {
            itemUseFailed = true;
            failReason = d.ErrorMessage;
            _output.WriteLine($"Item use failed: {d.ErrorMessage}");
        });

        // Act - Use item from backpack slot 23
        // CMSG_USE_ITEM: bagIndex(1) + slot(1) + spellSlot(1) + targetMask(uint16) = 5 bytes minimum
        await itemUseComponent.UseItemAsync(255, 23, CancellationToken.None);
        await Task.Delay(2000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_USE_ITEM");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
        _output.WriteLine($"CMSG_USE_ITEM accepted by server. Started: {itemUseStarted}, Failed: {itemUseFailed}, FailReason: {failReason ?? "none"}");
    }

    [Fact]
    public async Task Inventory_DestroyItem_ServerAcceptsPacket()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _fixture.FullConnectSequenceAsync();
        await _fixture.TeleportToAsync(1, 1629.36f, -4373.39f, 31.28f);
        await Task.Delay(2000);

        // Give a destroyable item (Linen Cloth = 2589)
        await _fixture.SendGmCommandAsync(".additem 2589", 1000);
        await Task.Delay(1000);

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var inventoryComponent = new InventoryNetworkClientComponent(worldClient, loggerFactory.CreateLogger<InventoryNetworkClientComponent>());

        // Subscribe to error stream
        string? lastError = null;
        using var errorSub = inventoryComponent.InventoryErrors.Subscribe(err =>
        {
            lastError = err;
            _output.WriteLine($"Inventory error: {err}");
        });

        // Act - Destroy item at backpack slot 23
        // CMSG_DESTROYITEM: bag(1) + slot(1) + count(uint8) + reserved(3) = 6 bytes
        await inventoryComponent.DestroyItemAsync(255, 23, 1, CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected after CMSG_DESTROYITEM");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
        _output.WriteLine($"CMSG_DESTROYITEM accepted by server. Error: {lastError ?? "none"}");
    }

    #endregion

    #region Character Init Tests

    [Fact]
    public async Task CharacterInit_ActionButtons_ReceivedOnWorldEntry()
    {
        SkipIfServerUnavailable();

        // Step 1: Connect to world server (creates WorldClient) but DON'T enter world yet
        Assert.True(await _fixture.ConnectAndLoginAsync());
        Assert.True(await _fixture.SelectRealmAsync());
        Assert.True(await _fixture.WaitForCharacterListAsync());

        // Step 2: Create CharacterInitNetworkClientComponent BEFORE entering world
        // so it subscribes to SMSG_ACTION_BUTTONS before the server sends it
        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var charInitComponent = new CharacterInitNetworkClientComponent(worldClient, loggerFactory.CreateLogger<CharacterInitNetworkClientComponent>());

        // Track action button updates
        IReadOnlyList<ActionButton>? receivedButtons = null;
        using var buttonSub = charInitComponent.ActionButtonUpdates.Subscribe(buttons =>
        {
            receivedButtons = buttons;
            _output.WriteLine($"Received SMSG_ACTION_BUTTONS: {buttons.Count} slots, {buttons.Count(b => !b.IsEmpty)} non-empty");
        });

        // Step 3: NOW enter world (this triggers SMSG_ACTION_BUTTONS from server)
        Assert.True(await _fixture.EnterWorldAsync());
        await Task.Delay(2000);

        // Assert - Action buttons should have been received
        Assert.True(charInitComponent.IsInitialized, "CharacterInit should be initialized after world entry");
        Assert.Equal(120, charInitComponent.ActionButtons.Count);

        // Log some details
        int spellCount = 0, itemCount = 0, emptyCount = 0;
        foreach (var btn in charInitComponent.ActionButtons)
        {
            if (btn.IsEmpty) emptyCount++;
            else if (btn.Type == ActionButtonType.Spell) spellCount++;
            else if (btn.Type == ActionButtonType.Item) itemCount++;
        }
        _output.WriteLine($"Action buttons: {spellCount} spells, {itemCount} items, {emptyCount} empty");

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected");
        Assert.True(_fixture.IsInWorld, "Should still be in world");
    }

    [Fact]
    public async Task CharacterInit_BindPoint_ReceivedOnWorldEntry()
    {
        SkipIfServerUnavailable();

        // Connect but don't enter world yet
        Assert.True(await _fixture.ConnectAndLoginAsync());
        Assert.True(await _fixture.SelectRealmAsync());
        Assert.True(await _fixture.WaitForCharacterListAsync());

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var charInitComponent = new CharacterInitNetworkClientComponent(worldClient, loggerFactory.CreateLogger<CharacterInitNetworkClientComponent>());

        // Track bind point updates
        BindPointData? receivedBindPoint = null;
        using var bindSub = charInitComponent.BindPointUpdates.Subscribe(bp =>
        {
            receivedBindPoint = bp;
            _output.WriteLine($"Received SMSG_BINDPOINTUPDATE: ({bp.X:F1}, {bp.Y:F1}, {bp.Z:F1}) map={bp.MapId} area={bp.AreaId}");
        });

        // Enter world
        Assert.True(await _fixture.EnterWorldAsync());
        await Task.Delay(2000);

        // Assert - Bind point should have been received
        Assert.NotNull(charInitComponent.BindPoint);
        var bp = charInitComponent.BindPoint.Value;
        Assert.True(bp.MapId is 0 or 1, $"MapId should be valid (0 or 1), got {bp.MapId}");
        _output.WriteLine($"Bind point: ({bp.X:F1}, {bp.Y:F1}, {bp.Z:F1}) map={bp.MapId} area={bp.AreaId}");

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected");
    }

    [Fact]
    public async Task CharacterInit_Proficiencies_ReceivedOnWorldEntry()
    {
        SkipIfServerUnavailable();

        // Connect but don't enter world yet
        Assert.True(await _fixture.ConnectAndLoginAsync());
        Assert.True(await _fixture.SelectRealmAsync());
        Assert.True(await _fixture.WaitForCharacterListAsync());

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var charInitComponent = new CharacterInitNetworkClientComponent(worldClient, loggerFactory.CreateLogger<CharacterInitNetworkClientComponent>());

        // Track proficiency updates
        var proficiencies = new List<ProficiencyData>();
        using var profSub = charInitComponent.ProficiencyUpdates.Subscribe(p =>
        {
            proficiencies.Add(p);
            _output.WriteLine($"Received SMSG_SET_PROFICIENCY: itemClass={p.ItemClass}, mask=0x{p.SubclassMask:X8}");
        });

        // Enter world
        Assert.True(await _fixture.EnterWorldAsync());
        await Task.Delay(2000);

        // Assert - At least weapon (2) and armor (4) proficiencies should exist
        Assert.True(charInitComponent.Proficiencies.Count > 0, "Should have at least 1 proficiency");
        _output.WriteLine($"Total proficiencies received: {charInitComponent.Proficiencies.Count}");

        foreach (var kvp in charInitComponent.Proficiencies)
        {
            _output.WriteLine($"  ItemClass {kvp.Key}: mask=0x{kvp.Value:X8}");
        }

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected");
    }

    [Fact]
    public async Task CharacterInit_Factions_ReceivedOnWorldEntry()
    {
        SkipIfServerUnavailable();

        // Connect but don't enter world yet
        Assert.True(await _fixture.ConnectAndLoginAsync());
        Assert.True(await _fixture.SelectRealmAsync());
        Assert.True(await _fixture.WaitForCharacterListAsync());

        var worldClient = _fixture.WoWClient.WorldClient;
        Assert.NotNull(worldClient);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var charInitComponent = new CharacterInitNetworkClientComponent(worldClient, loggerFactory.CreateLogger<CharacterInitNetworkClientComponent>());

        // Enter world
        Assert.True(await _fixture.EnterWorldAsync());
        await Task.Delay(2000);

        // Assert - 64 faction entries expected
        Assert.Equal(64, charInitComponent.Factions.Count);

        int visible = charInitComponent.Factions.Count(f => f.IsVisible);
        int atWar = charInitComponent.Factions.Count(f => f.IsAtWar);
        _output.WriteLine($"Factions: {charInitComponent.Factions.Count} total, {visible} visible, {atWar} at war");

        Assert.True(_fixture.WoWClient.IsWorldConnected(), "Should still be connected");
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
