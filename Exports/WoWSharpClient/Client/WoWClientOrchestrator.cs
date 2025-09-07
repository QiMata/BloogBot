using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Networking.Implementation;
using WoWSharpClient.Networking.I;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// High-level orchestrator that manages AuthClient and WorldClient lifecycles.
    /// Provides a unified interface for the complete WoW client experience.
    /// </summary>
    public sealed class WoWClientOrchestrator : IDisposable
    {
        private AuthClient? _authClient;
        private WorldClient? _worldClient;
        private bool _disposed;
        private uint _pingCounter = 0;

        /// <summary>
        /// Gets a value indicating whether the auth client is connected.
        /// </summary>
        public bool IsAuthConnected => _authClient?.IsConnected ?? false;

        /// <summary>
        /// Gets a value indicating whether the world client is connected.
        /// </summary>
        public bool IsWorldConnected => _worldClient?.IsConnected ?? false;

        /// <summary>
        /// Gets the current username from the auth client.
        /// </summary>
        public string Username => _authClient?.Username ?? string.Empty;

        /// <summary>
        /// Gets the session key from the auth client.
        /// </summary>
        public byte[] SessionKey => _authClient?.SessionKey ?? Array.Empty<byte>();

        /// <summary>
        /// Connects to the authentication server and performs login.
        /// </summary>
        /// <param name="host">The auth server hostname or IP address.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="port">The auth server port (default 3724).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoginAsync(string host, string username, string password, int port = 3724, CancellationToken cancellationToken = default)
        {
            if (_authClient != null)
            {
                _authClient.Dispose();
            }

            // Create auth client with networking stack
            var authConnection = new TcpConnection();
            var authEncryptor = new NoEncryption(); // Auth server uses no encryption
            IMessageFramer authFramer = new LengthPrefixedFramer(4, false); // Auth uses length-prefixed frames
            var authCodec = new WoWPacketCodec(); // Or create an AuthPacketCodec if needed
            var authRouter = new MessageRouter<Opcode>();

            _authClient = new AuthClient(authConnection, authFramer, authEncryptor, authCodec, authRouter);

            await _authClient.ConnectAsync(host, port, cancellationToken);
            await _authClient.LoginAsync(username, password, cancellationToken);
        }

        /// <summary>
        /// Gets the list of available realms from the authentication server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available realms.</returns>
        public async Task<List<Realm>> GetRealmListAsync(CancellationToken cancellationToken = default)
        {
            if (_authClient == null)
                throw new InvalidOperationException("Not connected to authentication server");

            return await _authClient.GetRealmListAsync(cancellationToken);
        }

        /// <summary>
        /// Connects to the specified realm's world server.
        /// </summary>
        /// <param name="realm">The realm to connect to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ConnectToRealmAsync(Realm realm, CancellationToken cancellationToken = default)
        {
            if (_authClient == null)
                throw new InvalidOperationException("Not authenticated");

            if (_worldClient != null)
            {
                _worldClient.Dispose();
            }

            // Create world client with networking stack
            var worldConnection = new TcpConnection();
            var worldEncryptor = new NoEncryption(); // Start with no encryption, switch to RC4 after auth
            IMessageFramer worldFramer = new WoWMessageFramer(); // World uses WoW-specific framing
            var worldCodec = new WoWPacketCodec();
            var worldRouter = new MessageRouter<Opcode>();

            _worldClient = new WorldClient(worldConnection, worldFramer, worldEncryptor, worldCodec, worldRouter);

            // Connect to world server using realm information and session key
            var realmHost = _authClient.IPAddress?.ToString() ?? "127.0.0.1";
            await _worldClient.ConnectAsync(Username, realmHost, SessionKey, realm.AddressPort, cancellationToken);
        }

        /// <summary>
        /// Disconnects from the authentication server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DisconnectAuthAsync(CancellationToken cancellationToken = default)
        {
            if (_authClient != null)
            {
                await _authClient.DisconnectAsync(cancellationToken);
                _authClient.Dispose();
                _authClient = null;
            }
        }

        /// <summary>
        /// Disconnects from the world server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DisconnectWorldAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient != null)
            {
                await _worldClient.DisconnectAsync(cancellationToken);
                _worldClient.Dispose();
                _worldClient = null;
            }
        }

        // Proxy methods for world client operations
        
        /// <summary>
        /// Sends a character enumeration request.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RefreshCharacterListAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendCharEnumAsync(cancellationToken);
        }

        /// <summary>
        /// Logs into the world with the specified character.
        /// </summary>
        /// <param name="characterGuid">The character GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task EnterWorldAsync(ulong characterGuid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendPlayerLoginAsync(characterGuid, cancellationToken);
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        /// <param name="type">The chat message type.</param>
        /// <param name="language">The language of the message.</param>
        /// <param name="destination">The destination (for whispers/channels).</param>
        /// <param name="message">The message text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendChatMessageAsync(ChatMsg type, Language language, string destination, string message, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendChatMessageAsync(type, language, destination, message, cancellationToken);
        }

        /// <summary>
        /// Sends a name query for the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendNameQueryAsync(guid, cancellationToken);
        }

        /// <summary>
        /// Sends a move world port acknowledge.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendMoveWorldPortAckAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendMoveWorldPortAckAsync(cancellationToken);
        }

        /// <summary>
        /// Sends a set active mover packet.
        /// </summary>
        /// <param name="guid">The mover GUID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendSetActiveMoverAsync(guid, cancellationToken);
        }

        /// <summary>
        /// Sends a movement packet.
        /// </summary>
        /// <param name="opcode">The movement opcode.</param>
        /// <param name="movementInfo">The movement information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendMovementAsync(Opcode opcode, byte[] movementInfo, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendMovementAsync(opcode, movementInfo, cancellationToken);
        }

        /// <summary>
        /// Sends a ping to the world server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendPingAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendPingAsync(_pingCounter++, cancellationToken);
        }

        /// <summary>
        /// Sends a time query to the world server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task QueryTimeAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            await _worldClient.SendQueryTimeAsync(cancellationToken);
        }

        /// <summary>
        /// Updates the world client's encryptor (typically to RC4 after authentication).
        /// </summary>
        /// <param name="newEncryptor">The new encryptor to use.</param>
        public void UpdateWorldEncryption(IEncryptor newEncryptor)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            _worldClient.UpdateEncryptor(newEncryptor);
        }

        // Event exposure

        /// <summary>
        /// Exposes world client connection events.
        /// </summary>
        public event Action? WorldConnected
        {
            add
            {
                if (_worldClient != null)
                    _worldClient.Connected += value;
            }
            remove
            {
                if (_worldClient != null)
                    _worldClient.Connected -= value;
            }
        }

        /// <summary>
        /// Exposes world client disconnection events.
        /// </summary>
        public event Action<Exception?>? WorldDisconnected
        {
            add
            {
                if (_worldClient != null)
                    _worldClient.Disconnected += value;
            }
            remove
            {
                if (_worldClient != null)
                    _worldClient.Disconnected -= value;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _authClient?.Dispose();
                _worldClient?.Dispose();
                _disposed = true;
            }
        }
    }
}