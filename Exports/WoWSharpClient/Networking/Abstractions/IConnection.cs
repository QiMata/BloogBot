using System;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Networking.Abstractions
{
    /// <summary>
    /// Represents a raw duplex byte connection with lifecycle management and events.
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
        /// Raised when the connection is successfully established.
        /// </summary>
        event Action? Connected;

        /// <summary>
        /// Raised when the connection is disconnected. The exception parameter indicates 
        /// the reason for disconnection (null for graceful disconnect).
        /// </summary>
        event Action<Exception?>? Disconnected;

        /// <summary>
        /// Raised when bytes are received from the remote host.
        /// </summary>
        event Action<ReadOnlyMemory<byte>>? BytesReceived;
    }
}