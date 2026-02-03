namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Provides a policy for reconnection attempts with exponential backoff.
    /// </summary>
    public interface IReconnectPolicy
    {
        /// <summary>
        /// Gets the delay before the next reconnection attempt.
        /// </summary>
        /// <param name="attempt">The number of reconnection attempts made (1-based).</param>
        /// <param name="lastError">The exception that caused the last disconnection, if any.</param>
        /// <returns>The delay before the next attempt, or null to stop reconnecting.</returns>
        TimeSpan? GetDelay(int attempt, Exception? lastError);
    }
}