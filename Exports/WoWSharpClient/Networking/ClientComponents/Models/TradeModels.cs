using System;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Trade status codes from MaNGOS 1.12.1 SharedDefines.h.
    /// Sent as uint32 in SMSG_TRADE_STATUS.
    /// </summary>
    public enum TradeStatus : uint
    {
        Busy = 0,
        BeginTrade = 1,
        OpenWindow = 2,
        TradeCanceled = 3,
        TradeAccept = 4,
        Busy2 = 5,
        NoTarget = 6,
        BackToTrade = 7,
        TradeComplete = 8,
        TradeRejected = 9,
        TargetTooFar = 10,
        WrongFaction = 11,
        CloseWindow = 12,
        Unknown13 = 13,
        IgnoreYou = 14,
        YouStunned = 15,
        TargetStunned = 16,
        YouDead = 17,
        TargetDead = 18,
        YouLogout = 19,
        TargetLogout = 20,
        TrialAccount = 21,
        OnlyConjured = 22
    }

    /// <summary>
    /// Information about a single item in a trade slot, parsed from SMSG_TRADE_STATUS_EXTENDED.
    /// </summary>
    public readonly record struct TradeItemInfo(
        byte SlotIndex,
        uint ItemEntry,
        uint DisplayInfoId,
        uint StackCount,
        bool IsWrapped,
        ulong GiftCreatorGuid,
        uint PermanentEnchantmentId,
        ulong CreatorGuid,
        int SpellCharges,
        uint SuffixFactor,
        uint RandomPropertyId,
        uint LockId,
        uint MaxDurability,
        uint CurrentDurability
    )
    {
        public bool IsEmpty => ItemEntry == 0;
    }

    /// <summary>
    /// Full trade window state parsed from SMSG_TRADE_STATUS_EXTENDED.
    /// </summary>
    public class TradeWindowData
    {
        /// <summary>Whether this is the trader's view (true) or our own view (false).</summary>
        public bool IsTraderView { get; init; }

        /// <summary>Gold amount offered by the viewed party.</summary>
        public uint Gold { get; init; }

        /// <summary>Spell ID cast on the non-traded slot (slot 6), 0 if none.</summary>
        public uint SpellId { get; init; }

        /// <summary>Items in the 7 trade slots (0-5 tradeable, 6 non-traded display).</summary>
        public TradeItemInfo[] Items { get; init; } = Array.Empty<TradeItemInfo>();
    }
}
