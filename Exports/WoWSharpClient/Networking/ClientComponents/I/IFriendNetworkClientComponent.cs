using GameData.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for managing the in-game friends list via network packets.
    /// Exposes reactive streams instead of events.
    /// </summary>
    public interface IFriendNetworkClientComponent : INetworkClientComponent
    {
        IReadOnlyList<FriendEntry> Friends { get; }
        bool IsFriendListInitialized { get; }
        IObservable<IReadOnlyList<FriendEntry>> FriendListUpdates { get; }
        IObservable<FriendEntry> FriendStatusUpdates { get; }

        Task RequestFriendListAsync(CancellationToken cancellationToken = default);
        Task AddFriendAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a friend by GUID (CMSG_DEL_FRIEND sends uint64 friendGUID).
        /// </summary>
        Task RemoveFriendAsync(ulong friendGuid, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// MaNGOS 1.12.1 FriendsResult enum — result codes in SMSG_FRIEND_STATUS.
    /// </summary>
    public enum FriendsResult : byte
    {
        DbError = 0x00,
        ListFull = 0x01,
        Online = 0x02,
        Offline = 0x03,
        NotFound = 0x04,
        Removed = 0x05,
        AddedOnline = 0x06,
        AddedOffline = 0x07,
        Already = 0x08,
        Self = 0x09,
        Enemy = 0x0A,
        IgnoreFull = 0x0B,
        IgnoreSelf = 0x0C,
        IgnoreNotFound = 0x0D,
        IgnoreAlready = 0x0E,
        IgnoreAdded = 0x0F,
        IgnoreRemoved = 0x10,
    }

    /// <summary>
    /// Represents a single friend entry from SMSG_FRIEND_LIST / SMSG_FRIEND_STATUS.
    /// </summary>
    public class FriendEntry
    {
        /// <summary>Friend character name (resolved from name cache, not from packet).</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Friend GUID (always provided by server).</summary>
        public ulong Guid { get; set; }
        /// <summary>Online status (status byte != 0).</summary>
        public bool IsOnline { get; set; }
        /// <summary>Status byte: 0=offline, 1=online, 2=AFK, 4=DND.</summary>
        public byte Status { get; set; }
        /// <summary>Friend level (uint32 from server, only when online).</summary>
        public uint Level { get; set; }
        /// <summary>Friend class (uint32 from server, only when online).</summary>
        public Class Class { get; set; }
        /// <summary>Area/zone ID (uint32 from server, only when online).</summary>
        public uint AreaId { get; set; }
    }
}
