using System;
using System.Reactive;
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
        /// Sends a packet with the specified opcode and payload data to the world server.
        /// This is a general-purpose method for sending any type of packet including movement,
        /// combat, targeting, chat, and other game protocol messages.
        /// </summary>
        /// <param name="opcode">The opcode identifying the type of packet to send (e.g., movement, combat, targeting).</param>
        /// <param name="payload">The packet payload data. Can be movement information, combat data, or any other packet-specific data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendOpcodeAsync(Opcode opcode, byte[] payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an observable stream for the specified server opcode. Each incoming packet payload
        /// for that opcode will be pushed to subscribers.
        /// </summary>
        /// <param name="opcode">The server opcode to observe.</param>
        /// <returns>An observable of raw payloads for the opcode.</returns>
        IObservable<ReadOnlyMemory<byte>> RegisterOpcodeHandler(Opcode opcode);

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

        // Reactive streams

        /// <summary>
        /// Stream that fires when the underlying connection is established.
        /// </summary>
        IObservable<Unit> WhenConnected { get; }

        /// <summary>
        /// Stream that fires when the underlying connection is disconnected.
        /// </summary>
        IObservable<Exception?> WhenDisconnected { get; }

        /// <summary>
        /// Fired when world server authentication succeeds.
        /// </summary>
        IObservable<Unit> AuthenticationSucceeded { get; }

        /// <summary>
        /// Fired when world server authentication fails (emits error code).
        /// </summary>
        IObservable<byte> AuthenticationFailed { get; }

        /// <summary>
        /// Stream of character enumeration results.
        /// </summary>
        IObservable<(ulong Guid, string Name, byte Race, byte Class, byte Gender)> CharacterFound { get; }

        /// <summary>
        /// Stream of attack state changes.
        /// </summary>
        IObservable<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)> AttackStateChanged { get; }

        /// <summary>
        /// Stream of attack errors (e.g., not in range, bad facing).
        /// </summary>
        IObservable<string> AttackErrors { get; }
    }
}