using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of spell casting network agent that handles spell operations in World of Warcraft.
    /// Manages spell casting, channeling, and spell state tracking using the Mangos protocol.
    /// </summary>
    public class SpellCastingNetworkClientComponent : ISpellCastingNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<SpellCastingNetworkClientComponent> _logger;
        private bool _isCasting;
        private bool _isChanneling;
        private uint? _currentSpellId;
        private ulong? _currentSpellTarget;
        private uint _remainingCastTime;
        private readonly Dictionary<uint, uint> _spellCooldowns;

        /// <summary>
        /// Initializes a new instance of the SpellCastingNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public SpellCastingNetworkClientComponent(IWorldClient worldClient, ILogger<SpellCastingNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _spellCooldowns = new Dictionary<uint, uint>();
        }

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

        /// <inheritdoc />
        public event Action<uint, uint, ulong?>? SpellCastStarted;

        /// <inheritdoc />
        public event Action<uint, ulong?>? SpellCastCompleted;

        /// <inheritdoc />
        public event Action<uint, string>? SpellCastFailed;

        /// <inheritdoc />
        public event Action<uint, uint>? ChannelingStarted;

        /// <inheritdoc />
        public event Action<uint, bool>? ChannelingEnded;

        /// <inheritdoc />
        public event Action<uint, uint>? SpellCooldownStarted;

        /// <inheritdoc />
        public event Action<uint, ulong, uint?, uint?>? SpellHit;

        /// <inheritdoc />
        public async Task CastSpellAsync(uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
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
                SpellCastFailed?.Invoke(spellId, $"Failed to cast spell: {ex.Message}");
                throw;
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
                SpellCastFailed?.Invoke(spellId, $"Failed to cast spell on target: {ex.Message}");
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
                SpellCastFailed?.Invoke(spellId, $"Failed to cast spell at location: {ex.Message}");
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
                SpellCastFailed?.Invoke(spellId, $"Failed to start auto-repeat: {ex.Message}");
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

                // Check if spell requires a target
                if (SpellRequiresTarget(spellId))
                {
                    // This would typically check if a target is selected
                    // and find an appropriate target if none is selected
                    // For now, just cast the spell without target checks
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
                throw;
            }
        }

        /// <inheritdoc />
        public bool CanCastSpell(uint spellId)
        {
            _logger.LogDebug("Checking if spell {SpellId} can be cast", spellId);
            
            // Check if already casting
            if (_isCasting || _isChanneling)
            {
                return false;
            }
            
            // Check cooldown
            if (_spellCooldowns.TryGetValue(spellId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return false;
                }
                
                // Remove expired cooldown
                _spellCooldowns.Remove(spellId);
            }
            
            // Additional checks would go here (mana, reagents, etc.)
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
                
                // Remove expired cooldown
                _spellCooldowns.Remove(spellId);
            }
            
            return 0;
        }

        /// <inheritdoc />
        public uint GetSpellManaCost(uint spellId)
        {
            // This would typically query spell data
            // Return placeholder value
            return 50;
        }

        /// <inheritdoc />
        public uint GetSpellCastTime(uint spellId)
        {
            // This would typically query spell data
            // Return placeholder value (3 seconds)
            return 3000;
        }

        /// <inheritdoc />
        public float GetSpellRange(uint spellId)
        {
            // This would typically query spell data
            // Return placeholder value (30 yards)
            return 30.0f;
        }

        /// <inheritdoc />
        public bool SpellRequiresTarget(uint spellId)
        {
            // This would typically query spell data
            // Return placeholder value
            return true;
        }

        /// <inheritdoc />
        public SpellSchool GetSpellSchool(uint spellId)
        {
            // This would typically query spell data
            // Return placeholder value
            return SpellSchool.Normal;
        }

        /// <inheritdoc />
        public bool KnowsSpell(uint spellId)
        {
            // This would typically check the player's spellbook
            // Return placeholder value
            return true;
        }

        /// <summary>
        /// Updates spell cast state based on server response.
        /// This should be called when receiving spell cast start packets.
        /// </summary>
        /// <param name="spellId">The ID of the spell being cast.</param>
        /// <param name="castTime">The cast time in milliseconds.</param>
        /// <param name="targetGuid">The target GUID if applicable.</param>
        public void UpdateSpellCastStarted(uint spellId, uint castTime, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed spell {SpellId} cast started with cast time {CastTime}ms", spellId, castTime);
            
            _isCasting = true;
            _currentSpellId = spellId;
            _currentSpellTarget = targetGuid;
            _remainingCastTime = castTime;
            
            SpellCastStarted?.Invoke(spellId, castTime, targetGuid);
        }

        /// <summary>
        /// Updates spell cast completion state based on server response.
        /// This should be called when receiving spell cast completion packets.
        /// </summary>
        /// <param name="spellId">The ID of the spell that was cast.</param>
        /// <param name="targetGuid">The target GUID if applicable.</param>
        public void UpdateSpellCastCompleted(uint spellId, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed spell {SpellId} cast completed", spellId);
            
            _isCasting = false;
            _currentSpellId = null;
            _currentSpellTarget = null;
            _remainingCastTime = 0;
            
            SpellCastCompleted?.Invoke(spellId, targetGuid);
        }

        /// <summary>
        /// Updates channeling state based on server response.
        /// This should be called when receiving channeling start packets.
        /// </summary>
        /// <param name="spellId">The ID of the channeled spell.</param>
        /// <param name="duration">The channel duration in milliseconds.</param>
        public void UpdateChannelingStarted(uint spellId, uint duration)
        {
            _logger.LogDebug("Server confirmed channeling started for spell {SpellId} with duration {Duration}ms", spellId, duration);
            
            _isChanneling = true;
            _currentSpellId = spellId;
            
            ChannelingStarted?.Invoke(spellId, duration);
        }

        /// <summary>
        /// Updates spell cooldown based on server response.
        /// This should be called when receiving spell cooldown packets.
        /// </summary>
        /// <param name="spellId">The ID of the spell on cooldown.</param>
        /// <param name="cooldownTime">The cooldown duration in milliseconds.</param>
        public void UpdateSpellCooldown(uint spellId, uint cooldownTime)
        {
            uint cooldownEnd = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cooldownTime;
            _spellCooldowns[spellId] = cooldownEnd;
            
            _logger.LogDebug("Spell {SpellId} cooldown updated: {CooldownTime}ms", spellId, cooldownTime);
            SpellCooldownStarted?.Invoke(spellId, cooldownTime);
        }
    }
}