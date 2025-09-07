using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of looting network agent that handles loot operations in World of Warcraft.
    /// Manages loot containers, automatic looting, and item collection using the Mangos protocol.
    /// </summary>
    public class LootingNetworkAgent : ILootingNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<LootingNetworkAgent> _logger;
        private bool _isLootWindowOpen;
        private ulong? _currentLootTarget;

        /// <summary>
        /// Initializes a new instance of the LootingNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public LootingNetworkAgent(IWorldClient worldClient, ILogger<LootingNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsLootWindowOpen => _isLootWindowOpen;

        /// <inheritdoc />
        public event Action<ulong>? LootWindowOpened;

        /// <inheritdoc />
        public event Action? LootWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint>? ItemLooted;

        /// <inheritdoc />
        public event Action<uint>? MoneyLooted;

        /// <inheritdoc />
        public event Action<string>? LootError;

        /// <inheritdoc />
        public async Task OpenLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening loot for target: {LootTargetGuid:X}", lootTargetGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(lootTargetGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_LOOT, payload, cancellationToken);

                _currentLootTarget = lootTargetGuid;
                _logger.LogInformation("Loot command sent for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open loot for target: {LootTargetGuid:X}", lootTargetGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LootMoneyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Looting money from current loot window");

                await _worldClient.SendMovementAsync(Opcode.CMSG_LOOT_MONEY, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Money loot command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot money");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LootItemAsync(byte lootSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Looting item from slot: {LootSlot}", lootSlot);

                var payload = new byte[1];
                payload[0] = lootSlot;

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item loot command sent for slot: {LootSlot}", lootSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot item from slot: {LootSlot}", lootSlot);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StoreLootInSlotAsync(byte lootSlot, byte bag, byte slot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Storing loot from slot {LootSlot} to bag {Bag}, slot {Slot}", lootSlot, bag, slot);

                var payload = new byte[3];
                payload[0] = lootSlot;
                payload[1] = bag;
                payload[2] = slot;

                await _worldClient.SendMovementAsync(Opcode.CMSG_STORE_LOOT_IN_SLOT, payload, cancellationToken);

                _logger.LogInformation("Store loot command sent for slot {LootSlot} to bag {Bag}, slot {Slot}", 
                    lootSlot, bag, slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store loot from slot {LootSlot} to bag {Bag}, slot {Slot}", 
                    lootSlot, bag, slot);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseLootAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing loot window");

                await _worldClient.SendMovementAsync(Opcode.CMSG_LOOT_RELEASE, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Loot release command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close loot window");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RollForLootAsync(ulong lootGuid, byte itemSlot, LootRollType rollType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Rolling {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(lootGuid).CopyTo(payload, 0);
                payload[8] = itemSlot;
                payload[9] = (byte)rollType;

                await _worldClient.SendMovementAsync(Opcode.CMSG_LOOT_ROLL, payload, cancellationToken);

                _logger.LogInformation("Loot roll {RollType} sent for slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LootAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isLootWindowOpen)
                {
                    _logger.LogWarning("Cannot loot all - no loot window is open");
                    return;
                }

                _logger.LogDebug("Looting all items and money");

                // First, loot all money
                await LootMoneyAsync(cancellationToken);

                // Small delay between operations
                await Task.Delay(50, cancellationToken);

                // Then loot all items (assuming maximum 8 loot slots)
                for (byte slot = 0; slot < 8; slot++)
                {
                    try
                    {
                        await LootItemAsync(slot, cancellationToken);
                        await Task.Delay(25, cancellationToken); // Small delay between item loots
                    }
                    catch (Exception ex)
                    {
                        // Continue with other slots if one fails
                        _logger.LogDebug("Failed to loot slot {Slot}: {Error}", slot, ex.Message);
                    }
                }

                // Close the loot window
                await Task.Delay(100, cancellationToken);
                await CloseLootAsync(cancellationToken);

                _logger.LogInformation("Completed looting all items and money");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot all items");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Quick looting target: {LootTargetGuid:X}", lootTargetGuid);

                // Open the loot
                await OpenLootAsync(lootTargetGuid, cancellationToken);

                // Wait for loot window to open
                await Task.Delay(200, cancellationToken);

                // Loot everything
                await LootAllAsync(cancellationToken);

                _logger.LogInformation("Quick loot completed for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to quick loot target: {LootTargetGuid:X}", lootTargetGuid);
                throw;
            }
        }

        /// <summary>
        /// Updates the loot window state based on server response.
        /// This should be called when receiving loot-related packets.
        /// </summary>
        /// <param name="isOpen">Whether the loot window is now open.</param>
        /// <param name="lootTargetGuid">The GUID of the loot target (optional).</param>
        public void UpdateLootWindowState(bool isOpen, ulong? lootTargetGuid = null)
        {
            if (_isLootWindowOpen != isOpen)
            {
                _isLootWindowOpen = isOpen;

                if (isOpen && lootTargetGuid.HasValue)
                {
                    _currentLootTarget = lootTargetGuid.Value;
                    _logger.LogDebug("Loot window opened for target: {LootTargetGuid:X}", lootTargetGuid.Value);
                    LootWindowOpened?.Invoke(lootTargetGuid.Value);
                }
                else if (!isOpen)
                {
                    _logger.LogDebug("Loot window closed");
                    _currentLootTarget = null;
                    LootWindowClosed?.Invoke();
                }
            }
        }

        /// <summary>
        /// Reports a loot event based on server response.
        /// This should be called when receiving loot-related packets.
        /// </summary>
        /// <param name="eventType">The type of loot event.</param>
        /// <param name="itemId">The item ID (optional).</param>
        /// <param name="quantity">The quantity (optional).</param>
        /// <param name="message">Optional message for the event.</param>
        public void ReportLootEvent(string eventType, uint? itemId = null, uint? quantity = null, string? message = null)
        {
            _logger.LogInformation("Loot event: {EventType}", eventType);

            switch (eventType.ToLowerInvariant())
            {
                case "item":
                    if (itemId.HasValue && quantity.HasValue)
                    {
                        ItemLooted?.Invoke(itemId.Value, quantity.Value);
                    }
                    break;
                case "money":
                    if (quantity.HasValue)
                    {
                        MoneyLooted?.Invoke(quantity.Value);
                    }
                    break;
                case "error":
                    LootError?.Invoke(message ?? "Loot error occurred");
                    break;
                default:
                    _logger.LogWarning("Unknown loot event type: {EventType}", eventType);
                    break;
            }
        }
    }
}