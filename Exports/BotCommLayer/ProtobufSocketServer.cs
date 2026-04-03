using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BotCommLayer
{
    public class ProtobufSocketServer<TRequest, TResponse> : IDisposable
        where TRequest : IMessage<TRequest>, new()
        where TResponse : IMessage<TResponse>, new()
    {
        private bool _disposed;
        private readonly TcpListener _server;
        private bool _isRunning;
        protected readonly ILogger _logger;
        private long _clientHandlerThreadSequence;

        public ProtobufSocketServer(string ipAddress, int port, ILogger logger)
        {
            _logger = logger;
            _server = new TcpListener(IPAddress.Parse(ipAddress), port);
            _server.Start(256); // AV-scale reconnect bursts can exceed 80 concurrent clients.
            _isRunning = true;

            Thread serverThread = new(Run)
            {
                IsBackground = true
            };
            serverThread.Start();
        }

        private void Run()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _server.AcceptTcpClient();
                    StartClientHandler(client);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Server error: {ex}");
                }
            }
        }

        private void StartClientHandler(TcpClient client)
        {
            var threadId = Interlocked.Increment(ref _clientHandlerThreadSequence);
            var clientThread = new Thread(() => HandleClient(client))
            {
                IsBackground = true,
                Name = $"{GetType().Name}-Client-{threadId}"
            };
            clientThread.Start();
        }

        /// <summary>
        /// Override this method to provide logic for handling requests.
        /// Remember: if your message uses wrapper types, you must check for null.
        /// </summary>
        protected virtual TResponse HandleRequest(TRequest request)
        {
            _logger.LogWarning("Base HandleRequest called — override this method.");
            return new TResponse();
        }

        private void HandleClient(TcpClient client)
        {
            using NetworkStream stream = client.GetStream();
            try
            {
                while (_isRunning && client.Connected)
                {
                    // Read incoming message length (includes compression flag)
                    byte[] lengthBuffer = new byte[4];
                    ReadExact(stream, lengthBuffer);
                    int length = BitConverter.ToInt32(lengthBuffer, 0);

                    // Read wire payload (flag + protobuf data)
                    byte[] wirePayload = new byte[length];
                    ReadExact(stream, wirePayload);

                    // Decode (decompress if needed) and deserialize
                    byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);
                    TRequest request = new();
                    request.MergeFrom(protobufBytes);

                    // Handle request -> get response
                    TResponse response = HandleRequest(request);

                    // Encode response (compress if above threshold)
                    byte[] encodedResponse = ProtobufCompression.Encode(response.ToByteArray());
                    stream.Write(encodedResponse, 0, encodedResponse.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Client connection closed or error occurred: {ex.Message}");
            }
        }

        private static void ReadExact(NetworkStream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Unexpected EOF");
                totalRead += bytesRead;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try { _server.Stop(); }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error stopping TCP listener: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
