namespace WoWSharpClient.Networking.Agent.I
{
    /// <summary>
    /// Interface for handling targeting operations in World of Warcraft.
    /// Focuses solely on target selection without combat functionality.
    /// </summary>
    public interface ITargetingNetworkAgent
    {
        /// <summary>
        /// Gets the currently targeted unit's GUID.
        /// </summary>
        ulong? CurrentTarget { get; }

        /// <summary>
        /// Event fired when the target changes.
        /// </summary>
        event Action<ulong?> TargetChanged;

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
    }
}