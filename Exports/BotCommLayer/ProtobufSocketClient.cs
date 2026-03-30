using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BotCommLayer
{
    public class ProtobufSocketClient<TRequest, TResponse> : IDisposable
        where TRequest : IMessage<TRequest>, new()
        where TResponse : IMessage<TResponse>, new()
    {
        private bool _disposed;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly ILogger? _logger;
        private readonly object _lock = new();

        // Store connection parameters for reconnection
        private readonly string? _ipAddress;
        private readonly int _port;

        public ProtobufSocketClient() { }

        public ProtobufSocketClient(string ipAddress, int port, ILogger logger)
        {
            _logger = logger;
            _ipAddress = ipAddress;
            _port = port;

            Connect();
        }

        private void Connect(int? reconnectBudgetMs = null)
        {
            _client?.Dispose();
            _client = new TcpClient();
            ConnectWithRetry(_ipAddress!, _port, reconnectBudgetMs);
            _stream = _client.GetStream();
            _stream.ReadTimeout = 5000;
            _stream.WriteTimeout = 5000;
        }

        private void ConnectWithRetry(string ipAddress, int port, int? reconnectBudgetMs = null)
        {
            if (!reconnectBudgetMs.HasValue)
            {
                const int maxRetries = 10;
                const int baseDelayMs = 500;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger?.LogInformation($"Attempting to connect to {ipAddress}:{port} (attempt {attempt}/{maxRetries})...");
                        _client?.Connect(IPAddress.Parse(ipAddress), port);
                        _logger?.LogInformation($"Successfully connected to {ipAddress}:{port}");
                        return;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        if (attempt == maxRetries)
                        {
                            _logger?.LogError($"Failed to connect to {ipAddress}:{port} after {maxRetries} attempts. Service may not be running.");
                            throw new InvalidOperationException(
                                $"Unable to connect to service at {ipAddress}:{port}. " +
                                $"Please ensure the service is running and accessible. " +
                                $"Last error: {ex.Message}", ex);
                        }

                        int delay = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                        _logger?.LogWarning($"Connection attempt {attempt} failed: {ex.Message}. Retrying in {delay}ms...");
                        Thread.Sleep(delay);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Unexpected error connecting to {ipAddress}:{port}: {ex.Message}");
                        throw;
                    }
                }

                return;
            }

            const int reconnectBaseDelayMs = 100;
            const int reconnectMaxDelayMs = 1000;
            var attemptCount = 0;
            var stopwatch = Stopwatch.StartNew();
            Exception? lastConnectionException = null;

            while (true)
            {
                attemptCount++;
                try
                {
                    _logger?.LogInformation(
                        "Attempting reconnect to {IpAddress}:{Port} (attempt {Attempt}, budget {Budget}ms)...",
                        ipAddress,
                        port,
                        attemptCount,
                        reconnectBudgetMs.Value);
                    _client?.Connect(IPAddress.Parse(ipAddress), port);
                    _logger?.LogInformation($"Successfully connected to {ipAddress}:{port}");
                    return;
                }
                catch (SocketException ex)
                {
                    lastConnectionException = ex;
                }
                catch (Exception ex)
                {
                    lastConnectionException = ex;
                }

                var remainingBudgetMs = reconnectBudgetMs.Value - (int)stopwatch.ElapsedMilliseconds;
                if (remainingBudgetMs <= 0)
                    break;

                var delay = Math.Min(reconnectBaseDelayMs * (1 << Math.Min(attemptCount - 1, 3)), reconnectMaxDelayMs);
                delay = Math.Min(delay, remainingBudgetMs);
                _logger?.LogWarning(
                    "Reconnect attempt {Attempt} to {IpAddress}:{Port} failed: {Message}. Retrying in {Delay}ms (remaining budget {Remaining}ms)...",
                    attemptCount,
                    ipAddress,
                    port,
                    lastConnectionException?.Message,
                    delay,
                    remainingBudgetMs);
                if (delay > 0)
                    Thread.Sleep(delay);
            }

            if (lastConnectionException != null)
            {
                throw new TimeoutException(
                    $"Reconnect to {ipAddress}:{port} exceeded {reconnectBudgetMs.Value}ms budget after {attemptCount} attempt(s).",
                    lastConnectionException);
            }

            throw new TimeoutException(
                $"Reconnect to {ipAddress}:{port} exceeded {reconnectBudgetMs.Value}ms budget after {attemptCount} attempt(s).");
        }

        public TResponse SendMessage(TRequest request)
        {
            return SendMessage(request, readTimeoutOverrideMs: null, writeTimeoutOverrideMs: null);
        }

        protected TResponse SendMessage(TRequest request, int readTimeoutMs, int writeTimeoutMs)
        {
            return SendMessage(request, (int?)readTimeoutMs, (int?)writeTimeoutMs);
        }

        private TResponse SendMessage(TRequest request, int? readTimeoutOverrideMs, int? writeTimeoutOverrideMs)
        {
            if (_stream == null && _ipAddress == null)
            {
                throw new InvalidOperationException("Client is not connected. Cannot send message.");
            }

            lock (_lock)
            {
                var previousReadTimeout = _stream?.ReadTimeout;
                var previousWriteTimeout = _stream?.WriteTimeout;
                try
                {
                    ApplyTimeoutOverrides(readTimeoutOverrideMs, writeTimeoutOverrideMs);
                    return SendMessageInternal(request);
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                    // Connection lost — attempt one reconnect
                    if (_ipAddress != null)
                    {
                        _logger?.LogWarning($"Connection lost ({ex.GetType().Name}). Attempting reconnect to {_ipAddress}:{_port}...");
                        try
                        {
                            Connect(ComputeReconnectBudgetMs(
                                readTimeoutOverrideMs,
                                writeTimeoutOverrideMs,
                                previousReadTimeout,
                                previousWriteTimeout));
                            ApplyTimeoutOverrides(readTimeoutOverrideMs, writeTimeoutOverrideMs);
                            _logger?.LogInformation($"Reconnected to {_ipAddress}:{_port}. Retrying message.");
                            return SendMessageInternal(request);
                        }
                        catch (Exception reconnectEx)
                        {
                            _logger?.LogError($"Reconnect failed: {reconnectEx.Message}");
                            throw;
                        }
                    }

                    _logger?.LogError($"Error sending message: {ex}");
                    throw;
                }
                finally
                {
                    RestoreTimeouts(previousReadTimeout, previousWriteTimeout);
                }
            }
        }

        private static int ComputeReconnectBudgetMs(
            int? readTimeoutOverrideMs,
            int? writeTimeoutOverrideMs,
            int? previousReadTimeout,
            int? previousWriteTimeout)
        {
            int? budget = null;

            void Consider(int? timeoutMs)
            {
                if (!timeoutMs.HasValue || timeoutMs.Value <= 0)
                    return;

                budget = !budget.HasValue
                    ? timeoutMs.Value
                    : Math.Min(budget.Value, timeoutMs.Value);
            }

            Consider(readTimeoutOverrideMs);
            Consider(writeTimeoutOverrideMs);
            Consider(previousReadTimeout);
            Consider(previousWriteTimeout);

            return budget.GetValueOrDefault(5000);
        }

        private void ApplyTimeoutOverrides(int? readTimeoutOverrideMs, int? writeTimeoutOverrideMs)
        {
            if (_stream == null)
                return;

            if (readTimeoutOverrideMs.HasValue)
                _stream.ReadTimeout = readTimeoutOverrideMs.Value;

            if (writeTimeoutOverrideMs.HasValue)
                _stream.WriteTimeout = writeTimeoutOverrideMs.Value;
        }

        private void RestoreTimeouts(int? previousReadTimeout, int? previousWriteTimeout)
        {
            if (_stream == null)
                return;

            if (previousReadTimeout.HasValue)
                _stream.ReadTimeout = previousReadTimeout.Value;

            if (previousWriteTimeout.HasValue)
                _stream.WriteTimeout = previousWriteTimeout.Value;
        }

        private TResponse SendMessageInternal(TRequest request)
        {
            if (_stream == null)
                throw new InvalidOperationException("No active stream.");

            // Encode with compression (flag + optional GZip)
            byte[] encoded = ProtobufCompression.Encode(request.ToByteArray());
            _stream.Write(encoded, 0, encoded.Length);

            // Read response length (includes compression flag)
            byte[] lengthBuffer = new byte[4];
            ReadExact(_stream, lengthBuffer);
            int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Read wire payload and decode (decompress if needed)
            byte[] wirePayload = new byte[responseLength];
            ReadExact(_stream, wirePayload);
            byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);

            TResponse response = new();
            response.MergeFrom(protobufBytes);

            return response;
        }

        private static void ReadExact(NetworkStream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Unexpected EOF while reading stream.");
                totalRead += bytesRead;
            }
        }

        // =====================================================================
        // ASYNC API — use these for high-concurrency scenarios (3000+ bots)
        // =====================================================================

        private readonly SemaphoreSlim _asyncLock = new(1, 1);

        /// <summary>
        /// Async send/receive. Does not block ThreadPool threads during I/O.
        /// Use instead of SendMessage for high-concurrency BotRunner tick loops.
        /// </summary>
        public async Task<TResponse> SendMessageAsync(TRequest request, CancellationToken ct = default)
        {
            if (_stream == null && _ipAddress == null)
                throw new InvalidOperationException("Client is not connected.");

            await _asyncLock.WaitAsync(ct);
            try
            {
                return await SendMessageInternalAsync(request, ct);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                if (_ipAddress != null)
                {
                    _logger?.LogWarning("Connection lost ({Type}). Reconnecting to {Ip}:{Port}...",
                        ex.GetType().Name, _ipAddress, _port);
                    try
                    {
                        await ConnectAsync(ct);
                        _logger?.LogInformation("Reconnected to {Ip}:{Port}. Retrying.", _ipAddress, _port);
                        return await SendMessageInternalAsync(request, ct);
                    }
                    catch (Exception reconnectEx)
                    {
                        _logger?.LogError("Reconnect failed: {Message}", reconnectEx.Message);
                        throw;
                    }
                }
                throw;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        private async Task<TResponse> SendMessageInternalAsync(TRequest request, CancellationToken ct)
        {
            if (_stream == null)
                throw new InvalidOperationException("No active stream.");

            // Encode
            byte[] encoded = ProtobufCompression.Encode(request.ToByteArray());
            await _stream.WriteAsync(encoded, ct);

            // Read response length (4 bytes)
            byte[] lengthBuffer = new byte[4];
            await ReadExactAsync(_stream, lengthBuffer, ct);
            int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Read response payload
            byte[] wirePayload = new byte[responseLength];
            await ReadExactAsync(_stream, wirePayload, ct);
            byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);

            TResponse response = new();
            response.MergeFrom(protobufBytes);
            return response;
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
                if (bytesRead == 0)
                    throw new IOException("Unexpected EOF while reading stream.");
                totalRead += bytesRead;
            }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            _client?.Dispose();
            _client = new TcpClient();

            const int maxRetries = 5;
            const int baseDelayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _client.ConnectAsync(IPAddress.Parse(_ipAddress!), _port, ct);
                    _stream = _client.GetStream();
                    _stream.ReadTimeout = 5000;
                    _stream.WriteTimeout = 5000;
                    return;
                }
                catch (SocketException) when (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (1 << (attempt - 1)), ct);
                }
            }

            throw new InvalidOperationException($"Failed to connect to {_ipAddress}:{_port} after {maxRetries} attempts.");
        }

        public void Close()
        {
            _stream?.Close();
            _client?.Close();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Close();
            _stream?.Dispose();
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
