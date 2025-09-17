namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling death and resurrection operations in World of Warcraft.
    /// Manages spirit release, corpse resurrection, and spirit healer interactions.
    /// </summary>
    public interface IDeadActorNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the character is currently dead.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Gets a value indicating whether the character is currently a ghost (spirit released).
        /// </summary>
        bool IsGhost { get; }

        /// <summary>
        /// Gets a value indicating whether there is a resurrection request pending.
        /// </summary>
        bool HasResurrectionRequest { get; }

        /// <summary>
        /// Gets the location of the character's corpse, if available.
        /// </summary>
        (float X, float Y, float Z)? CorpseLocation { get; }

        // Reactive opcode-backed streams (no events/subjects)
        /// <summary>
        /// Stream of death-related events derived from world opcodes
        /// (CharacterDied, SpiritReleased, CharacterResurrected, CorpseLocationUpdated).
        /// </summary>
        IObservable<WoWSharpClient.Networking.ClientComponents.Models.DeathData> DeathEvents { get; }

        /// <summary>
        /// Stream of resurrection notifications derived from world opcodes
        /// (player/NPC resurrection requests, confirmations, etc.).
        /// </summary>
        IObservable<WoWSharpClient.Networking.ClientComponents.Models.ResurrectionData> ResurrectionNotifications { get; }

        /// <summary>
        /// Stream of death/resurrection related errors.
        /// </summary>
        IObservable<WoWSharpClient.Networking.ClientComponents.Models.DeathErrorData> DeathErrors { get; }

        /// <summary>
        /// Releases the character's spirit, becoming a ghost.
        /// Sends CMSG_REPOP_REQUEST to release spirit at the nearest graveyard.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ReleaseSpiritAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to resurrect at the character's corpse.
        /// Sends CMSG_RECLAIM_CORPSE to return to life at the corpse location.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResurrectAtCorpseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Accepts a resurrection request from another player or NPC.
        /// Sends CMSG_RESURRECT_RESPONSE with accept flag.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AcceptResurrectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Declines a resurrection request from another player or NPC.
        /// Sends CMSG_RESURRECT_RESPONSE with decline flag.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeclineResurrectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Interacts with a spirit healer to get resurrected (with resurrection sickness).
        /// Sends CMSG_SPIRIT_HEALER_ACTIVATE with the spirit healer's GUID.
        /// </summary>
        /// <param name="spiritHealerGuid">The GUID of the spirit healer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResurrectWithSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the location of the character's corpse.
        /// Sends MSG_CORPSE_QUERY to get corpse location information.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryCorpseLocationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries available spirit healers in the area.
        /// Sends CMSG_AREA_SPIRIT_HEALER_QUERY to find nearby spirit healers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryAreaSpiritHealersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queues for resurrection at an area spirit healer.
        /// Sends CMSG_AREA_SPIRIT_HEALER_QUEUE to join the resurrection queue.
        /// </summary>
        /// <param name="spiritHealerGuid">The GUID of the spirit healer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueueForSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs self-resurrection (if the character has this ability).
        /// Sends CMSG_SELF_RES to resurrect without external help.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SelfResurrectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the distance to the character's corpse from current ghost position.
        /// </summary>
        /// <param name="currentX">Current X position.</param>
        /// <param name="currentY">Current Y position.</param>
        /// <param name="currentZ">Current Z position.</param>
        /// <returns>The distance to corpse, or null if corpse location is unknown.</returns>
        float? GetDistanceToCorpse(float currentX, float currentY, float currentZ);

        /// <summary>
        /// Checks if the character is close enough to their corpse to resurrect.
        /// </summary>
        /// <param name="currentX">Current X position.</param>
        /// <param name="currentY">Current Y position.</param>
        /// <param name="currentZ">Current Z position.</param>
        /// <param name="maxDistance">Maximum distance for resurrection (default 39 yards).</param>
        /// <returns>True if close enough to resurrect, false otherwise.</returns>
        bool IsCloseToCorpse(float currentX, float currentY, float currentZ, float maxDistance = 39.0f);

        /// <summary>
        /// Gets the time remaining until spirit healer resurrection becomes available.
        /// This information is typically provided by the server.
        /// </summary>
        /// <returns>Time remaining in seconds, or null if not available.</returns>
        TimeSpan? GetSpiritHealerResurrectionTime();

        /// <summary>
        /// Performs automatic death handling: release spirit, move to corpse, and resurrect.
        /// This is a convenience method for handling death automatically.
        /// </summary>
        /// <param name="allowSpiritHealer">Whether to use spirit healer if corpse resurrection fails.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AutoHandleDeathAsync(bool allowSpiritHealer = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the character's death state based on game events.
        /// This is typically called by the underlying game client when death state changes.
        /// </summary>
        /// <param name="isDead">Whether the character is dead.</param>
        /// <param name="isGhost">Whether the character is a ghost.</param>
        void UpdateDeathState(bool isDead, bool isGhost);

        /// <summary>
        /// Updates the corpse location information.
        /// This is typically called when corpse location data is received from the server.
        /// </summary>
        /// <param name="x">X coordinate of the corpse.</param>
        /// <param name="y">Y coordinate of the corpse.</param>
        /// <param name="z">Z coordinate of the corpse.</param>
        void UpdateCorpseLocation(float x, float y, float z);

        /// <summary>
        /// Handles a resurrection request from another player or NPC.
        /// This method should be called when a resurrection request is received from the server.
        /// </summary>
        /// <param name="resurrectorGuid">The GUID of the player or NPC offering resurrection.</param>
        /// <param name="resurrectorName">The name of the resurrector.</param>
        void HandleResurrectionRequest(ulong resurrectorGuid, string resurrectorName);

        /// <summary>
        /// Handles spirit healer resurrection timing information.
        /// This method should be called when spirit healer time data is received from the server.
        /// </summary>
        /// <param name="timeSpan">The time until resurrection becomes available.</param>
        void HandleSpiritHealerTime(TimeSpan timeSpan);

        /// <summary>
        /// Handles death-related error messages.
        /// This method should be called when death/resurrection operations fail.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        void HandleDeathError(string errorMessage);
    }
}