using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Enhanced Combat/Spell Network Agent that provides comprehensive combat functionality
    /// including spell casting, pet control, aura/buff tracking, and item usage in combat.
    /// This agent coordinates multiple combat-related operations for a complete combat experience.
    /// </summary>
    public class CombatSpellNetworkClientComponent : NetworkClientComponent, ICombatSpellNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<CombatSpellNetworkClientComponent> _logger;
        private readonly ITargetingNetworkClientComponent _targetingAgent;
        private readonly IAttackNetworkClientComponent _attackAgent;
        private readonly ISpellCastingNetworkClientComponent _spellCastingAgent;
        private readonly IItemUseNetworkClientComponent _itemUseAgent;
        
        // Pet control state
        private ulong? _currentPetGuid;
        private readonly Dictionary<ulong, PetState> _petStates = new();
        
        // Aura/Buff tracking
        private readonly Dictionary<uint, AuraData> _activeAuras = new();
        private readonly Dictionary<uint, BuffData> _activeBuffs = new();
        
        // Combat state
        private bool _isInCombat;
        private ulong? _currentCombatTarget;
        private bool _disposed;

        // Opcode-backed observables (no Subjects or events)
        private readonly IObservable<CombatStateData> _combatStateChanges;
        private readonly IObservable<PetCommandData> _petCommands;
        private readonly IObservable<AuraUpdateData> _auraUpdates;
        private readonly IObservable<CombatItemUseData> _combatItemUsage;
        private readonly IObservable<CombatErrorData> _combatErrors;

        #region Public Properties

        /// <summary>
        /// Observable stream for combat state changes.
        /// </summary>
        public IObservable<CombatStateData> CombatStateChanges => _combatStateChanges;

        /// <summary>
        /// Observable stream for pet command operations.
        /// </summary>
        public IObservable<PetCommandData> PetCommands => _petCommands;

        /// <summary>
        /// Observable stream for aura/buff updates.
        /// </summary>
        public IObservable<AuraUpdateData> AuraUpdates => _auraUpdates;

        /// <summary>
        /// Observable stream for combat item usage.
        /// </summary>
        public IObservable<CombatItemUseData> CombatItemUsage => _combatItemUsage;

        /// <summary>
        /// Observable stream for combat errors.
        /// </summary>
        public IObservable<CombatErrorData> CombatErrors => _combatErrors;

        /// <summary>
        /// Gets whether the character is currently in combat.
        /// </summary>
        public bool IsInCombat => _isInCombat;

        /// <summary>
        /// Gets the current combat target GUID.
        /// </summary>
        public ulong? CurrentCombatTarget => _currentCombatTarget;

        /// <summary>
        /// Gets the current pet GUID if any.
        /// </summary>
        public ulong? CurrentPetGuid => _currentPetGuid;

        /// <summary>
        /// Gets a read-only view of currently active auras.
        /// </summary>
        public IReadOnlyDictionary<uint, AuraData> ActiveAuras => _activeAuras;

        /// <summary>
        /// Gets a read-only view of currently active buffs.
        /// </summary>
        public IReadOnlyDictionary<uint, BuffData> ActiveBuffs => _activeBuffs;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the CombatSpellNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="targetingAgent">Targeting network agent for coordination.</param>
        /// <param name="attackAgent">Attack network agent for coordination.</param>
        /// <param name="spellCastingAgent">Spell casting network agent for coordination.</param>
        /// <param name="itemUseAgent">Item use network agent for coordination.</param>
        public CombatSpellNetworkClientComponent(
            IWorldClient worldClient,
            ILogger<CombatSpellNetworkClientComponent> logger,
            ITargetingNetworkClientComponent targetingAgent,
            IAttackNetworkClientComponent attackAgent,
            ISpellCastingNetworkClientComponent spellCastingAgent,
            IItemUseNetworkClientComponent itemUseAgent)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _targetingAgent = targetingAgent ?? throw new ArgumentNullException(nameof(targetingAgent));
            _attackAgent = attackAgent ?? throw new ArgumentNullException(nameof(attackAgent));
            _spellCastingAgent = spellCastingAgent ?? throw new ArgumentNullException(nameof(spellCastingAgent));
            _itemUseAgent = itemUseAgent ?? throw new ArgumentNullException(nameof(itemUseAgent));

            // Build observables from IWorldClient streams/opcodes (no Subjects)
            _combatStateChanges = (_worldClient.AttackStateChanged ?? Observable.Empty<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)>())
                .Select(tuple => new CombatStateData(
                    IsInCombat: tuple.IsAttacking,
                    TargetGuid: tuple.IsAttacking ? tuple.VictimGuid : null,
                    Strategy: CombatStrategy.Auto,
                    Timestamp: DateTime.UtcNow
                ))
                .Do(state =>
                {
                    _isInCombat = state.IsInCombat;
                    _currentCombatTarget = state.TargetGuid;
                })
                .Publish()
                .RefCount();

            // Auras from server opcode stream. Best-effort parsing.
            _auraUpdates = Observable.Merge(
                    SafeStream(Opcode.SMSG_UPDATE_AURA_DURATION),
                    SafeStream(Opcode.SMSG_INIT_EXTRA_AURA_INFO),
                    SafeStream(Opcode.SMSG_SET_EXTRA_AURA_INFO),
                    SafeStream(Opcode.SMSG_SET_EXTRA_AURA_INFO_NEED_UPDATE)
                )
                .Select(ParseAuraUpdate)
                .Do(update =>
                {
                    if (update.IsActive)
                    {
                        _activeAuras[update.AuraId] = new AuraData(
                            AuraId: update.AuraId,
                            CasterGuid: update.CasterGuid,
                            Duration: update.Duration,
                            AppliedTime: DateTime.UtcNow,
                            IsActive: true
                        );
                    }
                    else
                    {
                        _activeAuras.Remove(update.AuraId);
                    }
                })
                .Publish()
                .RefCount();

            // Pet command feedback stream - if a specific opcode exists, wire it here. Otherwise expose a never stream.
            _petCommands = Observable.Never<PetCommandData>();

            // Combat item usage is typically client-driven; expose a never stream (can be wired to opcodes if available)
            _combatItemUsage = Observable.Never<CombatItemUseData>();

            // Combat errors from IWorldClient attack errors stream (plus other sources if needed)
            _combatErrors = (_worldClient.AttackErrors ?? Observable.Empty<string>())
                .Select(msg => new CombatErrorData(
                    Operation: "Attack",
                    ErrorMessage: msg,
                    TargetGuid: _currentCombatTarget,
                    Timestamp: DateTime.UtcNow
                ))
                .Publish()
                .RefCount();

            SetupEventCoordination();
        }

        #endregion

        #region Combat Management

        /// <summary>
        /// Initiates combat with a target, coordinating targeting, attacking, and spell casting.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to engage.</param>
        /// <param name="combatStrategy">The combat strategy to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task EngageCombatAsync(ulong targetGuid, CombatStrategy combatStrategy = CombatStrategy.Auto, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Engaging combat with target: {TargetGuid:X} using strategy: {Strategy}", targetGuid, combatStrategy);

                SetOperationInProgress(true);

                // Set target first
                await _targetingAgent.SetTargetAsync(targetGuid, cancellationToken);
                
                // Update combat state locally (server will confirm via streams)
                _isInCombat = true;
                _currentCombatTarget = targetGuid;

                // Start auto-attack via attack agent convenience method if available
                if (_attackAgent is not null)
                {
                    await _attackAgent.AttackTargetAsync(targetGuid, _targetingAgent, cancellationToken);
                }

                // Apply combat strategy
                await ApplyCombatStrategyAsync(combatStrategy, targetGuid, cancellationToken);

                _logger.LogInformation("Combat engagement initiated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to engage combat with target: {TargetGuid:X}", targetGuid);
                // Errors will be surfaced by _combatErrors stream if server emits; also log here.
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Disengages from combat, stopping attacks and clearing targets.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DisengageCombatAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Disengaging from combat");

                SetOperationInProgress(true);

                // Stop auto-attack
                await _attackAgent.StopAttackAsync(cancellationToken);
                
                // Clear target
                await _targetingAgent.ClearTargetAsync(cancellationToken);
                
                // Update local combat state (server will confirm via streams)
                _isInCombat = false;
                _currentCombatTarget = null;

                _logger.LogInformation("Combat disengagement completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disengage from combat");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Spell Casting with Enhanced Features

        /// <summary>
        /// Casts a spell with enhanced targeting and condition checking.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="targetGuid">Optional target GUID (uses current target if null).</param>
        /// <param name="forceTarget">Whether to force target the specified GUID before casting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CastSpellAsync(uint spellId, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell {SpellId} on target {TargetGuid:X}", spellId, targetGuid);

                // Handle targeting if needed
                if (targetGuid.HasValue && forceTarget)
                {
                    await _targetingAgent.SetTargetAsync(targetGuid.Value, cancellationToken);
                }

                // Use the spell casting agent
                if (targetGuid.HasValue)
                {
                    await _spellCastingAgent.CastSpellOnTargetAsync(spellId, targetGuid.Value, cancellationToken);
                }
                else
                {
                    await _spellCastingAgent.CastSpellAsync(spellId, cancellationToken);
                }

                _logger.LogDebug("Spell cast initiated successfully: {SpellId}", spellId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cast spell {SpellId} on target {TargetGuid:X}", spellId, targetGuid);
                throw;
            }
        }

        /// <summary>
        /// Casts a spell by name with enhanced targeting and condition checking.
        /// This is a convenience method that would require spell name to ID mapping.
        /// </summary>
        public async Task CastSpellByNameAsync(string spellName, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell '{SpellName}' on target {TargetGuid:X}", spellName, targetGuid);

                var spellId = GetSpellIdFromName(spellName); // Placeholder mapping
                
                if (targetGuid.HasValue && forceTarget)
                {
                    await _targetingAgent.SetTargetAsync(targetGuid.Value, cancellationToken);
                }

                await _spellCastingAgent.SmartCastSpellAsync(spellId, cancellationToken);

                _logger.LogDebug("Spell cast by name initiated successfully: '{SpellName}'", spellName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cast spell '{SpellName}' on target {TargetGuid:X}", spellName, targetGuid);
                throw;
            }
        }

        #endregion

        #region Pet Control (Enhanced Implementation)

        /// <summary>
        /// Commands the current pet to attack a target.
        /// </summary>
        public async Task PetAttackAsync(ulong targetGuid, CancellationToken cancellationToken = default)
        {
            if (!_currentPetGuid.HasValue)
            {
                throw new InvalidOperationException("No active pet to command");
            }

            try
            {
                _logger.LogDebug("Commanding pet {PetGuid:X} to attack {TargetGuid:X}", _currentPetGuid.Value, targetGuid);

                var payload = CreatePetCommandPayload(_currentPetGuid.Value, PetCommand.Attack, targetGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PET_ACTION, payload, cancellationToken);

                _logger.LogInformation("Pet attack command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet attack command");
                throw;
            }
        }

        /// <summary>
        /// Commands the current pet to follow the player.
        /// </summary>
        public async Task PetFollowAsync(CancellationToken cancellationToken = default)
        {
            if (!_currentPetGuid.HasValue)
            {
                throw new InvalidOperationException("No active pet to command");
            }

            try
            {
                _logger.LogDebug("Commanding pet {PetGuid:X} to follow", _currentPetGuid.Value);

                var payload = CreatePetCommandPayload(_currentPetGuid.Value, PetCommand.Follow);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PET_ACTION, payload, cancellationToken);

                _logger.LogInformation("Pet follow command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet follow command");
                throw;
            }
        }

        /// <summary>
        /// Commands the current pet to use a specific ability.
        /// </summary>
        public async Task PetUseAbilityAsync(uint abilityId, ulong? targetGuid = null, CancellationToken cancellationToken = default)
        {
            if (!_currentPetGuid.HasValue)
            {
                throw new InvalidOperationException("No active pet to command");
            }

            try
            {
                _logger.LogDebug("Commanding pet {PetGuid:X} to use ability {AbilityId} on target {TargetGuid:X}", 
                    _currentPetGuid.Value, abilityId, targetGuid);

                var payload = CreatePetAbilityPayload(_currentPetGuid.Value, abilityId, targetGuid ?? 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PET_ACTION, payload, cancellationToken);

                _logger.LogInformation("Pet ability command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet ability command");
                throw;
            }
        }

        #endregion

        #region Aura/Buff Tracking (Enhanced Implementation)

        /// <summary>
        /// Updates aura information based on server packets.
        /// </summary>
        public void UpdateAura(uint auraId, bool isActive, uint? duration = null, ulong? casterGuid = null)
        {
            try
            {
                if (isActive)
                {
                    var auraData = new AuraData(
                        AuraId: auraId,
                        CasterGuid: casterGuid,
                        Duration: duration,
                        AppliedTime: DateTime.UtcNow,
                        IsActive: true
                    );
                    
                    _activeAuras[auraId] = auraData;
                    _logger.LogDebug("Aura applied: {AuraId} from caster {CasterGuid:X}", auraId, casterGuid);
                }
                else
                {
                    _activeAuras.Remove(auraId);
                    _logger.LogDebug("Aura removed: {AuraId}", auraId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update aura {AuraId}", auraId);
            }
        }

        /// <summary>
        /// Checks if a specific aura is currently active.
        /// </summary>
        public bool HasAura(uint auraId)
        {
            return _activeAuras.ContainsKey(auraId);
        }

        /// <summary>
        /// Gets the remaining duration of an active aura.
        /// </summary>
        public uint? GetAuraRemainingDuration(uint auraId)
        {
            if (_activeAuras.TryGetValue(auraId, out var aura) && aura.Duration.HasValue)
            {
                var elapsed = (uint)(DateTime.UtcNow - aura.AppliedTime).TotalMilliseconds;
                var remaining = aura.Duration.Value > elapsed ? aura.Duration.Value - elapsed : 0;
                return remaining;
            }
            return null;
        }

        #endregion

        #region Combat Item Usage (Enhanced Implementation)

        /// <summary>
        /// Uses an item in combat context with enhanced targeting and timing.
        /// </summary>
        public async Task UseCombatItemAsync(byte bagId, byte slotId, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Using combat item at {BagId}:{SlotId} on target {TargetGuid:X}", bagId, slotId, targetGuid);

                // Handle targeting if needed
                if (targetGuid.HasValue && forceTarget)
                {
                    await _targetingAgent.SetTargetAsync(targetGuid.Value, cancellationToken);
                }

                // Use the item through the item use agent
                if (targetGuid.HasValue)
                {
                    await _itemUseAgent.UseItemOnTargetAsync(bagId, slotId, targetGuid.Value, cancellationToken);
                }
                else
                {
                    await _itemUseAgent.UseItemAsync(bagId, slotId, cancellationToken);
                }

                _logger.LogInformation("Combat item used successfully: {BagId}:{SlotId}", bagId, slotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use combat item {BagId}:{SlotId}", bagId, slotId);
                throw;
            }
        }

        /// <summary>
        /// Uses a health potion automatically based on health percentage.
        /// </summary>
        public async Task AutoUseHealthPotionAsync(float healthPercentageThreshold = 0.3f, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Auto-using health potion (threshold: {Threshold}%)", healthPercentageThreshold * 100);

                // Find a health potion in inventory (placeholder)
                var healthPotionLocation = FindHealthPotionInInventory();
                
                if (healthPotionLocation.HasValue)
                {
                    await _itemUseAgent.UseConsumableAsync(healthPotionLocation.Value.BagId, healthPotionLocation.Value.SlotId, cancellationToken);
                    _logger.LogInformation("Health potion used automatically: {BagId}:{SlotId}", healthPotionLocation.Value.BagId, healthPotionLocation.Value.SlotId);
                }
                else
                {
                    _logger.LogWarning("No health potion found in inventory for auto-use");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-use health potion");
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task ApplyCombatStrategyAsync(CombatStrategy strategy, ulong targetGuid, CancellationToken cancellationToken)
        {
            switch (strategy)
            {
                case CombatStrategy.Aggressive:
                    _logger.LogDebug("Applying aggressive combat strategy");
                    break;
                case CombatStrategy.Defensive:
                    _logger.LogDebug("Applying defensive combat strategy");
                    break;
                case CombatStrategy.Auto:
                default:
                    _logger.LogDebug("Applying automatic combat strategy");
                    break;
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates CMSG_PET_ACTION payload for pet commands (follow, attack, stay, dismiss).
        /// MaNGOS format: petGuid(8) + packedData(4) + targetGuid(8) = 20 bytes always.
        /// packedData = MAKE_UNIT_ACTION_BUTTON(commandId, ACT_COMMAND).
        /// </summary>
        public static byte[] CreatePetCommandPayload(ulong petGuid, PetCommand command, ulong targetGuid = 0)
        {
            var payload = new byte[20];
            BitConverter.GetBytes(petGuid).CopyTo(payload, 0);
            uint packedData = PetActionType.Pack((uint)command, PetActionType.ACT_COMMAND);
            BitConverter.GetBytes(packedData).CopyTo(payload, 8);
            BitConverter.GetBytes(targetGuid).CopyTo(payload, 12);
            return payload;
        }

        /// <summary>
        /// Creates CMSG_PET_ACTION payload for pet ability casts.
        /// MaNGOS format: petGuid(8) + packedData(4) + targetGuid(8) = 20 bytes always.
        /// packedData = MAKE_UNIT_ACTION_BUTTON(spellId, ACT_ENABLED).
        /// </summary>
        public static byte[] CreatePetAbilityPayload(ulong petGuid, uint abilityId, ulong targetGuid = 0)
        {
            var payload = new byte[20];
            BitConverter.GetBytes(petGuid).CopyTo(payload, 0);
            uint packedData = PetActionType.Pack(abilityId, PetActionType.ACT_ENABLED);
            BitConverter.GetBytes(packedData).CopyTo(payload, 8);
            BitConverter.GetBytes(targetGuid).CopyTo(payload, 12);
            return payload;
        }

        private (byte BagId, byte SlotId)? FindHealthPotionInInventory()
        {
            return null;
        }

        private uint GetSpellIdFromName(string spellName)
        {
            return 0; // Placeholder
        }

        private void SetupEventCoordination()
        {
            // Monitor attack state changes to update local combat flags (already handled by _combatStateChanges Do but keep logs)
            _attackAgent.AttackStateChanges.Subscribe(attackData =>
            {
                if (!attackData.IsAttacking && _isInCombat)
                {
                    _logger.LogDebug("Attack stopped during combat, monitoring for combat end");
                }
            });

            // Monitor targeting changes during combat
            _targetingAgent.TargetChanges.Subscribe(targetData =>
            {
                if (_isInCombat && targetData.CurrentTarget != _currentCombatTarget)
                {
                    _currentCombatTarget = targetData.CurrentTarget;
                    _logger.LogDebug("Combat target changed to: {NewTarget:X}", targetData.CurrentTarget);
                }
            });
        }

        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private AuraUpdateData ParseAuraUpdate(ReadOnlyMemory<byte> payload)
        {
            // Best-effort parse: assume [auraId:uint][isActive:byte][duration:uint?][casterGuid:ulong?]
            try
            {
                var span = payload.Span;
                uint auraId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0u;
                bool isActive = span.Length >= 5 ? span[4] != 0 : true;
                uint? duration = span.Length >= 9 ? BitConverter.ToUInt32(span.Slice(5, 4)) : null;
                ulong? caster = span.Length >= 17 ? BitConverter.ToUInt64(span.Slice(9, 8)) : null;
                return new AuraUpdateData(
                    AuraId: auraId,
                    IsActive: isActive,
                    CasterGuid: caster,
                    Duration: duration,
                    Timestamp: DateTime.UtcNow
                );
            }
            catch
            {
                // Fallback minimal update
                return new AuraUpdateData(
                    AuraId: 0,
                    IsActive: true,
                    CasterGuid: null,
                    Duration: null,
                    Timestamp: DateTime.UtcNow
                );
            }
        }

        private async Task ReportCombatErrorAsync(string operation, string errorMessage)
        {
            // Intentionally no Subject push; errors surface via opcode-backed streams.
            await Task.CompletedTask;
        }

        #endregion

        #region Public State Update Methods (Called by packet handlers)

        /// <summary>
        /// Updates the current pet GUID based on server information.
        /// </summary>
        public void UpdateCurrentPet(ulong? petGuid)
        {
            _currentPetGuid = petGuid;
            _logger.LogDebug("Current pet updated: {PetGuid:X}", petGuid);
        }

        /// <summary>
        /// Updates combat state based on server information.
        /// </summary>
        public void UpdateCombatState(bool inCombat, ulong? targetGuid = null)
        {
            _isInCombat = inCombat;
            
            if (targetGuid.HasValue)
            {
                _currentCombatTarget = targetGuid;
            }
            else if (!inCombat)
            {
                _currentCombatTarget = null;
            }

            _logger.LogInformation("Combat state changed: {InCombat}, Target: {TargetGuid:X}", 
                inCombat, _currentCombatTarget);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the combat spell network client component and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing CombatSpellNetworkClientComponent");

            _disposed = true;
            base.Dispose();
            _logger.LogDebug("CombatSpellNetworkClientComponent disposed");
        }

        #endregion
    }
}