using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling attack operations in World of Warcraft.
    /// Focuses solely on combat actions like starting and stopping auto-attack.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface IAttackNetworkAgent
    {
        #region Properties

        /// <summary>
        /// Gets whether the character is currently in auto-attack mode.
        /// </summary>
        bool IsAttacking { get; }

        /// <summary>
        /// Gets a value indicating whether an attack operation is currently in progress.
        /// </summary>
        bool IsOperationInProgress { get; }

        /// <summary>
        /// Gets the timestamp of the last attack operation.
        /// </summary>
        DateTime? LastOperationTime { get; }

        /// <summary>
        /// Gets the current victim's GUID if attacking.
        /// </summary>
        ulong? CurrentVictim { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of attack state changes (start/stop).
        /// </summary>
        IObservable<AttackStateData> AttackStateChanges { get; }

        /// <summary>
        /// Observable stream of weapon swing information.
        /// </summary>
        IObservable<WeaponSwingData> WeaponSwings { get; }

        /// <summary>
        /// Observable stream of attack errors.
        /// </summary>
        IObservable<AttackErrorData> AttackErrors { get; }

        #endregion

        #region Operations

        /// <summary>
        /// Starts auto-attack on the current target.
        /// Sends CMSG_ATTACKSWING to the server.
        /// Requires a target to be set via the targeting agent.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no target is selected.</exception>
        Task StartAttackAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops auto-attack.
        /// Sends CMSG_ATTACKSTOP to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopAttackAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts auto-attack on a specific target.
        /// This is a convenience method that sets the target and starts attacking.
        /// Coordinates with the targeting agent to set the target first.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to attack.</param>
        /// <param name="targetingAgent">The targeting agent to use for target selection.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AttackTargetAsync(ulong targetGuid, ITargetingNetworkAgent targetingAgent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Toggles auto-attack state. If attacking, stops. If not attacking, starts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when trying to start attack with no target selected.</exception>
        Task ToggleAttackAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Server Response Handling

        /// <summary>
        /// Handles attack state change notification from the server.
        /// This method should be called by the packet handler when attack state changes.
        /// </summary>
        /// <param name="isAttacking">Whether attacking started or stopped.</param>
        /// <param name="attackerGuid">The attacker's GUID.</param>
        /// <param name="victimGuid">The victim's GUID.</param>
        void HandleAttackStateChanged(bool isAttacking, ulong attackerGuid, ulong victimGuid);

        /// <summary>
        /// Handles weapon swing data from the server.
        /// </summary>
        /// <param name="attackerGuid">The attacker's GUID.</param>
        /// <param name="victimGuid">The victim's GUID.</param>
        /// <param name="damage">The damage dealt.</param>
        /// <param name="isCritical">Whether the hit was critical.</param>
        void HandleWeaponSwing(ulong attackerGuid, ulong victimGuid, uint damage, bool isCritical);

        /// <summary>
        /// Handles attack error from the server.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="targetGuid">The target that caused the error.</param>
        void HandleAttackError(string errorMessage, ulong? targetGuid = null);

        #endregion

        #region Legacy Callback Support (for backwards compatibility)

        /// <summary>
        /// Sets the callback function to be invoked when auto-attack starts.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the victim's GUID.</param>
        [Obsolete("Use AttackStateChanges observable instead")]
        void SetAttackStartedCallback(Action<ulong>? callback);

        /// <summary>
        /// Sets the callback function to be invoked when auto-attack stops.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function to invoke when attack stops.</param>
        [Obsolete("Use AttackStateChanges observable instead")]
        void SetAttackStoppedCallback(Action? callback);

        /// <summary>
        /// Sets the callback function to be invoked when an attack error occurs.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the error message.</param>
        [Obsolete("Use AttackErrors observable instead")]
        void SetAttackErrorCallback(Action<string>? callback);

        #endregion
    }
}