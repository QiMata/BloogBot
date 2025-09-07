using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// TCP implementation of IConnection that manages a TcpClient and NetworkStream.
    /// </summary>
    public sealed class TcpConnection : IConnection
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private Task? _readerTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
        private readonly byte[] _readBuffer = new byte[8192];
        private bool _disposed;

        public bool IsConnected => _tcpClient?.Connected == true;

        public event Action? Connected;
        public event Action<Exception?>? Disconnected;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived;

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpConnection));

            if (IsConnected)
                await DisconnectAsync(cancellationToken);

            _cancellationTokenSource = new CancellationTokenSource();
            _tcpClient = new TcpClient();

            try
            {
                await _tcpClient.ConnectAsync(host, port, cancellationToken);
                _stream = _tcpClient.GetStream();
                
                // Start the read loop
                _readerTask = Task.Run(() => ReadLoopAsync(_cancellationTokenSource.Token), cancellationToken);
                
                Connected?.Invoke();
            }
            catch (Exception ex)
            {
                await CleanupAsync();
                throw new InvalidOperationException($"Failed to connect to {host}:{port}", ex);
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return;

            await CleanupAsync();
            Disconnected?.Invoke(null);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (_disposed || _stream == null)
                throw new ObjectDisposedException(nameof(TcpConnection));

            if (!IsConnected)
                throw new InvalidOperationException("Not connected");

            await _sendSemaphore.WaitAsync(cancellationToken);
            try
            {
                await _stream.WriteAsync(data, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _stream != null)
                {
                    int bytesRead = await _stream.ReadAsync(_readBuffer, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        // End of stream - graceful disconnect
                        break;
                    }

                    var receivedData = new ReadOnlyMemory<byte>(_readBuffer, 0, bytesRead);
                    BytesReceived?.Invoke(receivedData);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Unexpected error during read
                Disconnected?.Invoke(ex);
                return;
            }

            // Normal completion or cancellation
            Disconnected?.Invoke(null);
        }

        private async Task CleanupAsync()
        {
            _cancellationTokenSource?.Cancel();

            if (_readerTask != null)
            {
                try
                {
                    await _readerTask;
                }
                catch
                {
                    // Ignore exceptions during cleanup
                }
                _readerTask = null;
            }

            _stream?.Close();
            _stream = null;

            _tcpClient?.Close();
            _tcpClient = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Synchronous cleanup for dispose
            _cancellationTokenSource?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            _sendSemaphore.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}