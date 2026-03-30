using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BotCommLayer;

/// <summary>
/// High-performance async TCP server using System.IO.Pipelines.
/// Drop-in replacement for ProtobufSocketServer with the same API contract.
///
/// Key differences from ProtobufSocketServer:
///   - async/await instead of ThreadPool thread-per-connection
///   - System.IO.Pipelines for zero-copy framing
///   - Backlog 4096 (was 50) — handles 3000+ pending connections
///   - No dedicated threads — all I/O on the ThreadPool via async
///   - SemaphoreSlim to cap concurrent handler execution
///
/// Wire format (unchanged): [4-byte LE length][compression flag + protobuf payload]
/// </summary>
public class ProtobufPipelineSocketServer<TRequest, TResponse> : IDisposable
    where TRequest : IMessage<TRequest>, new()
    where TResponse : IMessage<TResponse>, new()
{
    private const int Backlog = 4096;
    private const int HeaderSize = 4; // Length prefix
    private const int MaxMessageSize = 16 * 1024 * 1024; // 16MB safety limit

    private readonly Socket _listenSocket;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _concurrencyLimit;
    private readonly Task _acceptTask;
    private bool _disposed;
    private long _totalRequests;
    private long _activeConnections;

    /// <summary>Total requests processed across all connections.</summary>
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    /// <summary>Currently active connections.</summary>
    public long ActiveConnections => Interlocked.Read(ref _activeConnections);

    public ProtobufPipelineSocketServer(string ipAddress, int port, ILogger logger, int maxConcurrency = 4096)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrencyLimit = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        _listenSocket.Listen(Backlog);

        _acceptTask = AcceptLoopAsync(_cts.Token);

        _logger.LogInformation("PipelineServer listening on {Ip}:{Port} (backlog={Backlog}, maxConcurrency={Max})",
            ipAddress, port, Backlog, maxConcurrency);
    }

    /// <summary>
    /// Override this method to provide logic for handling requests.
    /// Same contract as ProtobufSocketServer.HandleRequest.
    /// </summary>
    protected virtual TResponse HandleRequest(TRequest request)
    {
        _logger.LogWarning("Base HandleRequest called — override this method.");
        return new TResponse();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listenSocket.AcceptAsync(ct);
                clientSocket.NoDelay = true;
                // Fire-and-forget — each connection runs independently
                _ = HandleConnectionAsync(clientSocket, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accept error");
            }
        }
    }

    private async Task HandleConnectionAsync(Socket socket, CancellationToken ct)
    {
        Interlocked.Increment(ref _activeConnections);
        var pipe = new Pipe();

        try
        {
            await using var stream = new NetworkStream(socket, ownsSocket: true);

            // Run read and process concurrently
            var readTask = FillPipeAsync(stream, pipe.Writer, ct);
            var processTask = ProcessPipeAsync(stream, pipe.Reader, ct);

            await Task.WhenAll(readTask, processTask);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { } // Connection reset
        catch (Exception ex)
        {
            _logger.LogDebug("Connection error: {Message}", ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
            try { socket.Dispose(); } catch { }
        }
    }

    /// <summary>Reads from the network stream and writes into the pipe.</summary>
    private static async Task FillPipeAsync(NetworkStream stream, PipeWriter writer, CancellationToken ct)
    {
        const int minBufferSize = 4096;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var memory = writer.GetMemory(minBufferSize);
                int bytesRead = await stream.ReadAsync(memory, ct);
                if (bytesRead == 0) break; // EOF

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(ct);
                if (result.IsCompleted) break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallow — connection closed
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    /// <summary>Reads framed messages from the pipe, processes them, writes responses.</summary>
    private async Task ProcessPipeAsync(NetworkStream stream, PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out var messagePayload))
                {
                    await _concurrencyLimit.WaitAsync(ct);
                    try
                    {
                        // Decode, handle, encode, write response
                        var protobufBytes = ProtobufCompression.Decode(messagePayload);
                        var request = new TRequest();
                        request.MergeFrom(protobufBytes);

                        var response = HandleRequest(request);
                        Interlocked.Increment(ref _totalRequests);

                        var encodedResponse = ProtobufCompression.Encode(response.ToByteArray());
                        await stream.WriteAsync(encodedResponse, ct);
                    }
                    finally
                    {
                        _concurrencyLimit.Release();
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Connection closed
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    /// <summary>
    /// Tries to read a complete length-prefixed message from the buffer.
    /// Returns true if a complete message was found, advances the buffer past it.
    /// </summary>
    private static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out byte[] payload)
    {
        payload = [];

        if (buffer.Length < HeaderSize)
            return false;

        // Read 4-byte length prefix
        Span<byte> headerSpan = stackalloc byte[HeaderSize];
        buffer.Slice(0, HeaderSize).CopyTo(headerSpan);
        int length = BinaryPrimitives.ReadInt32LittleEndian(headerSpan);

        if (length <= 0 || length > MaxMessageSize)
        {
            // Invalid length — skip this frame
            buffer = buffer.Slice(HeaderSize);
            return false;
        }

        // Check if full message is available
        if (buffer.Length < HeaderSize + length)
            return false;

        // Extract payload (compression flag + protobuf data)
        payload = new byte[length];
        buffer.Slice(HeaderSize, length).CopyTo(payload);

        // Advance buffer past this message
        buffer = buffer.Slice(HeaderSize + length);
        return true;
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _listenSocket.Close(); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _concurrencyLimit.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
