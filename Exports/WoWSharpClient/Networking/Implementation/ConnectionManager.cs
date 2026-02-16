using WoWSharpClient.Networking.Abstractions;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// Manages connection lifecycle with automatic reconnection based on a policy.
    /// </summary>
    public sealed class ConnectionManager : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IReconnectPolicy _reconnectPolicy;
        private readonly string _host;
        private readonly int _port;
        private int _reconnectAttempts;
        private bool _disposed;
        private bool _isReconnecting;
        private CancellationTokenSource? _reconnectCancellation;
        private IDisposable? _connSubscriptions;

        public ConnectionManager(IConnection connection, IReconnectPolicy reconnectPolicy, string host, int port)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reconnectPolicy = reconnectPolicy ?? throw new ArgumentNullException(nameof(reconnectPolicy));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;

            _connSubscriptions = new CompositeDisposable(
                _connection.WhenConnected.Subscribe(_ => OnConnected()),
                _connection.WhenDisconnected.Subscribe(ex => OnDisconnected(ex))
            );
        }

        /// <summary>
        /// Gets a value indicating whether the connection is established.
        /// </summary>
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        /// Gets the underlying connection.
        /// </summary>
        public IConnection Connection => _connection;

        /// <summary>
        /// Connects to the configured host and port.
        /// </summary>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
            => _connection.ConnectAsync(_host, _port, cancellationToken);

        /// <summary>
        /// Disconnects and stops any reconnection attempts.
        /// </summary>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _reconnectCancellation?.Cancel();
            await _connection.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Raised when the connection is successfully established (including after reconnection).
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Raised when the connection is permanently lost (after all reconnection attempts fail).
        /// </summary>
        public event Action<Exception?>? Disconnected;

        private void OnConnected()
        {
            _reconnectAttempts = 0;
            _isReconnecting = false;
            Connected?.Invoke();
        }

        private async void OnDisconnected(Exception? exception)
        {
            if (_disposed) return;

            // If we manually disconnected (graceful disconnect), don't attempt to reconnect
            if (exception == null)
            {
                _isReconnecting = false;
                Disconnected?.Invoke(null);
                return;
            }

            if (_isReconnecting) return;
            _isReconnecting = true;

            while (true)
            {
                _reconnectAttempts++;
                var delay = _reconnectPolicy.GetDelay(_reconnectAttempts, exception);
                if (!delay.HasValue)
                {
                    // No more reconnection attempts
                    _isReconnecting = false;
                    Disconnected?.Invoke(exception);
                    return;
                }

                try
                {
                    _reconnectCancellation = new CancellationTokenSource();
                    await Task.Delay(delay.Value, _reconnectCancellation.Token);
                    if (_reconnectCancellation.Token.IsCancellationRequested || _disposed)
                    {
                        _isReconnecting = false;
                        return;
                    }

                    await _connection.ConnectAsync(_host, _port, _reconnectCancellation.Token);
                    // If we get here, connection was successful
                    _isReconnecting = false;
                    return;
                }
                catch (Exception reconnectException)
                {
                    // Connection failed, will try again in the next loop iteration
                    exception = reconnectException;
                }
                finally
                {
                    _reconnectCancellation?.Dispose();
                    _reconnectCancellation = null;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reconnectCancellation?.Cancel();
                _connSubscriptions?.Dispose();
                _connection.Dispose();
                _reconnectCancellation?.Dispose();
                _disposed = true;
            }
        }
    }
}