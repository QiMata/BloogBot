using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Subjects;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    public sealed class InMemoryConnection : IConnection
    {
        private readonly ConcurrentQueue<byte[]> _incomingData = new();
        private readonly ConcurrentQueue<byte[]> _outgoingData = new();
        private bool _isConnected;
        private bool _disposed;
        private bool _shouldFailConnections;
        private Exception? _connectionFailureException;

        // Use Subjects from System.Reactive
        private readonly Subject<Unit> _whenConnected = new();
        private readonly Subject<Exception?> _whenDisconnected = new();
        private readonly Subject<ReadOnlyMemory<byte>> _receivedBytes = new();

        public bool IsConnected => _isConnected;

        public IObservable<Unit> WhenConnected => _whenConnected;
        public IObservable<Exception?> WhenDisconnected => _whenDisconnected;
        public IObservable<ReadOnlyMemory<byte>> ReceivedBytes => _receivedBytes;

        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            if (_shouldFailConnections)
            {
                throw _connectionFailureException ?? new InvalidOperationException("Connection failed");
            }

            _isConnected = true;
            _whenConnected.OnNext(Unit.Default);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            _isConnected = false;
            _shouldFailConnections = false;
            _connectionFailureException = null;
            _whenDisconnected.OnNext(null);
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            if (!_isConnected)
                throw new InvalidOperationException("Connection is not connected");

            _outgoingData.Enqueue(data.ToArray());
            return Task.CompletedTask;
        }

        public void InjectIncomingData(ReadOnlyMemory<byte> data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));
            if (!_isConnected)
                throw new InvalidOperationException("Connection is not connected");

            _incomingData.Enqueue(data.ToArray());
            _receivedBytes.OnNext(data);
        }

        public byte[][] GetSentData()
        {
            var result = new List<byte[]>();
            while (_outgoingData.TryDequeue(out var data))
                result.Add(data);
            return result.ToArray();
        }

        public byte[][] GetReceivedData()
        {
            var result = new List<byte[]>();
            while (_incomingData.TryDequeue(out var data))
                result.Add(data);
            return result.ToArray();
        }

        /// <summary>
        /// Test helper: simulates a connection error and optionally forces subsequent reconnection attempts to fail.
        /// </summary>
        /// <param name="ex">The exception representing the connection error.</param>
        /// <param name="shouldFailReconnections">If true, future ConnectAsync calls will fail with the provided exception.</param>
        public void SimulateConnectionError(Exception ex, bool shouldFailReconnections = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryConnection));

            _isConnected = false;
            _shouldFailConnections = shouldFailReconnections;
            _connectionFailureException = shouldFailReconnections ? ex : null;
            _whenDisconnected.OnNext(ex);
        }

        /// <summary>
        /// Test helper: clears any buffered sent/received data in the in-memory queues.
        /// </summary>
        public void ClearData()
        {
            while (_incomingData.TryDequeue(out _)) { }
            while (_outgoingData.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _whenConnected.OnCompleted();
            _whenDisconnected.OnCompleted();
            _receivedBytes.OnCompleted();

            _whenConnected.Dispose();
            _whenDisconnected.Dispose();
            _receivedBytes.Dispose();
        }
    }
}