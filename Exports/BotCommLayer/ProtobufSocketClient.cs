using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BotCommLayer
{
    public class ProtobufSocketClient<TRequest, TResponse>
        where TRequest : IMessage<TRequest>, new()
        where TResponse : IMessage<TResponse>, new()
    {
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

        private void Connect()
        {
            _client?.Dispose();
            _client = new TcpClient();
            ConnectWithRetry(_ipAddress!, _port);
            _stream = _client.GetStream();
            _stream.ReadTimeout = 5000;
            _stream.WriteTimeout = 5000;
        }

        private void ConnectWithRetry(string ipAddress, int port)
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
        }

        public TResponse SendMessage(TRequest request)
        {
            if (_stream == null && _ipAddress == null)
            {
                throw new InvalidOperationException("Client is not connected. Cannot send message.");
            }

            lock (_lock)
            {
                try
                {
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
                            Connect();
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
            }
        }

        private TResponse SendMessageInternal(TRequest request)
        {
            if (_stream == null)
                throw new InvalidOperationException("No active stream.");

            byte[] messageBytes = request.ToByteArray();
            byte[] length = BitConverter.GetBytes(messageBytes.Length);

            _stream.Write(length);
            _stream.Write(messageBytes);

            byte[] lengthBuffer = new byte[4];
            ReadExact(_stream, lengthBuffer);
            int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

            byte[] buffer = new byte[responseLength];
            ReadExact(_stream, buffer);

            TResponse response = new();
            response.MergeFrom(buffer);

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

        public void Close()
        {
            _stream?.Close();
            _client?.Close();
        }
    }
}
