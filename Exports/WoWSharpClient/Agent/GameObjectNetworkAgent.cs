using GameData.Core.Enums;
using WoWSharpClient.Client;
using Microsoft.Extensions.Logging;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Implementation of game object network agent that handles game object interactions in World of Warcraft.
    /// Manages interactions with chests, gathering nodes, doors, and other game objects using the Mangos protocol.
    /// </summary>
    public class GameObjectNetworkAgent : IGameObjectNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GameObjectNetworkAgent> _logger;

        /// <summary>
        /// Initializes a new instance of the GameObjectNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public GameObjectNetworkAgent(IWorldClient worldClient, ILogger<GameObjectNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public event Action<ulong>? GameObjectInteracted;

        /// <inheritdoc />
        public event Action<ulong, string>? GameObjectInteractionFailed;

        /// <inheritdoc />
        public event Action<ulong>? ChestOpened;

        /// <inheritdoc />
        public event Action<ulong, uint>? NodeHarvested;

        /// <inheritdoc />
        public event Action<ulong, string>? GatheringFailed;

        /// <inheritdoc />
        public async Task InteractWithGameObjectAsync(ulong gameObjectGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Interacting with game object: {GameObjectGuid:X}", gameObjectGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(gameObjectGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_GAMEOBJ_USE, payload, cancellationToken);

                _logger.LogInformation("Game object interaction sent for: {GameObjectGuid:X}", gameObjectGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to interact with game object: {GameObjectGuid:X}", gameObjectGuid);
                throw;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task SmartInteractAsync(ulong gameObjectGuid, GameObjectType? gameObjectType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Smart interacting with game object: {GameObjectGuid:X}, Type: {ObjectType}", 
                    gameObjectGuid, gameObjectType);

                // Use the provided type or default to generic interaction
                switch (gameObjectType)
                {
                    case GameObjectType.Chest:
                        await OpenChestAsync(gameObjectGuid, cancellationToken);
                        break;
                    case GameObjectType.Goober: // Often used for gathering nodes
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

        /// <inheritdoc />
        public bool CanInteractWith(ulong gameObjectGuid, GameObjectInteractionType interactionType)
        {
            // This would typically check game state, distance, requirements, etc.
            // For now, we'll return true as a basic implementation
            // In a full implementation, this would check:
            // - Distance to object
            // - Object state (e.g., already looted, requires key, etc.)
            // - Player state (e.g., has required tools for gathering)
            // - Line of sight
            
            _logger.LogDebug("Checking interaction capability for {GameObjectGuid:X} with type {InteractionType}", 
                gameObjectGuid, interactionType);

            return true; // Simplified implementation
        }

        /// <inheritdoc />
        public float GetInteractionDistance(GameObjectType gameObjectType)
        {
            // Return appropriate interaction distances based on object type
            return gameObjectType switch
            {
                GameObjectType.Chest => 3.0f,
                GameObjectType.Goober => 3.5f, // Gathering nodes
                GameObjectType.Door => 2.5f,
                GameObjectType.Button => 2.0f,
                GameObjectType.QuestGiver => 4.0f,
                GameObjectType.Mailbox => 3.0f,
                GameObjectType.AuctionHouse => 4.0f,
                GameObjectType.TradeSkillMaster => 4.0f,
                _ => 3.0f // Default interaction distance
            };
        }

        /// <summary>
        /// Reports a game object interaction event based on server response.
        /// This should be called when receiving game object-related packets.
        /// </summary>
        /// <param name="eventType">The type of interaction event.</param>
        /// <param name="gameObjectGuid">The GUID of the game object.</param>
        /// <param name="itemId">Optional item ID for gathering events.</param>
        /// <param name="message">Optional message for error events.</param>
        public void ReportInteractionEvent(string eventType, ulong gameObjectGuid, uint? itemId = null, string? message = null)
        {
            _logger.LogInformation("Game object interaction event: {EventType} for object {GameObjectGuid:X}", 
                eventType, gameObjectGuid);

            switch (eventType.ToLowerInvariant())
            {
                case "success":
                case "interacted":
                    GameObjectInteracted?.Invoke(gameObjectGuid);
                    break;
                case "chest_opened":
                    ChestOpened?.Invoke(gameObjectGuid);
                    GameObjectInteracted?.Invoke(gameObjectGuid);
                    break;
                case "node_harvested":
                case "gathered":
                    if (itemId.HasValue)
                    {
                        NodeHarvested?.Invoke(gameObjectGuid, itemId.Value);
                    }
                    GameObjectInteracted?.Invoke(gameObjectGuid);
                    break;
                case "gathering_failed":
                    GatheringFailed?.Invoke(gameObjectGuid, message ?? "Gathering failed");
                    break;
                case "interaction_failed":
                case "failed":
                    GameObjectInteractionFailed?.Invoke(gameObjectGuid, message ?? "Interaction failed");
                    break;
                default:
                    _logger.LogWarning("Unknown game object interaction event type: {EventType}", eventType);
                    break;
            }
        }

        /// <summary>
        /// Updates the state of a game object based on server response.
        /// This can be used to track object states for optimization.
        /// </summary>
        /// <param name="gameObjectGuid">The GUID of the game object.</param>
        /// <param name="newState">The new state of the object.</param>
        public void UpdateGameObjectState(ulong gameObjectGuid, string newState)
        {
            _logger.LogDebug("Game object {GameObjectGuid:X} state updated to: {NewState}", 
                gameObjectGuid, newState);

            // Here you could maintain a cache of object states for optimization
            // For example, tracking which chests are already looted, which nodes are depleted, etc.
        }
    }
}