using BotCommLayer;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Pathfinding;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PathfindingService.Tests;

public sealed class ProtobufSocketServerLoggingTests
{
    [Fact]
    public async Task ClientCloseAfterCompleteRequest_DoesNotLogUnexpectedEofWarning()
    {
        var logger = new CapturingLogger();
        var port = GetFreePort();
        using var server = new TestSocketServer(port, logger);

        await SendCompleteRequestAndCloseAsync(port);
        await Task.Delay(150);

        Assert.DoesNotContain(
            logger.Entries,
            static entry => entry.Level >= LogLevel.Warning &&
                entry.Message.Contains("Unexpected EOF", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClientCloseDuringPayload_StillLogsUnexpectedEofWarning()
    {
        var logger = new CapturingLogger();
        var port = GetFreePort();
        using var server = new TestSocketServer(port, logger);

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, port);
            await client.GetStream().WriteAsync(BitConverter.GetBytes(64));
        }

        Assert.True(
            SpinWaitUntil(
                () => logger.Entries.Any(static entry =>
                    entry.Level >= LogLevel.Warning &&
                    entry.Message.Contains("Unexpected EOF", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(2)),
            "Expected a truncated payload to remain a warning.");
    }

    private static async Task SendCompleteRequestAndCloseAsync(int port)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();

        var request = new PathfindingRequest { Path = new CalculatePathRequest { MapId = 1 } };
        var wire = ProtobufCompression.Encode(request.ToByteArray());
        await stream.WriteAsync(wire);

        var lengthBytes = await ReadExactAsync(stream, 4);
        var responseLength = BitConverter.ToInt32(lengthBytes, 0);
        _ = await ReadExactAsync(stream, responseLength);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset));
            if (read == 0)
                throw new IOException("Unexpected EOF while reading response.");

            offset += read;
        }

        return buffer;
    }

    private static bool SpinWaitUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return true;

            Thread.Sleep(25);
        }

        return predicate();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class TestSocketServer(int port, ILogger logger)
        : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>("127.0.0.1", port, logger)
    {
        protected override PathfindingResponse HandleRequest(PathfindingRequest request)
            => new() { Error = new Error { Message = "ok" } };
    }

    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
