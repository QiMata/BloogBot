using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents validation result for operations.
    /// </summary>
    public record ValidationResult(
        bool IsValid,
        string? ErrorMessage = null);

    /// <summary>
    /// Represents data for targeting operations.
    /// </summary>
    public record TargetingData(
        ulong? PreviousTarget,
        ulong? CurrentTarget,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for targeting errors.
    /// </summary>
    public record TargetingErrorData(
        string ErrorMessage,
        ulong? TargetGuid,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for assist operations.
    /// </summary>
    public record AssistData(
        ulong PlayerGuid,
        ulong? AssistTarget,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for attack state changes.
    /// </summary>
    public record AttackStateData(
        bool IsAttacking,
        ulong? VictimGuid,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for attack errors.
    /// </summary>
    public record AttackErrorData(
        string ErrorMessage,
        ulong? TargetGuid,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for weapon swing information.
    /// </summary>
    public record WeaponSwingData(
        ulong AttackerGuid,
        ulong VictimGuid,
        uint Damage,
        bool IsCritical,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for quest operations.
    /// </summary>
    public record QuestData(
        uint QuestId,
        string QuestTitle,
        ulong QuestGiverGuid,
        QuestOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for quest progress updates.
    /// </summary>
    public record QuestProgressData(
        uint QuestId,
        string QuestTitle,
        string ProgressText,
        uint CompletedObjectives,
        uint TotalObjectives,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for quest rewards.
    /// </summary>
    public record QuestRewardData(
        uint QuestId,
        uint RewardIndex,
        uint ItemId,
        string ItemName,
        uint Quantity,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for quest errors.
    /// </summary>
    public record QuestErrorData(
        string ErrorMessage,
        uint? QuestId,
        ulong? QuestGiverGuid,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for loot operations.
    /// </summary>
    public record LootData(
        ulong LootTargetGuid,
        uint ItemId,
        string ItemName,
        uint Quantity,
        ItemQuality Quality,
        byte LootSlot,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for money loot.
    /// </summary>
    public record MoneyLootData(
        ulong LootTargetGuid,
        uint Amount,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for loot window state changes.
    /// </summary>
    public record LootWindowData(
        bool IsOpen,
        ulong? LootTargetGuid,
        uint AvailableItems,
        uint AvailableMoney,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for loot roll operations.
    /// </summary>
    public record LootRollData(
        ulong LootGuid,
        byte ItemSlot,
        uint ItemId,
        LootRollType RollType,
        uint RollResult,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for loot errors.
    /// </summary>
    public record LootErrorData(
        string ErrorMessage,
        ulong? LootTargetGuid,
        byte? LootSlot,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for bind on pickup confirmations.
    /// </summary>
    public record BindOnPickupData(
        byte LootSlot,
        uint ItemId,
        string ItemName,
        ItemQuality Quality,
        bool RequiresConfirmation,
        DateTime Timestamp);

    /// <summary>
    /// Represents information about a loot slot.
    /// </summary>
    public record LootSlotInfo(
        byte SlotIndex,
        uint ItemId,
        string ItemName,
        uint Quantity,
        ItemQuality Quality,
        bool IsBindOnPickup,
        bool RequiresRoll,
        LootSlotType SlotType,
        ulong? RollGuid = null);

    /// <summary>
    /// Represents group loot notification data.
    /// </summary>
    public record GroupLootNotificationData(
        GroupLootNotificationType NotificationType,
        uint ItemId,
        string ItemName,
        string? PlayerName,
        LootRollType? RollType,
        uint? RollResult,
        DateTime Timestamp);

    /// <summary>
    /// Represents master loot assignment data.
    /// </summary>
    public record MasterLootData(
        byte LootSlot,
        uint ItemId,
        string ItemName,
        ulong AssignerGuid,
        string AssignerName,
        ulong TargetPlayerGuid,
        string TargetPlayerName,
        DateTime Timestamp);

    /// <summary>
    /// Represents loot roll preferences for automated rolling.
    /// </summary>
    public record LootRollPreferences(
        LootRollType DefaultRoll = LootRollType.Greed,
        LootRollType ArmorRoll = LootRollType.Greed,
        LootRollType WeaponRoll = LootRollType.Greed,
        LootRollType ConsumableRoll = LootRollType.Greed,
        ItemQuality MinimumNeedQuality = ItemQuality.Uncommon,
        ItemQuality MinimumGreedQuality = ItemQuality.Poor,
        bool AutoPassOnUnneedableItems = true,
        bool NeedOnClassItems = true,
        bool GreedOnAllItems = false,
        HashSet<uint>? AlwaysNeedItems = null,
        HashSet<uint>? AlwaysPassItems = null);

    // Inventory Agent Data Models
    /// <summary>
    /// Represents data for inventory operations.
    /// </summary>
    public record InventoryData(
        ulong ItemGuid,
        uint ItemId,
        byte SourceBag,
        byte SourceSlot,
        byte DestinationBag,
        byte DestinationSlot,
        uint Quantity,
        InventoryOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for inventory errors.
    /// </summary>
    public record InventoryErrorData(
        string ErrorMessage,
        ulong? ItemGuid,
        byte? BagSlot,
        InventoryOperationType? OperationType,
        DateTime Timestamp);

    // Item Use Agent Data Models
    /// <summary>
    /// Represents data for item use operations.
    /// </summary>
    public record ItemUseData(
        ulong ItemGuid,
        uint ItemId,
        ulong? TargetGuid,
        uint? CastTime,
        bool IsConsumable,
        DateTime Timestamp);

    // Equipment Agent Data Models
    /// <summary>
    /// Represents data for equipment operations.
    /// </summary>
    public record EquipmentData(
        ulong ItemGuid,
        uint ItemId,
        EquipmentSlot Slot,
        EquipmentOperationType OperationType,
        uint? CurrentDurability,
        uint? MaxDurability,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for equipment errors.
    /// </summary>
    public record EquipmentErrorData(
        string ErrorMessage,
        ulong? ItemGuid,
        EquipmentSlot? Slot,
        EquipmentOperationType? OperationType,
        DateTime Timestamp);

    // Spell Casting Agent Data Models
    /// <summary>
    /// Represents data for spell casting operations.
    /// </summary>
    public record SpellCastData(
        uint SpellId,
        string SpellName,
        ulong? TargetGuid,
        uint? CastTime,
        uint? ChannelTime,
        SpellCastState State,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for spell cast results.
    /// </summary>
    public record SpellResultData(
        uint SpellId,
        ulong CasterGuid,
        ulong TargetGuid,
        uint? Damage,
        uint? Healed,
        bool IsCritical,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for spell casting errors.
    /// </summary>
    public record SpellCastErrorData(
        string ErrorMessage,
        uint SpellId,
        ulong? TargetGuid,
        DateTime Timestamp);

    // Game Object Agent Data Models
    /// <summary>
    /// Represents data for game object interactions.
    /// </summary>
    public record GameObjectData(
        ulong GameObjectGuid,
        uint GameObjectId,
        GameObjectType ObjectType,
        GameObjectOperationType OperationType,
        bool Success,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for game object errors.
    /// </summary>
    public record GameObjectErrorData(
        string ErrorMessage,
        ulong GameObjectGuid,
        GameObjectOperationType? OperationType,
        DateTime Timestamp);

    // Vendor Agent Data Models
    /// <summary>
    /// Represents data for vendor operations.
    /// </summary>
    public record VendorData(
        ulong VendorGuid,
        uint ItemId,
        uint Quantity,
        uint Cost,
        VendorOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for vendor window state.
    /// </summary>
    public record VendorWindowData(
        bool IsOpen,
        ulong? VendorGuid,
        string? VendorName,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for vendor errors.
    /// </summary>
    public record VendorErrorData(
        string ErrorMessage,
        ulong? VendorGuid,
        VendorOperationType? OperationType,
        DateTime Timestamp);

    // Flight Master Agent Data Models
    /// <summary>
    /// Represents data for flight master operations.
    /// </summary>
    public record FlightData(
        ulong FlightMasterGuid,
        uint SourceNodeId,
        uint DestinationNodeId,
        uint Cost,
        FlightOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for taxi node information.
    /// </summary>
    public record TaxiNodeData(
        uint NodeId,
        string NodeName,
        TaxiNodeStatus Status,
        uint Cost,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for flight master errors.
    /// </summary>
    public record FlightErrorData(
        string ErrorMessage,
        ulong? FlightMasterGuid,
        FlightOperationType? OperationType,
        DateTime Timestamp);

    // Dead Actor Agent Data Models
    /// <summary>
    /// Represents data for death-related operations.
    /// </summary>
    public record DeathData(
        ulong CharacterGuid,
        ulong? KillerGuid,
        float CorpseX,
        float CorpseY,
        float CorpseZ,
        DeathOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for resurrection operations.
    /// </summary>
    public record ResurrectionData(
        ulong CharacterGuid,
        ulong? ResurrectorGuid,
        string? ResurrectorName,
        ResurrectionType Type,
        DateTime Timestamp);

    /// <summary>
    /// Represents data for death-related errors.
    /// </summary>
    public record DeathErrorData(
        string ErrorMessage,
        ulong? CharacterGuid,
        DeathOperationType? OperationType,
        DateTime Timestamp);

    // Auction House Agent Data Models
    /// <summary>
    /// Represents auction house auction data.
    /// </summary>
    public record AuctionData(
        uint AuctionId,
        uint ItemId,
        string ItemName,
        uint Quantity,
        uint StartBid,
        uint BuyoutPrice,
        uint CurrentBid,
        uint TimeLeft,
        string SellerName,
        DateTime Timestamp);

    /// <summary>
    /// Represents auction house operation data.
    /// </summary>
    public record AuctionOperationData(
        uint AuctionId,
        AuctionOperationType OperationType,
        uint Amount,
        bool Success,
        DateTime Timestamp);

    /// <summary>
    /// Represents auction house window state.
    /// </summary>
    public record AuctionHouseWindowData(
        bool IsOpen,
        ulong? AuctioneerGuid,
        AuctionHouseType HouseType,
        DateTime Timestamp);

    /// <summary>
    /// Represents auction house notification data.
    /// </summary>
    public record AuctionNotificationData(
        AuctionNotificationType NotificationType,
        uint AuctionId,
        uint ItemId,
        string Message,
        DateTime Timestamp);

    /// <summary>
    /// Represents auction house errors.
    /// </summary>
    public record AuctionErrorData(
        string ErrorMessage,
        uint? AuctionId,
        AuctionOperationType? OperationType,
        DateTime Timestamp);

    // Bank Agent Data Models
    /// <summary>
    /// Represents bank operation data.
    /// </summary>
    public record BankData(
        ulong ItemGuid,
        uint ItemId,
        uint Quantity,
        byte? BankSlot,
        byte? BagSlot,
        BankOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents bank window state.
    /// </summary>
    public record BankWindowData(
        bool IsOpen,
        ulong? BankerGuid,
        uint AvailableSlots,
        uint PurchasedBagSlots,
        DateTime Timestamp);

    /// <summary>
    /// Represents bank gold operations.
    /// </summary>
    public record BankGoldData(
        uint Amount,
        BankGoldOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents bank errors.
    /// </summary>
    public record BankErrorData(
        string ErrorMessage,
        BankOperationType? OperationType,
        DateTime Timestamp);

    // Mail Agent Data Models
    /// <summary>
    /// Represents mail data.
    /// </summary>
    public record MailData(
        uint MailId,
        string Subject,
        string Body,
        string SenderName,
        string RecipientName,
        uint? AttachedMoney,
        uint? AttachedItemId,
        uint? AttachedItemQuantity,
        MailType MailType,
        DateTime SendTime,
        DateTime Timestamp);

    /// <summary>
    /// Represents mail operation data.
    /// </summary>
    public record MailOperationData(
        uint MailId,
        MailOperationType OperationType,
        bool Success,
        DateTime Timestamp);

    /// <summary>
    /// Represents mail errors.
    /// </summary>
    public record MailErrorData(
        string ErrorMessage,
        uint? MailId,
        MailOperationType? OperationType,
        DateTime Timestamp);

    // Guild Agent Data Models
    /// <summary>
    /// Represents guild operation data.
    /// </summary>
    public record GuildData(
        ulong GuildId,
        string GuildName,
        string? PlayerName,
        GuildOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents guild member data.
    /// </summary>
    public record GuildMemberData(
        ulong PlayerGuid,
        string PlayerName,
        GuildRank Rank,
        string Note,
        string OfficerNote,
        bool IsOnline,
        DateTime LastLogin,
        DateTime Timestamp);

    /// <summary>
    /// Represents guild errors.
    /// </summary>
    public record GuildErrorData(
        string ErrorMessage,
        GuildOperationType? OperationType,
        DateTime Timestamp);

    // Party Agent Data Models
    /// <summary>
    /// Represents party operation data.
    /// </summary>
    public record PartyData(
        ulong? PartyId,
        string? PlayerName,
        ulong? PlayerGuid,
        PartyOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents party member data.
    /// </summary>
    public record PartyMemberData(
        ulong PlayerGuid,
        string PlayerName,
        bool IsLeader,
        bool IsOnline,
        uint Level,
        uint HealthPercent,
        uint ManaPercent,
        DateTime Timestamp);

    /// <summary>
    /// Represents party errors.
    /// </summary>
    public record PartyErrorData(
        string ErrorMessage,
        PartyOperationType? OperationType,
        DateTime Timestamp);

    // Trainer Agent Data Models
    /// <summary>
    /// Represents trainer operation data.
    /// </summary>
    public record TrainerData(
        ulong TrainerGuid,
        string TrainerName,
        uint SpellId,
        string SpellName,
        uint Cost,
        TrainerOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents available spell data from trainer.
    /// </summary>
    public record TrainerSpellData(
        uint SpellId,
        string SpellName,
        uint Cost,
        uint RequiredLevel,
        uint RequiredSpellId,
        bool CanLearn,
        DateTime Timestamp);

    /// <summary>
    /// Represents trainer errors.
    /// </summary>
    public record TrainerErrorData(
        string ErrorMessage,
        ulong? TrainerGuid,
        TrainerOperationType? OperationType,
        DateTime Timestamp);

    // Talent Agent Data Models
    /// <summary>
    /// Represents talent operation data.
    /// </summary>
    public record TalentData(
        uint TalentId,
        string TalentName,
        uint TalentTree,
        uint TalentTier,
        uint TalentColumn,
        uint CurrentRank,
        uint MaxRank,
        TalentOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents talent points data.
    /// </summary>
    public record TalentPointsData(
        uint AvailablePoints,
        uint UsedPoints,
        uint TotalPoints,
        DateTime Timestamp);

    /// <summary>
    /// Represents talent errors.
    /// </summary>
    public record TalentErrorData(
        string ErrorMessage,
        uint? TalentId,
        TalentOperationType? OperationType,
        DateTime Timestamp);

    // Professions Agent Data Models
    /// <summary>
    /// Represents profession operation data.
    /// </summary>
    public record ProfessionData(
        uint ProfessionId,
        string ProfessionName,
        uint CurrentSkill,
        uint MaxSkill,
        uint RecipeId,
        string RecipeName,
        ProfessionOperationType OperationType,
        DateTime Timestamp);

    /// <summary>
    /// Represents profession recipe data.
    /// </summary>
    public record ProfessionRecipeData(
        uint RecipeId,
        string RecipeName,
        uint RequiredSkill,
        uint ItemId,
        string ItemName,
        uint Quantity,
        RecipeDifficulty Difficulty,
        DateTime Timestamp);

    /// <summary>
    /// Represents profession errors.
    /// </summary>
    public record ProfessionErrorData(
        string ErrorMessage,
        uint? ProfessionId,
        ProfessionOperationType? OperationType,
        DateTime Timestamp);

    // Emote Agent Data Models
    /// <summary>
    /// Represents emote operation data.
    /// </summary>
    public record EmoteData(
        uint EmoteId,
        string EmoteName,
        ulong? TargetGuid,
        string? TargetName,
        EmoteType EmoteType,
        DateTime Timestamp);

    /// <summary>
    /// Represents emote errors.
    /// </summary>
    public record EmoteErrorData(
        string ErrorMessage,
        uint? EmoteId,
        DateTime Timestamp);

    /// <summary>
    /// Types of quest operations.
    /// </summary>
    public enum QuestOperationType
    {
        Offered,
        Accepted,
        Completed,
        Abandoned,
        ProgressUpdated,
        RewardChosen,
        Shared
    }

    /// <summary>
    /// Types of loot roll in group situations.
    /// </summary>
    public enum LootRollType : byte
    {
        Pass = 0,
        Need = 1,
        Greed = 2
    }

    /// <summary>
    /// Types of inventory operations.
    /// </summary>
    public enum InventoryOperationType
    {
        ItemMoved,
        ItemSplit,
        ItemSwapped,
        ItemDestroyed,
        ItemPickedUp,
        ItemDropped
    }

    /// <summary>
    /// Types of equipment operations.
    /// </summary>
    public enum EquipmentOperationType
    {
        Equipped,
        Unequipped,
        Swapped,
        DurabilityChanged,
        Repaired
    }

    /// <summary>
    /// Spell cast states.
    /// </summary>
    public enum SpellCastState
    {
        Started,
        Channeling,
        Completed,
        Failed,
        Interrupted
    }

    /// <summary>
    /// Types of game object operations.
    /// </summary>
    public enum GameObjectOperationType
    {
        Interaction,
        ChestOpened,
        NodeHarvested,
        DoorUsed,
        ButtonActivated,
        LeverPulled
    }

    /// <summary>
    /// Types of vendor operations.
    /// </summary>
    public enum VendorOperationType
    {
        WindowOpened,
        WindowClosed,
        ItemPurchased,
        ItemSold,
        ItemRepaired,
        AllItemsRepaired
    }

    /// <summary>
    /// Types of flight operations.
    /// </summary>
    public enum FlightOperationType
    {
        MapOpened,
        MapClosed,
        FlightActivated,
        NodeLearned,
        NodeStatusReceived
    }

    /// <summary>
    /// Taxi node status.
    /// </summary>
    public enum TaxiNodeStatus : byte
    {
        Unknown = 0,
        Current = 1,
        Reachable = 2,
        Unreachable = 3
    }

    /// <summary>
    /// Types of death operations.
    /// </summary>
    public enum DeathOperationType
    {
        CharacterDied,
        SpiritReleased,
        CharacterResurrected,
        ResurrectionRequested,
        CorpseLocationUpdated
    }

    /// <summary>
    /// Types of resurrection.
    /// </summary>
    public enum ResurrectionType
    {
        Spirit,
        Player,
        Ankh,
        SoulStone
    }

    /// <summary>
    /// Types of auction operations.
    /// </summary>
    public enum AuctionOperationType
    {
        HouseOpened,
        HouseClosed,
        Search,
        BidPlaced,
        BuyoutUsed,
        AuctionPosted,
        AuctionCancelled,
        AuctionWon,
        AuctionExpired
    }

    /// <summary>
    /// Types of auction houses.
    /// </summary>
    public enum AuctionHouseType
    {
        Alliance,
        Horde,
        Neutral
    }

    /// <summary>
    /// Types of auction notifications.
    /// </summary>
    public enum AuctionNotificationType
    {
        BidSuccessful,
        BidFailed,
        Outbid,
        Won,
        Sold,
        Expired,
        Cancelled
    }

    /// <summary>
    /// Types of bank operations.
    /// </summary>
    public enum BankOperationType
    {
        WindowOpened,
        WindowClosed,
        ItemDeposited,
        ItemWithdrawn,
        ItemsSwapped,
        SlotPurchased,
        InfoUpdated
    }

    /// <summary>
    /// Types of bank gold operations.
    /// </summary>
    public enum BankGoldOperationType
    {
        Deposited,
        Withdrawn
    }

    /// <summary>
    /// Types of mail.
    /// </summary>
    public enum MailType
    {
        Normal,
        Auction,
        Creature,
        GameObject,
        Calendar
    }

    /// <summary>
    /// Types of mail operations.
    /// </summary>
    public enum MailOperationType
    {
        MailboxOpened,
        MailboxClosed,
        MailSent,
        MailReceived,
        MailRead,
        MailDeleted,
        AttachmentTaken,
        MoneyTaken
    }

    /// <summary>
    /// Types of guild operations.
    /// </summary>
    public enum GuildOperationType
    {
        Invited,
        InviteAccepted,
        InviteDeclined,
        MemberJoined,
        MemberLeft,
        MemberPromoted,
        MemberDemoted,
        MemberKicked,
        InfoUpdated,
        MessageOfTheDayChanged
    }

    /// <summary>
    /// Guild ranks.
    /// </summary>
    public enum GuildRank
    {
        GuildMaster = 0,
        Officer = 1,
        Veteran = 2,
        Member = 3,
        Initiate = 4
    }

    /// <summary>
    /// Types of party operations.
    /// </summary>
    public enum PartyOperationType
    {
        Invited,
        InviteAccepted,
        InviteDeclined,
        MemberJoined,
        MemberLeft,
        MemberKicked,
        LeaderChanged,
        LootMethodChanged,
        Disbanded
    }

    /// <summary>
    /// Types of trainer operations.
    /// </summary>
    public enum TrainerOperationType
    {
        WindowOpened,
        WindowClosed,
        SpellsListed,
        SpellLearned,
        SpellLearningFailed
    }

    /// <summary>
    /// Types of talent operations.
    /// </summary>
    public enum TalentOperationType
    {
        PointAllocated,
        PointRemoved,
        TalentsReset,
        PointsUpdated
    }

    /// <summary>
    /// Types of profession operations.
    /// </summary>
    public enum ProfessionOperationType
    {
        SkillGained,
        RecipeLearned,
        ItemCrafted,
        CraftingStarted,
        CraftingCompleted,
        CraftingFailed
    }

    /// <summary>
    /// Recipe difficulty levels.
    /// </summary>
    public enum RecipeDifficulty
    {
        Trivial,
        Easy,
        Medium,
        Hard,
        Impossible
    }

    /// <summary>
    /// Types of emotes.
    /// </summary>
    public enum EmoteType
    {
        Text,
        Animated,
        Sound,
        State
    }

    /// <summary>
    /// Types of group loot methods.
    /// </summary>
    public enum GroupLootMethod
    {
        FreeForAll = 0,
        RoundRobin = 1,
        MasterLoot = 2,
        GroupLoot = 3,
        NeedBeforeGreed = 4
    }

    /// <summary>
    /// Types of loot slots.
    /// </summary>
    public enum LootSlotType
    {
        Item,
        Money,
        QuestItem,
        Currency
    }

    /// <summary>
    /// Types of group loot notifications.
    /// </summary>
    public enum GroupLootNotificationType
    {
        RollStarted,
        RollEnded,
        PlayerRolled,
        ItemWon,
        ItemAssigned,
        RollPassed,
        AutoPassed,
        LootMethodChanged,
        ThresholdChanged,
        MasterLooterChanged
    }
}