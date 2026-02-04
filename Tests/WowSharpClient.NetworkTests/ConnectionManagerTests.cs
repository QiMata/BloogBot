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
            var permanentDisconnectCalled = false;

            connectionManager.Connected += () => connectionEvents++;
            connectionManager.Disconnected += (ex) => permanentDisconnectCalled = true;

            // Act
            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectionEvents);
            
            // Simulate connection error that will persist for reconnection attempts
            var error = new InvalidOperationException("Connection failed");
            connection.SimulateConnectionError(error, shouldFailReconnections: true);

            // Wait for policy to exhaust retries
            await Task.Delay(500);

            // Assert - Should eventually stop reconnecting and fire permanent disconnect
            Assert.True(permanentDisconnectCalled);
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
            var finalDisconnectCalled = false;

            connectionManager.Connected += () => connectionEvents++;
            connectionManager.Disconnected += (ex) => finalDisconnectCalled = true;

            // Act
            await connectionManager.ConnectAsync();
            Assert.Equal(1, connectionEvents);
            
            // Simulate connection failure that will persist for reconnection attempts
            var error = new InvalidOperationException("Connection failed");
            connection.SimulateConnectionError(error, shouldFailReconnections: true);
            
            // Wait for all reconnection attempts to complete
            await Task.Delay(500);

            // Assert - After max attempts, should fire final disconnect event
            Assert.True(finalDisconnectCalled);
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
            var disconnectedCalled = false;

            connectionManager.Connected += () => connectedCalled = true;
            connectionManager.Disconnected += (ex) => disconnectedCalled = true;

            // Act
            await connectionManager.ConnectAsync();
            Assert.True(connectedCalled);
            
            // Simulate error that will eventually lead to permanent disconnect
            // due to policy max attempts being reached
            var testException = new InvalidOperationException("Test error");
            connection.SimulateConnectionError(testException, shouldFailReconnections: true);

            await Task.Delay(500); // Wait for disconnect processing

            // Assert
            Assert.True(connectedCalled);
            Assert.True(disconnectedCalled); // Should eventually get permanent disconnect
        }
    }
}