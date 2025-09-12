using System;
using System.Buffers;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Abstractions;

namespace WoWSharpClient.Networking.Implementation
{
    /// <summary>
    /// TCP implementation of IConnection that manages a TcpClient and NetworkStream.
    /// Provides events and observables for lifecycle and incoming data.
    /// </summary>
    public sealed class TcpConnection : IConnection
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private IDisposable? _readSubscription;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
        private bool _disposed;

        // Events API (for backward compatibility/tests)
        public event Action? Connected;
        public event Action<Exception?>? Disconnected;
        public event Action<ReadOnlyMemory<byte>>? BytesReceived;

        // Reactive observables backing the public streams (use Subjects)
        private readonly Subject<System.Reactive.Unit> _whenConnected = new();
        private readonly Subject<Exception?> _whenDisconnected = new();
        private readonly Subject<ReadOnlyMemory<byte>> _receivedBytes = new();

        public bool IsConnected => _tcpClient?.Connected == true;

        public IObservable<System.Reactive.Unit> WhenConnected => _whenConnected;
        public IObservable<Exception?> WhenDisconnected => _whenDisconnected;
        public IObservable<ReadOnlyMemory<byte>> ReceivedBytes => _receivedBytes;

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

                // Start the reactive read pipeline without ambiguous Subscribe overloads
                _readSubscription = CreateReadObservable(_stream, _cancellationTokenSource.Token)
                    .Subscribe(new InlineObserver<ReadOnlyMemory<byte>>(
                        data => { _receivedBytes.OnNext(data); BytesReceived?.Invoke(data); },
                        ex => { _whenDisconnected.OnNext(ex); Disconnected?.Invoke(ex); },
                        () => { _whenDisconnected.OnNext(null); Disconnected?.Invoke(null); }
                    ));

                // Notify connection established
                _whenConnected.OnNext(System.Reactive.Unit.Default);
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
            {
                await CleanupAsync();
                return;
            }

            await CleanupAsync();
            _whenDisconnected.OnNext(null);
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

        private static IObservable<ReadOnlyMemory<byte>> CreateReadObservable(NetworkStream stream, CancellationToken cancellationToken)
        {
            // Observable that reads from the network stream until EOF or cancellation.
            return Observable.Create<ReadOnlyMemory<byte>>(async (observer, ct) =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
                var token = linkedCts.Token;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            // EOF
                            break;
                        }

                        var chunk = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                        observer.OnNext(chunk);
                    }

                    observer.OnCompleted();
                }
                catch (OperationCanceledException)
                {
                    observer.OnCompleted();
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // Dispose action cancels the read loop
                return () =>
                {
                    try { linkedCts.Cancel(); } catch { /* ignore */ }
                    linkedCts.Dispose();
                };
            });
        }

        private async Task CleanupAsync()
        {
            try { _cancellationTokenSource?.Cancel(); } catch { /* ignore */ }

            _readSubscription?.Dispose();
            _readSubscription = null;

            if (_stream != null)
            {
                try { await _stream.FlushAsync(); } catch { /* ignore */ }
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

            try { _cancellationTokenSource?.Cancel(); } catch { /* ignore */ }
            _readSubscription?.Dispose();
            _stream?.Close();
            _tcpClient?.Close();
            _sendSemaphore.Dispose();
            _cancellationTokenSource?.Dispose();

            // Complete observables
            _receivedBytes.OnCompleted();
            _whenDisconnected.OnCompleted();
            _whenConnected.OnCompleted();

            _receivedBytes.Dispose();
            _whenDisconnected.Dispose();
            _whenConnected.Dispose();
        }

        private sealed class InlineObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            private readonly Action<Exception>? _onError;
            private readonly Action? _onCompleted;

            public InlineObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
            {
                _onNext = onNext;
                _onError = onError;
                _onCompleted = onCompleted;
            }

            public void OnCompleted() => _onCompleted?.Invoke();
            public void OnError(Exception error) => _onError?.Invoke(error);
            public void OnNext(T value) => _onNext(value);
        }
    }
}