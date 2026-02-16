using GameData.Core.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling game object interactions in World of Warcraft.
    /// Reactive variant exposing IObservable streams instead of C# events.
    /// </summary>
    public interface IGameObjectNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Stream fired when a game object interaction succeeds (generic or specialized).
        /// </summary>
        IObservable<ulong> GameObjectInteracted { get; }

        /// <summary>
        /// Stream fired when a game object interaction fails (guid, reason).
        /// </summary>
        IObservable<(ulong GameObjectGuid, string Reason)> GameObjectInteractionFailed { get; }

        /// <summary>
        /// Stream fired when a chest or container is opened.
        /// </summary>
        IObservable<ulong> ChestOpened { get; }

        /// <summary>
        /// Stream fired when a gathering node is harvested (guid, itemId).
        /// </summary>
        IObservable<(ulong GameObjectGuid, uint ItemId)> NodeHarvested { get; }

        /// <summary>
        /// Stream fired when a gathering operation fails (guid, reason).
        /// </summary>
        IObservable<(ulong GameObjectGuid, string Reason)> GatheringFailed { get; }

        /// <summary>
        /// Interacts with a game object (chest, node, door, etc.). Sends CMSG_GAMEOBJ_USE to the server.
        /// </summary>
        Task InteractWithGameObjectAsync(ulong gameObjectGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a chest or container game object.
        /// </summary>
        Task OpenChestAsync(ulong chestGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gathers from a resource node (herbs, mining nodes, etc.).
        /// </summary>
        Task GatherFromNodeAsync(ulong nodeGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses a door or portal game object.
        /// </summary>
        Task UseDoorAsync(ulong doorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a button or switch game object.
        /// </summary>
        Task ActivateButtonAsync(ulong buttonGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a smart interaction with a game object.
        /// Automatically determines the appropriate interaction type based on object properties.
        /// </summary>
        Task SmartInteractAsync(ulong gameObjectGuid, GameObjectType? gameObjectType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a game object can be interacted with.
        /// </summary>
        bool CanInteractWith(ulong gameObjectGuid, GameObjectInteractionType interactionType);

        /// <summary>
        /// Gets the distance required for interacting with a game object.
        /// </summary>
        float GetInteractionDistance(GameObjectType gameObjectType);
    }

    /// <summary>
    /// Enumeration for different types of game object interactions.
    /// </summary>
    public enum GameObjectInteractionType
    {
        Generic,
        OpenChest,
        Gather,
        UseDoor,
        ActivateButton,
        Read
    }
}