using Communication;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;

namespace BotCommLayer
{
    public class ProtobufAsyncSocketServer<T> : IDisposable where T : IMessage<T>
    {
        private bool _disposed;
        private readonly TcpListener _server;
        private bool _isRunning;
        private readonly ILogger _logger;
        /// <summary>
        /// Maps request ID → the TcpClient that sent it.
        /// Used to route the async response back to the correct connection.
        /// ConcurrentDictionary because handler threads and the Rx callback
        /// (SendMessageToClient) access this concurrently.
        /// </summary>
        private readonly ConcurrentDictionary<ulong, TcpClient> _clients;
        protected Subject<AsyncRequest> _instanceObservable;

        public ProtobufAsyncSocketServer(string ipAddress, int port, ILogger logger)
        {
            _logger = logger;
            _server = new TcpListener(IPAddress.Parse(ipAddress), port);
            _server.Start();

            _isRunning = true;

            _instanceObservable = new();
            _clients = new();

            Thread serverThread = new(Run) { IsBackground = true };
            serverThread.Start();
        }

        private void Run()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _server.AcceptTcpClient();

                    // Enable TCP keepalive so the OS detects dead connections
                    // instead of blocking forever on Read().
                    client.Client.SetSocketOption(
                        SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    Thread clientThread = new(() => HandleClient(client)) { IsBackground = true };
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        _logger.LogError($"Error: {ex.Message}");
                }
            }
        }

        public void SendMessageToClient(ulong id, T ouboundMessage)
        {
            if (_clients.TryGetValue(id, out TcpClient? client))
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    // Encode with compression (flag + optional GZip)
                    byte[] encoded = ProtobufCompression.Encode(ouboundMessage.ToByteArray());
                    stream.Write(encoded, 0, encoded.Length);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        _logger.LogWarning($"Failed to send response for request {id}: {ex.Message}");
                    // Remove stale mapping on write failure
                    _clients.TryRemove(id, out _);
                }
            }
            else
            {
                _logger.LogWarning($"No client found for request id {id} — response dropped.");
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            while (_isRunning)
            {
                try
                {
                    // Read the length of the incoming message (must read all 4 bytes)
                    byte[] lengthBuffer = new byte[4];
                    ReadExact(stream, lengthBuffer, 4);

                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    if (length <= 0 || length > 16 * 1024 * 1024) // sanity: max 16 MB
                    {
                        _logger.LogError($"Invalid message length: {length}. Dropping connection.");
                        break;
                    }

                    // Read wire payload (flag + protobuf data)
                    byte[] wirePayload = new byte[length];
                    ReadExact(stream, wirePayload, length);

                    // Decode (decompress if needed) and deserialize
                    byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);
                    AsyncRequest request = new();
                    request.MergeFrom(protobufBytes);

                    // Map this request's ID to the connection so SendMessageToClient
                    // can route the response back. Overwrites are fine — same client.
                    _clients[request.Id] = client;

                    // Process
                    _instanceObservable.OnNext(request);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        _logger.LogError($"Client Error: {ex.Message}");
                    break;
                }
            }

            // Clean up all mappings that point to this connection
            foreach (var kvp in _clients)
            {
                if (ReferenceEquals(kvp.Value, client))
                    _clients.TryRemove(kvp.Key, out _);
            }

            try { client.Close(); }
            catch { /* already closed */ }
        }

        /// <summary>
        /// Read exactly <paramref name="count"/> bytes from the stream, looping
        /// until all bytes are received. Throws <see cref="IOException"/> on
        /// premature connection close.
        /// </summary>
        private static void ReadExact(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Connection closed while reading.");
                totalRead += bytesRead;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try { _server.Stop(); }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping TCP listener: {ex.Message}");
            }

            // Close all tracked client connections
            foreach (var kvp in _clients)
            {
                try { kvp.Value.Close(); }
                catch { /* already closed */ }
            }
            _clients.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _instanceObservable?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}