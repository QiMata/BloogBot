using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling game object interactions in World of Warcraft.
    /// Manages interactions with chests, gathering nodes, doors, and other game objects.
    /// </summary>
    public interface IGameObjectNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Event fired when a game object interaction succeeds.
        /// </summary>
        event Action<ulong> GameObjectInteracted;

        /// <summary>
        /// Event fired when a game object interaction fails.
        /// </summary>
        event Action<ulong, string> GameObjectInteractionFailed;

        /// <summary>
        /// Event fired when a chest or container is opened.
        /// </summary>
        event Action<ulong> ChestOpened;

        /// <summary>
        /// Event fired when a gathering node is harvested.
        /// </summary>
        event Action<ulong, uint> NodeHarvested; // NodeGuid, ItemId

        /// <summary>
        /// Event fired when a gathering operation fails.
        /// </summary>
        event Action<ulong, string> GatheringFailed;

        /// <summary>
        /// Interacts with a game object (chest, node, door, etc.).
        /// Sends CMSG_GAMEOBJ_USE to the server.
        /// </summary>
        /// <param name="gameObjectGuid">The GUID of the game object to interact with.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InteractWithGameObjectAsync(ulong gameObjectGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a chest or container game object.
        /// This is a specialized interaction for loot containers.
        /// </summary>
        /// <param name="chestGuid">The GUID of the chest to open.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenChestAsync(ulong chestGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gathers from a resource node (herbs, mining nodes, etc.).
        /// This is a specialized interaction for gathering nodes.
        /// </summary>
        /// <param name="nodeGuid">The GUID of the node to gather from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GatherFromNodeAsync(ulong nodeGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses a door or portal game object.
        /// This is a specialized interaction for transportation objects.
        /// </summary>
        /// <param name="doorGuid">The GUID of the door or portal to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseDoorAsync(ulong doorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a button or switch game object.
        /// This is a specialized interaction for activation objects.
        /// </summary>
        /// <param name="buttonGuid">The GUID of the button or switch to activate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ActivateButtonAsync(ulong buttonGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a smart interaction with a game object.
        /// Automatically determines the appropriate interaction type based on object properties.
        /// </summary>
        /// <param name="gameObjectGuid">The GUID of the game object.</param>
        /// <param name="gameObjectType">The type of the game object (optional for optimization).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SmartInteractAsync(ulong gameObjectGuid, GameObjectType? gameObjectType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a game object can be interacted with.
        /// This method can be used to validate interactions before attempting them.
        /// </summary>
        /// <param name="gameObjectGuid">The GUID of the game object.</param>
        /// <param name="interactionType">The type of interaction to check.</param>
        /// <returns>True if the interaction is possible, false otherwise.</returns>
        bool CanInteractWith(ulong gameObjectGuid, GameObjectInteractionType interactionType);

        /// <summary>
        /// Gets the distance required for interacting with a game object.
        /// Different object types may require different interaction distances.
        /// </summary>
        /// <param name="gameObjectType">The type of game object.</param>
        /// <returns>The required interaction distance in yards.</returns>
        float GetInteractionDistance(GameObjectType gameObjectType);
    }

    /// <summary>
    /// Enumeration for different types of game object interactions.
    /// </summary>
    public enum GameObjectInteractionType
    {
        /// <summary>
        /// Generic interaction.
        /// </summary>
        Generic,

        /// <summary>
        /// Opening a chest or container.
        /// </summary>
        OpenChest,

        /// <summary>
        /// Gathering from a resource node.
        /// </summary>
        Gather,

        /// <summary>
        /// Using a door or portal.
        /// </summary>
        UseDoor,

        /// <summary>
        /// Activating a button or switch.
        /// </summary>
        ActivateButton,

        /// <summary>
        /// Reading text or examining an object.
        /// </summary>
        Read
    }
}