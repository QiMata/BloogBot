using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Reactive implementation of game object network agent that handles game object interactions.
    /// Produces observable streams instead of C# events.
    /// </summary>
    public class GameObjectNetworkClientComponent : NetworkClientComponent, IGameObjectNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GameObjectNetworkClientComponent> _logger;
        private bool _disposed;

        // Subjects (hot observables) for consumers to subscribe to
        private readonly Subject<ulong> _gameObjectInteracted = new();
        private readonly Subject<(ulong GameObjectGuid, string Reason)> _gameObjectInteractionFailed = new();
        private readonly Subject<ulong> _chestOpened = new();
        private readonly Subject<(ulong GameObjectGuid, uint ItemId)> _nodeHarvested = new();
        private readonly Subject<(ulong GameObjectGuid, string Reason)> _gatheringFailed = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GameObjectNetworkClientComponent"/> class.
        /// </summary>
        public GameObjectNetworkClientComponent(IWorldClient worldClient, ILogger<GameObjectNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Observable Streams
        /// <summary>
        /// Stream of successful generic interactions (includes chests/nodes after specialized emissions).
        /// </summary>
        public IObservable<ulong> GameObjectInteracted => _gameObjectInteracted.AsObservable();
        /// <summary>
        /// Stream of failed generic interactions (guid, reason).
        /// </summary>
        public IObservable<(ulong GameObjectGuid, string Reason)> GameObjectInteractionFailed => _gameObjectInteractionFailed.AsObservable();
        /// <summary>
        /// Stream when a chest is opened (guid).
        /// </summary>
        public IObservable<ulong> ChestOpened => _chestOpened.AsObservable();
        /// <summary>
        /// Stream when a gathering node is harvested (guid, itemId).
        /// </summary>
        public IObservable<(ulong GameObjectGuid, uint ItemId)> NodeHarvested => _nodeHarvested.AsObservable();
        /// <summary>
        /// Stream when gathering fails (guid, reason).
        /// </summary>
        public IObservable<(ulong GameObjectGuid, string Reason)> GatheringFailed => _gatheringFailed.AsObservable();
        #endregion

        public async Task InteractWithGameObjectAsync(ulong gameObjectGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Interacting with game object: {GameObjectGuid:X}", gameObjectGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(gameObjectGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GAMEOBJ_USE, payload, cancellationToken);

                _logger.LogInformation("Game object interaction sent for: {GameObjectGuid:X}", gameObjectGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to interact with game object: {GameObjectGuid:X}", gameObjectGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task OpenChestAsync(ulong chestGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening chest: {ChestGuid:X}", chestGuid);
                await InteractWithGameObjectAsync(chestGuid, cancellationToken);
                _logger.LogInformation("Chest open command sent for: {ChestGuid:X}", chestGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open chest: {ChestGuid:X}", chestGuid);
                throw;
            }
        }

        public async Task GatherFromNodeAsync(ulong nodeGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Gathering from node: {NodeGuid:X}", nodeGuid);
                await InteractWithGameObjectAsync(nodeGuid, cancellationToken);
                _logger.LogInformation("Gather command sent for node: {NodeGuid:X}", nodeGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to gather from node: {NodeGuid:X}", nodeGuid);
                throw;
            }
        }

        public async Task UseDoorAsync(ulong doorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Using door: {DoorGuid:X}", doorGuid);
                await InteractWithGameObjectAsync(doorGuid, cancellationToken);
                _logger.LogInformation("Door use command sent for: {DoorGuid:X}", doorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use door: {DoorGuid:X}", doorGuid);
                throw;
            }
        }

        public async Task ActivateButtonAsync(ulong buttonGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Activating button: {ButtonGuid:X}", buttonGuid);
                await InteractWithGameObjectAsync(buttonGuid, cancellationToken);
                _logger.LogInformation("Button activation sent for: {ButtonGuid:X}", buttonGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate button: {ButtonGuid:X}", buttonGuid);
                throw;
            }
        }

        public async Task SmartInteractAsync(ulong gameObjectGuid, GameObjectType? gameObjectType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Smart interacting with game object: {GameObjectGuid:X}, Type: {ObjectType}", gameObjectGuid, gameObjectType);

                switch (gameObjectType)
                {
                    case GameObjectType.Chest:
                        await OpenChestAsync(gameObjectGuid, cancellationToken);
                        break;
                    case GameObjectType.Goober: // gathering node
                        await GatherFromNodeAsync(gameObjectGuid, cancellationToken);
                        break;
                    case GameObjectType.Door:
                        await UseDoorAsync(gameObjectGuid, cancellationToken);
                        break;
                    case GameObjectType.Button:
                        await ActivateButtonAsync(gameObjectGuid, cancellationToken);
                        break;
                    default:
                        await InteractWithGameObjectAsync(gameObjectGuid, cancellationToken);
                        break;
                }

                _logger.LogInformation("Smart interaction completed for: {GameObjectGuid:X}", gameObjectGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed smart interaction with game object: {GameObjectGuid:X}", gameObjectGuid);
                throw;
            }
        }

        public bool CanInteractWith(ulong gameObjectGuid, GameObjectInteractionType interactionType)
        {
            _logger.LogDebug("Checking interaction capability for {GameObjectGuid:X} with type {InteractionType}", gameObjectGuid, interactionType);
            return true; // Placeholder logic
        }

        public float GetInteractionDistance(GameObjectType gameObjectType)
        {
            return gameObjectType switch
            {
                GameObjectType.Chest => 3.0f,
                GameObjectType.Goober => 3.5f,
                GameObjectType.Door => 2.5f,
                GameObjectType.Button => 2.0f,
                GameObjectType.QuestGiver => 4.0f,
                GameObjectType.Mailbox => 3.0f,
                GameObjectType.AuctionHouse => 4.0f,
                GameObjectType.SpellCaster => 4.0f,
                _ => 3.0f
            };
        }

        /// <summary>
        /// Called by higher-level packet handler to translate server responses into reactive emissions.
        /// </summary>
        public void ReportInteractionEvent(string eventType, ulong gameObjectGuid, uint? itemId = null, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return;

            _logger.LogInformation("Game object interaction event: {EventType} for object {GameObjectGuid:X}", eventType, gameObjectGuid);

            switch (eventType.ToLowerInvariant())
            {
                case "success":
                case "interacted":
                    _gameObjectInteracted.OnNext(gameObjectGuid);
                    break;
                case "chest_opened":
                    _chestOpened.OnNext(gameObjectGuid);
                    _gameObjectInteracted.OnNext(gameObjectGuid);
                    break;
                case "node_harvested":
                case "gathered":
                    if (itemId.HasValue)
                        _nodeHarvested.OnNext((gameObjectGuid, itemId.Value));
                    _gameObjectInteracted.OnNext(gameObjectGuid);
                    break;
                case "gathering_failed":
                    _gatheringFailed.OnNext((gameObjectGuid, message ?? "Gathering failed"));
                    break;
                case "interaction_failed":
                case "failed":
                    _gameObjectInteractionFailed.OnNext((gameObjectGuid, message ?? "Interaction failed"));
                    break;
                default:
                    _logger.LogWarning("Unknown game object interaction event type: {EventType}", eventType);
                    break;
            }
        }

        /// <summary>
        /// Placeholder for storing updated object state (e.g., caching to avoid duplicate attempts).
        /// </summary>
        public void UpdateGameObjectState(ulong gameObjectGuid, string newState)
        {
            _logger.LogDebug("Game object {GameObjectGuid:X} state updated to: {NewState}", gameObjectGuid, newState);
            // Extend with state tracking if needed.
        }

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("Disposing GameObjectNetworkClientComponent");

            // Complete and dispose subjects
            _gameObjectInteracted.OnCompleted();
            _gameObjectInteractionFailed.OnCompleted();
            _chestOpened.OnCompleted();
            _nodeHarvested.OnCompleted();
            _gatheringFailed.OnCompleted();

            _gameObjectInteracted.Dispose();
            _gameObjectInteractionFailed.Dispose();
            _chestOpened.Dispose();
            _nodeHarvested.Dispose();
            _gatheringFailed.Dispose();

            _logger.LogDebug("GameObjectNetworkClientComponent disposed");
        }
        #endregion
    }
}