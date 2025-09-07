using GameData.Core.Enums;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Interface for handling attack operations in World of Warcraft.
    /// Focuses solely on combat actions like starting and stopping auto-attack.
    /// </summary>
    public interface IAttackNetworkAgent
    {
        /// <summary>
        /// Gets whether the character is currently in auto-attack mode.
        /// </summary>
        bool IsAttacking { get; }

        /// <summary>
        /// Event fired when auto-attack starts.
        /// </summary>
        event Action<ulong> AttackStarted;

        /// <summary>
        /// Event fired when auto-attack stops.
        /// </summary>
        event Action AttackStopped;

        /// <summary>
        /// Event fired when an attack error occurs (not in range, bad facing, etc.).
        /// </summary>
        event Action<string> AttackError;

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
    }
}