using BotRunner;
using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Test runner that boots up a BotRunnerService for integration testing.
/// Uses GM commands to set up test scenarios and validates bot behavior.
/// 
/// Prerequisites:
/// - WoW emulator server must be running externally
/// - PathfindingService will be started automatically if not running
/// </summary>
public class BotRunnerTester : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LiveServerFixture _serverFixture;

    private BotRunnerService? _botRunner;
    private CharacterStateUpdateClient? _characterStateUpdateClient;
    private CancellationTokenSource? _botCancellation;
    private Task? _botTask;

    public WoWSharpObjectManager ObjectManager => _serverFixture.ObjectManager;
    public BotRunnerService? BotRunner => _botRunner;
    public bool IsBotRunning => _botTask != null && !_botTask.IsCompleted;

    /// <summary>
    /// Indicates whether the external WoW server is available.
    /// </summary>
    public bool IsServerAvailable => _serverFixture.IsServerAvailable;

    /// <summary>
    /// Indicates whether PathfindingService is available.
    /// </summary>
    public bool IsPathfindingServiceAvailable => _serverFixture.IsPathfindingServiceAvailable;

    public BotRunnerTester(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _serverFixture = new LiveServerFixture(output);
    }

    public async Task InitializeAsync()
    {
        await _serverFixture.InitializeAsync();
    }

    /// <summary>
    /// Ensures the server is available before running a test.
    /// Throws SkipException if server is not available.
    /// </summary>
    public void EnsureServerAvailable()
    {
        _serverFixture.EnsureServerAvailable();
    }

    /// <summary>
    /// Connects to the server and enters the world, preparing for bot testing.
    /// </summary>
    public async Task<bool> ConnectAndPrepareAsync(
        string username = TestAccountSettings.TestAccountUsername,
        string password = TestAccountSettings.TestAccountPassword,
        CancellationToken cancellationToken = default)
    {
        EnsureServerAvailable();

        var connected = await _serverFixture.FullConnectSequenceAsync(username, password, cancellationToken);
        if (!connected)
        {
            Log("Failed to connect to server");
            return false;
        }

        // Initialize the CharacterStateUpdateClient
        _characterStateUpdateClient = new CharacterStateUpdateClient(
            TestAccountSettings.CharacterStateListenerIpAddress,
            TestAccountSettings.CharacterStateListenerPort,
            _loggerFactory.CreateLogger<CharacterStateUpdateClient>()
        );

        return true;
    }

    /// <summary>
    /// Starts the BotRunnerService in test mode.
    /// </summary>
    public void StartBotRunner()
    {
        if (_botRunner != null)
        {
            Log("BotRunner already started");
            return;
        }

        Log("Starting BotRunnerService...");

        // Create stub services for testing - the actual combat/looting/positioning
        // behaviors are not tested through this integration tester
        var targetEngagementService = new StubTargetEngagementService();
        var lootingService = new StubLootingService();
        var targetPositioningService = new StubTargetPositioningService();

        _botRunner = new BotRunnerService(
            ObjectManager,
            _characterStateUpdateClient!,
            targetEngagementService,
            lootingService,
            targetPositioningService
        );

        _botCancellation = new CancellationTokenSource();
        _botTask = Task.Run(() =>
        {
            _botRunner.Start();
        }, _botCancellation.Token);

        Log("BotRunnerService started");
    }

    /// <summary>
    /// Stops the BotRunnerService.
    /// </summary>
    public async Task StopBotRunnerAsync()
    {
        if (_botCancellation != null)
        {
            _botCancellation.Cancel();
            if (_botTask != null)
            {
                try
                {
                    await Task.WhenAny(_botTask, Task.Delay(5000));
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            _botCancellation.Dispose();
            _botCancellation = null;
            _botTask = null;
        }
        _botRunner = null;
        Log("BotRunnerService stopped");
    }

    #region Scenario Setup Methods

    /// <summary>
    /// Sets up a basic grinding scenario with the player at a known location.
    /// </summary>
    public async Task SetupGrindingScenarioAsync(
        uint mapId = 1,
        float x = 1629.36f,
        float y = -4373.39f,
        float z = 31.28f,
        int playerLevel = 60,
        CancellationToken cancellationToken = default)
    {
        Log($"Setting up grinding scenario at ({x}, {y}, {z}) on map {mapId}");

        await _serverFixture.TeleportToAsync(mapId, x, y, z, cancellationToken);
        await _serverFixture.SetLevelAsync(playerLevel, cancellationToken);
        await _serverFixture.FullHealAsync(cancellationToken);
        await _serverFixture.LearnAllSpellsAsync(cancellationToken);
        await _serverFixture.ResetCooldownsAsync(cancellationToken);

        // Add basic consumables
        await _serverFixture.AddItemAsync(5479, 20, cancellationToken); // Food
        await _serverFixture.AddItemAsync(1179, 20, cancellationToken); // Drink

        Log("Grinding scenario setup complete");
    }

    /// <summary>
    /// Sets up a combat scenario by spawning an NPC near the player.
    /// </summary>
    public async Task SetupCombatScenarioAsync(
        int npcEntry,
        int npcCount = 1,
        CancellationToken cancellationToken = default)
    {
        Log($"Setting up combat scenario with NPC entry {npcEntry} x{npcCount}");

        for (int i = 0; i < npcCount; i++)
        {
            await _serverFixture.SpawnNpcAsync(npcEntry, cancellationToken);
        }

        Log("Combat scenario setup complete");
    }

    /// <summary>
    /// Sets up a rest scenario by damaging the player.
    /// </summary>
    public async Task SetupRestScenarioAsync(int healthPercent = 50, int manaPercent = 30, CancellationToken cancellationToken = default)
    {
        Log($"Setting up rest scenario with {healthPercent}% health and {manaPercent}% mana");

        var player = ObjectManager.Player;
        var targetHealth = (int)(player.MaxHealth * healthPercent / 100);
        var targetMana = (int)(player.MaxMana * manaPercent / 100);

        await _serverFixture.SendGmCommandAsync($".mod hp {targetHealth}", 500, cancellationToken);
        await _serverFixture.SendGmCommandAsync($".mod mana {targetMana}", 500, cancellationToken);

        Log("Rest scenario setup complete");
    }

    /// <summary>
    /// Sets up a travel scenario by teleporting the player far from a destination.
    /// </summary>
    public async Task SetupTravelScenarioAsync(
        Position destination,
        CancellationToken cancellationToken = default)
    {
        Log($"Setting up travel scenario to ({destination.X}, {destination.Y}, {destination.Z})");

        // Calculate a starting position some distance away
        var startX = destination.X - 100; // 100 yards away
        var startY = destination.Y;
        var startZ = destination.Z;

        await _serverFixture.TeleportToAsync(1, startX, startY, startZ, cancellationToken);

        Log("Travel scenario setup complete");
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Waits for a condition to become true within a timeout.
    /// </summary>
    public async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (condition())
            {
                Log($"Condition met: {description}");
                return true;
            }
            await Task.Delay(TestAccountSettings.PollingIntervalMs, cancellationToken);
        }
        Log($"Condition timeout: {description}");
        return false;
    }

    /// <summary>
    /// Validates that the player's health is above a threshold.
    /// </summary>
    public bool ValidateHealthAbove(float threshold)
    {
        var healthPercent = ObjectManager.Player.HealthPercent;
        Log($"Health validation: {healthPercent:F1}% (threshold: {threshold}%)");
        return healthPercent >= threshold;
    }

    /// <summary>
    /// Validates that the player has moved from their original position.
    /// </summary>
    public bool ValidatePlayerMoved(Position originalPosition, float minDistance = 1.0f)
    {
        var currentPosition = ObjectManager.Player.Position;
        var distance = originalPosition.DistanceTo(currentPosition);
        Log($"Movement validation: moved {distance:F2} yards (min: {minDistance})");
        return distance >= minDistance;
    }

    /// <summary>
    /// Validates that an NPC was killed (no longer exists or is dead).
    /// </summary>
    public bool ValidateNpcKilled(ulong npcGuid)
    {
        var npc = ObjectManager.Objects
            .OfType<WoWSharpClient.Models.WoWUnit>()
            .FirstOrDefault(u => u.Guid == npcGuid);

        if (npc == null)
        {
            Log($"NPC {npcGuid:X} no longer exists (killed or despawned)");
            return true;
        }

        var isDead = npc.Health == 0;
        Log($"NPC {npcGuid:X} health: {npc.Health}/{npc.MaxHealth} (dead: {isDead})");
        return isDead;
    }

    /// <summary>
    /// Validates that the player is in combat.
    /// </summary>
    public bool ValidateInCombat()
    {
        var inCombat = ObjectManager.Player.IsInCombat;
        Log($"Combat validation: in combat = {inCombat}");
        return inCombat;
    }

    /// <summary>
    /// Validates that the player is eating or drinking.
    /// </summary>
    public bool ValidateIsResting()
    {
        var isEating = ObjectManager.Player.IsEating;
        var isDrinking = ObjectManager.Player.IsDrinking;
        Log($"Rest validation: eating = {isEating}, drinking = {isDrinking}");
        return isEating || isDrinking;
    }

    /// <summary>
    /// Validates player position is near target.
    /// </summary>
    public bool ValidateNearPosition(Position target, float tolerance = 5.0f)
    {
        var distance = ObjectManager.Player.Position.DistanceTo(target);
        Log($"Position validation: {distance:F2} yards from target (tolerance: {tolerance})");
        return distance <= tolerance;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets a snapshot of the current player state for comparison.
    /// </summary>
    public PlayerStateSnapshot GetPlayerSnapshot()
    {
        return new PlayerStateSnapshot
        {
            Position = new Position(
                ObjectManager.Player.Position.X,
                ObjectManager.Player.Position.Y,
                ObjectManager.Player.Position.Z),
            Health = ObjectManager.Player.Health,
            MaxHealth = ObjectManager.Player.MaxHealth,
            Mana = ObjectManager.Player.Mana,
            MaxMana = ObjectManager.Player.MaxMana,
            IsInCombat = ObjectManager.Player.IsInCombat,
            IsEating = ObjectManager.Player.IsEating,
            IsDrinking = ObjectManager.Player.IsDrinking,
            Facing = ObjectManager.Player.Facing,
            Level = ObjectManager.Player.Level,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Sends a GM command through the fixture.
    /// </summary>
    public async Task SendGmCommandAsync(string command, int delayAfterMs = 500, CancellationToken cancellationToken = default)
    {
        await _serverFixture.SendGmCommandAsync(command, delayAfterMs, cancellationToken);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _output.WriteLine($"[{timestamp}][BotTester] {message}");
        Console.WriteLine($"[{timestamp}][BotRunnerTester] {message}");
    }

    #endregion

    public async Task DisposeAsync()
    {
        await StopBotRunnerAsync();
        await _serverFixture.DisposeAsync();
    }
}

/// <summary>
/// Snapshot of player state for comparison in tests.
/// </summary>
public record PlayerStateSnapshot
{
    public Position Position { get; init; } = new(0, 0, 0);
    public uint Health { get; init; }
    public uint MaxHealth { get; init; }
    public uint Mana { get; init; }
    public uint MaxMana { get; init; }
    public bool IsInCombat { get; init; }
    public bool IsEating { get; init; }
    public bool IsDrinking { get; init; }
    public float Facing { get; init; }
    public uint Level { get; init; }
    public DateTime Timestamp { get; init; }

    public float HealthPercent => MaxHealth > 0 ? (float)Health / MaxHealth * 100 : 0;
    public float ManaPercent => MaxMana > 0 ? (float)Mana / MaxMana * 100 : 0;
}

/// <summary>
/// Stub implementation of ITargetEngagementService for testing.
/// </summary>
internal class StubTargetEngagementService : ITargetEngagementService
{
    public ulong? CurrentTargetGuid => null;

    public Task EngageAsync(IWoWUnit target, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Stub implementation of ILootingService for testing.
/// </summary>
internal class StubLootingService : ILootingService
{
    public Task<bool> TryLootAsync(ulong targetGuid, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// Stub implementation of ITargetPositioningService for testing.
/// </summary>
internal class StubTargetPositioningService : ITargetPositioningService
{
    public bool EnsureInCombatRange(IWoWUnit target)
    {
        return true;
    }
}
