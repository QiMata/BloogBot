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

        /// <summary>
        /// Optional reference to the CharacterInit component for action bar slot resolution.
        /// Set this after construction to enable CastSpellFromActionBarAsync.
        /// </summary>
        public ICharacterInitNetworkClientComponent? CharacterInitComponent { get; set; }

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
            _spellCastStarts = SafeOpcodeStream(Opcode.SMSG_SPELL_START)
                .Select(payload => ParseSpellStart(payload))
                .Do(data =>
                {
                    _isCasting = true;
                    _currentSpellId = data.SpellId;
                    _currentSpellTarget = data.TargetGuid;
                    _remainingCastTime = data.CastTime;
                })
                .Publish()
                .RefCount();

            _spellCastCompletions = SafeOpcodeStream(Opcode.SMSG_SPELL_GO)
                .Select(payload => ParseSpellGo(payload))
                .Do(data =>
                {
                    _isCasting = false;
                    _currentSpellId = null;
                    _currentSpellTarget = null;
                    _remainingCastTime = 0;
                })
                .Publish()
                .RefCount();

            _spellCastErrors = Observable.Merge(
                    SafeOpcodeStream(Opcode.SMSG_CAST_FAILED),
                    SafeOpcodeStream(Opcode.SMSG_SPELL_FAILURE),
                    SafeOpcodeStream(Opcode.SMSG_SPELL_FAILED_OTHER)
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
                })
                .Publish()
                .RefCount();

            _channelingEvents = Observable.Merge(
                    SafeOpcodeStream(Opcode.MSG_CHANNEL_START),
                    SafeOpcodeStream(Opcode.MSG_CHANNEL_UPDATE)
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
                })
                .Publish()
                .RefCount();

            _spellCooldownsStream = Observable.Merge(
                    SafeOpcodeStream(Opcode.SMSG_SPELL_COOLDOWN),
                    SafeOpcodeStream(Opcode.SMSG_COOLDOWN_EVENT),
                    SafeOpcodeStream(Opcode.SMSG_CLEAR_COOLDOWN)
                )
                .Select(payload => ParseCooldown(payload))
                .Do(cd =>
                {
                    var end = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cd.CooldownTime;
                    _spellCooldowns[cd.SpellId] = end;
                })
                .Publish()
                .RefCount();

            _spellHits = Observable.Merge(
                    SafeOpcodeStream(Opcode.SMSG_SPELLNONMELEEDAMAGELOG),
                    SafeOpcodeStream(Opcode.SMSG_SPELLHEALLOG),
                    SafeOpcodeStream(Opcode.SMSG_SPELLDAMAGESHIELD),
                    SafeOpcodeStream(Opcode.SMSG_SPELLLOGEXECUTE)
                )
                .Select(payload => ParseSpellHit(payload))
                .Publish()
                .RefCount();
        }

        // Provides a non-null observable stream for an opcode.
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
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
                _logger.LogDebug("Casting spell {SpellId} (self-cast)", spellId);

                // CMSG_CAST_SPELL: uint32 spellId + SpellCastTargets (uint16 targetMask)
                // TARGET_FLAG_SELF = 0x0000
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(spellId);
                w.Write((ushort)0x0000); // TARGET_FLAG_SELF

                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = null;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, ms.ToArray(), cancellationToken);
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

                // CMSG_CAST_SPELL: uint32 spellId + SpellCastTargets
                // TARGET_FLAG_UNIT = 0x0002 + packed GUID
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(spellId);
                w.Write((ushort)0x0002); // TARGET_FLAG_UNIT
                WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(w, targetGuid);

                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = targetGuid;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, ms.ToArray(), cancellationToken);
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

                // CMSG_CAST_SPELL: uint32 spellId + SpellCastTargets
                // TARGET_FLAG_DEST_LOCATION = 0x0040 + 3x float
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(spellId);
                w.Write((ushort)0x0040); // TARGET_FLAG_DEST_LOCATION
                w.Write(x);
                w.Write(y);
                w.Write(z);

                _isCasting = true;
                _currentSpellId = spellId;
                _currentSpellTarget = null;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, ms.ToArray(), cancellationToken);
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
                _logger.LogDebug("Interrupting current spell cast (spellId={SpellId})", _currentSpellId);

                // CMSG_CANCEL_CAST: uint32 spellId — server uses it in InterruptNonMeleeSpells
                var payload = new byte[4];
                BitConverter.GetBytes(_currentSpellId ?? 0u).CopyTo(payload, 0);
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
                _logger.LogDebug("Stopping current channeled spell (spellId={SpellId})", _currentSpellId);

                // CMSG_CANCEL_CHANNELLING: uint32 spellId (server reads but skips)
                var payload = new byte[4];
                BitConverter.GetBytes(_currentSpellId ?? 0u).CopyTo(payload, 0);
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

                // First set the target (full 8-byte GUID)
                var targetPayload = BitConverter.GetBytes(targetGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_SELECTION, targetPayload, cancellationToken);

                // Then cast the spell with proper SpellCastTargets
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(spellId);
                w.Write((ushort)0x0002); // TARGET_FLAG_UNIT
                WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(w, targetGuid);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, ms.ToArray(), cancellationToken);

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

                // CMSG_CANCEL_AUTO_REPEAT_SPELL: empty packet (handler reads nothing)
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_AUTO_REPEAT_SPELL, [], cancellationToken);
                
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

                if (actionBarSlot >= 120)
                    throw new ArgumentOutOfRangeException(nameof(actionBarSlot), "Action bar slot must be 0-119");

                var charInit = CharacterInitComponent;
                if (charInit == null || !charInit.IsInitialized)
                    throw new InvalidOperationException(
                        "Action bar data not available. Ensure CharacterInitComponent is set and SMSG_ACTION_BUTTONS has been received.");

                var buttons = charInit.ActionButtons;
                if (actionBarSlot >= buttons.Count)
                    throw new InvalidOperationException($"Action bar slot {actionBarSlot} out of range (have {buttons.Count} buttons)");

                var button = buttons[actionBarSlot];
                if (button.IsEmpty)
                    throw new InvalidOperationException($"Action bar slot {actionBarSlot} is empty");

                switch (button.Type)
                {
                    case Models.ActionButtonType.Spell:
                        _logger.LogDebug("Action bar slot {Slot} resolved to spell {SpellId}", actionBarSlot, button.ActionId);
                        await CastSpellAsync(button.ActionId, cancellationToken);
                        break;

                    case Models.ActionButtonType.Item:
                        _logger.LogWarning("Action bar slot {Slot} contains item {ItemId} — use ItemUseNetworkClientComponent instead",
                            actionBarSlot, button.ActionId);
                        throw new InvalidOperationException(
                            $"Action bar slot {actionBarSlot} contains an item (ID {button.ActionId}), not a spell. Use ItemUseNetworkClientComponent.");

                    case Models.ActionButtonType.Macro:
                    case Models.ActionButtonType.ClickMacro:
                        _logger.LogWarning("Action bar slot {Slot} contains macro {MacroId} — macros not supported in headless client",
                            actionBarSlot, button.ActionId);
                        throw new InvalidOperationException(
                            $"Action bar slot {actionBarSlot} contains a macro (ID {button.ActionId}). Macros are not supported in headless client.");

                    default:
                        throw new InvalidOperationException(
                            $"Action bar slot {actionBarSlot} has unsupported type {button.Type} (action ID {button.ActionId})");
                }

                _logger.LogInformation("Action bar spell cast from slot {Slot} completed", actionBarSlot);
            }
            catch (Exception ex) when (ex is not ArgumentOutOfRangeException and not InvalidOperationException)
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