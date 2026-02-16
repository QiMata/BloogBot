using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling item usage operations in World of Warcraft.
    /// Manages using consumables, activating items, and handling item interactions.
    /// Exposes opcode-backed observables instead of events.
    /// </summary>
    public interface IItemUseNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether an item use operation is currently in progress.
        /// </summary>
        bool IsUsingItem { get; }

        /// <summary>
        /// Gets the GUID of the item currently being used, if any.
        /// </summary>
        ulong? CurrentItemInUse { get; }

        // Reactive streams
        IObservable<ItemUseStartedData> ItemUseStarted { get; }
        IObservable<ItemUseCompletedData> ItemUseCompleted { get; }
        IObservable<ItemUseErrorData> ItemUseFailed { get; }
        IObservable<ConsumableEffectData> ConsumableEffectApplied { get; }

        // Commands
        Task UseItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task UseItemOnTargetAsync(byte bagId, byte slotId, ulong targetGuid, CancellationToken cancellationToken = default);
        Task UseItemAtLocationAsync(byte bagId, byte slotId, float x, float y, float z, CancellationToken cancellationToken = default);
        Task ActivateItemAsync(ulong itemGuid, CancellationToken cancellationToken = default);
        Task UseConsumableAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task OpenContainerAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task UseToolAsync(byte bagId, byte slotId, ulong? targetGuid = null, CancellationToken cancellationToken = default);
        Task CancelItemUseAsync(CancellationToken cancellationToken = default);

        // Capability helpers
        bool CanUseItem(uint itemId);
        uint GetItemCooldown(uint itemId);

        // Convenience
        Task<bool> FindAndUseItemAsync(uint itemId, CancellationToken cancellationToken = default);

        // State updates (optional)
        void UpdateItemUsed(ulong itemGuid, uint itemId, ulong? targetGuid = null);
        void UpdateItemUseStarted(ulong itemGuid, uint castTime);
        void UpdateItemCooldown(uint itemId, uint cooldownTime);
    }
}