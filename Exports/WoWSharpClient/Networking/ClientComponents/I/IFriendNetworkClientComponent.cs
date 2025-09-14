using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for managing the in-game friends list via network packets.
    /// Supports requesting the list, adding and removing friends, and handling friend status updates.
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
        /// Raised when the full friend list is received or refreshed.
        /// </summary>
        event Action<IReadOnlyList<FriendEntry>>? FriendListUpdated;

        /// <summary>
        /// Raised when the status of a single friend changes (online/offline/area/etc.).
        /// </summary>
        event Action<FriendEntry>? FriendStatusChanged;

        /// <summary>
        /// Raised when an operation fails.
        /// </summary>
        event Action<string, string>? FriendOperationFailed;

        /// <summary>
        /// Requests the friends list from the server (CMSG_FRIEND_LIST).
        /// The result should come in via SMSG_FRIEND_LIST and will update the cache.
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

        /// <summary>
        /// Processes server responses for friend-related opcodes.
        /// Should be called by the world client dispatch when SMSG_FRIEND_LIST or SMSG_FRIEND_STATUS are received.
        /// </summary>
        void HandleServerResponse(Opcode opcode, byte[] data);
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
