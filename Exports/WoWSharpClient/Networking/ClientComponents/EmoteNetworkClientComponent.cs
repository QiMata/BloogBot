using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Network agent for handling emote operations via network packets.
    /// Provides functionality to perform both animated emotes and text-based emotes.
    /// Uses opcode-backed observables (no events/subjects).
    /// </summary>
    public class EmoteNetworkClientComponent : NetworkClientComponent, IEmoteNetworkClientComponent
    {
        #region Fields

        private readonly IWorldClient _worldClient;
        private readonly ILogger<EmoteNetworkClientComponent> _logger;
        private volatile bool _isEmoting;
        private bool _disposed;

        // Reactive opcode-backed observables
        private readonly IObservable<EmoteData> _animatedEmotes;
        private readonly IObservable<EmoteData> _textEmotes;
        private readonly IObservable<EmoteErrorData> _emoteErrors;

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

            // Build incoming emote observables from opcode streams
            _animatedEmotes = SafeOpcodeStream(Opcode.SMSG_EMOTE)
                .Select(ParseSmsgEmote)
                .Do(data => _logger.LogDebug("Received animated emote {EmoteName} (Id: {EmoteId})", data.EmoteName, data.EmoteId))
                .Publish()
                .RefCount();

            _textEmotes = SafeOpcodeStream(Opcode.SMSG_TEXT_EMOTE)
                .Select(ParseSmsgTextEmote)
                .Do(data => _logger.LogDebug("Received text emote {EmoteName} (Id: {EmoteId})", data.EmoteName, data.EmoteId))
                .Publish()
                .RefCount();

            // No dedicated error opcode stream for emotes; expose a never-completing empty stream
            _emoteErrors = Observable.Never<EmoteErrorData>();
        }

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

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<EmoteData> AnimatedEmotes => _animatedEmotes;

        /// <inheritdoc />
        public IObservable<EmoteData> TextEmotes => _textEmotes;

        /// <inheritdoc />
        public IObservable<EmoteErrorData> EmoteErrors => _emoteErrors;

        #endregion

        #region Emote Operations

        /// <inheritdoc />
        public async Task PerformEmoteAsync(Emote emote, CancellationToken cancellationToken = default)
        {
            if (!IsValidEmote(emote))
            {
                var error = $"Invalid emote: {emote}";
                _logger.LogWarning(error);
                // Do not throw; tests expect no packet to be sent and no exception for invalid emotes
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
            }
            catch (Exception ex)
            {
                var error = $"Failed to perform emote {emote}: {ex.Message}";
                _logger.LogError(ex, error);
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
                // Do not throw; align behavior with PerformEmoteAsync for invalid inputs
                return;
            }

            try
            {
                SetOperationInProgress(true);
                _isEmoting = true;
                _logger.LogDebug("Performing text emote: {TextEmote} on target: {Target}", textEmote, targetGuid?.ToString("X") ?? "none");

                // CMSG_TEXT_EMOTE: uint32 textEmoteId + uint32 emoteNum + uint64 targetGuid = 16 bytes
                var packet = new byte[16];
                BitConverter.GetBytes((uint)textEmote).CopyTo(packet, 0);
                BitConverter.GetBytes((uint)0).CopyTo(packet, 4); // emoteNum (server resolves animation from EmotesText.dbc)
                BitConverter.GetBytes(targetGuid ?? 0).CopyTo(packet, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TEXT_EMOTE, packet, cancellationToken);

                LastTextEmote = textEmote;
                LastEmoteTime = DateTime.UtcNow;

                var targetText = targetGuid.HasValue ? $" on target {targetGuid.Value:X}" : "";
                _logger.LogInformation("Successfully performed text emote: {TextEmote}{Target}", GetTextEmoteName(textEmote), targetText);
            }
            catch (Exception ex)
            {
                var error = $"Failed to perform text emote {textEmote}: {ex.Message}";
                _logger.LogError(ex, error);
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

        #region Server Response Handlers (compat)

        /// <inheritdoc />
        public void HandleEmoteReceived(ulong sourceGuid, Emote emote)
        {
            // Kept for compatibility with external callers; actual handling is via opcode-backed observables
            try
            {
                _logger.LogDebug("Received emote {Emote} from player {PlayerGuid:X}", emote, sourceGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received emote from {PlayerGuid:X}", sourceGuid);
            }
        }

        /// <inheritdoc />
        public void HandleTextEmoteReceived(ulong sourceGuid, TextEmote textEmote, ulong? targetGuid)
        {
            // Kept for compatibility with external callers; actual handling is via opcode-backed observables
            try
            {
                var targetText = targetGuid.HasValue ? $" targeting {targetGuid.Value:X}" : "";
                _logger.LogDebug("Received text emote {TextEmote} from player {PlayerGuid:X}{Target}", textEmote, sourceGuid, targetText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received text emote from {PlayerGuid:X}", sourceGuid);
            }
        }

        #endregion

        #region Private Helper Methods

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        /// <summary>
        /// Parses SMSG_EMOTE payload.
        /// MaNGOS format: uint32 emoteId + ObjectGuid(8) = 12 bytes.
        /// </summary>
        private EmoteData ParseSmsgEmote(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // MaNGOS format: uint32 emoteId + ObjectGuid(8) = 12 bytes
                uint emoteId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0u;
                ulong sourceGuid = span.Length >= 12 ? BitConverter.ToUInt64(span.Slice(4, 8)) : 0UL;
                string name = GetEmoteName((Emote)emoteId);
                return new EmoteData(emoteId, name, sourceGuid > 0 ? sourceGuid : null, null, EmoteType.Animated, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse SMSG_EMOTE payload ({Length} bytes)", payload.Length);
                return new EmoteData(0u, "Unknown", null, null, EmoteType.Animated, DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Parses SMSG_TEXT_EMOTE payload.
        /// MaNGOS format: ObjectGuid(8) + uint32 textEmoteId + uint32 emoteNum + uint32 nameLen + name[].
        /// </summary>
        private EmoteData ParseSmsgTextEmote(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // MaNGOS format: ObjectGuid(8) + uint32 textEmoteId + uint32 emoteNum + uint32 nameLen + name[]
                ulong sourceGuid = span.Length >= 8 ? BitConverter.ToUInt64(span[..8]) : 0UL;
                uint emoteId = span.Length >= 12 ? BitConverter.ToUInt32(span.Slice(8, 4)) : 0u;
                // emoteNum at offset 12 (uint32) - animation ID, informational only
                string? targetName = null;
                if (span.Length >= 20)
                {
                    uint nameLen = BitConverter.ToUInt32(span.Slice(16, 4));
                    if (nameLen > 0 && span.Length >= 20 + (int)nameLen)
                    {
                        targetName = System.Text.Encoding.UTF8.GetString(span.Slice(20, (int)nameLen));
                    }
                }

                string name = (emoteId != 0) ? GetTextEmoteName((TextEmote)emoteId) : "Unknown";
                return new EmoteData(emoteId, name, sourceGuid > 0 ? sourceGuid : null, targetName, EmoteType.Text, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse SMSG_TEXT_EMOTE payload ({Length} bytes)", payload.Length);
                return new EmoteData(0u, "Unknown", null, null, EmoteType.Text, DateTime.UtcNow);
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the emote network client component and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;
            _logger.LogDebug("Disposing EmoteNetworkClientComponent");
            _disposed = true;
            _logger.LogDebug("EmoteNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion
    }
}