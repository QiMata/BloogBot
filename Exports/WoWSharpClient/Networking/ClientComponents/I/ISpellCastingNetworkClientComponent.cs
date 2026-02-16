using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Represents the type of spell being cast.
    /// </summary>
    public enum SpellCastType
    {
        Normal,
        Channeled,
        Instant,
        AutoRepeating,
        Triggered
    }

    /// <summary>
    /// Represents spell school types.
    /// </summary>
    public enum SpellSchool
    {
        Normal = 0,
        Holy = 1,
        Fire = 2,
        Nature = 3,
        Frost = 4,
        Shadow = 5,
        Arcane = 6
    }

    /// <summary>
    /// Interface for handling spell casting operations in World of Warcraft.
    /// Manages spell casting, channeling, and spell state tracking.
    /// Uses reactive observables for event streams.
    /// </summary>
    public interface ISpellCastingNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether a spell is currently being cast.
        /// </summary>
        bool IsCasting { get; }

        /// <summary>
        /// Gets a value indicating whether a channeled spell is currently active.
        /// </summary>
        bool IsChanneling { get; }

        /// <summary>
        /// Gets the ID of the spell currently being cast, if any.
        /// </summary>
        uint? CurrentSpellId { get; }

        /// <summary>
        /// Gets the target GUID of the current spell cast, if any.
        /// </summary>
        ulong? CurrentSpellTarget { get; }

        /// <summary>
        /// Gets the remaining cast time in milliseconds for the current spell.
        /// </summary>
        uint RemainingCastTime { get; }

        #region Reactive Observables
        /// <summary>
        /// Observable stream for spell cast start events.
        /// </summary>
        IObservable<SpellCastStartData> SpellCastStarts { get; }

        /// <summary>
        /// Observable stream for spell cast completion events.
        /// </summary>
        IObservable<SpellCastCompleteData> SpellCastCompletions { get; }

        /// <summary>
        /// Observable stream for spell cast errors/failures.
        /// </summary>
        IObservable<SpellCastErrorData> SpellCastErrors { get; }

        /// <summary>
        /// Observable stream for channeling start/stop events.
        /// </summary>
        IObservable<ChannelingData> ChannelingEvents { get; }

        /// <summary>
        /// Observable stream for spell cooldown updates.
        /// </summary>
        IObservable<SpellCooldownData> SpellCooldowns { get; }

        /// <summary>
        /// Observable stream for spell hit results (damage/heal).
        /// </summary>
        IObservable<SpellHitData> SpellHits { get; }
        #endregion

        /// <summary>
        /// Casts a spell without a target.
        /// Sends CMSG_CAST_SPELL with the spell ID.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CastSpellAsync(uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Casts a spell on a specific target.
        /// Sends CMSG_CAST_SPELL with the spell ID and target GUID.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="targetGuid">The GUID of the target.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CastSpellOnTargetAsync(uint spellId, ulong targetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Casts a spell at specific coordinates.
        /// Sends CMSG_CAST_SPELL with the spell ID and world coordinates.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="z">The Z coordinate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CastSpellAtLocationAsync(uint spellId, float x, float y, float z, CancellationToken cancellationToken = default);

        /// <summary>
        /// Interrupts the current spell cast.
        /// Sends CMSG_CANCEL_CAST to stop the current spell.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InterruptCastAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the current channeled spell.
        /// Sends CMSG_CANCEL_CHANNELLING to end the channel.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopChannelingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts auto-repeating a spell (like auto-attack for wands).
        /// Sends CMSG_SET_ACTIVE_MOVER and CMSG_CAST_SPELL for auto-repeat setup.
        /// </summary>
        /// <param name="spellId">The ID of the spell to auto-repeat.</param>
        /// <param name="targetGuid">The target GUID for the auto-repeat.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAutoRepeatSpellAsync(uint spellId, ulong targetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops auto-repeating spell casting.
        /// Sends CMSG_CANCEL_AUTO_REPEAT_SPELL to stop auto-repeat.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopAutoRepeatSpellAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Casts a spell from a specific action bar slot.
        /// This uses the client's action bar configuration.
        /// </summary>
        /// <param name="actionBarSlot">The action bar slot number (0-119).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CastSpellFromActionBarAsync(byte actionBarSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to cast a spell with automatic target selection.
        /// If no target is selected and the spell requires one, it will try to use the closest valid target.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SmartCastSpellAsync(uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a spell can be cast based on cooldowns, mana, and other requirements.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>True if the spell can be cast, false otherwise.</returns>
        bool CanCastSpell(uint spellId);

        /// <summary>
        /// Gets the remaining cooldown time for a spell in milliseconds.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The remaining cooldown time in milliseconds, or 0 if no cooldown.</returns>
        uint GetSpellCooldown(uint spellId);

        /// <summary>
        /// Gets the mana cost of a spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The mana cost of the spell.</returns>
        uint GetSpellManaCost(uint spellId);

        /// <summary>
        /// Gets the cast time of a spell in milliseconds.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The cast time in milliseconds.</returns>
        uint GetSpellCastTime(uint spellId);

        /// <summary>
        /// Gets the range of a spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The maximum range of the spell.</returns>
        float GetSpellRange(uint spellId);

        /// <summary>
        /// Checks if a spell requires a target.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>True if the spell requires a target, false otherwise.</returns>
        bool SpellRequiresTarget(uint spellId);

        /// <summary>
        /// Gets the spell school of a spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The spell school.</returns>
        SpellSchool GetSpellSchool(uint spellId);

        /// <summary>
        /// Checks if the player knows a specific spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>True if the player knows the spell, false otherwise.</returns>
        bool KnowsSpell(uint spellId);
    }
}