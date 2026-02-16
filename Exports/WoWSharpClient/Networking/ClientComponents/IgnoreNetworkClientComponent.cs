using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Ignore list management over the WoW protocol.
    /// MaNGOS 1.12.1 packet formats.
    /// </summary>
    public class IgnoreNetworkClientComponent : NetworkClientComponent, IIgnoreNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<IgnoreNetworkClientComponent> _logger;

        private readonly List<ulong> _ignored = [];
        private readonly object _lock = new();
        private bool _disposed;

        private readonly IObservable<IReadOnlyList<ulong>> _ignoreListUpdates;

        public IgnoreNetworkClientComponent(IWorldClient worldClient, ILogger<IgnoreNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _ignoreListUpdates = SafeOpcodeStream(Opcode.SMSG_IGNORE_LIST)
                .Select(payload => ParseIgnoreList(payload.Span))
                .Do(ApplyIgnoreList)
                .Publish()
                .RefCount();
        }

        public IReadOnlyList<ulong> IgnoredPlayers
        {
            get { lock (_lock) return _ignored.ToList().AsReadOnly(); }
        }

        public bool IsIgnoreListInitialized { get; private set; }

        public IObservable<IReadOnlyList<ulong>> IgnoreListUpdates => _ignoreListUpdates;

        #region Legacy Event Interface (Not Supported)
        event Action<IReadOnlyList<ulong>>? IIgnoreNetworkClientComponent.IgnoreListUpdated { add => ThrowEventsNotSupported(); remove { } }
        event Action<string, string>? IIgnoreNetworkClientComponent.IgnoreOperationFailed { add => ThrowEventsNotSupported(); remove { } }
        private void ThrowEventsNotSupported() => throw new NotSupportedException("Events are not supported; use reactive observables instead (IgnoreListUpdates).");
        #endregion

        public async Task RequestIgnoreListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting ignore list (via friend list refresh)");
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_FRIEND_LIST, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request ignore list");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// CMSG_ADD_IGNORE: CString playerName (null-terminated).
        /// </summary>
        public async Task AddIgnoreAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                _logger.LogDebug("Adding ignore: {Player}", playerName);
                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[^1] = 0;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ADD_IGNORE, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add ignore: {Player}", playerName);
                throw;
            }
        }

        /// <summary>
        /// CMSG_DEL_IGNORE: uint64 ignoreGUID.
        /// MaNGOS reads an ObjectGuid (8 bytes), not a player name.
        /// </summary>
        public async Task RemoveIgnoreAsync(ulong ignoreGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Removing ignore GUID: {Guid}", ignoreGuid);
                var payload = new byte[8];
                BitConverter.GetBytes(ignoreGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_DEL_IGNORE, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove ignore GUID: {Guid}", ignoreGuid);
                throw;
            }
        }

        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                if (opcode == Opcode.SMSG_IGNORE_LIST)
                {
                    var list = ParseIgnoreList(data);
                    ApplyIgnoreList(list);
                }
                else
                {
                    _logger.LogDebug("Unhandled ignore-related opcode: {Opcode}", opcode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ignore server response for opcode: {Opcode}", opcode);
            }
        }

        #region Parsing & Reactive Helpers
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode) => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        /// <summary>
        /// Parses SMSG_IGNORE_LIST. MaNGOS 1.12.1 format:
        /// uint8 ignoreCount
        /// [repeat ignoreCount]
        ///   uint64 ignoreGUID
        /// </summary>
        public static IReadOnlyList<ulong> ParseIgnoreList(ReadOnlySpan<byte> data)
        {
            if (data.Length < 1) return Array.Empty<ulong>();

            int offset = 0;
            byte count = data[offset++];
            var list = new List<ulong>(count);

            for (int i = 0; i < count; i++)
            {
                if (offset + 8 > data.Length) break;
                list.Add(BitConverter.ToUInt64(data.Slice(offset, 8)));
                offset += 8;
            }

            return list.AsReadOnly();
        }

        private void ApplyIgnoreList(IReadOnlyList<ulong> list)
        {
            lock (_lock)
            {
                _ignored.Clear();
                _ignored.AddRange(list);
                IsIgnoreListInitialized = true;
            }
            _logger.LogInformation("Ignore list received (entries: {Count})", list.Count);
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("Disposing IgnoreNetworkClientComponent");
        }
        #endregion
    }
}
