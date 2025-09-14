using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for the enhanced Combat/Spell Network Agent that provides comprehensive combat functionality.
    /// Coordinates spell casting, pet control, aura/buff tracking, and item usage in combat scenarios.
    /// </summary>
    public interface ICombatSpellNetworkClientComponent : INetworkClientComponent
    {
        #region Reactive Observables

        /// <summary>
        /// Observable stream for combat state changes.
        /// </summary>
        IObservable<CombatStateData> CombatStateChanges { get; }

        /// <summary>
        /// Observable stream for pet command operations.
        /// </summary>
        IObservable<PetCommandData> PetCommands { get; }

        /// <summary>
        /// Observable stream for aura/buff updates.
        /// </summary>
        IObservable<AuraUpdateData> AuraUpdates { get; }

        /// <summary>
        /// Observable stream for combat item usage.
        /// </summary>
        IObservable<CombatItemUseData> CombatItemUsage { get; }

        /// <summary>
        /// Observable stream for combat errors.
        /// </summary>
        IObservable<CombatErrorData> CombatErrors { get; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether the character is currently in combat.
        /// </summary>
        bool IsInCombat { get; }

        /// <summary>
        /// Gets the current combat target GUID.
        /// </summary>
        ulong? CurrentCombatTarget { get; }

        /// <summary>
        /// Gets the current pet GUID if any.
        /// </summary>
        ulong? CurrentPetGuid { get; }

        /// <summary>
        /// Gets a read-only view of currently active auras.
        /// </summary>
        IReadOnlyDictionary<uint, AuraData> ActiveAuras { get; }

        /// <summary>
        /// Gets a read-only view of currently active buffs.
        /// </summary>
        IReadOnlyDictionary<uint, BuffData> ActiveBuffs { get; }

        #endregion

        #region Combat Management

        /// <summary>
        /// Initiates combat with a target, coordinating targeting, attacking, and spell casting.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to engage.</param>
        /// <param name="combatStrategy">The combat strategy to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task EngageCombatAsync(ulong targetGuid, CombatStrategy combatStrategy = CombatStrategy.Auto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disengages from combat, stopping attacks and clearing targets.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisengageCombatAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Enhanced Spell Casting

        /// <summary>
        /// Casts a spell with enhanced targeting and condition checking.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="targetGuid">Optional target GUID (uses current target if null).</param>
        /// <param name="forceTarget">Whether to force target the specified GUID before casting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CastSpellAsync(uint spellId, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Casts a spell by name with enhanced targeting and condition checking.
        /// </summary>
        /// <param name="spellName">The name of the spell to cast.</param>
        /// <param name="targetGuid">Optional target GUID (uses current target if null).</param>
        /// <param name="forceTarget">Whether to force target the specified GUID before casting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CastSpellByNameAsync(string spellName, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default);

        #endregion

        #region Pet Control

        /// <summary>
        /// Commands the current pet to attack a target.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target for the pet to attack.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PetAttackAsync(ulong targetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commands the current pet to follow the player.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PetFollowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commands the current pet to use a specific ability.
        /// </summary>
        /// <param name="abilityId">The ID of the ability for the pet to use.</param>
        /// <param name="targetGuid">Optional target for the ability.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PetUseAbilityAsync(uint abilityId, ulong? targetGuid = null, CancellationToken cancellationToken = default);

        #endregion

        #region Aura/Buff Tracking

        /// <summary>
        /// Updates aura information based on server packets.
        /// </summary>
        /// <param name="auraId">The ID of the aura.</param>
        /// <param name="isActive">Whether the aura is active or was removed.</param>
        /// <param name="duration">The duration of the aura in milliseconds.</param>
        /// <param name="casterGuid">The GUID of the caster.</param>
        void UpdateAura(uint auraId, bool isActive, uint? duration = null, ulong? casterGuid = null);

        /// <summary>
        /// Checks if a specific aura is currently active.
        /// </summary>
        /// <param name="auraId">The ID of the aura to check.</param>
        /// <returns>True if the aura is active, false otherwise.</returns>
        bool HasAura(uint auraId);

        /// <summary>
        /// Gets the remaining duration of an active aura.
        /// </summary>
        /// <param name="auraId">The ID of the aura.</param>
        /// <returns>The remaining duration in milliseconds, or null if aura is not active.</returns>
        uint? GetAuraRemainingDuration(uint auraId);

        #endregion

        #region Combat Item Usage

        /// <summary>
        /// Uses an item in combat context with enhanced targeting and timing.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="targetGuid">Optional target for the item.</param>
        /// <param name="forceTarget">Whether to force target before using the item.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UseCombatItemAsync(byte bagId, byte slotId, ulong? targetGuid = null, bool forceTarget = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses a health potion automatically based on health percentage.
        /// </summary>
        /// <param name="healthPercentageThreshold">The health percentage threshold to trigger potion use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AutoUseHealthPotionAsync(float healthPercentageThreshold = 0.3f, CancellationToken cancellationToken = default);

        #endregion

        #region State Update Methods (Called by packet handlers)

        /// <summary>
        /// Updates the current pet GUID based on server information.
        /// </summary>
        /// <param name="petGuid">The GUID of the current pet, or null if no pet.</param>
        void UpdateCurrentPet(ulong? petGuid);

        /// <summary>
        /// Updates combat state based on server information.
        /// </summary>
        /// <param name="inCombat">Whether the character is in combat.</param>
        /// <param name="targetGuid">The current combat target.</param>
        void UpdateCombatState(bool inCombat, ulong? targetGuid = null);

        #endregion
    }
}