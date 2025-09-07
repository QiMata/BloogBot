using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// In-memory connection implementation for testing purposes.
    /// Allows direct byte injection without network I/O.
    /// </summary>
    public sealed class InMemoryConnection : IConnection
    {
        private readonly ConcurrentQueue<byte[]> _incomingData = new();
        private readonly ConcurrentQueue<byte[]> _outgoingData = new();
        private bool _isConnected;
        private bool _disposed;
        private bool _shouldFailConnections;
        private Exception? _connectionFailureException;

        /// <summary>
        /// Gets a value indicating whether the connection is connected.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Fired when the connection is established.
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Fired when the connection is lost.
        /// </summary>
        public event Action<Exception?>? Disconnected;

        /// <summary>
        /// Fired when bytes are received.
        /// </summary>
        public event Action<ReadOnlyMemory<byte>>? BytesReceived;

        /// <summary>
        /// Simulates connecting to a host.
        /// </summary>
        /// <param name="host">The host (ignored for in-memory connection).</param>
        /// <param name="port">The port (ignored for in-memory connection).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            if (_shouldFailConnections)
            {
                throw _connectionFailureException ?? new InvalidOperationException("Connection failed");
            }

            _isConnected = true;
            Connected?.Invoke();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Simulates disconnecting from the host.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            _isConnected = false;
            _shouldFailConnections = false; // Reset failure state on manual disconnect
            Disconnected?.Invoke(null);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Simulates sending data. The data is queued for retrieval via GetSentData().
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            if (!_isConnected)
                throw new InvalidOperationException("Connection is not connected");

            _outgoingData.Enqueue(data.ToArray());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Injects incoming data into the connection, triggering BytesReceived event.
        /// </summary>
        /// <param name="data">The data to inject.</param>
        public void InjectIncomingData(ReadOnlyMemory<byte> data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            if (!_isConnected)
                throw new InvalidOperationException("Connection is not connected");

            _incomingData.Enqueue(data.ToArray());
            BytesReceived?.Invoke(data);
        }

        /// <summary>
        /// Retrieves all data that was sent through SendAsync.
        /// </summary>
        /// <returns>Array of byte arrays representing sent data in order.</returns>
        public byte[][] GetSentData()
        {
            var result = new List<byte[]>();
            while (_outgoingData.TryDequeue(out var data))
            {
                result.Add(data);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Retrieves all data that was injected through InjectIncomingData.
        /// </summary>
        /// <returns>Array of byte arrays representing received data in order.</returns>
        public byte[][] GetReceivedData()
        {
            var result = new List<byte[]>();
            while (_incomingData.TryDequeue(out var data))
            {
                result.Add(data);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Clears all sent and received data queues.
        /// </summary>
        public void ClearData()
        {
            while (_outgoingData.TryDequeue(out _)) { }
            while (_incomingData.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Simulates a connection error and disconnects.
        /// </summary>
        /// <param name="exception">The exception to report as the disconnection cause.</param>
        /// <param name="shouldFailReconnections">If true, subsequent connection attempts will fail.</param>
        public void SimulateConnectionError(Exception exception, bool shouldFailReconnections = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            _isConnected = false;
            _shouldFailConnections = shouldFailReconnections;
            _connectionFailureException = exception;
            Disconnected?.Invoke(exception);
        }

        /// <summary>
        /// Allows connections to succeed again after a simulated failure.
        /// </summary>
        public void RestoreConnectivity()
        {
            _shouldFailConnections = false;
            _connectionFailureException = null;
        }

        /// <summary>
        /// Disposes the connection.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    Disconnected?.Invoke(null);
                }

                ClearData();
                _disposed = true;
            }
        }
    }
}