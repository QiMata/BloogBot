using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive.Linq;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Ignore list management over the WoW protocol.
    /// </summary>
    public class IgnoreNetworkClientComponent : NetworkClientComponent, IIgnoreNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<IgnoreNetworkClientComponent> _logger;

        private readonly List<string> _ignored = [];
        private readonly object _lock = new();
        private bool _disposed;

        public IgnoreNetworkClientComponent(IWorldClient worldClient, ILogger<IgnoreNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to world client opcode stream (guard against null observable in tests/mocks)
            var stream = _worldClient.RegisterOpcodeHandler(Opcode.SMSG_IGNORE_LIST);
            if (stream is not null)
            {
                _ = stream.Subscribe(payload => HandleServerResponse(Opcode.SMSG_IGNORE_LIST, payload.ToArray()));
            }
        }

        public IReadOnlyList<string> IgnoredPlayers
        {
            get { lock (_lock) return _ignored.ToList().AsReadOnly(); }
        }

        public bool IsIgnoreListInitialized { get; private set; }

        public event Action<IReadOnlyList<string>>? IgnoreListUpdated;
        public event Action<string, string>? IgnoreOperationFailed;

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
                IgnoreOperationFailed?.Invoke("RequestIgnoreList", ex.Message);
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
                IgnoreOperationFailed?.Invoke("AddIgnore", ex.Message);
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
                IgnoreOperationFailed?.Invoke("RemoveIgnore", ex.Message);
                throw;
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

        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_IGNORE_LIST:
                        HandleIgnoreList(data);
                        break;
                    default:
                        _logger.LogDebug("Unhandled ignore-related opcode: {Opcode}", opcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ignore server response for opcode: {Opcode}", opcode);
            }
        }

        private void HandleIgnoreList(byte[] data)
        {
            try
            {
                using var br = new BinaryReader(new MemoryStream(data));
                uint count = 0;
                if (br.BaseStream.Length - br.BaseStream.Position >= 4)
                    count = br.ReadUInt32();

                var list = new List<string>((int)count);
                for (int i = 0; i < count && br.BaseStream.Position < br.BaseStream.Length; i++)
                {
                    var name = ReadCString(br);
                    list.Add(name);
                }

                lock (_lock)
                {
                    _ignored.Clear();
                    _ignored.AddRange(list);
                    IsIgnoreListInitialized = true;
                }

                _logger.LogInformation("Ignore list received (entries: {Count})", list.Count);
                IgnoreListUpdated?.Invoke(IgnoredPlayers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse ignore list payload of {Len} bytes", data.Length);
            }
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the ignore network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing IgnoreNetworkClientComponent");

            // Clear events to prevent memory leaks
            IgnoreListUpdated = null;
            IgnoreOperationFailed = null;

            _disposed = true;
            _logger.LogDebug("IgnoreNetworkClientComponent disposed");
        }

        #endregion
    }
}
