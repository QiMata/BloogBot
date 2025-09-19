using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Friend list management over the WoW protocol (reactive variant).
    /// </summary>
    public class FriendNetworkClientComponent : NetworkClientComponent, IFriendNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<FriendNetworkClientComponent> _logger;
        private readonly List<FriendEntry> _friends = [];
        private readonly object _lock = new();
        private readonly Subject<IReadOnlyList<FriendEntry>> _friendListUpdates = new();
        private readonly Subject<FriendEntry> _friendStatusUpdates = new();

        private bool _disposed;

        public FriendNetworkClientComponent(IWorldClient worldClient, ILogger<FriendNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to world client handlers directly (guard against null observable in tests/mocks)
            var listStream = _worldClient.RegisterOpcodeHandler(Opcode.SMSG_FRIEND_LIST);
            if (listStream is not null)
            {
                _ = listStream.Subscribe(payload => HandleServerResponse(Opcode.SMSG_FRIEND_LIST, payload.ToArray()));
            }

            var statusStream = _worldClient.RegisterOpcodeHandler(Opcode.SMSG_FRIEND_STATUS);
            if (statusStream is not null)
            {
                _ = statusStream.Subscribe(payload => HandleServerResponse(Opcode.SMSG_FRIEND_STATUS, payload.ToArray()));
            }
        }

        public IReadOnlyList<FriendEntry> Friends
        {
            get { lock (_lock) return _friends.ToList().AsReadOnly(); }
        }

        public bool IsFriendListInitialized { get; private set; }

        public IObservable<IReadOnlyList<FriendEntry>> FriendListUpdates => _friendListUpdates.AsObservable();
        public IObservable<FriendEntry> FriendStatusUpdates => _friendStatusUpdates.AsObservable();

        public async Task RequestFriendListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting friend list");
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_FRIEND_LIST, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request friend list");
                throw;
            }
        }

        public async Task AddFriendAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                _logger.LogDebug("Adding friend: {Player}", playerName);
                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[^1] = 0;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ADD_FRIEND, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add friend: {Player}", playerName);
                throw;
            }
        }

        public async Task RemoveFriendAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                _logger.LogDebug("Removing friend: {Player}", playerName);
                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[^1] = 0;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_DEL_FRIEND, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove friend: {Player}", playerName);
                throw;
            }
        }

        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_FRIEND_LIST:
                        HandleFriendList(data);
                        break;
                    case Opcode.SMSG_FRIEND_STATUS:
                        HandleFriendStatus(data);
                        break;
                    default:
                        _logger.LogDebug("Unhandled friend-related opcode: {Opcode}", opcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling friend server response for opcode: {Opcode}", opcode);
            }
        }

        private static string ReadCString(BinaryReader br)
        {
            var bytes = new List<byte>(32);
            byte b;
            while (br.BaseStream.Position < br.BaseStream.Length && (b = br.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private void HandleFriendList(byte[] data)
        {
            try
            {
                using var br = new BinaryReader(new MemoryStream(data));
                uint count = 0;
                if (br.BaseStream.Length - br.BaseStream.Position >= 4)
                    count = br.ReadUInt32();

                var list = new List<FriendEntry>((int)count);

                for (int i = 0; i < count && br.BaseStream.Position < br.BaseStream.Length; i++)
                {
                    var entry = new FriendEntry();
                    entry.Name = ReadCString(br);

                    if (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        // Online status (0 = offline, 1 = online) - heuristic
                        byte status = br.ReadByte();
                        entry.IsOnline = status != 0;
                    }

                    if (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        // Optional level + class if provided (heuristic)
                        entry.Level = br.ReadByte();
                    }

                    if (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        entry.Class = (Class)br.ReadByte();
                    }

                    // Optional area string (depending on server implementation)
                    if (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        try
                        {
                            entry.Area = ReadCString(br);
                        }
                        catch
                        {
                            entry.Area = string.Empty;
                        }
                    }

                    list.Add(entry);
                }

                lock (_lock)
                {
                    _friends.Clear();
                    _friends.AddRange(list);
                    IsFriendListInitialized = true;
                }

                _logger.LogInformation("Friend list received (entries: {Count})", list.Count);
                _friendListUpdates.OnNext(Friends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse friend list payload of {Len} bytes", data.Length);
            }
        }

        private enum FriendStatusCode : byte
        {
            Added = 0,
            Ignored = 1,
            Removed = 2,
            Online = 3,
            Offline = 4
        }

        private void HandleFriendStatus(byte[] data)
        {
            try
            {
                using var br = new BinaryReader(new MemoryStream(data));
                if (br.BaseStream.Length == 0) return;

                var code = (FriendStatusCode)br.ReadByte();
                var name = ReadCString(br);

                FriendEntry entry;
                lock (_lock)
                {
                    entry = _friends.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            ?? new FriendEntry { Name = name };

                    switch (code)
                    {
                        case FriendStatusCode.Added:
                            if (!_friends.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                _friends.Add(entry);
                            break;
                        case FriendStatusCode.Removed:
                            _friends.RemoveAll(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                            entry.IsOnline = false; // emit a final offline snapshot
                            break;
                        case FriendStatusCode.Online:
                            entry.IsOnline = true;
                            if (br.BaseStream.Position < br.BaseStream.Length) entry.Level = br.ReadByte();
                            if (br.BaseStream.Position < br.BaseStream.Length) entry.Class = (Class)br.ReadByte();
                            if (br.BaseStream.Position < br.BaseStream.Length) entry.Area = ReadCString(br);
                            if (!_friends.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                                _friends.Add(entry);
                            break;
                        case FriendStatusCode.Offline:
                            entry.IsOnline = false;
                            break;
                    }
                }

                _friendStatusUpdates.OnNext(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse friend status payload of {Len} bytes", data.Length);
            }
        }

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("Disposing FriendNetworkClientComponent");
            _friendListUpdates.OnCompleted();
            _friendStatusUpdates.OnCompleted();
            _friendListUpdates.Dispose();
            _friendStatusUpdates.Dispose();
        }
        #endregion
    }
}
