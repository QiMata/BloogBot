using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for managing the in-game ignore list via network packets.
    /// Supports requesting the list, adding and removing ignored players.
    /// </summary>
    public interface IIgnoreNetworkAgent
    {
        /// <summary>
        /// Gets a readonly snapshot of the current ignore list.
        /// </summary>
        IReadOnlyList<string> IgnoredPlayers { get; }

        /// <summary>
        /// True if the client has requested and cached the list at least once.
        /// </summary>
        bool IsIgnoreListInitialized { get; }

        /// <summary>
        /// Raised when the ignore list is updated from the server.
        /// </summary>
        event Action<IReadOnlyList<string>>? IgnoreListUpdated;

        /// <summary>
        /// Raised when an operation fails.
        /// </summary>
        event Action<string, string>? IgnoreOperationFailed;

        /// <summary>
        /// Requests the ignore list from the server (CMSG_IGNORE_LIST). The response is SMSG_IGNORE_LIST.
        /// </summary>
        Task RequestIgnoreListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a player to the ignore list by name (CMSG_ADD_IGNORE).
        /// </summary>
        Task AddIgnoreAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a player from the ignore list by name (CMSG_DEL_IGNORE).
        /// </summary>
        Task RemoveIgnoreAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes server responses for ignore-related opcodes (SMSG_IGNORE_LIST).
        /// </summary>
        void HandleServerResponse(Opcode opcode, byte[] data);
    }
}