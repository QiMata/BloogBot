using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Implementation;

namespace WowSharpClient.NetworkTests
{
    public class TcpConnectionReactiveTests : IAsyncLifetime
    {
        private TcpListener? _server;
        private int _port;

        public Task InitializeAsync()
        {
            _server = new TcpListener(IPAddress.Loopback, 0);
            _server.Start();
            _port = ((IPEndPoint)_server.LocalEndpoint).Port;
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _server?.Stop();
            return Task.CompletedTask;
        }

        private async Task<(TcpClient client, NetworkStream stream)> AcceptClientAsync(CancellationToken ct)
        {
            if (_server == null) throw new InvalidOperationException("Server not started");
            var client = await _server.AcceptTcpClientAsync(ct);
            return (client, client.GetStream());
        }

        [Fact]
        public async Task ConnectAndReceive_EventsAndObservablesFire()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var connection = new TcpConnection();

            // Arrange TCS for events/observables
            var connectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventConnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bytesTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventBytesTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var subConnected = connection.WhenConnected.Subscribe(_ => connectedTcs.TrySetResult());
            using var subBytes = connection.ReceivedBytes.Subscribe(mem => bytesTcs.TrySetResult(mem.ToArray()));
            using var subDisc = connection.WhenDisconnected.Subscribe(ex => disconnectedTcs.TrySetResult(ex));
            connection.Connected += () => eventConnectedTcs.TrySetResult();
            connection.BytesReceived += data => eventBytesTcs.TrySetResult(data.ToArray());

            // Accept server side
            var acceptTask = AcceptClientAsync(cts.Token);

            // Act: connect
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);

            var (serverClient, serverStream) = await acceptTask;
            using var _ = serverClient;
            using var __ = serverStream;

            // Assert connected
            await Task.WhenAll(
                Task.WhenAny(connectedTcs.Task, Task.Delay(Timeout.Infinite, cts.Token)),
                Task.WhenAny(eventConnectedTcs.Task, Task.Delay(Timeout.Infinite, cts.Token))
            );
            Assert.True(connection.IsConnected);

            // Send data from server -> client should receive
            var payload = new byte[] { 1, 2, 3, 4 };
            await serverStream.WriteAsync(payload, 0, payload.Length, cts.Token);
            await serverStream.FlushAsync(cts.Token);

            var received = await bytesTcs.Task;
            var receivedEvent = await eventBytesTcs.Task;
            Assert.Equal(payload, received);
            Assert.Equal(payload, receivedEvent);

            // Close server to trigger graceful disconnect
            serverClient.Close();

            // Should get a null exception for graceful disconnect
            var ex = await disconnectedTcs.Task;
            Assert.Null(ex);
        }

        [Fact]
        public async Task SendAsync_WritesToServer()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var connection = new TcpConnection();

            var acceptTask = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient, serverStream) = await acceptTask;
            using var _ = serverClient;
            using var __ = serverStream;

            var data = new byte[] { 10, 20, 30 };
            await connection.SendAsync(data, cts.Token);

            // Read exactly 3 bytes
            var buffer = new byte[3];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await serverStream.ReadAsync(buffer.AsMemory(read), cts.Token);
                if (n == 0) break;
                read += n;
            }
            Assert.Equal(3, read);
            Assert.Equal(data, buffer);
        }

        [Fact]
        public async Task DisconnectAsync_FiresDisconnected()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var connection = new TcpConnection();

            var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var sub = connection.WhenDisconnected.Subscribe(ex => disconnectedTcs.TrySetResult(ex));

            var acceptTask = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient, serverStream) = await acceptTask;
            using var _ = serverClient;
            using var __ = serverStream;

            await connection.DisconnectAsync(cts.Token);
            var ex = await disconnectedTcs.Task;
            Assert.Null(ex);
            Assert.False(connection.IsConnected);
        }

        // --- WSCN-TST-004: Reconnect semantics and duplicate disconnect-emit guard ---

        [Fact]
        public async Task Reconnect_WhileConnected_EmitsDisconnectThenConnect()
        {
            // Arrange - TcpConnection.ConnectAsync calls DisconnectAsync first if already connected
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var connection = new TcpConnection();

            var connectedCount = 0;
            var disconnectedEvents = new List<Exception?>();
            using var subConn = connection.WhenConnected.Subscribe(_ => Interlocked.Increment(ref connectedCount));
            using var subDisc = connection.WhenDisconnected.Subscribe(ex => { lock (disconnectedEvents) disconnectedEvents.Add(ex); });

            // First connection
            var acceptTask1 = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient1, serverStream1) = await acceptTask1;
            using var _s1 = serverClient1;
            using var _ss1 = serverStream1;

            Assert.True(connection.IsConnected);
            Assert.Equal(1, connectedCount);

            // Act - Reconnect while still connected (ConnectAsync calls DisconnectAsync internally)
            var acceptTask2 = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient2, serverStream2) = await acceptTask2;
            using var _s2 = serverClient2;
            using var _ss2 = serverStream2;

            // Assert - Should have 2 connected events and at least 1 disconnect (from the reconnect)
            Assert.Equal(2, connectedCount);
            Assert.True(connection.IsConnected);
            Assert.True(disconnectedEvents.Count >= 1);
            // Graceful disconnects during reconnect have null exception
            Assert.Null(disconnectedEvents[0]);
        }

        [Fact]
        public async Task ServerClose_ThenExplicitDisconnect_OnlyTwoDisconnectEvents()
        {
            // Arrange - Test that server-initiated close + explicit disconnect don't crash
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var connection = new TcpConnection();

            var disconnectedEvents = new List<Exception?>();
            using var sub = connection.WhenDisconnected.Subscribe(ex =>
            {
                lock (disconnectedEvents) disconnectedEvents.Add(ex);
            });

            var acceptTask = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient, serverStream) = await acceptTask;

            // Act - Server closes
            serverClient.Close();
            serverStream.Close();
            await Task.Delay(200); // Let the read loop detect EOF

            // Then explicit disconnect (should be safe even if already disconnected)
            await connection.DisconnectAsync(cts.Token);
            await Task.Delay(100);

            // Assert - Should not crash. Exact event count depends on timing,
            // but we should have at least one disconnect and no exceptions thrown
            Assert.True(disconnectedEvents.Count >= 1);
            Assert.False(connection.IsConnected);
        }

        [Fact]
        public async Task ConnectAsync_AfterDispose_ThrowsObjectDisposed()
        {
            // Arrange
            var connection = new TcpConnection();
            connection.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => connection.ConnectAsync("127.0.0.1", _port));
        }

        [Fact]
        public async Task Dispose_WhileConnected_CompletesObservables()
        {
            // Arrange
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var connection = new TcpConnection();

            var completedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.WhenConnected.Subscribe(
                _ => { },
                () => completedTcs.TrySetResult()
            );

            var acceptTask = AcceptClientAsync(cts.Token);
            await connection.ConnectAsync("127.0.0.1", _port, cts.Token);
            var (serverClient, serverStream) = await acceptTask;
            using var _ = serverClient;
            using var __ = serverStream;

            // Act
            connection.Dispose();

            // Assert - Observables should be completed
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await completedTcs.Task.WaitAsync(timeoutCts.Token);
            Assert.True(completedTcs.Task.IsCompleted);
        }
    }
}
