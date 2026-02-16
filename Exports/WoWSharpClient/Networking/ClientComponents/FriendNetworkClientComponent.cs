using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Friend list management over the WoW protocol (reactive variant).
    /// MaNGOS 1.12.1 packet formats.
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

        /// <summary>
        /// CMSG_ADD_FRIEND: CString playerName (null-terminated).
        /// </summary>
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

        /// <summary>
        /// CMSG_DEL_FRIEND: uint64 friendGUID.
        /// MaNGOS reads an ObjectGuid (8 bytes), not a player name.
        /// </summary>
        public async Task RemoveFriendAsync(ulong friendGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Removing friend GUID: {Guid}", friendGuid);
                var payload = new byte[8];
                BitConverter.GetBytes(friendGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_DEL_FRIEND, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove friend GUID: {Guid}", friendGuid);
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

        /// <summary>
        /// Parses SMSG_FRIEND_LIST. MaNGOS 1.12.1 format:
        /// uint8 friendCount
        /// [repeat friendCount]
        ///   uint64 friendGUID
        ///   uint8  status (0=offline, 1=online, 2=AFK, 4=DND)
        ///   [if status != 0]
        ///     uint32 areaID
        ///     uint32 level
        ///     uint32 class
        /// </summary>
        public static IReadOnlyList<FriendEntry> ParseFriendList(ReadOnlySpan<byte> data)
        {
            if (data.Length < 1) return Array.Empty<FriendEntry>();

            int offset = 0;
            byte count = data[offset++];
            var list = new List<FriendEntry>(count);

            for (int i = 0; i < count; i++)
            {
                if (offset + 9 > data.Length) break; // need at least guid(8) + status(1)

                var entry = new FriendEntry();
                entry.Guid = BitConverter.ToUInt64(data.Slice(offset, 8));
                offset += 8;
                entry.Status = data[offset++];
                entry.IsOnline = entry.Status != 0;

                if (entry.Status != 0)
                {
                    if (offset + 12 > data.Length) break; // need area(4) + level(4) + class(4)
                    entry.AreaId = BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                    entry.Level = BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                    entry.Class = (Class)BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                }

                list.Add(entry);
            }

            return list.AsReadOnly();
        }

        private void HandleFriendList(byte[] data)
        {
            try
            {
                var list = ParseFriendList(data);

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

        /// <summary>
        /// Parses SMSG_FRIEND_STATUS. MaNGOS 1.12.1 format:
        /// uint8  resultCode (FriendsResult enum)
        /// uint64 friendGUID
        /// [if resultCode == AddedOnline or Online]
        ///   uint8  status (online sub-status for 1.12.1)
        ///   uint32 areaID
        ///   uint32 level
        ///   uint32 class
        /// </summary>
        public static (FriendsResult Result, FriendEntry Entry) ParseFriendStatus(ReadOnlySpan<byte> data)
        {
            if (data.Length < 9) return (FriendsResult.DbError, new FriendEntry());

            int offset = 0;
            var result = (FriendsResult)data[offset++];
            ulong guid = BitConverter.ToUInt64(data.Slice(offset, 8));
            offset += 8;

            var entry = new FriendEntry { Guid = guid };

            if (result == FriendsResult.AddedOnline || result == FriendsResult.Online)
            {
                if (offset + 13 <= data.Length) // status(1) + area(4) + level(4) + class(4)
                {
                    entry.Status = data[offset++];
                    entry.IsOnline = true;
                    entry.AreaId = BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                    entry.Level = BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                    entry.Class = (Class)BitConverter.ToUInt32(data.Slice(offset, 4));
                    offset += 4;
                }
            }

            return (result, entry);
        }

        private void HandleFriendStatus(byte[] data)
        {
            try
            {
                var (result, entry) = ParseFriendStatus(data);
                _logger.LogDebug("Friend status: {Result} for GUID {Guid}", result, entry.Guid);

                lock (_lock)
                {
                    switch (result)
                    {
                        case FriendsResult.AddedOnline:
                        case FriendsResult.AddedOffline:
                            if (!_friends.Any(f => f.Guid == entry.Guid))
                                _friends.Add(entry);
                            break;
                        case FriendsResult.Removed:
                            _friends.RemoveAll(f => f.Guid == entry.Guid);
                            entry.IsOnline = false;
                            break;
                        case FriendsResult.Online:
                            var existing = _friends.FirstOrDefault(f => f.Guid == entry.Guid);
                            if (existing != null)
                            {
                                existing.IsOnline = true;
                                existing.Status = entry.Status;
                                existing.AreaId = entry.AreaId;
                                existing.Level = entry.Level;
                                existing.Class = entry.Class;
                                entry = existing;
                            }
                            else
                            {
                                _friends.Add(entry);
                            }
                            break;
                        case FriendsResult.Offline:
                            var offlineEntry = _friends.FirstOrDefault(f => f.Guid == entry.Guid);
                            if (offlineEntry != null)
                            {
                                offlineEntry.IsOnline = false;
                                offlineEntry.Status = 0;
                                entry = offlineEntry;
                            }
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
