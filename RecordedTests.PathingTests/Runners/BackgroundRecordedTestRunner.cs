using System.Security.Cryptography;
using System.Text;
using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RecordedTests.PathingTests.Models;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using WoWSharpClient;
using WoWSharpClient.Client;

namespace RecordedTests.PathingTests.Runners;

/// <summary>
/// Background bot runner that executes the actual pathfinding test by connecting to the WoW server,
/// navigating from start to end position, and handling transport scenarios.
/// </summary>
public class BackgroundRecordedTestRunner : IBotRunner
{
    private readonly PathingTestDefinition _testDefinition;
    private readonly PathfindingClient _pathfindingClient;
    private readonly string _account;
    private readonly string _password;
    private readonly string _character;
    private readonly ITestLogger _logger;

    private WoWClientOrchestrator? _orchestrator;
    private WoWSharpObjectManager? _objectManager;
    private WoWClient? _wowClient;
    private readonly List<Position> _positionHistory = new();
    private const int StuckCheckHistorySize = 10;
    private const float StuckDistanceThreshold = 1.0f;
    private const float SuccessRadiusYards = 50.0f;

    /// <summary>
    /// Initializes a new instance of the BackgroundRecordedTestRunner.
    /// </summary>
    public BackgroundRecordedTestRunner(
        PathingTestDefinition testDefinition,
        PathfindingClient pathfindingClient,
        string account,
        string password,
        string character,
        ITestLogger logger)
    {
        _testDefinition = testDefinition ?? throw new ArgumentNullException(nameof(testDefinition));
        _pathfindingClient = pathfindingClient ?? throw new ArgumentNullException(nameof(pathfindingClient));
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _character = character ?? throw new ArgumentNullException(nameof(character));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken)
    {
        _logger.Info($"[BG] Connecting to server {server.Host}:{server.Port} with account '{_account}'");

        _orchestrator = WoWClientFactory.CreateOrchestrator();

        // Login to auth server
        await _orchestrator.LoginAsync(server.Host, _account, _password, server.Port, cancellationToken);
        _logger.Info("[BG] Authentication successful");

        // Get realm list
        var realms = await _orchestrator.GetRealmListAsync(cancellationToken);
        if (realms.Count == 0)
            throw new InvalidOperationException("No realms available");

        // Connect to first realm (or match by server.Realm if specified)
        var realm = string.IsNullOrEmpty(server.Realm)
            ? realms[0]
            : realms.First(r => string.Equals(r.RealmName, server.Realm, StringComparison.OrdinalIgnoreCase));

        await _orchestrator.ConnectToRealmAsync(realm, cancellationToken);
        _logger.Info($"[BG] Connected to realm '{realm.RealmName}'");

        // Resolve character GUID by parsing SMSG_CHAR_ENUM
        var characterGuid = await ResolveCharacterGuidAsync(_character, cancellationToken);

        // Enter world with resolved character GUID
        await _orchestrator.EnterWorldAsync(characterGuid, cancellationToken);
        _logger.Info($"[BG] Entered world as '{_character}'");

        // Create WoWClient for ObjectManager initialization
        _wowClient = WoWClientFactory.CreateModernWoWClient();

        // Initialize WoWSharpObjectManager
        _objectManager = WoWSharpObjectManager.Instance;
        _objectManager.Initialize(
            _wowClient,
            _pathfindingClient,
            NullLogger<WoWSharpObjectManager>.Instance);

        // Start the game loop for movement updates
        _objectManager.StartGameLoop();
        _logger.Info("[BG] Object manager initialized and game loop started");
    }

    /// <summary>
    /// Resolves character GUID by name from the character selection screen.
    /// </summary>
    private async Task<ulong> ResolveCharacterGuidAsync(string characterName, CancellationToken cancellationToken)
    {
        // Request character list
        await _orchestrator!.RefreshCharacterListAsync(cancellationToken);

        // Wait for SMSG_CHAR_ENUM response
        await Task.Delay(1500, cancellationToken);

        // Access character list from WoWSharpObjectManager
        var characters = WoWSharpObjectManager.Instance.CharacterSelectScreen.CharacterSelects;

        var character = characters.FirstOrDefault(c =>
            string.Equals(c.Name, characterName, StringComparison.OrdinalIgnoreCase));

        if (character == null)
        {
            throw new InvalidOperationException(
                $"Character '{characterName}' not found. Available characters: " +
                string.Join(", ", characters.Select(c => c.Name)));
        }

        _logger.Info($"[BG] Resolved character '{characterName}' to GUID: 0x{character.Guid:X16}");
        return character.Guid;
    }

    /// <inheritdoc />
    public async Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (_testDefinition.EndPosition == null)
            throw new InvalidOperationException($"Test '{_testDefinition.Name}' has no end position defined");

        var startPos = _testDefinition.StartPosition;
        var endPos = _testDefinition.EndPosition;
        var expectedEndMapId = _testDefinition.EndMapId ?? _testDefinition.MapId;

        _logger.Info($"[BG] Starting pathfinding test: {_testDefinition.Name}");
        _logger.Info($"[BG] Start: ({startPos.X:F2}, {startPos.Y:F2}, {startPos.Z:F2}) MapId: {_testDefinition.MapId}");
        _logger.Info($"[BG] End: ({endPos.X:F2}, {endPos.Y:F2}, {endPos.Z:F2}) MapId: {expectedEndMapId}");
        _logger.Info($"[BG] Transport Mode: {_testDefinition.Transport}");

        // Set up timeout based on ExpectedDuration * 1.5
        var maxDuration = _testDefinition.ExpectedDuration * 1.5;
        using var timeoutCts = new CancellationTokenSource(maxDuration);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Wait for player to be ready (teleport from GM commands to take effect)
            await Task.Delay(2000, linkedCts.Token);

            await NavigateToDestinationAsync(endPos, expectedEndMapId, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Test exceeded maximum duration ({maxDuration.TotalMinutes:F1} minutes = 1.5x expected duration of {_testDefinition.ExpectedDuration.TotalMinutes:F1} minutes)");
        }
        finally
        {
            StopMovement();
        }

        // Validate success criteria
        ValidateSuccessCriteria(endPos, expectedEndMapId);
    }

    private async Task NavigateToDestinationAsync(Position endPos, uint expectedEndMapId, CancellationToken cancellationToken)
    {
        var currentMapId = GetCurrentMapId();

        // Calculate initial path
        var path = _pathfindingClient.GetPath(
            currentMapId,
            GetCurrentPosition(),
            endPos,
            smoothPath: true);

        if (path == null || path.Length == 0)
            throw new InvalidOperationException("Pathfinding returned empty path");

        _logger.Info($"[BG] Path calculated: {path.Length} waypoints");

        var currentWaypointIndex = 0;
        var lastRepathTime = DateTime.UtcNow;
        var stuckCheckCounter = 0;
        var failureRetryTimestamp = DateTimeOffset.UtcNow;

        while (currentWaypointIndex < path.Length && !cancellationToken.IsCancellationRequested)
        {
            var waypoint = path[currentWaypointIndex];
            var currentPos = GetCurrentPosition();

            // Record position for stuck detection
            _positionHistory.Add(currentPos);
            if (_positionHistory.Count > StuckCheckHistorySize)
                _positionHistory.RemoveAt(0);

            // Check if we've reached the waypoint
            var distanceToWaypoint = currentPos.DistanceTo(waypoint);
            if (distanceToWaypoint < 2.0f)
            {
                _logger.Info($"[BG] Reached waypoint {currentWaypointIndex + 1}/{path.Length}");
                currentWaypointIndex++;
                continue;
            }

            // Move toward waypoint
            MoveTowardWaypoint(waypoint);

            // Check for stuck condition every 5 seconds
            stuckCheckCounter++;
            if (stuckCheckCounter >= 50) // 50 * 100ms = 5 seconds
            {
                stuckCheckCounter = 0;
                if (IsStuck())
                {
                    var retryCount = CalculateRetryCount(_testDefinition.Name, failureRetryTimestamp);
                    _logger.Warn($"[BG] Stuck detected, recalculating path (retry count: {retryCount})");
                    failureRetryTimestamp = DateTimeOffset.UtcNow;

                    path = _pathfindingClient.GetPath(
                        GetCurrentMapId(),
                        currentPos,
                        endPos,
                        smoothPath: true);
                    currentWaypointIndex = 0;
                    lastRepathTime = DateTime.UtcNow;
                    _positionHistory.Clear();
                }
            }

            // Handle transport scenarios
            if (_testDefinition.Transport != TransportMode.None)
            {
                await HandleTransportAsync(expectedEndMapId, cancellationToken);
            }

            // Periodic repath check (every 30 seconds)
            if ((DateTime.UtcNow - lastRepathTime).TotalSeconds > 30)
            {
                _logger.Info("[BG] Periodic repath check");
                var newPath = _pathfindingClient.GetPath(
                    GetCurrentMapId(),
                    currentPos,
                    endPos,
                    smoothPath: true);

                if (newPath.Length > 0 && newPath.Length < path.Length)
                {
                    _logger.Info("[BG] Found shorter path, updating");
                    path = newPath;
                    currentWaypointIndex = 0;
                }
                lastRepathTime = DateTime.UtcNow;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    /// <summary>
    /// Validates success criteria: within 50 yards of end position AND correct mapId.
    /// </summary>
    private void ValidateSuccessCriteria(Position endPos, uint expectedMapId)
    {
        var finalPos = GetCurrentPosition();
        var distance = finalPos.DistanceTo(endPos);
        var currentMapId = GetCurrentMapId();

        _logger.Info($"[BG] Final position: ({finalPos.X:F2}, {finalPos.Y:F2}, {finalPos.Z:F2})");
        _logger.Info($"[BG] Distance to destination: {distance:F1} yards, MapId: {currentMapId} (expected: {expectedMapId})");

        if (distance > SuccessRadiusYards)
        {
            throw new InvalidOperationException(
                $"Test failed: Final position is {distance:F1} yards from destination " +
                $"(must be within {SuccessRadiusYards} yards)");
        }

        if (currentMapId != expectedMapId)
        {
            throw new InvalidOperationException(
                $"Test failed: Final MapId is {currentMapId} (expected: {expectedMapId})");
        }

        _logger.Info($"[BG] Success criteria met: {distance:F1} yards from destination on MapId {currentMapId}");
    }

    /// <summary>
    /// Calculates retry count using seeded hash of test name and timestamp.
    /// </summary>
    private static int CalculateRetryCount(string testName, DateTimeOffset timestamp)
    {
        var hashInput = $"{testName}:{timestamp:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var seedValue = BitConverter.ToInt32(hash, 0);
        var random = new Random(Math.Abs(seedValue));
        return random.Next(1, 6); // 1..5 inclusive
    }

    /// <summary>
    /// Gets the current player position from WoWSharpObjectManager.
    /// </summary>
    private Position GetCurrentPosition()
    {
        if (_objectManager?.Player == null)
        {
            _logger.Warn("[BG] Object manager player not available, returning start position");
            return _testDefinition.StartPosition;
        }
        return _objectManager.Player.Position;
    }

    /// <summary>
    /// Gets the current map ID from WoWSharpObjectManager.
    /// </summary>
    private uint GetCurrentMapId()
    {
        return _objectManager?.Player?.MapId ?? _testDefinition.MapId;
    }

    /// <summary>
    /// Moves the character toward the specified waypoint using WoWSharpObjectManager.
    /// </summary>
    private void MoveTowardWaypoint(Position waypoint)
    {
        if (_objectManager == null)
        {
            _logger.Warn("[BG] Object manager not available, cannot move");
            return;
        }

        // Calculate facing direction toward waypoint
        var currentPos = GetCurrentPosition();
        var dx = waypoint.X - currentPos.X;
        var dy = waypoint.Y - currentPos.Y;
        var facing = MathF.Atan2(dy, dx);

        _objectManager.MoveToward(waypoint, facing);
    }

    /// <summary>
    /// Stops all character movement.
    /// </summary>
    private void StopMovement()
    {
        // Stop all movement by stopping forward movement
        _objectManager?.StopMovement(ControlBits.Front);
        _logger.Info("[BG] Stopping movement");
    }

    /// <summary>
    /// Checks if player is currently on a transport.
    /// </summary>
    private bool IsOnTransport()
    {
        if (_objectManager?.Player == null) return false;
        return _objectManager.Player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ONTRANSPORT)
               && _objectManager.Player.TransportGuid != 0;
    }

    /// <summary>
    /// Checks if player has disembarked from transport.
    /// </summary>
    private bool HasDisembarkedTransport()
    {
        if (_objectManager?.Player == null) return false;
        return _objectManager.Player.TransportGuid == 0;
    }

    /// <summary>
    /// Detects if the character is stuck based on position history.
    /// </summary>
    private bool IsStuck()
    {
        if (_positionHistory.Count < StuckCheckHistorySize)
            return false;

        // Calculate total distance moved in the last N positions
        float totalDistance = 0;
        for (int i = 0; i < _positionHistory.Count - 1; i++)
        {
            totalDistance += _positionHistory[i].DistanceTo(_positionHistory[i + 1]);
        }

        // If we haven't moved much, we're stuck
        return totalDistance < StuckDistanceThreshold;
    }

    /// <summary>
    /// Handles transport scenarios (boats, zeppelins).
    /// Transport boarding requires MOVEFLAG_ONTRANSPORT + TransportGuid != 0
    /// Arrival requires TransportGuid == 0 + dock radius 50 + expected mapId
    /// </summary>
    private async Task HandleTransportAsync(uint expectedEndMapId, CancellationToken cancellationToken)
    {
        var currentMapId = GetCurrentMapId();
        var failureRetryTimestamp = DateTimeOffset.UtcNow;

        // Check if we've boarded transport (MOVEFLAG_ONTRANSPORT + TransportGuid != 0)
        if (IsOnTransport())
        {
            _logger.Info("[BG] Boarded transport, waiting for arrival...");

            // Wait for disembarkation (TransportGuid == 0)
            var transportTimeout = TimeSpan.FromMinutes(15); // Max transport ride time
            var transportStartTime = DateTime.UtcNow;

            while (!HasDisembarkedTransport() && !cancellationToken.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - transportStartTime) > transportTimeout)
                {
                    var retryCount = CalculateRetryCount(_testDefinition.Name, failureRetryTimestamp);
                    _logger.Warn($"[BG] Transport timeout (retry count: {retryCount})");
                    throw new TimeoutException("Transport did not arrive within expected time");
                }
                await Task.Delay(500, cancellationToken);
            }

            // Wait a moment for mapId to update
            await Task.Delay(1000, cancellationToken);

            // Verify we're at expected dock area (radius 50 yards and expected mapId)
            var currentPos = GetCurrentPosition();
            var endPos = _testDefinition.EndPosition!;
            var dockDistance = currentPos.DistanceTo(endPos);
            var newMapId = GetCurrentMapId();

            if (dockDistance <= 50.0f && newMapId == expectedEndMapId)
            {
                _logger.Info($"[BG] Successfully arrived at dock (distance: {dockDistance:F1} yards, MapId: {newMapId})");
            }
            else
            {
                _logger.Warn($"[BG] Transport arrived but not at expected dock. " +
                            $"Distance: {dockDistance:F1}, MapId: {newMapId} (expected: {expectedEndMapId})");
            }
        }
    }

    /// <inheritdoc />
    public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Server state preparation is handled by foreground runner
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Server state reset is handled by foreground runner
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
    {
        // Background runner doesn't provide recording target (foreground runner handles this)
        return Task.FromResult(new RecordingTarget(RecordingTargetType.Screen));
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        // Note: WoWSharpObjectManager doesn't have StopGameLoop - the game loop continues
        // but we're disconnecting the underlying orchestrator

        if (_orchestrator != null)
        {
            _logger.Info("[BG] Disconnecting from server");
            await _orchestrator.DisconnectWorldAsync(cancellationToken);
            await _orchestrator.DisconnectAuthAsync(cancellationToken);
            _orchestrator.Dispose();
            _orchestrator = null;
        }

        _wowClient?.Dispose();
        _wowClient = null;
    }

    /// <inheritdoc />
    public Task ShutdownUiAsync(CancellationToken cancellationToken)
    {
        // No UI to shutdown for this runner
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
    }
}
