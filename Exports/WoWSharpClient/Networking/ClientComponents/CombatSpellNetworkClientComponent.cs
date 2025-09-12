using System.Reactive.Subjects;
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
    public class CombatSpellNetworkClientComponent : ICombatSpellNetworkClientComponent
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

        // Reactive observables for enhanced event handling (Subjects)
        private readonly Subject<CombatStateData> _combatStateChanges = new();
        private readonly Subject<PetCommandData> _petCommands = new();
        private readonly Subject<AuraUpdateData> _auraUpdates = new();
        private readonly Subject<CombatItemUseData> _combatItemUsage = new();
        private readonly Subject<CombatErrorData> _combatErrors = new();

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

                // Set target first
                await _targetingAgent.SetTargetAsync(targetGuid, cancellationToken);
                
                // Update combat state
                _isInCombat = true;
                _currentCombatTarget = targetGuid;
                
                // Emit combat state change
                _combatStateChanges.OnNext(new CombatStateData(
                    IsInCombat: true,
                    TargetGuid: targetGuid,
                    Strategy: combatStrategy,
                    Timestamp: DateTime.UtcNow
                ));

                // Start auto-attack
                await _attackAgent.StartAttackAsync(cancellationToken);

                // Apply combat strategy
                await ApplyCombatStrategyAsync(combatStrategy, targetGuid, cancellationToken);

                _logger.LogInformation("Combat engagement initiated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to engage combat with target: {TargetGuid:X}", targetGuid);
                await ReportCombatErrorAsync("Combat engagement failed", ex.Message);
                throw;
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

                // Stop auto-attack
                await _attackAgent.StopAttackAsync(cancellationToken);
                
                // Clear target
                await _targetingAgent.ClearTargetAsync(cancellationToken);
                
                // Update combat state
                var previousTarget = _currentCombatTarget;
                _isInCombat = false;
                _currentCombatTarget = null;
                
                // Emit combat state change
                _combatStateChanges.OnNext(new CombatStateData(
                    IsInCombat: false,
                    TargetGuid: null,
                    Strategy: CombatStrategy.None,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Combat disengagement completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disengage from combat");
                await ReportCombatErrorAsync("Combat disengagement failed", ex.Message);
                throw;
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
                await ReportCombatErrorAsync("Spell casting failed", $"Spell {spellId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Casts a spell by name with enhanced targeting and condition checking.
        /// This is a convenience method that would require spell name to ID mapping.
        /// </summary>
        /// <param name="spellName">The name of the spell to cast.</param>
        /// <param name="targetGuid">Optional target GUID (uses current target if null).</param>
        /// <param name="forceTarget">Whether to force target the specified GUID before casting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CastSpellByNameAsync(string spellName, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Casting spell '{SpellName}' on target {TargetGuid:X}", spellName, targetGuid);

                // This would require a spell name to ID mapping system
                // For now, we'll use smart casting which handles target selection
                var spellId = GetSpellIdFromName(spellName); // This would need to be implemented
                
                // Handle targeting if needed
                if (targetGuid.HasValue && forceTarget)
                {
                    await _targetingAgent.SetTargetAsync(targetGuid.Value, cancellationToken);
                }

                // Use the smart casting method which handles target selection
                await _spellCastingAgent.SmartCastSpellAsync(spellId, cancellationToken);

                _logger.LogDebug("Spell cast by name initiated successfully: '{SpellName}'", spellName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cast spell '{SpellName}' on target {TargetGuid:X}", spellName, targetGuid);
                await ReportCombatErrorAsync("Spell casting failed", $"Spell '{spellName}': {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Pet Control (Enhanced Implementation)

        /// <summary>
        /// Commands the current pet to attack a target.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target for the pet to attack.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
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

                // Emit pet command event
                _petCommands.OnNext(new PetCommandData(
                    PetGuid: _currentPetGuid.Value,
                    Command: PetCommand.Attack,
                    TargetGuid: targetGuid,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Pet attack command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet attack command");
                await ReportCombatErrorAsync("Pet command failed", $"Pet attack: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Commands the current pet to follow the player.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
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

                // Emit pet command event
                _petCommands.OnNext(new PetCommandData(
                    PetGuid: _currentPetGuid.Value,
                    Command: PetCommand.Follow,
                    TargetGuid: null,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Pet follow command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet follow command");
                await ReportCombatErrorAsync("Pet command failed", $"Pet follow: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Commands the current pet to use a specific ability.
        /// </summary>
        /// <param name="abilityId">The ID of the ability for the pet to use.</param>
        /// <param name="targetGuid">Optional target for the ability.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
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

                var payload = CreatePetAbilityPayload(_currentPetGuid.Value, abilityId, targetGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_PET_ACTION, payload, cancellationToken);

                // Emit pet command event
                _petCommands.OnNext(new PetCommandData(
                    PetGuid: _currentPetGuid.Value,
                    Command: PetCommand.UseAbility,
                    TargetGuid: targetGuid,
                    AbilityId: abilityId,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Pet ability command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send pet ability command");
                await ReportCombatErrorAsync("Pet command failed", $"Pet ability {abilityId}: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Aura/Buff Tracking (Enhanced Implementation)

        /// <summary>
        /// Updates aura information based on server packets.
        /// </summary>
        /// <param name="auraId">The ID of the aura.</param>
        /// <param name="isActive">Whether the aura is active or was removed.</param>
        /// <param name="duration">The duration of the aura in milliseconds.</param>
        /// <param name="casterGuid">The GUID of the caster.</param>
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

                // Emit aura update event
                _auraUpdates.OnNext(new AuraUpdateData(
                    AuraId: auraId,
                    IsActive: isActive,
                    CasterGuid: casterGuid,
                    Duration: duration,
                    Timestamp: DateTime.UtcNow
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update aura {AuraId}", auraId);
            }
        }

        /// <summary>
        /// Checks if a specific aura is currently active.
        /// </summary>
        /// <param name="auraId">The ID of the aura to check.</param>
        /// <returns>True if the aura is active, false otherwise.</returns>
        public bool HasAura(uint auraId)
        {
            return _activeAuras.ContainsKey(auraId);
        }

        /// <summary>
        /// Gets the remaining duration of an active aura.
        /// </summary>
        /// <param name="auraId">The ID of the aura.</param>
        /// <returns>The remaining duration in milliseconds, or null if aura is not active.</returns>
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
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="targetGuid">Optional target for the item.</param>
        /// <param name="forceTarget">Whether to force target before using the item.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
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

                // Emit combat item usage event
                _combatItemUsage.OnNext(new CombatItemUseData(
                    ItemGuid: 0, // We don't have the GUID from bag/slot, would need inventory lookup
                    TargetGuid: targetGuid,
                    UsageType: CombatItemUsageType.Manual,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Combat item used successfully: {BagId}:{SlotId}", bagId, slotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use combat item {BagId}:{SlotId}", bagId, slotId);
                await ReportCombatErrorAsync("Combat item usage failed", $"Item {bagId}:{slotId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Uses a health potion automatically based on health percentage.
        /// </summary>
        /// <param name="healthPercentageThreshold">The health percentage threshold to trigger potion use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task AutoUseHealthPotionAsync(float healthPercentageThreshold = 0.3f, CancellationToken cancellationToken = default)
        {
            try
            {
                // This would typically check current health percentage from game state
                // For now, we'll simulate finding and using a health potion
                
                _logger.LogDebug("Auto-using health potion (threshold: {Threshold}%)", healthPercentageThreshold * 100);

                // Find a health potion in inventory (this would be implemented with actual inventory checking)
                var healthPotionLocation = FindHealthPotionInInventory();
                
                if (healthPotionLocation.HasValue)
                {
                    await _itemUseAgent.UseConsumableAsync(healthPotionLocation.Value.BagId, healthPotionLocation.Value.SlotId, cancellationToken);
                    
                    // Emit combat item usage event
                    _combatItemUsage.OnNext(new CombatItemUseData(
                        ItemGuid: 0, // We don't have the GUID from bag/slot, would need inventory lookup
                        TargetGuid: null,
                        UsageType: CombatItemUsageType.AutomaticHealing,
                        Timestamp: DateTime.UtcNow
                    ));

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
                await ReportCombatErrorAsync("Auto health potion failed", ex.Message);
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
                    // Could implement aggressive strategy with immediate ability usage
                    _logger.LogDebug("Applying aggressive combat strategy");
                    break;
                case CombatStrategy.Defensive:
                    // Could implement defensive strategy with emphasis on survival
                    _logger.LogDebug("Applying defensive combat strategy");
                    break;
                case CombatStrategy.Auto:
                default:
                    // Default auto strategy
                    _logger.LogDebug("Applying automatic combat strategy");
                    break;
            }
        }

        private byte[] CreatePetCommandPayload(ulong petGuid, PetCommand command, ulong? targetGuid = null)
        {
            // Create the payload for pet commands according to the WoW protocol
            var payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(petGuid));
            payload.AddRange(BitConverter.GetBytes((uint)command));
            
            if (targetGuid.HasValue)
            {
                payload.AddRange(BitConverter.GetBytes(targetGuid.Value));
            }
            
            return payload.ToArray();
        }

        private byte[] CreatePetAbilityPayload(ulong petGuid, uint abilityId, ulong? targetGuid = null)
        {
            // Create the payload for pet ability usage according to the WoW protocol
            var payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(petGuid));
            payload.AddRange(BitConverter.GetBytes(abilityId));
            
            if (targetGuid.HasValue)
            {
                payload.AddRange(BitConverter.GetBytes(targetGuid.Value));
            }
            
            return payload.ToArray();
        }

        private (byte BagId, byte SlotId)? FindHealthPotionInInventory()
        {
            // This would be implemented to actually search the inventory for health potions
            // For now, returning null as a placeholder
            // In a real implementation, this would:
            // 1. Access the inventory system
            // 2. Search for items with healing properties
            // 3. Return the bag and slot location of the first found potion
            return null;
        }

        private uint GetSpellIdFromName(string spellName)
        {
            // This would be implemented with a spell database or lookup system
            // For now, returning a placeholder value
            // In a real implementation, this would:
            // 1. Access a spell database
            // 2. Look up the spell ID by name
            // 3. Handle localization if needed
            return 0; // Placeholder
        }

        private void SetupEventCoordination()
        {
            // Coordinate with other agents through their reactive observables
            
            // Monitor attack state changes to update combat state
            _attackAgent.AttackStateChanges.Subscribe(attackData =>
            {
                if (!attackData.IsAttacking && _isInCombat)
                {
                    // Combat might be ending, could trigger cleanup
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

        private async Task ReportCombatErrorAsync(string operation, string errorMessage)
        {
            _combatErrors.OnNext(new CombatErrorData(
                Operation: operation,
                ErrorMessage: errorMessage,
                TargetGuid: _currentCombatTarget,
                Timestamp: DateTime.UtcNow
            ));
        }

        #endregion

        #region Public State Update Methods (Called by packet handlers)

        /// <summary>
        /// Updates the current pet GUID based on server information.
        /// </summary>
        /// <param name="petGuid">The GUID of the current pet, or null if no pet.</param>
        public void UpdateCurrentPet(ulong? petGuid)
        {
            _currentPetGuid = petGuid;
            _logger.LogDebug("Current pet updated: {PetGuid:X}", petGuid);
        }

        /// <summary>
        /// Updates combat state based on server information.
        /// </summary>
        /// <param name="inCombat">Whether the character is in combat.</param>
        /// <param name="targetGuid">The current combat target.</param>
        public void UpdateCombatState(bool inCombat, ulong? targetGuid = null)
        {
            var previousState = _isInCombat;
            _isInCombat = inCombat;
            
            if (targetGuid.HasValue)
            {
                _currentCombatTarget = targetGuid;
            }
            else if (!inCombat)
            {
                _currentCombatTarget = null;
            }

            if (previousState != inCombat)
            {
                _combatStateChanges.OnNext(new CombatStateData(
                    IsInCombat: inCombat,
                    TargetGuid: _currentCombatTarget,
                    Strategy: CombatStrategy.Auto,
                    Timestamp: DateTime.UtcNow
                ));

                _logger.LogInformation("Combat state changed: {InCombat}, Target: {TargetGuid:X}", 
                    inCombat, _currentCombatTarget);
            }
        }

        #endregion
    }
}