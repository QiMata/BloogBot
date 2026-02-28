using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.Implementation;

namespace WowSharpClient.NetworkTests
{
    public class ConnectionManagerTests
    {
        [Fact]
        public async Task DisconnectWithTransientError_RetriesWithBackoff()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(50),
                maxDelay: TimeSpan.FromSeconds(1));

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectedEvents = 0;
            var disconnectedEvents = 0;

            connectionManager.Connected += () => connectedEvents++;
            connectionManager.Disconnected += (ex) => disconnectedEvents++;

            // Act - Start connection
            await connectionManager.ConnectAsync();
            var initialConnectedEvents = connectedEvents;

            // Simulate transient connection error that allows reconnection
            var transientError = new InvalidOperationException("Transient network error");
            connection.SimulateConnectionError(transientError, shouldFailReconnections: false);

            // Wait for reconnection attempts
            await Task.Delay(200);

            // Assert - Should have reconnected automatically
            Assert.True(connectedEvents > initialConnectedEvents || connectionManager.IsConnected);
        }

        [Fact]
        public async Task PolicyReturnsNull_StopReconnecting()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromMilliseconds(50), maxAttempts: 1); // Only 1 attempt

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectionEvents = 0;
            var permanentDisconnectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            connectionManager.Connected += () => connectionEvents++;
            connectionManager.Disconnected += (ex) => permanentDisconnectTcs.TrySetResult();

            // Act
            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectionEvents);

            // Simulate connection error that will persist for reconnection attempts
            var error = new InvalidOperationException("Connection failed");
            connection.SimulateConnectionError(error, shouldFailReconnections: true);

            // Wait deterministically for permanent disconnect event (with timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await permanentDisconnectTcs.Task.WaitAsync(cts.Token);

            // Assert - Should have fired permanent disconnect
            Assert.True(permanentDisconnectTcs.Task.IsCompleted);
        }

        [Fact]
        public async Task MaxAttemptsReached_Stops()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 2,
                initialDelay: TimeSpan.FromMilliseconds(20),
                maxDelay: TimeSpan.FromMilliseconds(50));

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectionEvents = 0;
            var finalDisconnectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            connectionManager.Connected += () => connectionEvents++;
            connectionManager.Disconnected += (ex) => finalDisconnectTcs.TrySetResult();

            // Act
            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectionEvents);

            // Simulate connection failure that will persist for reconnection attempts
            var error = new InvalidOperationException("Connection failed");
            connection.SimulateConnectionError(error, shouldFailReconnections: true);

            // Wait deterministically for final disconnect (with timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await finalDisconnectTcs.Task.WaitAsync(cts.Token);

            // Assert - After max attempts, should fire final disconnect event
            Assert.True(finalDisconnectTcs.Task.IsCompleted);
        }

        [Fact]
        public async Task GracefulDisconnect_DoesNotTriggerReconnection()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(1));

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectionEvents = 0;
            var disconnectionEvents = 0;

            connectionManager.Connected += () => connectionEvents++;
            connectionManager.Disconnected += (ex) => disconnectionEvents++;

            // Act
            await connectionManager.ConnectAsync();
            var initialConnectionEvents = connectionEvents;

            // Graceful disconnect (no exception)
            await connectionManager.DisconnectAsync();

            await Task.Delay(500); // Wait to see if any reconnection attempts happen

            // Assert
            // Should not have additional connection events after graceful disconnect
            Assert.Equal(initialConnectionEvents, connectionEvents);
        }

        [Fact]
        public async Task ConcurrentConnections_HandledSafely()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromMilliseconds(100), maxAttempts: 2);

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            // Act - Try to connect multiple times concurrently
            var tasks = new[]
            {
                connectionManager.ConnectAsync(),
                connectionManager.ConnectAsync(),
                connectionManager.ConnectAsync()
            };

            await Task.WhenAll(tasks);

            // Assert - Should not throw exceptions and should be connected
            Assert.True(connectionManager.IsConnected);
        }

        [Fact]
        public async Task Dispose_StopsReconnectionAttempts()
        {
            // Arrange
            var connection = new InMemoryConnection();
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 10,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(1));

            var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            await connectionManager.ConnectAsync();

            // Simulate error to start reconnection attempts
            var error = new InvalidOperationException("Connection failed");
            connection.SimulateConnectionError(error);

            // Act - Dispose while reconnection might be in progress
            connectionManager.Dispose();

            // Wait a bit to ensure no reconnection attempts happen after disposal
            await Task.Delay(300);

            // Assert - Connection manager should be properly disposed
            Assert.False(connectionManager.IsConnected);
        }

        [Fact]
        public void ExponentialBackoffPolicy_CalculatesCorrectDelays()
        {
            // Arrange
            var policy = new ExponentialBackoffPolicy(
                maxAttempts: 4,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromSeconds(10),
                backoffMultiplier: 2.0);

            // Act & Assert
            var delay1 = policy.GetDelay(1, null);
            var delay2 = policy.GetDelay(2, null);
            var delay3 = policy.GetDelay(3, null);
            var delay4 = policy.GetDelay(4, null);
            var delay5 = policy.GetDelay(5, null); // Should exceed max attempts

            Assert.NotNull(delay1);
            Assert.NotNull(delay2);
            Assert.NotNull(delay3);
            Assert.NotNull(delay4);
            Assert.Null(delay5); // Exceeds max attempts

            // Verify exponential growth
            Assert.True(delay2 > delay1);
            Assert.True(delay3 > delay2);
            Assert.True(delay4 > delay3);

            // Verify respects max delay
            Assert.True(delay4 <= TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void FixedDelayPolicy_ReturnsConstantDelay()
        {
            // Arrange
            var fixedDelay = TimeSpan.FromSeconds(2);
            var policy = new FixedDelayPolicy(fixedDelay, maxAttempts: 3);

            // Act & Assert
            var delay1 = policy.GetDelay(1, null);
            var delay2 = policy.GetDelay(2, null);
            var delay3 = policy.GetDelay(3, null);
            var delay4 = policy.GetDelay(4, null); // Should exceed max attempts

            Assert.Equal(fixedDelay, delay1);
            Assert.Equal(fixedDelay, delay2);
            Assert.Equal(fixedDelay, delay3);
            Assert.Null(delay4); // Exceeds max attempts
        }

        [Fact]
        public async Task ConnectionEvents_ForwardedCorrectly()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromMilliseconds(100), maxAttempts: 1);

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectedCalled = false;
            var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            connectionManager.Connected += () => connectedCalled = true;
            connectionManager.Disconnected += (ex) => disconnectedTcs.TrySetResult();

            // Act
            await connectionManager.ConnectAsync();
            Assert.True(connectedCalled);

            // Simulate error that will eventually lead to permanent disconnect
            // due to policy max attempts being reached
            var testException = new InvalidOperationException("Test error");
            connection.SimulateConnectionError(testException, shouldFailReconnections: true);

            // Wait deterministically for disconnect (with timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await disconnectedTcs.Task.WaitAsync(cts.Token);

            // Assert
            Assert.True(connectedCalled);
            Assert.True(disconnectedTcs.Task.IsCompleted);
        }

        // --- WSCN-TST-003: Deterministic reconnect cancellation/dispose tests ---

        [Fact]
        public async Task DisconnectAsync_CancelsActiveReconnectLoop()
        {
            // Arrange - Use a long backoff so the reconnect loop is in the delay
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(30), maxAttempts: 10);

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectedCount = 0;
            connectionManager.Connected += () => Interlocked.Increment(ref connectedCount);

            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectedCount);

            // Trigger reconnect loop (30-second delay per attempt)
            connection.SimulateConnectionError(new InvalidOperationException("error"), shouldFailReconnections: false);

            // Act - Immediately cancel via graceful disconnect
            await connectionManager.DisconnectAsync();

            // Wait briefly - if cancel didn't work, we'd see another Connected event
            await Task.Delay(200);

            // Assert - No additional connections happened (reconnect was cancelled)
            Assert.Equal(1, connectedCount);
        }

        [Fact]
        public async Task Dispose_DuringBackoffDelay_StopsImmediately()
        {
            // Arrange - Long backoff delay to ensure we're mid-backoff when disposing
            var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(30), maxAttempts: 100);

            var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            var connectedCount = 0;
            connectionManager.Connected += () => Interlocked.Increment(ref connectedCount);

            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectedCount);

            // Start reconnect loop
            connection.SimulateConnectionError(new InvalidOperationException("error"));

            // Act - Dispose while in the 30-second backoff
            connectionManager.Dispose();

            // Wait to verify no reconnection happens
            await Task.Delay(200);

            // Assert
            Assert.Equal(1, connectedCount);
            Assert.False(connectionManager.IsConnected);
        }

        [Fact]
        public async Task GracefulDisconnect_FiresDisconnectedWithNull()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromMilliseconds(50), maxAttempts: 3);

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            Exception? receivedEx = new InvalidOperationException("sentinel");
            var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connectionManager.Disconnected += (ex) =>
            {
                receivedEx = ex;
                disconnectedTcs.TrySetResult();
            };

            await connectionManager.ConnectAsync();

            // Act - Graceful disconnect (null exception)
            await connectionManager.DisconnectAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await disconnectedTcs.Task.WaitAsync(cts.Token);

            // Assert - Graceful disconnect passes null, no reconnect attempts
            Assert.Null(receivedEx);
        }

        [Fact]
        public async Task ErrorDisconnect_ExhaustsPolicy_ThenFiresDisconnectedWithException()
        {
            // Arrange
            using var connection = new InMemoryConnection();
            var policy = new FixedDelayPolicy(TimeSpan.FromMilliseconds(10), maxAttempts: 2);

            using var connectionManager = new ConnectionManager(connection, policy, "127.0.0.1", 8085);

            Exception? receivedEx = null;
            var disconnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connectionManager.Disconnected += (ex) =>
            {
                receivedEx = ex;
                disconnectedTcs.TrySetResult();
            };

            await connectionManager.ConnectAsync();

            // Act - Error that persists across all reconnect attempts
            var testError = new InvalidOperationException("persistent failure");
            connection.SimulateConnectionError(testError, shouldFailReconnections: true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await disconnectedTcs.Task.WaitAsync(cts.Token);

            // Assert - Permanent disconnect fires with the last exception
            Assert.NotNull(receivedEx);
        }
    }
}