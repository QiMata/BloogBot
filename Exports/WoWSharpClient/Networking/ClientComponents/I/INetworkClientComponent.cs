namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Base interface for all network client components.
    /// Provides common operation state and lifetime management.
    /// </summary>
    public interface INetworkClientComponent : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether an operation is currently in progress.
        /// </summary>
        bool IsOperationInProgress { get; }

        /// <summary>
        /// Gets the timestamp of the last operation.
        /// </summary>
        DateTime? LastOperationTime { get; }
    }
}
