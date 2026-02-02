using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Ignore list management over the WoW protocol (reactive implementation).
    /// Provides an observable stream of ignore list snapshots sourced from the world client's opcode stream.
    /// </summary>
    public class IgnoreNetworkClientComponent : NetworkClientComponent, IIgnoreNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<IgnoreNetworkClientComponent> _logger;

        private readonly List<string> _ignored = [];
        private readonly object _lock = new();
        private bool _disposed;

        // Reactive stream for ignore list updates
        private readonly IObservable<IReadOnlyList<string>> _ignoreListUpdates;

        public IgnoreNetworkClientComponent(IWorldClient worldClient, ILogger<IgnoreNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _ignoreListUpdates = SafeOpcodeStream(Opcode.SMSG_IGNORE_LIST)
                .Select(ParseIgnoreList)
                .Do(ApplyIgnoreList)
                .Publish()
                .RefCount();
        }

        public IReadOnlyList<string> IgnoredPlayers
        {
            get { lock (_lock) return _ignored.ToList().AsReadOnly(); }
        }

        public bool IsIgnoreListInitialized { get; private set; }

        /// <summary>
        /// Stream of ignore list snapshots. Each emission is the full current list.
        /// </summary>
        public IObservable<IReadOnlyList<string>> IgnoreListUpdates => _ignoreListUpdates;

        #region Legacy Event Interface (Not Supported)
        event Action<IReadOnlyList<string>>? IIgnoreNetworkClientComponent.IgnoreListUpdated { add => ThrowEventsNotSupported(); remove { } }
        event Action<string, string>? IIgnoreNetworkClientComponent.IgnoreOperationFailed { add => ThrowEventsNotSupported(); remove { } }
        private void ThrowEventsNotSupported() => throw new NotSupportedException("Events are not supported; use reactive observables instead (IgnoreListUpdates).");
        #endregion

        public async Task RequestIgnoreListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting ignore list (via friend list refresh)");
                // Classic protocol provides only CMSG_FRIEND_LIST; server answers both friend and ignore lists
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

        public async Task RemoveIgnoreAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                _logger.LogDebug("Removing ignore: {Player}", playerName);
                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[^1] = 0;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_DEL_IGNORE, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove ignore: {Player}", playerName);
                throw;
            }
        }

        // Interface test hook compatibility
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

        private static IReadOnlyList<string> ParseIgnoreList(ReadOnlyMemory<byte> payload)
        {
            using var br = new BinaryReader(new MemoryStream(payload.ToArray()));
            uint count = 0;
            if (br.BaseStream.Length - br.BaseStream.Position >= 4)
                count = br.ReadUInt32();

            var list = new List<string>((int)count);
            for (int i = 0; i < count && br.BaseStream.Position < br.BaseStream.Length; i++)
            {
                list.Add(ReadCString(br));
            }
            return list.AsReadOnly();
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

        private void ApplyIgnoreList(IReadOnlyList<string> list)
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
