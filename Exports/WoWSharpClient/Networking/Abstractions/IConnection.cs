namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Represents a raw duplex byte connection with lifecycle management and reactive streams.
    /// </summary>
    public interface IConnection : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the connection is currently established.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the specified host and port.
        /// </summary>
        /// <param name="host">The hostname or IP address to connect to.</param>
        /// <param name="port">The port number to connect to.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous connect operation.</returns>
        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the remote host.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous disconnect operation.</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends data to the remote host.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Observable that fires when the connection is established.
        /// </summary>
        IObservable<System.Reactive.Unit> WhenConnected { get; }

        /// <summary>
        /// Observable that fires when the connection is disconnected.
        /// Exception is null for graceful disconnects.
        /// </summary>
        IObservable<Exception?> WhenDisconnected { get; }

        /// <summary>
        /// Observable stream of bytes received from the remote host.
        /// </summary>
        IObservable<ReadOnlyMemory<byte>> ReceivedBytes { get; }
    }
}