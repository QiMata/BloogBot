using BotRunner.Clients;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using WoWSharpClient.Client;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Test fixture for integration tests that require a live WoW server connection.
/// 
/// Prerequisites (all services must be running externally):
/// - WoW emulator server (realmd + mangosd) must be running
/// - PathfindingService must be running
/// - Test accounts must exist with GM level 3
/// 
/// This fixture does NOT start any services - it expects them to be running.
/// Tests will be skipped gracefully if services are not available.
/// </summary>
public class LiveServerFixture : IAsyncLifetime
{
    private readonly ITestOutputHelper? _output;
    private readonly ILoggerFactory _loggerFactory;

    public WoWClient WoWClient { get; private set; } = null!;
    public PathfindingClient PathfindingClient { get; private set; } = null!;
    public WoWSharpObjectManager ObjectManager => WoWSharpObjectManager.Instance;
    public bool IsConnected { get; private set; }
    public bool IsInWorld { get; private set; }

    /// <summary>
    /// Indicates whether the external WoW server is available.
    /// </summary>
    public bool IsServerAvailable { get; private set; }

    /// <summary>
    /// Indicates whether the PathfindingService is available.
    /// </summary>
    public bool IsPathfindingServiceAvailable { get; private set; }

    public LiveServerFixture(ITestOutputHelper? output = null)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    public async Task InitializeAsync()
    {
        // Check if external WoW server is running
        IsServerAvailable = await CheckServiceAvailableAsync(
            TestAccountSettings.ServerIpAddress,
            TestAccountSettings.AuthPort,
            "WoW Auth Server");

        if (!IsServerAvailable)
        {
            Log("WARNING: WoW server is not running. Integration tests will be skipped.");
            Log($"Expected server at {TestAccountSettings.ServerIpAddress}:{TestAccountSettings.AuthPort}");
            return;
        }

        // Check if PathfindingService is running (expected to be running externally)
        IsPathfindingServiceAvailable = await CheckServiceAvailableAsync(
            TestAccountSettings.PathfindingServiceIpAddress,
            TestAccountSettings.PathfindingServicePort,
            "PathfindingService");

        if (!IsPathfindingServiceAvailable)
        {
            Log("WARNING: PathfindingService is not running. Some tests may fail.");
            Log($"Expected PathfindingService at {TestAccountSettings.PathfindingServiceIpAddress}:{TestAccountSettings.PathfindingServicePort}");
        }

        // Initialize clients
        WoWClient = new WoWClient();
        WoWClient.SetIpAddress(TestAccountSettings.ServerIpAddress);

        PathfindingClient = new PathfindingClient(
            TestAccountSettings.PathfindingServiceIpAddress,
            TestAccountSettings.PathfindingServicePort,
            _loggerFactory.CreateLogger<PathfindingClient>()
        );

        // Initialize ObjectManager
        WoWSharpObjectManager.Instance.Initialize(
            WoWClient,
            PathfindingClient,
            _loggerFactory.CreateLogger<WoWSharpObjectManager>()
        );

        Log("LiveServerFixture initialized successfully");
    }

    /// <summary>
    /// Checks if a TCP service is available at the specified address and port.
    /// </summary>
    private async Task<bool> CheckServiceAvailableAsync(string ipAddress, int port, string serviceName)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TestAccountSettings.ServiceHealthCheckTimeoutMs));

            if (connectTask.IsCompletedSuccessfully && client.Connected)
            {
                Log($"{serviceName} is available at {ipAddress}:{port}");
                return true;
            }

            Log($"{serviceName} is NOT available at {ipAddress}:{port}");
            return false;
        }
        catch (Exception ex)
        {
            Log($"{serviceName} check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures the server is available before running a test.
    /// Call this at the start of each test method.
    /// </summary>
    public void EnsureServerAvailable()
    {
        if (!IsServerAvailable)
        {
            throw new SkipException("WoW server is not running. Start the server and re-run tests.");
        }
    }

    /// <summary>
    /// Connects to the auth server and logs in with the specified credentials.
    /// </summary>
    public async Task<bool> ConnectAndLoginAsync(
        string username = TestAccountSettings.TestAccountUsername,
        string password = TestAccountSettings.TestAccountPassword,
        CancellationToken cancellationToken = default)
    {
        EnsureServerAvailable();

        Log($"Connecting to auth server at {TestAccountSettings.ServerIpAddress}:{TestAccountSettings.AuthPort}");

        try
        {
            WoWClient.Login(username, password);

            // Wait for login to complete
            var startTime = DateTime.UtcNow;
            while (!WoWClient.IsLoggedIn &&
                   (DateTime.UtcNow - startTime).TotalMilliseconds < TestAccountSettings.ConnectionTimeoutMs)
            {
                await Task.Delay(TestAccountSettings.PollingIntervalMs, cancellationToken);
            }

            IsConnected = WoWClient.IsLoggedIn;
            Log($"Login result: {(IsConnected ? "SUCCESS" : "FAILED")}");
            return IsConnected;
        }
        catch (Exception ex)
        {
            Log($"Login failed with exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Selects the first available realm after login.
    /// </summary>
    public async Task<bool> SelectRealmAsync(CancellationToken cancellationToken = default)
    {
        if (!WoWClient.IsLoggedIn)
        {
            Log("Cannot select realm - not logged in");
            return false;
        }

        Log("Fetching realm list...");
        var realms = WoWClient.GetRealmList();

        if (realms.Count == 0)
        {
            Log("No realms available");
            return false;
        }

        Log($"Found {realms.Count} realm(s). Selecting first realm: {realms[0].RealmName}");
        WoWClient.SelectRealm(realms[0]);

        // Wait for world connection
        var startTime = DateTime.UtcNow;
        while (!WoWClient.IsWorldConnected() &&
               (DateTime.UtcNow - startTime).TotalMilliseconds < TestAccountSettings.ConnectionTimeoutMs)
        {
            await Task.Delay(TestAccountSettings.PollingIntervalMs, cancellationToken);
        }

        Log($"World connection: {(WoWClient.IsWorldConnected() ? "SUCCESS" : "FAILED")}");
        return WoWClient.IsWorldConnected();
    }

    /// <summary>
    /// Waits for the character list to be received from the server.
    /// </summary>
    public async Task<bool> WaitForCharacterListAsync(CancellationToken cancellationToken = default)
    {
        Log("Waiting for character list...");

        var startTime = DateTime.UtcNow;
        while (!ObjectManager.CharacterSelectScreen.HasReceivedCharacterList &&
               (DateTime.UtcNow - startTime).TotalMilliseconds < TestAccountSettings.ConnectionTimeoutMs)
        {
            await Task.Delay(TestAccountSettings.PollingIntervalMs, cancellationToken);
        }

        if (ObjectManager.CharacterSelectScreen.HasReceivedCharacterList)
        {
            Log($"Received {ObjectManager.CharacterSelectScreen.CharacterSelects.Count} character(s)");
            return true;
        }

        Log("Timeout waiting for character list");
        return false;
    }

    /// <summary>
    /// Enters the world with the specified character (or first character if not specified).
    /// </summary>
    public async Task<bool> EnterWorldAsync(ulong? characterGuid = null, CancellationToken cancellationToken = default)
    {
        if (!ObjectManager.CharacterSelectScreen.HasReceivedCharacterList ||
            ObjectManager.CharacterSelectScreen.CharacterSelects.Count == 0)
        {
            Log("No characters available to enter world");
            return false;
        }

        var guid = characterGuid ?? ObjectManager.CharacterSelectScreen.CharacterSelects[0].Guid;
        var character = ObjectManager.CharacterSelectScreen.CharacterSelects.FirstOrDefault(c => c.Guid == guid);

        if (character == null)
        {
            Log($"Character with GUID {guid} not found");
            return false;
        }

        Log($"Entering world with character: {character.Name} (Level {character.Level} {character.Race} {character.Class})");
        ObjectManager.EnterWorld(guid);

        // Wait for world entry
        var startTime = DateTime.UtcNow;
        while (!ObjectManager.HasEnteredWorld &&
               (DateTime.UtcNow - startTime).TotalMilliseconds < TestAccountSettings.WorldEntryTimeoutMs)
        {
            await Task.Delay(TestAccountSettings.PollingIntervalMs, cancellationToken);
        }

        IsInWorld = ObjectManager.HasEnteredWorld;
        Log($"World entry: {(IsInWorld ? "SUCCESS" : "FAILED")}");
        return IsInWorld;
    }

    /// <summary>
    /// Full connection sequence: Login -> Select Realm -> Wait for Characters -> Enter World
    /// </summary>
    public async Task<bool> FullConnectSequenceAsync(
        string username = TestAccountSettings.TestAccountUsername,
        string password = TestAccountSettings.TestAccountPassword,
        CancellationToken cancellationToken = default)
    {
        EnsureServerAvailable();

        if (!await ConnectAndLoginAsync(username, password, cancellationToken))
            return false;

        if (!await SelectRealmAsync(cancellationToken))
            return false;

        if (!await WaitForCharacterListAsync(cancellationToken))
            return false;

        if (!await EnterWorldAsync(cancellationToken: cancellationToken))
            return false;

        // Give the server a moment to fully sync
        await Task.Delay(1000, cancellationToken);

        return true;
    }

    /// <summary>
    /// Sends a GM command via chat message and waits for it to be processed.
    /// </summary>
    public async Task SendGmCommandAsync(string command, int delayAfterMs = 500, CancellationToken cancellationToken = default)
    {
        if (!IsInWorld)
        {
            throw new InvalidOperationException("Cannot send GM command - not in world");
        }

        Log($"Executing GM command: {command}");
        ObjectManager.SendChatMessage(command);
        await Task.Delay(delayAfterMs, cancellationToken);
    }

    /// <summary>
    /// Teleports the player to specified coordinates using GM command.
    /// </summary>
    public async Task TeleportToAsync(uint mapId, float x, float y, float z, CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync($".go {mapId} {x} {y} {z}", 2000, cancellationToken);
    }

    /// <summary>
    /// Teleports the player to a named location using GM command.
    /// </summary>
    public async Task TeleportToLocationAsync(string locationName, CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync($".go {locationName}", 2000, cancellationToken);
    }

    /// <summary>
    /// Sets the player's level using GM command.
    /// </summary>
    public async Task SetLevelAsync(int level, CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync($".character level {level}", 500, cancellationToken);
    }

    /// <summary>
    /// Adds an item to the player's inventory using GM command.
    /// </summary>
    public async Task AddItemAsync(int itemId, int count = 1, CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync($".additem {itemId} {count}", 500, cancellationToken);
    }

    /// <summary>
    /// Spawns an NPC at the player's location using GM command.
    /// </summary>
    public async Task SpawnNpcAsync(int entryId, CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync($".npc add {entryId}", 1000, cancellationToken);
    }

    /// <summary>
    /// Removes all spawned NPCs around the player using GM command.
    /// </summary>
    public async Task DespawnNearbyNpcsAsync(CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync(".npc delete", 500, cancellationToken);
    }

    /// <summary>
    /// Fully heals the player using GM command.
    /// </summary>
    public async Task FullHealAsync(CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync(".die", 500, cancellationToken); // Reset state
        await SendGmCommandAsync(".revive", 500, cancellationToken);
        await SendGmCommandAsync(".mod hp 99999", 500, cancellationToken);
        await SendGmCommandAsync(".mod mana 99999", 500, cancellationToken);
    }

    /// <summary>
    /// Repairs all items using GM command.
    /// </summary>
    public async Task RepairItemsAsync(CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync(".repairitems", 500, cancellationToken);
    }

    /// <summary>
    /// Grants all spells/abilities for the class using GM command.
    /// </summary>
    public async Task LearnAllSpellsAsync(CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync(".learn all", 1000, cancellationToken);
    }

    /// <summary>
    /// Resets all cooldowns using GM command.
    /// </summary>
    public async Task ResetCooldownsAsync(CancellationToken cancellationToken = default)
    {
        await SendGmCommandAsync(".cooldown", 500, cancellationToken);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _output?.WriteLine($"[{timestamp}] {message}");
        Console.WriteLine($"[{timestamp}][LiveServerFixture] {message}");
    }

    public Task DisposeAsync()
    {
        WoWClient?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Exception thrown when a test should be skipped due to missing prerequisites.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}

/// <summary>
/// Collection fixture for sharing LiveServerFixture across tests that need the same connection.
/// </summary>
[CollectionDefinition("LiveServerTests")]
public class LiveServerCollection : ICollectionFixture<LiveServerFixture>
{
}
