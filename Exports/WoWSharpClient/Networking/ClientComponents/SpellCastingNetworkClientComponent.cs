using System.Buffers.Binary;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using SpellSchool = WoWSharpClient.Networking.ClientComponents.I.SpellSchool;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of spell casting network agent that handles spell operations in World of Warcraft.
    /// Manages spell casting, channeling, and spell state tracking using the Mangos protocol.
    /// Uses reactive observables like other components and inherits from NetworkClientComponent.
    /// </summary>
    public class SpellCastingNetworkClientComponent : NetworkClientComponent, ISpellCastingNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<SpellCastingNetworkClientComponent> _logger;

        private bool _isCasting;
        private bool _isChanneling;
        private uint? _currentSpellId;
        private ulong? _currentSpellTarget;
        private uint _remainingCastTime;
        private readonly Dictionary<uint, uint> _spellCooldowns;
        private bool _disposed;

        // Reactive observable pipelines (wired to opcode handlers)
        private readonly IObservable<SpellCastStartData> _spellCastStarts;
        private readonly IObservable<SpellCastCompleteData> _spellCastCompletions;
        private readonly IObservable<SpellCastErrorData> _spellCastErrors;
        private readonly IObservable<ChannelingData> _channelingEvents;
        private readonly IObservable<SpellCooldownData> _spellCooldownsStream;
        private readonly IObservable<SpellHitData> _spellHits;

        /// <summary>
        /// Initializes a new instance of the SpellCastingNetworkClientComponent class.
        /// </summary>
        public SpellCastingNetworkClientComponent(IWorldClient worldClient, ILogger<SpellCastingNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _spellCooldowns = new Dictionary<uint, uint>();

            // Wire reactive pipelines to opcode streams. Parsing is best-effort and may use defaults if payloads differ.
            _spellCastStarts = SafeStream(Opcode.SMSG_SPELL_START)
                .Select(payload => ParseSpellStart(payload))
                .Do(data =>
                {
                    _isCasting = true;
                    _currentSpellId = data.SpellId;
                    _currentSpellTarget = data.TargetGuid;
                    _remainingCastTime = data.CastTime;
                });

            _spellCastCompletions = SafeStream(Opcode.SMSG_SPELL_GO)
                .Select(payload => ParseSpellGo(payload))
                .Do(data =>
                {
                    _isCasting = false;
                    _currentSpellId = null;
                    _currentSpellTarget = null;
                    _remainingCastTime = 0;
                });

            _spellCastErrors = Observable.Merge(
                    SafeStream(Opcode.SMSG_CAST_FAILED),
                    SafeStream(Opcode.SMSG_SPELL_FAILURE),
                    SafeStream(Opcode.SMSG_SPELL_FAILED_OTHER)
                )
                .Select(payload => ParseSpellError(payload))
                .Do(err =>
                {
                    _isCasting = false;
                    _remainingCastTime = 0;
                    if (_currentSpellId == err.SpellId)
                    {
                        _currentSpellId = null;
                        _currentSpellTarget = null;
                    }
                });

            _channelingEvents = Observable.Merge(
                    SafeStream(Opcode.MSG_CHANNEL_START),
                    SafeStream(Opcode.MSG_CHANNEL_UPDATE)
                )
                .Select((payload, index) => ParseChannelEvent(payload, index == 0))
                .Do(ch =>
                {
                    _isChanneling = ch.IsChanneling;
                    if (!ch.IsChanneling)
                    {
                        _currentSpellId = null;
                        _currentSpellTarget = null;
                    }
                });

            _spellCooldownsStream = Observable.Merge(
                    SafeStream(Opcode.SMSG_SPELL_COOLDOWN),
                    SafeStream(Opcode.SMSG_COOLDOWN_EVENT),
                    SafeStream(Opcode.SMSG_CLEAR_COOLDOWN)
                )
                .Select(payload => ParseCooldown(payload))
                .Do(cd =>
                {
                    var end = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cd.CooldownTime;
                    _spellCooldowns[cd.SpellId] = end;
                });

            _spellHits = Observable.Merge(
                    SafeStream(Opcode.SMSG_SPELLNONMELEEDAMAGELOG),
                    SafeStream(Opcode.SMSG_SPELLHEALLOG),
                    SafeStream(Opcode.SMSG_SPELLDAMAGESHIELD),
                    SafeStream(Opcode.SMSG_SPELLLOGEXECUTE)
                )
                .Select(payload => ParseSpellHit(payload));
        }

        // Provides a non-null observable stream for an opcode.
        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        // Observable properties (reactive pattern like other components)
        public IObservable<SpellCastStartData> SpellCastStarts => _spellCastStarts;
        public IObservable<SpellCastCompleteData> SpellCastCompletions => _spellCastCompletions;
        public IObservable<SpellCastErrorData> SpellCastErrors => _spellCastErrors;
        public IObservable<ChannelingData> ChannelingEvents => _channelingEvents;
        public IObservable<SpellCooldownData> SpellCooldowns => _spellCooldownsStream;
        public IObservable<SpellHitData> SpellHits => _spellHits;

        /// <inheritdoc />
        public bool IsCasting => _isCasting;

        /// <inheritdoc />
        public bool IsChanneling => _isChanneling;

        /// <inheritdoc />
        public uint? CurrentSpellId => _currentSpellId;

        /// <inheritdoc />
        public ulong? CurrentSpellTarget => _currentSpellTarget;

        /// <inheritdoc />
        public uint RemainingCastTime => _remainingCastTime;

        private void PublishSpellError(uint spellId, string message)
        {
            _logger.LogWarning("Spell error for {SpellId}: {Message}", spellId, message);
        }

        /// <inheritdoc />
        public async Task CastSpellAsync(uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Casting spell {SpellId}", spellId);

                var payload = BitConverter.GetBytes(spellId);
                
                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = null;
                
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);
                _logger.LogInformation("Spell cast command sent successfully");
            }
            catch (Exception ex)
            {
                _isCasting = false;
                _currentSpellId = null;
                _logger.LogError(ex, "Failed to cast spell {SpellId}", spellId);
                PublishSpellError(spellId, $"Failed to cast spell: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CastSpellOnTargetAsync(uint spellId, ulong targetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell {SpellId} on target {TargetGuid:X}", spellId, targetGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(spellId).CopyTo(payload, 0);
                BitConverter.GetBytes(targetGuid).CopyTo(payload, 4);

                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = targetGuid;
                
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);
                _logger.LogInformation("Targeted spell cast command sent successfully");
            }
            catch (Exception ex)
            {
                _isCasting = false;
                _currentSpellId = null;
                _currentSpellTarget = null;
                _logger.LogError(ex, "Failed to cast spell {SpellId} on target {TargetGuid:X}", spellId, targetGuid);
                PublishSpellError(spellId, $"Failed to cast spell on target: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CastSpellAtLocationAsync(uint spellId, float x, float y, float z, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell {SpellId} at location ({X}, {Y}, {Z})", spellId, x, y, z);

                var payload = new byte[16];
                BitConverter.GetBytes(spellId).CopyTo(payload, 0);
                BitConverter.GetBytes(x).CopyTo(payload, 4);
                BitConverter.GetBytes(y).CopyTo(payload, 8);
                BitConverter.GetBytes(z).CopyTo(payload, 12);

                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = null;
                
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);
                _logger.LogInformation("Location-targeted spell cast command sent successfully");
            }
            catch (Exception ex)
            {
                _isCasting = false;
                _currentSpellId = null;
                _logger.LogError(ex, "Failed to cast spell {SpellId} at location ({X}, {Y}, {Z})", spellId, x, y, z);
                PublishSpellError(spellId, $"Failed to cast spell at location: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task InterruptCastAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Interrupting current spell cast");

                var payload = new byte[4]; // Empty payload for cancel
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_CAST, payload, cancellationToken);
                
                _isCasting = false;
                _currentSpellId = null;
                _currentSpellTarget = null;
                _remainingCastTime = 0;
                
                _logger.LogInformation("Spell cast interrupted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to interrupt spell cast");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StopChannelingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Stopping current channeled spell");

                var payload = new byte[4]; // Empty payload for cancel channeling
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_CHANNELLING, payload, cancellationToken);
                
                _isChanneling = false;
                _currentSpellId = null;
                _currentSpellTarget = null;
                
                _logger.LogInformation("Channeled spell stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop channeled spell");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StartAutoRepeatSpellAsync(uint spellId, ulong targetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting auto-repeat for spell {SpellId} on target {TargetGuid:X}", spellId, targetGuid);

                // First set the target
                var targetPayload = BitConverter.GetBytes(targetGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_SELECTION, targetPayload, cancellationToken);

                // Then start auto-repeat
                var spellPayload = new byte[12];
                BitConverter.GetBytes(spellId).CopyTo(spellPayload, 0);
                BitConverter.GetBytes(targetGuid).CopyTo(spellPayload, 4);
                
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, spellPayload, cancellationToken);
                
                _logger.LogInformation("Auto-repeat spell started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start auto-repeat for spell {SpellId}", spellId);
                PublishSpellError(spellId, $"Failed to start auto-repeat: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StopAutoRepeatSpellAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Stopping auto-repeat spell");

                var payload = new byte[4]; // Empty payload for stopping auto-repeat
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_AUTO_REPEAT_SPELL, payload, cancellationToken);
                
                _logger.LogInformation("Auto-repeat spell stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop auto-repeat spell");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CastSpellFromActionBarAsync(byte actionBarSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell from action bar slot {ActionBarSlot}", actionBarSlot);

                var payload = new byte[1] { actionBarSlot };
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Action bar spell cast command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cast spell from action bar slot {ActionBarSlot}", actionBarSlot);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SmartCastSpellAsync(uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Smart casting spell {SpellId}", spellId);

                if (SpellRequiresTarget(spellId))
                {
                    await CastSpellAsync(spellId, cancellationToken);
                }
                else
                {
                    await CastSpellAsync(spellId, cancellationToken);
                }
                
                _logger.LogInformation("Smart spell cast completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to smart cast spell {SpellId}", spellId);
                PublishSpellError(spellId, $"Failed to smart cast spell: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool CanCastSpell(uint spellId)
        {
            _logger.LogDebug("Checking if spell {SpellId} can be cast", spellId);
            
            if (_isCasting || _isChanneling)
            {
                return false;
            }
            
            if (_spellCooldowns.TryGetValue(spellId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return false;
                }
                _spellCooldowns.Remove(spellId);
            }
            return true;
        }

        /// <inheritdoc />
        public uint GetSpellCooldown(uint spellId)
        {
            if (_spellCooldowns.TryGetValue(spellId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return cooldownEnd - currentTime;
                }
                _spellCooldowns.Remove(spellId);
            }
            return 0;
        }

        /// <inheritdoc />
        public uint GetSpellManaCost(uint spellId) => 50;

        /// <inheritdoc />
        public uint GetSpellCastTime(uint spellId) => 3000;

        /// <inheritdoc />
        public float GetSpellRange(uint spellId) => 30.0f;

        /// <inheritdoc />
        public bool SpellRequiresTarget(uint spellId) => true;

        /// <inheritdoc />
        public SpellSchool GetSpellSchool(uint spellId) => SpellSchool.Normal;

        /// <inheritdoc />
        public bool KnowsSpell(uint spellId) => true;

        // Server-driven state updates (optional manual calls)
        public void UpdateSpellCastStarted(uint spellId, uint castTime, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed spell {SpellId} cast started with cast time {CastTime}ms", spellId, castTime);
            _isCasting = true;
            _currentSpellId = spellId;
            _currentSpellTarget = targetGuid;
            _remainingCastTime = castTime;
        }

        public void UpdateSpellCastCompleted(uint spellId, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed spell {SpellId} cast completed", spellId);
            _isCasting = false;
            _currentSpellId = null;
            _currentSpellTarget = null;
            _remainingCastTime = 0;
        }

        public void UpdateChannelingStarted(uint spellId, uint duration)
        {
            _logger.LogDebug("Server confirmed channeling started for spell {SpellId} with duration {Duration}ms", spellId, duration);
            _isChanneling = true;
            _currentSpellId = spellId;
        }

        public void UpdateSpellCooldown(uint spellId, uint cooldownTime)
        {
            uint cooldownEnd = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cooldownTime;
            _spellCooldowns[spellId] = cooldownEnd;
            _logger.LogDebug("Spell {SpellId} cooldown updated: {CooldownTime}ms", spellId, cooldownTime);
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            base.Dispose();
        }

        #region Parsing helpers (best-effort)
        private static uint ReadUInt32(ReadOnlySpan<byte> span, int offset)
        {
            return (uint)(span.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) : 0u);
        }
        private static ulong ReadUInt64(ReadOnlySpan<byte> span, int offset)
        {
            return span.Length >= offset + 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)) : 0UL;
        }

        private SpellCastStartData ParseSpellStart(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            // Typical packet contains caster guid(s), spellId, cast time. We attempt to read spellId and cast time safely.
            uint spellId = ReadUInt32(span, 0);
            uint castTime = ReadUInt32(span, 4);
            ulong? target = span.Length >= 16 ? ReadUInt64(span, 8) : null;
            return new SpellCastStartData(
                SpellId: spellId,
                CastTime: castTime,
                TargetGuid: target,
                CastType: I.SpellCastType.Normal,
                Timestamp: DateTime.UtcNow
            );
        }

        private SpellCastCompleteData ParseSpellGo(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            ulong? target = span.Length >= 16 ? ReadUInt64(span, 8) : null;
            return new SpellCastCompleteData(
                SpellId: spellId,
                TargetGuid: target,
                Timestamp: DateTime.UtcNow
            );
        }

        private SpellCastErrorData ParseSpellError(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            string message = "Spell cast failed";
            return new SpellCastErrorData(
                ErrorMessage: message,
                SpellId: spellId,
                TargetGuid: _currentSpellTarget,
                Timestamp: DateTime.UtcNow
            );
        }

        private ChannelingData ParseChannelEvent(ReadOnlyMemory<byte> payload, bool isStart)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            uint? duration = isStart ? ReadUInt32(span, 4) : (uint?)null;
            return new ChannelingData(
                SpellId: spellId,
                IsChanneling: isStart,
                Duration: duration,
                Timestamp: DateTime.UtcNow
            );
        }

        private SpellCooldownData ParseCooldown(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            uint cooldownMs = ReadUInt32(span, 4);
            return new SpellCooldownData(
                SpellId: spellId,
                CooldownTime: cooldownMs,
                Timestamp: DateTime.UtcNow
            );
        }

        private SpellHitData ParseSpellHit(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            ulong target = span.Length >= 16 ? ReadUInt64(span, 8) : 0UL;
            uint? dmg = span.Length >= 20 ? ReadUInt32(span, 16) : null;
            return new SpellHitData(
                SpellId: spellId,
                TargetGuid: target,
                Damage: dmg,
                Heal: null,
                Timestamp: DateTime.UtcNow
            );
        }
        #endregion
    }
}