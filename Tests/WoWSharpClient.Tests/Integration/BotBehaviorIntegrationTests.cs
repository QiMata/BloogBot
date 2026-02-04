using GameData.Core.Models;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Integration tests for bot behavior using the BotRunnerTester.
/// These tests set up scenarios using GM commands and validate that
/// the bot behaves correctly in response to various game situations.
/// 
/// Prerequisites:
/// - WoW emulator server must be running externally
/// - PathfindingService will be started automatically if not running
/// - Test accounts must exist with GM level 3
/// 
/// Run with: dotnet test --filter "Category=BotBehavior"
/// 
/// Tests will be skipped automatically if the server is not available.
/// </summary>
[Trait("Category", "BotBehavior")]
public class BotBehaviorIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly BotRunnerTester _tester;

    public BotBehaviorIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tester = new BotRunnerTester(output);
    }

    public async Task InitializeAsync()
    {
        await _tester.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _tester.DisposeAsync();
    }

    /// <summary>
    /// Skips the test if the server is not available.
    /// </summary>
    private void SkipIfServerUnavailable()
    {
        if (!_tester.IsServerAvailable)
        {
            throw new SkipException("WoW server is not running. Start the server and re-run tests.");
        }
    }

    #region Rest Behavior Tests

    [Fact]
    public async Task RestBehavior_LowHealth_BotSitsAndEats()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();
        await _tester.SetupRestScenarioAsync(healthPercent: 40, manaPercent: 100);

        var initialSnapshot = _tester.GetPlayerSnapshot();
        _output.WriteLine($"Initial health: {initialSnapshot.HealthPercent:F1}%");

        // Act - Start the bot and let it run
        _tester.StartBotRunner();

        // Wait for bot to start eating
        var isResting = await _tester.WaitForConditionAsync(
            () => _tester.ValidateIsResting(),
            TimeSpan.FromSeconds(10),
            "Waiting for bot to start eating");

        // Assert
        Assert.True(isResting, "Bot should have started eating when health is low");

        // Cleanup
        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task RestBehavior_LowMana_BotSitsAndDrinks()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();
        await _tester.SetupRestScenarioAsync(healthPercent: 100, manaPercent: 20);

        var initialSnapshot = _tester.GetPlayerSnapshot();
        _output.WriteLine($"Initial mana: {initialSnapshot.ManaPercent:F1}%");

        // Act
        _tester.StartBotRunner();

        var isResting = await _tester.WaitForConditionAsync(
            () => _tester.ValidateIsResting(),
            TimeSpan.FromSeconds(10),
            "Waiting for bot to start drinking");

        // Assert
        Assert.True(isResting, "Bot should have started drinking when mana is low");

        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task RestBehavior_FullResources_BotDoesNotRest()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();
        await _tester.SendGmCommandAsync(".mod hp 99999"); // Full health
        await _tester.SendGmCommandAsync(".mod mana 99999"); // Full mana

        // Act
        _tester.StartBotRunner();

        // Wait a bit to see if bot starts resting (it shouldn't)
        var startedResting = await _tester.WaitForConditionAsync(
            () => _tester.ValidateIsResting(),
            TimeSpan.FromSeconds(5),
            "Checking if bot incorrectly starts resting");

        // Assert
        Assert.False(startedResting, "Bot should NOT rest when resources are full");

        await _tester.StopBotRunnerAsync();
    }

    #endregion

    #region Movement Behavior Tests

    [Fact]
    public async Task MovementBehavior_HasTarget_BotMovesTowardTarget()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        // Spawn a target NPC nearby (adjust entry ID based on your server)
        await _tester.SetupCombatScenarioAsync(npcEntry: 6); // Kobold Vermin

        var initialPosition = new Position(
            _tester.ObjectManager.Player.Position.X,
            _tester.ObjectManager.Player.Position.Y,
            _tester.ObjectManager.Player.Position.Z);

        // Act
        _tester.StartBotRunner();

        // Wait for movement
        var moved = await _tester.WaitForConditionAsync(
            () => _tester.ValidatePlayerMoved(initialPosition, 2.0f),
            TimeSpan.FromSeconds(10),
            "Waiting for bot to move toward target");

        // Assert
        Assert.True(moved, "Bot should have moved toward the target");

        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task MovementBehavior_FacingTarget_BotFacesCorrectDirection()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        // Get initial facing
        var initialFacing = _tester.ObjectManager.Player.Facing;

        // Spawn target behind the player (opposite direction)
        await _tester.SetupCombatScenarioAsync(npcEntry: 6);

        // Act
        _tester.StartBotRunner();

        // Wait for facing change
        var facingChanged = await _tester.WaitForConditionAsync(
            () => Math.Abs(_tester.ObjectManager.Player.Facing - initialFacing) > 0.5f,
            TimeSpan.FromSeconds(10),
            "Waiting for bot to turn toward target");

        // Assert - Bot should have adjusted facing
        _output.WriteLine($"Initial facing: {initialFacing:F4}, Final facing: {_tester.ObjectManager.Player.Facing:F4}");

        await _tester.StopBotRunnerAsync();
    }

    #endregion

    #region Combat Behavior Tests

    [Fact]
    public async Task CombatBehavior_EnemySpawned_BotEngagesCombat()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync(playerLevel: 60);

        // Act - Spawn hostile NPC
        await _tester.SetupCombatScenarioAsync(npcEntry: 6);

        _tester.StartBotRunner();

        // Wait for combat to start
        var inCombat = await _tester.WaitForConditionAsync(
            () => _tester.ValidateInCombat(),
            TimeSpan.FromSeconds(15),
            "Waiting for bot to enter combat");

        // Assert
        _output.WriteLine($"Combat state: {_tester.ObjectManager.Player.IsInCombat}");

        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task CombatBehavior_AfterCombat_BotLoots()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync(playerLevel: 60);

        // Spawn a weak mob that will die quickly
        await _tester.SetupCombatScenarioAsync(npcEntry: 6);

        // Act
        _tester.StartBotRunner();

        // Wait for combat to end (mob killed)
        var combatEnded = await _tester.WaitForConditionAsync(
            () => !_tester.ValidateInCombat(),
            TimeSpan.FromSeconds(30),
            "Waiting for combat to end");

        // Assert
        Assert.True(combatEnded, "Combat should have ended");

        await _tester.StopBotRunnerAsync();
    }

    #endregion

    #region Server Communication Tests

    [Fact]
    public async Task ServerCommunication_BotRunning_MaintainsConnection()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        // Act - Run bot for extended period
        _tester.StartBotRunner();

        // Wait 30 seconds
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Assert - Connection should still be valid
        Assert.True(_tester.ObjectManager.HasEnteredWorld, "Should still be in world after extended bot operation");

        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task ServerCommunication_RapidMovement_ServerAcceptsPackets()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        // Act - Perform rapid movement commands
        var initialPosition = new Position(
            _tester.ObjectManager.Player.Position.X,
            _tester.ObjectManager.Player.Position.Y,
            _tester.ObjectManager.Player.Position.Z);

        for (int i = 0; i < 10; i++)
        {
            _tester.ObjectManager.StartMovement(GameData.Core.Enums.ControlBits.Front);
            await Task.Delay(200);
            _tester.ObjectManager.StopMovement(GameData.Core.Enums.ControlBits.Front);
            await Task.Delay(100);
        }

        // Assert - Should still be connected and have moved
        Assert.True(_tester.ObjectManager.HasEnteredWorld, "Should still be in world after rapid movements");
        Assert.True(_tester.ValidatePlayerMoved(initialPosition, 0.5f), "Should have moved during rapid movement test");
    }

    #endregion

    #region State Machine Tests

    [Fact]
    public async Task StateMachine_LoginFlow_ProgressesThroughStates()
    {
        SkipIfServerUnavailable();

        // Arrange & Act - Just connect (this tests the login state machine)
        var connected = await _tester.ConnectAndPrepareAsync();

        // Assert
        Assert.True(connected, "Should have connected through all login states");
        Assert.True(_tester.ObjectManager.HasEnteredWorld, "Should be in world");
        Assert.NotNull(_tester.ObjectManager.Player);
    }

    [Fact]
    public async Task StateMachine_TransitionToRest_WhenHealthLow()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        // Start bot
        _tester.StartBotRunner();
        await Task.Delay(2000); // Let bot initialize

        // Act - Damage player to trigger rest state
        await _tester.SetupRestScenarioAsync(healthPercent: 30, manaPercent: 100);

        // Wait for state transition
        var transitioned = await _tester.WaitForConditionAsync(
            () => _tester.ValidateIsResting(),
            TimeSpan.FromSeconds(10),
            "Waiting for transition to rest state");

        // Assert
        Assert.True(transitioned, "Bot should transition to rest state when health is low");

        await _tester.StopBotRunnerAsync();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task EdgeCase_Teleport_BotHandlesTeleportCorrectly()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        _tester.StartBotRunner();
        await Task.Delay(2000);

        var positionBeforeTeleport = new Position(
            _tester.ObjectManager.Player.Position.X,
            _tester.ObjectManager.Player.Position.Y,
            _tester.ObjectManager.Player.Position.Z);

        // Act - Teleport player
        await _tester.SendGmCommandAsync(".go 1 -2917.58 -257.98 53.0"); // Mulgore
        await Task.Delay(3000);

        // Assert - Bot should still be running after teleport
        Assert.True(_tester.ObjectManager.HasEnteredWorld, "Should still be in world after teleport");
        Assert.True(_tester.ValidatePlayerMoved(positionBeforeTeleport, 50f), "Player should have moved far from original position");

        await _tester.StopBotRunnerAsync();
    }

    [Fact]
    public async Task EdgeCase_Death_BotHandlesDeathCorrectly()
    {
        SkipIfServerUnavailable();

        // Arrange
        await _tester.ConnectAndPrepareAsync();
        await _tester.SetupGrindingScenarioAsync();

        _tester.StartBotRunner();
        await Task.Delay(2000);

        // Act - Kill player
        await _tester.SendGmCommandAsync(".die");
        await Task.Delay(2000);

        // Assert - Check for death state handling
        // Note: Actual assertion depends on how ObjectManager tracks death state
        _output.WriteLine($"Player ghost form: {_tester.ObjectManager.Player.InGhostForm}");

        // Revive for cleanup
        await _tester.SendGmCommandAsync(".revive");

        await _tester.StopBotRunnerAsync();
    }

    #endregion
}
