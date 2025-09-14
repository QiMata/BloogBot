using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Network agent for handling emote operations via network packets.
    /// Provides functionality to perform both animated emotes and text-based emotes.
    /// </summary>
    public class EmoteNetworkClientComponent : IEmoteNetworkClientComponent
    {
        #region Fields

        private readonly IWorldClient _worldClient;
        private readonly ILogger<EmoteNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();
        private volatile bool _isEmoting;
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the EmoteNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger for emote operations.</param>
        public EmoteNetworkClientComponent(IWorldClient worldClient, ILogger<EmoteNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region INetworkClientComponent Implementation

        /// <inheritdoc />
        public bool IsOperationInProgress
        {
            get
            {
                lock (_stateLock)
                {
                    return _isOperationInProgress;
                }
            }
        }

        /// <inheritdoc />
        public DateTime? LastOperationTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastOperationTime;
                }
            }
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event Action<Emote>? EmotePerformed;

        /// <inheritdoc />
        public event Action<TextEmote, ulong?>? TextEmotePerformed;

        /// <inheritdoc />
        public event Action<string>? EmoteError;

        /// <inheritdoc />
        public event Action<ulong, Emote>? EmoteReceived;

        /// <inheritdoc />
        public event Action<ulong, TextEmote, ulong?>? TextEmoteReceived;

        #endregion

        #region Properties

        /// <inheritdoc />
        public Emote? LastEmote { get; private set; }

        /// <inheritdoc />
        public TextEmote? LastTextEmote { get; private set; }

        /// <inheritdoc />
        public DateTime? LastEmoteTime { get; private set; }

        /// <inheritdoc />
        public bool IsEmoting => _isEmoting;

        #endregion

        #region Emote Operations

        /// <inheritdoc />
        public async Task PerformEmoteAsync(Emote emote, CancellationToken cancellationToken = default)
        {
            if (!IsValidEmote(emote))
            {
                var error = $"Invalid emote: {emote}";
                _logger.LogWarning(error);
                EmoteError?.Invoke(error);
                return;
            }

            try
            {
                SetOperationInProgress(true);
                _isEmoting = true;
                _logger.LogDebug("Performing emote: {Emote}", emote);

                // Create CMSG_EMOTE packet
                // Packet structure: uint32 emoteId
                var packet = new byte[4];
                BitConverter.GetBytes((uint)emote).CopyTo(packet, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_EMOTE, packet, cancellationToken);

                LastEmote = emote;
                LastEmoteTime = DateTime.UtcNow;

                _logger.LogInformation("Successfully performed emote: {Emote}", GetEmoteName(emote));
                EmotePerformed?.Invoke(emote);
            }
            catch (Exception ex)
            {
                var error = $"Failed to perform emote {emote}: {ex.Message}";
                _logger.LogError(ex, error);
                EmoteError?.Invoke(error);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
                _isEmoting = false;
            }
        }

        /// <inheritdoc />
        public async Task PerformTextEmoteAsync(TextEmote textEmote, ulong? targetGuid = null, CancellationToken cancellationToken = default)
        {
            if (!IsValidTextEmote(textEmote))
            {
                var error = $"Invalid text emote: {textEmote}";
                _logger.LogWarning(error);
                EmoteError?.Invoke(error);
                return;
            }

            try
            {
                SetOperationInProgress(true);
                _isEmoting = true;
                _logger.LogDebug("Performing text emote: {TextEmote} on target: {Target}", textEmote, targetGuid?.ToString("X") ?? "none");

                // Create CMSG_TEXT_EMOTE packet
                // Packet structure: uint32 textEmoteId, uint64 targetGuid
                var packet = new byte[12];
                BitConverter.GetBytes((uint)textEmote).CopyTo(packet, 0);
                BitConverter.GetBytes(targetGuid ?? 0).CopyTo(packet, 4);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TEXT_EMOTE, packet, cancellationToken);

                LastTextEmote = textEmote;
                LastEmoteTime = DateTime.UtcNow;

                var targetText = targetGuid.HasValue ? $" on target {targetGuid.Value:X}" : "";
                _logger.LogInformation("Successfully performed text emote: {TextEmote}{Target}", GetTextEmoteName(textEmote), targetText);
                TextEmotePerformed?.Invoke(textEmote, targetGuid);
            }
            catch (Exception ex)
            {
                var error = $"Failed to perform text emote {textEmote}: {ex.Message}";
                _logger.LogError(ex, error);
                EmoteError?.Invoke(error);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
                _isEmoting = false;
            }
        }

        #endregion

        #region Convenience Methods

        /// <inheritdoc />
        public Task WaveAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_WAVE, cancellationToken);

        /// <inheritdoc />
        public Task DanceAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_STATE_DANCE, cancellationToken);

        /// <inheritdoc />
        public Task BowAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_BOW, cancellationToken);

        /// <inheritdoc />
        public Task CheerAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_CHEER, cancellationToken);

        /// <inheritdoc />
        public Task LaughAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_LAUGH, cancellationToken);

        /// <inheritdoc />
        public Task PointAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_POINT, cancellationToken);

        /// <inheritdoc />
        public Task SaluteAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_ONESHOT_SALUTE, cancellationToken);

        /// <inheritdoc />
        public Task SitAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_STATE_SIT, cancellationToken);

        /// <inheritdoc />
        public Task StandAsync(CancellationToken cancellationToken = default) => 
            PerformEmoteAsync(Emote.EMOTE_STATE_STAND, cancellationToken);

        /// <inheritdoc />
        public Task HelloAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default) => 
            PerformTextEmoteAsync(TextEmote.TEXTEMOTE_HELLO, targetGuid, cancellationToken);

        /// <inheritdoc />
        public Task ByeAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default) => 
            PerformTextEmoteAsync(TextEmote.TEXTEMOTE_BYE, targetGuid, cancellationToken);

        /// <inheritdoc />
        public Task ThankAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default)
        {
            // Use CONGRATULATE as the closest to "thank" in WoW Classic
            return PerformTextEmoteAsync(TextEmote.TEXTEMOTE_CONGRATULATE, targetGuid, cancellationToken);
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public bool IsValidEmote(Emote emote)
        {
            // Check if the emote is within valid range and defined
            return Enum.IsDefined(typeof(Emote), emote) && emote != Emote.EMOTE_ONESHOT_NONE;
        }

        /// <inheritdoc />
        public bool IsValidTextEmote(TextEmote textEmote)
        {
            // Check if the text emote is within valid range and defined
            return Enum.IsDefined(typeof(TextEmote), textEmote);
        }

        /// <inheritdoc />
        public string GetEmoteName(Emote emote)
        {
            return emote switch
            {
                Emote.EMOTE_ONESHOT_WAVE => "Wave",
                Emote.EMOTE_ONESHOT_BOW => "Bow",
                Emote.EMOTE_ONESHOT_CHEER => "Cheer", 
                Emote.EMOTE_ONESHOT_LAUGH => "Laugh",
                Emote.EMOTE_ONESHOT_POINT => "Point",
                Emote.EMOTE_ONESHOT_SALUTE => "Salute",
                Emote.EMOTE_STATE_DANCE => "Dance",
                Emote.EMOTE_STATE_SIT => "Sit",
                Emote.EMOTE_STATE_STAND => "Stand",
                Emote.EMOTE_ONESHOT_APPLAUD => "Applaud",
                Emote.EMOTE_ONESHOT_FLEX => "Flex",
                Emote.EMOTE_ONESHOT_KNEEL => "Kneel",
                Emote.EMOTE_ONESHOT_CRY => "Cry",
                Emote.EMOTE_ONESHOT_CHICKEN => "Chicken",
                Emote.EMOTE_ONESHOT_BEG => "Beg",
                Emote.EMOTE_ONESHOT_RUDE => "Rude",
                Emote.EMOTE_ONESHOT_ROAR => "Roar",
                Emote.EMOTE_ONESHOT_KISS => "Kiss",
                Emote.EMOTE_ONESHOT_EAT => "Eat",
                Emote.EMOTE_ONESHOT_SHOUT => "Shout",
                Emote.EMOTE_ONESHOT_SHY => "Shy",
                Emote.EMOTE_ONESHOT_TALK => "Talk",
                _ => emote.ToString().Replace("EMOTE_", "").Replace("ONESHOT_", "").Replace("STATE_", "")
            };
        }

        /// <inheritdoc />
        public string GetTextEmoteName(TextEmote textEmote)
        {
            return textEmote switch
            {
                TextEmote.TEXTEMOTE_HELLO => "Hello",
                TextEmote.TEXTEMOTE_BYE => "Bye",
                TextEmote.TEXTEMOTE_CONGRATULATE => "Thank",
                TextEmote.TEXTEMOTE_AGREE => "Agree",
                TextEmote.TEXTEMOTE_BOW => "Bow",
                TextEmote.TEXTEMOTE_CHEER => "Cheer",
                TextEmote.TEXTEMOTE_DANCE => "Dance",
                TextEmote.TEXTEMOTE_LAUGH => "Laugh",
                TextEmote.TEXTEMOTE_APPLAUD => "Applaud",
                TextEmote.TEXTEMOTE_CRY => "Cry",
                TextEmote.TEXTEMOTE_KNEEL => "Kneel",
                TextEmote.TEXTEMOTE_KISS => "Kiss",
                TextEmote.TEXTEMOTE_WAVE => "Wave",
                TextEmote.TEXTEMOTE_SALUTE => "Salute",
                TextEmote.TEXTEMOTE_POINT => "Point",
                TextEmote.TEXTEMOTE_SIT => "Sit",
                TextEmote.TEXTEMOTE_STAND => "Stand",
                _ => textEmote.ToString().Replace("TEXTEMOTE_", "")
            };
        }

        #endregion

        #region Server Response Handlers

        /// <inheritdoc />
        public void HandleEmoteReceived(ulong sourceGuid, Emote emote)
        {
            try
            {
                _logger.LogDebug("Received emote {Emote} from player {PlayerGuid:X}", emote, sourceGuid);
                EmoteReceived?.Invoke(sourceGuid, emote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received emote from {PlayerGuid:X}", sourceGuid);
            }
        }

        /// <inheritdoc />
        public void HandleTextEmoteReceived(ulong sourceGuid, TextEmote textEmote, ulong? targetGuid)
        {
            try
            {
                var targetText = targetGuid.HasValue ? $" targeting {targetGuid.Value:X}" : "";
                _logger.LogDebug("Received text emote {TextEmote} from player {PlayerGuid:X}{Target}", textEmote, sourceGuid, targetText);
                TextEmoteReceived?.Invoke(sourceGuid, textEmote, targetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received text emote from {PlayerGuid:X}", sourceGuid);
            }
        }

        #endregion

        #region Private Helper Methods

        private void SetOperationInProgress(bool inProgress)
        {
            lock (_stateLock)
            {
                _isOperationInProgress = inProgress;
                if (inProgress)
                {
                    _lastOperationTime = DateTime.UtcNow;
                }
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the emote network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing EmoteNetworkClientComponent");

            // Clear events to prevent memory leaks
            EmotePerformed = null;
            TextEmotePerformed = null;
            EmoteError = null;
            EmoteReceived = null;
            TextEmoteReceived = null;

            _disposed = true;
            _logger.LogDebug("EmoteNetworkClientComponent disposed");
        }

        #endregion
    }
}