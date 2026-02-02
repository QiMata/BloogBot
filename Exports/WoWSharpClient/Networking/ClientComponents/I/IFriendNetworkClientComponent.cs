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
        /// <summary>
        /// Gets a readonly snapshot of the current friends list.
        /// </summary>
        IReadOnlyList<FriendEntry> Friends { get; }

        /// <summary>
        /// True if the client has requested and cached the list at least once.
        /// </summary>
        bool IsFriendListInitialized { get; }

        /// <summary>
        /// Stream of full friend list refreshes (after parsing SMSG_FRIEND_LIST).
        /// Emits the current cached snapshot each time.
        /// </summary>
        IObservable<IReadOnlyList<FriendEntry>> FriendListUpdates { get; }

        /// <summary>
        /// Stream of individual friend status changes (after parsing SMSG_FRIEND_STATUS).
        /// Emits the updated FriendEntry snapshot (for removals emits the removed entry with IsOnline=false).
        /// </summary>
        IObservable<FriendEntry> FriendStatusUpdates { get; }

        /// <summary>
        /// Requests the friends list from the server (CMSG_FRIEND_LIST).
        /// The result will arrive via FriendListUpdates.
        /// </summary>
        Task RequestFriendListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a friend by player name (CMSG_ADD_FRIEND).
        /// </summary>
        Task AddFriendAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a friend by player name (CMSG_DEL_FRIEND).
        /// </summary>
        Task RemoveFriendAsync(string playerName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a single friend entry snapshot.
    /// </summary>
    public class FriendEntry
    {
        /// <summary>
        /// Friend character name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// GUID if known (may be null until status update provides it).
        /// </summary>
        public ulong? Guid { get; set; }
        /// <summary>
        /// Online status.
        /// </summary>
        public bool IsOnline { get; set; }
        /// <summary>
        /// Friend level when online (if provided by server).
        /// </summary>
        public uint Level { get; set; }
        /// <summary>
        /// Friend class when online (if provided by server).
        /// </summary>
        public Class Class { get; set; }
        /// <summary>
        /// Area/zone name (if provided by server when online).
        /// </summary>
        public string Area { get; set; } = string.Empty;
    }
}
