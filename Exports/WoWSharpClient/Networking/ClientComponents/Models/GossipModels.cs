using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    #region Enums

    /// <summary>
    /// Represents the current state of a gossip menu.
    /// </summary>
    public enum GossipMenuState
    {
        /// <summary>
        /// No gossip menu is open.
        /// </summary>
        Closed,

        /// <summary>
        /// Gossip menu is opening.
        /// </summary>
        Opening,

        /// <summary>
        /// Gossip menu is open and ready for interaction.
        /// </summary>
        Open,

        /// <summary>
        /// Gossip menu is waiting for server response.
        /// </summary>
        Waiting,

        /// <summary>
        /// Gossip menu is closing.
        /// </summary>
        Closing,

        /// <summary>
        /// An error occurred with the gossip menu.
        /// </summary>
        Error
    }

    /// <summary>
    /// Types of gossip operations.
    /// </summary>
    public enum GossipOperationType
    {
        /// <summary>
        /// Greeting an NPC.
        /// </summary>
        Greet,

        /// <summary>
        /// Selecting a gossip option.
        /// </summary>
        SelectOption,

        /// <summary>
        /// Querying NPC text.
        /// </summary>
        QueryText,

        /// <summary>
        /// Closing gossip conversation.
        /// </summary>
        Close,

        /// <summary>
        /// Navigating to a service.
        /// </summary>
        NavigateToService,

        /// <summary>
        /// Accepting a quest.
        /// </summary>
        AcceptQuest,

        /// <summary>
        /// Selecting quest reward.
        /// </summary>
        SelectQuestReward
    }

    /// <summary>
    /// Types of services available through gossip.
    /// </summary>
    public enum GossipServiceType
    {
        /// <summary>
        /// General gossip conversation.
        /// </summary>
        Gossip,

        /// <summary>
        /// Vendor services.
        /// </summary>
        Vendor,

        /// <summary>
        /// Taxi/flight master services.
        /// </summary>
        Taxi,

        /// <summary>
        /// Trainer services.
        /// </summary>
        Trainer,

        /// <summary>
        /// Healer services.
        /// </summary>
        Healer,

        /// <summary>
        /// Innkeeper/binder services.
        /// </summary>
        Binder,

        /// <summary>
        /// Banker services.
        /// </summary>
        Banker,

        /// <summary>
        /// Petition services.
        /// </summary>
        Petition,

        /// <summary>
        /// Tabard vendor services.
        /// </summary>
        Tabard,

        /// <summary>
        /// Battlemaster services.
        /// </summary>
        Battlemaster,

        /// <summary>
        /// Auctioneer services.
        /// </summary>
        Auctioneer,

        /// <summary>
        /// Quest giver services.
        /// </summary>
        QuestGiver,

        /// <summary>
        /// Unknown or custom service.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Navigation strategies for multi-step conversations.
    /// </summary>
    public enum GossipNavigationStrategy
    {
        /// <summary>
        /// Find and navigate to quest-related options.
        /// </summary>
        FindQuests,

        /// <summary>
        /// Find and navigate to vendor options.
        /// </summary>
        FindVendor,

        /// <summary>
        /// Find and navigate to trainer options.
        /// </summary>
        FindTrainer,

        /// <summary>
        /// Find and navigate to taxi options.
        /// </summary>
        FindTaxi,

        /// <summary>
        /// Find and navigate to banker options.
        /// </summary>
        FindBanker,

        /// <summary>
        /// Find and navigate to any service.
        /// </summary>
        FindAnyService,

        /// <summary>
        /// Navigate through all available options systematically.
        /// </summary>
        ExploreAll,

        /// <summary>
        /// Use custom navigation logic.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Quest reward selection strategies.
    /// </summary>
    public enum QuestRewardSelectionStrategy
    {
        /// <summary>
        /// Always select the first available reward.
        /// </summary>
        FirstReward,

        /// <summary>
        /// Select the reward with the highest vendor value.
        /// </summary>
        HighestValue,

        /// <summary>
        /// Select the reward best suited for the character's class.
        /// </summary>
        BestForClass,

        /// <summary>
        /// Select the reward that provides the best stat upgrade.
        /// </summary>
        BestStatUpgrade,

        /// <summary>
        /// Select the reward most needed by the character.
        /// </summary>
        MostNeeded,

        /// <summary>
        /// Use custom selection logic.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Quest state in gossip context.
    /// </summary>
    public enum QuestGossipState
    {
        /// <summary>
        /// Quest is available to accept.
        /// </summary>
        Available,

        /// <summary>
        /// Quest is in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Quest is completable (all objectives met).
        /// </summary>
        Completable,

        /// <summary>
        /// Quest is completed.
        /// </summary>
        Completed,

        /// <summary>
        /// Quest is not available due to requirements.
        /// </summary>
        NotAvailable,

        /// <summary>
        /// Quest is failed.
        /// </summary>
        Failed
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Represents gossip menu data received from the server.
    /// </summary>
    public class GossipMenuData
    {
        /// <summary>
        /// Gets the NPC GUID associated with this gossip menu.
        /// </summary>
        public ulong NpcGuid { get; init; }

        /// <summary>
        /// Gets the menu ID.
        /// </summary>
        public uint MenuId { get; init; }

        /// <summary>
        /// Gets the text ID for the gossip text.
        /// </summary>
        public uint TextId { get; init; }

        /// <summary>
        /// Gets the available gossip options.
        /// </summary>
        public IReadOnlyList<GossipOptionData> Options { get; init; } = [];

        /// <summary>
        /// Gets the available quest options.
        /// </summary>
        public IReadOnlyList<GossipQuestOption> QuestOptions { get; init; } = [];

        /// <summary>
        /// Gets the gossip text content.
        /// </summary>
        public string? GossipText { get; init; }

        /// <summary>
        /// Gets a value indicating whether this menu has multiple pages.
        /// </summary>
        public bool HasMultiplePages { get; init; }

        /// <summary>
        /// Gets a value indicating whether this menu has quest options.
        /// </summary>
        public bool HasQuestOptions => QuestOptions.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this menu has service options.
        /// </summary>
        public bool HasServiceOptions => Options.Any(o => o.ServiceType != GossipServiceType.Gossip);

        /// <summary>
        /// Gets the timestamp when this menu was received.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the GossipMenuData class.
        /// </summary>
        /// <param name="npcGuid">The NPC GUID.</param>
        /// <param name="menuId">The menu ID.</param>
        /// <param name="textId">The text ID.</param>
        public GossipMenuData(ulong npcGuid, uint menuId, uint textId)
        {
            NpcGuid = npcGuid;
            MenuId = menuId;
            TextId = textId;
        }
    }

    /// <summary>
    /// Represents a gossip option available in a menu.
    /// </summary>
    public class GossipOptionData
    {
        /// <summary>
        /// Gets the option index.
        /// </summary>
        public uint Index { get; init; }

        /// <summary>
        /// Gets the option text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the gossip type for this option.
        /// </summary>
        public GossipTypes GossipType { get; init; }

        /// <summary>
        /// Gets the service type for this option.
        /// </summary>
        public GossipServiceType ServiceType { get; init; }

        /// <summary>
        /// Gets a value indicating whether this option requires a payment.
        /// </summary>
        public bool RequiresPayment { get; init; }

        /// <summary>
        /// Gets the cost required for this option (if any).
        /// </summary>
        public uint Cost { get; init; }

        /// <summary>
        /// Gets a value indicating whether this option leads to another menu.
        /// </summary>
        public bool LeadsToSubmenu { get; init; }

        /// <summary>
        /// Initializes a new instance of the GossipOptionData class.
        /// </summary>
        /// <param name="index">The option index.</param>
        /// <param name="text">The option text.</param>
        /// <param name="gossipType">The gossip type.</param>
        public GossipOptionData(uint index, string text, GossipTypes gossipType)
        {
            Index = index;
            Text = text;
            GossipType = gossipType;
            ServiceType = ConvertGossipTypeToServiceType(gossipType);
        }

        /// <summary>
        /// Converts a GossipTypes enum to GossipServiceType.
        /// </summary>
        /// <param name="gossipType">The gossip type to convert.</param>
        /// <returns>The corresponding service type.</returns>
        private static GossipServiceType ConvertGossipTypeToServiceType(GossipTypes gossipType)
        {
            return gossipType switch
            {
                GossipTypes.Gossip => GossipServiceType.Gossip,
                GossipTypes.Vendor => GossipServiceType.Vendor,
                GossipTypes.Taxi => GossipServiceType.Taxi,
                GossipTypes.Trainer => GossipServiceType.Trainer,
                GossipTypes.Healer => GossipServiceType.Healer,
                GossipTypes.Binder => GossipServiceType.Binder,
                GossipTypes.Banker => GossipServiceType.Banker,
                GossipTypes.Petition => GossipServiceType.Petition,
                GossipTypes.Tabard => GossipServiceType.Tabard,
                GossipTypes.Battlemaster => GossipServiceType.Battlemaster,
                GossipTypes.Auctioneer => GossipServiceType.Auctioneer,
                _ => GossipServiceType.Unknown
            };
        }
    }

    /// <summary>
    /// Represents a quest option available in a gossip menu.
    /// </summary>
    public class GossipQuestOption
    {
        /// <summary>
        /// Gets the quest ID.
        /// </summary>
        public uint QuestId { get; init; }

        /// <summary>
        /// Gets the quest title.
        /// </summary>
        public string QuestTitle { get; init; } = string.Empty;

        /// <summary>
        /// Gets the quest level.
        /// </summary>
        public uint QuestLevel { get; init; }

        /// <summary>
        /// Gets the quest state (available, completable, etc.).
        /// </summary>
        public QuestGossipState State { get; init; }

        /// <summary>
        /// Gets the option index for this quest.
        /// </summary>
        public uint Index { get; init; }

        /// <summary>
        /// Gets a value indicating whether this quest has multiple reward choices.
        /// </summary>
        public bool HasRewardChoices { get; init; }

        /// <summary>
        /// Gets the number of available reward choices.
        /// </summary>
        public uint RewardChoiceCount { get; init; }

        /// <summary>
        /// Initializes a new instance of the GossipQuestOption class.
        /// </summary>
        /// <param name="questId">The quest ID.</param>
        /// <param name="questTitle">The quest title.</param>
        /// <param name="questLevel">The quest level.</param>
        /// <param name="state">The quest state.</param>
        /// <param name="index">The option index.</param>
        public GossipQuestOption(uint questId, string questTitle, uint questLevel, QuestGossipState state, uint index)
        {
            QuestId = questId;
            QuestTitle = questTitle;
            QuestLevel = questLevel;
            State = state;
            Index = index;
        }
    }

    /// <summary>
    /// Represents the result of a gossip option selection.
    /// </summary>
    public class GossipOptionResult
    {
        /// <summary>
        /// Gets the option that was selected.
        /// </summary>
        public GossipOptionData SelectedOption { get; init; }

        /// <summary>
        /// Gets a value indicating whether the selection was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the error message if the selection failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets a value indicating whether the selection requires a reward choice.
        /// </summary>
        public bool RequiresRewardChoice { get; init; }

        /// <summary>
        /// Gets the available reward choices (if any).
        /// </summary>
        public IReadOnlyList<QuestRewardChoice>? RewardChoices { get; init; }

        /// <summary>
        /// Gets a value indicating whether the selection opened a new menu.
        /// </summary>
        public bool OpenedNewMenu { get; init; }

        /// <summary>
        /// Gets the timestamp of the selection.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the GossipOptionResult class.
        /// </summary>
        /// <param name="selectedOption">The selected option.</param>
        /// <param name="success">Whether the selection was successful.</param>
        public GossipOptionResult(GossipOptionData selectedOption, bool success)
        {
            SelectedOption = selectedOption;
            Success = success;
        }
    }

    /// <summary>
    /// Represents quest reward choice data.
    /// </summary>
    public class QuestRewardChoice
    {
        /// <summary>
        /// Gets the reward index.
        /// </summary>
        public uint Index { get; init; }

        /// <summary>
        /// Gets the item ID for this reward.
        /// </summary>
        public uint ItemId { get; init; }

        /// <summary>
        /// Gets the item name.
        /// </summary>
        public string ItemName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the item quantity.
        /// </summary>
        public uint Quantity { get; init; }

        /// <summary>
        /// Gets the item quality.
        /// </summary>
        public ItemQuality Quality { get; init; }

        /// <summary>
        /// Gets the item's vendor value.
        /// </summary>
        public uint VendorValue { get; init; }

        /// <summary>
        /// Gets a value indicating whether this item is suitable for the character's class.
        /// </summary>
        public bool SuitableForClass { get; init; }

        /// <summary>
        /// Gets the stat improvement score for this item.
        /// </summary>
        public float StatScore { get; init; }

        /// <summary>
        /// Initializes a new instance of the QuestRewardChoice class.
        /// </summary>
        /// <param name="index">The reward index.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="quantity">The quantity.</param>
        public QuestRewardChoice(uint index, uint itemId, string itemName, uint quantity)
        {
            Index = index;
            ItemId = itemId;
            ItemName = itemName;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Represents gossip service discovery data.
    /// </summary>
    public class GossipServiceData
    {
        /// <summary>
        /// Gets the NPC GUID providing the service.
        /// </summary>
        public ulong NpcGuid { get; init; }

        /// <summary>
        /// Gets the type of service discovered.
        /// </summary>
        public GossipServiceType ServiceType { get; init; }

        /// <summary>
        /// Gets the gossip option associated with this service.
        /// </summary>
        public GossipOptionData ServiceOption { get; init; }

        /// <summary>
        /// Gets additional service information.
        /// </summary>
        public string? ServiceInfo { get; init; }

        /// <summary>
        /// Gets the timestamp when this service was discovered.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the GossipServiceData class.
        /// </summary>
        /// <param name="npcGuid">The NPC GUID.</param>
        /// <param name="serviceType">The service type.</param>
        /// <param name="serviceOption">The service option.</param>
        public GossipServiceData(ulong npcGuid, GossipServiceType serviceType, GossipOptionData serviceOption)
        {
            NpcGuid = npcGuid;
            ServiceType = serviceType;
            ServiceOption = serviceOption;
        }
    }

    /// <summary>
    /// Represents gossip error data.
    /// </summary>
    public class GossipErrorData
    {
        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string ErrorMessage { get; init; }

        /// <summary>
        /// Gets the NPC GUID that caused the error (if applicable).
        /// </summary>
        public ulong? NpcGuid { get; init; }

        /// <summary>
        /// Gets the operation that caused the error.
        /// </summary>
        public GossipOperationType? OperationType { get; init; }

        /// <summary>
        /// Gets the timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the GossipErrorData class.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public GossipErrorData(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Initializes a new instance of the GossipErrorData class.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="npcGuid">The NPC GUID.</param>
        /// <param name="operationType">The operation type.</param>
        public GossipErrorData(string errorMessage, ulong? npcGuid, GossipOperationType? operationType)
        {
            ErrorMessage = errorMessage;
            NpcGuid = npcGuid;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// Quest acceptance filter for filtering which quests to accept.
    /// </summary>
    public class QuestAcceptanceFilter
    {
        /// <summary>
        /// Gets or sets the minimum quest level to accept.
        /// </summary>
        public uint? MinLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum quest level to accept.
        /// </summary>
        public uint? MaxLevel { get; set; }

        /// <summary>
        /// Gets or sets quest titles to include (if any match, quest is accepted).
        /// </summary>
        public IReadOnlyList<string>? IncludeTitles { get; set; }

        /// <summary>
        /// Gets or sets quest titles to exclude (if any match, quest is rejected).
        /// </summary>
        public IReadOnlyList<string>? ExcludeTitles { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to only accept daily quests.
        /// </summary>
        public bool? OnlyDailyQuests { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to accept elite quests.
        /// </summary>
        public bool? AcceptEliteQuests { get; set; }

        /// <summary>
        /// Gets or sets a custom filter function.
        /// </summary>
        public Func<GossipQuestOption, bool>? CustomFilter { get; set; }

        /// <summary>
        /// Determines whether the specified quest option passes this filter.
        /// </summary>
        /// <param name="questOption">The quest option to check.</param>
        /// <returns>True if the quest should be accepted.</returns>
        public bool ShouldAcceptQuest(GossipQuestOption questOption)
        {
            // Check level constraints
            if (MinLevel.HasValue && questOption.QuestLevel < MinLevel.Value)
                return false;

            if (MaxLevel.HasValue && questOption.QuestLevel > MaxLevel.Value)
                return false;

            // Check title inclusion
            if (IncludeTitles != null && IncludeTitles.Count > 0)
            {
                if (!IncludeTitles.Any(title => questOption.QuestTitle.Contains(title, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            // Check title exclusion
            if (ExcludeTitles != null && ExcludeTitles.Count > 0)
            {
                if (ExcludeTitles.Any(title => questOption.QuestTitle.Contains(title, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            // Apply custom filter
            if (CustomFilter != null)
            {
                return CustomFilter(questOption);
            }

            return true;
        }
    }

    #endregion
}