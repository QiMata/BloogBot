using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling targeting operations in World of Warcraft.
    /// Focuses solely on target selection without combat functionality.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface ITargetingNetworkAgent
    {
        #region Properties

        /// <summary>
        /// Gets the currently targeted unit's GUID.
        /// </summary>
        ulong? CurrentTarget { get; }

        /// <summary>
        /// Gets a value indicating whether a targeting operation is currently in progress.
        /// </summary>
        bool IsOperationInProgress { get; }

        /// <summary>
        /// Gets the timestamp of the last targeting operation.
        /// </summary>
        DateTime? LastOperationTime { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of targeting data when targets change.
        /// </summary>
        IObservable<TargetingData> TargetChanges { get; }

        /// <summary>
        /// Observable stream of assist operations.
        /// </summary>
        IObservable<AssistData> AssistOperations { get; }

        /// <summary>
        /// Observable stream of targeting errors.
        /// </summary>
        IObservable<TargetingErrorData> TargetingErrors { get; }

        #endregion

        #region Operations

        /// <summary>
        /// Sets the current target to the specified GUID.
        /// Sends CMSG_SET_SELECTION to the server.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to select. Use 0 to clear target.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetTargetAsync(ulong targetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the current target by setting it to 0.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ClearTargetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Assists another player by targeting what they are targeting.
        /// This targets the specified player, allowing the server to automatically
        /// switch your target to whatever they're targeting.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to assist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AssistAsync(ulong playerGuid, CancellationToken cancellationToken = default);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if the specified GUID is currently targeted.
        /// </summary>
        /// <param name="guid">The GUID to check.</param>
        /// <returns>True if the specified GUID is the current target, false otherwise.</returns>
        bool IsTargeted(ulong guid);

        /// <summary>
        /// Checks if any target is currently selected.
        /// </summary>
        /// <returns>True if a target is selected, false otherwise.</returns>
        bool HasTarget();

        #endregion

        #region Server Response Handling

        /// <summary>
        /// Handles a target change notification from the server.
        /// This method should be called by the packet handler when the target changes.
        /// </summary>
        /// <param name="newTarget">The new target GUID (null if target is cleared).</param>
        void HandleTargetChanged(ulong? newTarget);

        /// <summary>
        /// Handles targeting error from the server.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="targetGuid">The target that caused the error.</param>
        void HandleTargetingError(string errorMessage, ulong? targetGuid = null);

        #endregion

        #region Legacy Callback Support (for backwards compatibility)

        /// <summary>
        /// Sets the callback function to be invoked when the target changes.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the new target GUID (null if target is cleared).</param>
        [Obsolete("Use TargetChanges observable instead")]
        void SetTargetChangedCallback(Action<ulong?>? callback);

        #endregion
    }
}