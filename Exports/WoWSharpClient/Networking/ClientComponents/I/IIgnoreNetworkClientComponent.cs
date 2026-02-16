using GameData.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for managing the in-game ignore list via network packets.
    /// </summary>
    public interface IIgnoreNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a readonly snapshot of ignored player GUIDs.
        /// MaNGOS SMSG_IGNORE_LIST sends GUIDs, not names.
        /// </summary>
        IReadOnlyList<ulong> IgnoredPlayers { get; }

        bool IsIgnoreListInitialized { get; }

        event Action<IReadOnlyList<ulong>>? IgnoreListUpdated;
        event Action<string, string>? IgnoreOperationFailed;

        Task RequestIgnoreListAsync(CancellationToken cancellationToken = default);
        Task AddIgnoreAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a player from the ignore list by GUID (CMSG_DEL_IGNORE sends uint64 ignoreGUID).
        /// </summary>
        Task RemoveIgnoreAsync(ulong ignoreGuid, CancellationToken cancellationToken = default);

        void HandleServerResponse(Opcode opcode, byte[] data);
    }
}