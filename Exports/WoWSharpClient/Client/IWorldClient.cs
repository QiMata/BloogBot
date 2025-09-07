using GameData.Core.Enums;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// Interface for world client operations, enabling testability and abstraction.
    /// </summary>
    public interface IWorldClient : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets a value indicating whether the client is authenticated with the world server.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Connects to the world server and stores authentication details for later use.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="host">The hostname or IP address.</param>
        /// <param name="sessionKey">The session key from authentication server.</param>
        /// <param name="port">The port number (default 8085).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ConnectAsync(string username, string host, byte[] sessionKey, int port = 8085, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the world server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a movement packet with the specified opcode and movement data.
        /// </summary>
        /// <param name="opcode">The movement opcode.</param>
        /// <param name="movementInfo">The movement information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendMovementAsync(Opcode opcode, byte[] movementInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a character enumeration request.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendCharEnumAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a ping packet to the server.
        /// </summary>
        /// <param name="sequence">The ping sequence number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendPingAsync(uint sequence, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a time query request to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendQueryTimeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a player login request for the specified character GUID.
        /// </summary>
        /// <param name="guid">The character GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendPlayerLoginAsync(ulong guid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        /// <param name="type">The chat message type.</param>
        /// <param name="language">The language of the message.</param>
        /// <param name="destinationName">The destination (for whispers/channels).</param>
        /// <param name="message">The message text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendChatMessageAsync(ChatMsg type, Language language, string destinationName, string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a name query for the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a move world port acknowledge.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendMoveWorldPortAckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a set active mover packet.
        /// </summary>
        /// <param name="guid">The mover GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the encryptor (typically switching from NoEncryption to RC4 after authentication).
        /// </summary>
        /// <param name="newEncryptor">The new encryptor to use.</param>
        void UpdateEncryptor(IEncryptor newEncryptor);

        /// <summary>
        /// Exposes connection events.
        /// </summary>
        event Action? Connected;

        /// <summary>
        /// Exposes disconnection events.
        /// </summary>
        event Action<Exception?>? Disconnected;

        /// <summary>
        /// Fired when world server authentication succeeds.
        /// </summary>
        event Action? OnAuthenticationSuccessful;

        /// <summary>
        /// Fired when world server authentication fails.
        /// </summary>
        /// <param name="errorCode">The authentication error code.</param>
        event Action<byte>? OnAuthenticationFailed;

        /// <summary>
        /// Fired when a character is found during character enumeration.
        /// </summary>
        /// <param name="guid">Character GUID.</param>
        /// <param name="name">Character name.</param>
        /// <param name="race">Character race.</param>
        /// <param name="characterClass">Character class.</param>
        /// <param name="gender">Character gender.</param>
        event Action<ulong, string, byte, byte, byte>? OnCharacterFound;

        /// <summary>
        /// Fired when attack state changes (start/stop).
        /// </summary>
        /// <param name="isAttacking">Whether attacking started or stopped.</param>
        /// <param name="attackerGuid">The attacker's GUID.</param>
        /// <param name="victimGuid">The victim's GUID.</param>
        event Action<bool, ulong, ulong>? OnAttackStateChanged;

        /// <summary>
        /// Fired when an attack error occurs (not in range, bad facing, etc.).
        /// </summary>
        /// <param name="errorMessage">The error message describing why the attack failed.</param>
        event Action<string>? OnAttackError;
    }
}