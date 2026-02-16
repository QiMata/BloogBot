using GameData.Core.Enums;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Client
{
    /// <summary>
    /// Modern WoW Client implementation using the new composable networking architecture.
    /// This class provides backward compatibility while using the new AuthClient and WorldClient internally.
    /// </summary>
    public class WoWClient : IDisposable
    {
        private string _ipAddress = "127.0.0.1";
        private AuthClient? _authClient;
        private IWorldClient? _worldClient;
        private bool _isLoggedIn;
        private volatile bool _loginInProgress;
        private uint _pingCounter = 0;
        private bool _disposed;

        public bool IsLoggedIn => _isLoggedIn;

        public IWorldClient? WorldClient => _worldClient;

        public void Dispose()
        {
            if (!_disposed)
            {
                _authClient?.Dispose();
                _worldClient?.Dispose();
                _disposed = true;
            }
        }

        public bool IsLoginConnected() => _authClient?.IsConnected ?? false;
        public bool IsWorldConnected() => _worldClient?.IsConnected ?? false;

        private async Task ConnectToLoginServerAsync(CancellationToken cancellationToken = default)
        {
            _authClient?.Dispose();
            _authClient = WoWClientFactory.CreateAuthClient();
            
            await _authClient.ConnectAsync(_ipAddress, cancellationToken: cancellationToken);
            WoWSharpEventEmitter.Instance.FireOnLoginConnect();
        }

        public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            // Prevent concurrent login attempts — fire-and-forget callers can race
            if (_loginInProgress)
                return;

            _loginInProgress = true;
            try
            {
                if (_authClient == null || !_authClient.IsConnected)
                    await ConnectToLoginServerAsync(cancellationToken);

                await _authClient!.LoginAsync(username, password, cancellationToken);
                _isLoggedIn = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred during login: {ex}");
                _isLoggedIn = false;
                throw;
            }
            finally
            {
                _loginInProgress = false;
            }
        }

        public async Task<List<Realm>> GetRealmListAsync(CancellationToken cancellationToken = default)
        {
            if (_authClient == null)
                throw new InvalidOperationException("Not connected to auth server");
            
            return await _authClient.GetRealmListAsync(cancellationToken);
        }

        public async Task SelectRealmAsync(Realm realm, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(realm);
            
            if (_authClient == null)
                throw new InvalidOperationException("Not connected to auth server");

            _worldClient?.Dispose();
            _worldClient = WoWClientFactory.CreateWorldClient();
            
            var sessionKey = _authClient.SessionKey;
            var username = _authClient.Username;
            
            await _worldClient.ConnectAsync(username, _ipAddress, sessionKey, realm.AddressPort, cancellationToken);
        }

        public async Task RefreshCharacterSelectsAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendCharEnumAsync(cancellationToken);
        }

        public async Task EnterWorldAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendPlayerLoginAsync(guid, cancellationToken);
        }

        public async Task SendChatMessageAsync(ChatMsg chatMsgType, Language language, string destination, string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendChatMessageAsync(chatMsgType, language, destination, text, cancellationToken);
        }

        public async Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendNameQueryAsync(guid, cancellationToken);
        }

        public async Task SendMoveWorldPortAcknowledgeAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendMoveWorldPortAckAsync(cancellationToken);
        }

        public async Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendSetActiveMoverAsync(guid, cancellationToken);
        }

        public virtual async Task SendMovementOpcodeAsync(Opcode opcode, byte[] movementInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(movementInfo);
            
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendOpcodeAsync(opcode, movementInfo, cancellationToken);
        }

        public async Task SendMSGPackedAsync(Opcode opcode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendOpcodeAsync(opcode, payload, cancellationToken);
        }

        public async Task SendCharacterCreateAsync(string name, Race race, Class clazz, Gender gender, byte skin, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
            
            writer.Write(Encoding.UTF8.GetBytes(name));
            writer.Write((byte)0); // null terminator
            writer.Write((byte)race);
            writer.Write((byte)clazz);
            writer.Write((byte)gender);
            writer.Write(skin);
            writer.Write(face);
            writer.Write(hairStyle);
            writer.Write(hairColor);
            writer.Write(facialHair);
            writer.Write(outfitId);

            writer.Flush();
            await _worldClient.SendOpcodeAsync(Opcode.CMSG_CHAR_CREATE, ms.ToArray(), cancellationToken);
        }

        public async Task SendPingAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendPingAsync(_pingCounter++, cancellationToken);
        }

        public async Task QueryTimeAsync(CancellationToken cancellationToken = default)
        {
            if (_worldClient == null)
                throw new InvalidOperationException("Not connected to world server");
                
            await _worldClient.SendQueryTimeAsync(cancellationToken);
        }

        public void SetIpAddress(string ipAddress)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
            
            _ipAddress = ipAddress;
        }

        /// <summary>
        /// Disconnects from both authentication and world servers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            
            if (_worldClient != null)
            {
                tasks.Add(_worldClient.DisconnectAsync(cancellationToken));
            }
            
            if (_authClient != null)
            {
                tasks.Add(_authClient.DisconnectAsync(cancellationToken));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            _isLoggedIn = false;
        }

        /// <summary>
        /// Gets the current authentication state information.
        /// </summary>
        public ClientConnectionInfo GetConnectionInfo()
        {
            return new ClientConnectionInfo
            {
                IsAuthConnected = IsLoginConnected(),
                IsWorldConnected = IsWorldConnected(),
                IsLoggedIn = _isLoggedIn,
                IpAddress = _ipAddress,
                Username = _authClient?.Username ?? string.Empty
            };
        }

        /// <summary>
        /// Represents the current connection state of the WoW client.
        /// </summary>
        public record ClientConnectionInfo
        {
            public bool IsAuthConnected { get; init; }
            public bool IsWorldConnected { get; init; }
            public bool IsLoggedIn { get; init; }
            public string IpAddress { get; init; } = string.Empty;
            public string Username { get; init; } = string.Empty;
        }
    }
}
