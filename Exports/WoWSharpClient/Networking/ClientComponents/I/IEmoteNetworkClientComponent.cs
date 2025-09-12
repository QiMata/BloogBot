using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for the Emote Network Agent that handles emote operations via network packets.
    /// Supports both animated emotes (like /wave, /dance) and text-based emotes with optional targeting.
    /// </summary>
    public interface IEmoteNetworkAgent
    {
        #region Events

        /// <summary>
        /// Triggered when an emote is successfully performed.
        /// </summary>
        event Action<Emote> EmotePerformed;

        /// <summary>
        /// Triggered when a text emote is successfully performed.
        /// </summary>
        event Action<TextEmote, ulong?> TextEmotePerformed;

        /// <summary>
        /// Triggered when an emote operation fails.
        /// </summary>
        event Action<string> EmoteError;

        /// <summary>
        /// Triggered when another player performs an emote that we can see.
        /// </summary>
        event Action<ulong, Emote> EmoteReceived;

        /// <summary>
        /// Triggered when another player performs a text emote that we can see.
        /// </summary>
        event Action<ulong, TextEmote, ulong?> TextEmoteReceived;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the last emote that was performed by this character.
        /// </summary>
        Emote? LastEmote { get; }

        /// <summary>
        /// Gets the last text emote that was performed by this character.
        /// </summary>
        TextEmote? LastTextEmote { get; }

        /// <summary>
        /// Gets the timestamp of the last emote performed.
        /// </summary>
        DateTime? LastEmoteTime { get; }

        /// <summary>
        /// Gets whether an emote operation is currently in progress.
        /// </summary>
        bool IsEmoting { get; }

        #endregion

        #region Emote Operations

        /// <summary>
        /// Performs an animated emote (like /wave, /dance).
        /// </summary>
        /// <param name="emote">The emote to perform.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PerformEmoteAsync(Emote emote, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a text-based emote (like /hello, /bye).
        /// </summary>
        /// <param name="textEmote">The text emote to perform.</param>
        /// <param name="targetGuid">Optional target for the emote. If null, performs as a general emote.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PerformTextEmoteAsync(TextEmote textEmote, ulong? targetGuid = null, CancellationToken cancellationToken = default);

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Performs a wave emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a dance emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DanceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a bow emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a cheer emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CheerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a laugh emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LaughAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a point emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PointAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a salute emote.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaluteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sits down (enters sitting state).
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stands up (enters standing state).
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StandAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Says hello to the specified target or generally.
        /// </summary>
        /// <param name="targetGuid">Optional target for the greeting.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HelloAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Says goodbye to the specified target or generally.
        /// </summary>
        /// <param name="targetGuid">Optional target for the farewell.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ByeAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a thank you emote to the specified target or generally.
        /// </summary>
        /// <param name="targetGuid">Optional target for the thanks.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ThankAsync(ulong? targetGuid = null, CancellationToken cancellationToken = default);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if the specified emote is valid and can be performed.
        /// </summary>
        /// <param name="emote">The emote to validate.</param>
        /// <returns>True if the emote is valid, false otherwise.</returns>
        bool IsValidEmote(Emote emote);

        /// <summary>
        /// Checks if the specified text emote is valid and can be performed.
        /// </summary>
        /// <param name="textEmote">The text emote to validate.</param>
        /// <returns>True if the text emote is valid, false otherwise.</returns>
        bool IsValidTextEmote(TextEmote textEmote);

        /// <summary>
        /// Gets the display name for the specified emote.
        /// </summary>
        /// <param name="emote">The emote to get the name for.</param>
        /// <returns>The display name of the emote.</returns>
        string GetEmoteName(Emote emote);

        /// <summary>
        /// Gets the display name for the specified text emote.
        /// </summary>
        /// <param name="textEmote">The text emote to get the name for.</param>
        /// <returns>The display name of the text emote.</returns>
        string GetTextEmoteName(TextEmote textEmote);

        #endregion

        #region Server Response Handlers

        /// <summary>
        /// Handles incoming emote packets from the server.
        /// This method should be called when SMSG_EMOTE is received.
        /// </summary>
        /// <param name="sourceGuid">The GUID of the player performing the emote.</param>
        /// <param name="emote">The emote being performed.</param>
        void HandleEmoteReceived(ulong sourceGuid, Emote emote);

        /// <summary>
        /// Handles incoming text emote packets from the server.
        /// This method should be called when SMSG_TEXT_EMOTE is received.
        /// </summary>
        /// <param name="sourceGuid">The GUID of the player performing the text emote.</param>
        /// <param name="textEmote">The text emote being performed.</param>
        /// <param name="targetGuid">The target of the text emote, if any.</param>
        void HandleTextEmoteReceived(ulong sourceGuid, TextEmote textEmote, ulong? targetGuid);

        #endregion
    }
}