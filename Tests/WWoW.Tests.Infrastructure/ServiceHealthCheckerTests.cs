using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="ServiceHealthChecker"/>.
/// Validates timeout behavior, unreachable endpoint handling, and bounded return times
/// without requiring live external services.
/// </summary>
[UnitTest]
public class ServiceHealthCheckerTests : IDisposable
{
    private readonly ServiceHealthChecker _checker = new();
    private TcpListener? _listener;

    public void Dispose()
    {
        _listener?.Stop();
    }

    // ======== Constructor ========

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrow()
    {
        var checker = new ServiceHealthChecker(null);
        Assert.NotNull(checker);
    }

    // ======== Unreachable Endpoint ========

    [Fact]
    public async Task IsServiceAvailableAsync_UnreachableEndpoint_ReturnsFalse()
    {
        // Use a non-routable IP to guarantee failure. RFC 5737 TEST-NET-1.
        var result = await _checker.IsServiceAvailableAsync("192.0.2.1", 1, timeoutMs: 500);
        Assert.False(result);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_UnreachableEndpoint_ReturnsBounded()
    {
        var sw = Stopwatch.StartNew();
        await _checker.IsServiceAvailableAsync("192.0.2.1", 1, timeoutMs: 1000);
        sw.Stop();

        // Should return within a reasonable window around the timeout
        // Allow generous upper bound to avoid flakiness on slow CI
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Expected bounded return within ~1s timeout, but took {sw.ElapsedMilliseconds}ms");
    }

    // ======== Refused Connection ========

    [Fact]
    public async Task IsServiceAvailableAsync_RefusedConnection_ReturnsFalse()
    {
        // Port 1 on localhost is almost certainly not listening and should be refused quickly
        var result = await _checker.IsServiceAvailableAsync("127.0.0.1", 1, timeoutMs: 2000);
        Assert.False(result);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_RefusedConnection_ReturnsFast()
    {
        var sw = Stopwatch.StartNew();
        await _checker.IsServiceAvailableAsync("127.0.0.1", 1, timeoutMs: 5000);
        sw.Stop();

        // Refused connections should return much faster than the timeout
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"Refused connection should return quickly, but took {sw.ElapsedMilliseconds}ms");
    }

    // ======== Reachable Endpoint (Local Listener) ========

    [Fact]
    public async Task IsServiceAvailableAsync_ReachableEndpoint_ReturnsTrue()
    {
        // Start a local TCP listener on an ephemeral port
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var result = await _checker.IsServiceAvailableAsync("127.0.0.1", port, timeoutMs: 2000);
        Assert.True(result);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ReachableEndpoint_ReturnsFast()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var sw = Stopwatch.StartNew();
        await _checker.IsServiceAvailableAsync("127.0.0.1", port, timeoutMs: 5000);
        sw.Stop();

        // Local connection should be very fast
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Local connection should be fast, but took {sw.ElapsedMilliseconds}ms");
    }

    // ======== Invalid Input ========

    [Fact]
    public async Task IsServiceAvailableAsync_InvalidIp_ReturnsFalse()
    {
        var result = await _checker.IsServiceAvailableAsync("not-a-valid-ip", 80, timeoutMs: 1000);
        Assert.False(result);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_EmptyIp_ReturnsFalse()
    {
        var result = await _checker.IsServiceAvailableAsync("", 80, timeoutMs: 500);
        Assert.False(result);
    }

    // ======== Repeated Calls ========

    [Fact]
    public async Task IsServiceAvailableAsync_RepeatedCalls_DoNotHang()
    {
        // Verify that calling multiple times in succession does not cause resource leaks
        // or runaway background work
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            await _checker.IsServiceAvailableAsync("127.0.0.1", 1, timeoutMs: 200);
        }
        sw.Stop();

        // 5 calls with 200ms timeout each should complete well under 10 seconds
        Assert.True(sw.ElapsedMilliseconds < 10000,
            $"5 repeated calls should not accumulate delays, but took {sw.ElapsedMilliseconds}ms");
    }

    // ======== Convenience Methods ========

    [Fact]
    public async Task IsWoWServerAvailableAsync_UsesConfigAuthEndpoint()
    {
        // With a local listener acting as the "auth server"
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var config = new IntegrationTestConfig
        {
            AuthServerIp = "127.0.0.1",
            AuthServerPort = port,
            HealthCheckTimeoutMs = 2000
        };

        var result = await _checker.IsWoWServerAvailableAsync(config);
        Assert.True(result);
    }

    [Fact]
    public async Task IsWoWServerAvailableAsync_NoServer_ReturnsFalse()
    {
        var config = new IntegrationTestConfig
        {
            AuthServerIp = "127.0.0.1",
            AuthServerPort = 1,
            HealthCheckTimeoutMs = 500
        };

        var result = await _checker.IsWoWServerAvailableAsync(config);
        Assert.False(result);
    }

    [Fact]
    public async Task IsPathfindingServiceAvailableAsync_UsesConfigEndpoint()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var config = new IntegrationTestConfig
        {
            PathfindingServiceIp = "127.0.0.1",
            PathfindingServicePort = port,
            HealthCheckTimeoutMs = 2000
        };

        var result = await _checker.IsPathfindingServiceAvailableAsync(config);
        Assert.True(result);
    }

    [Fact]
    public async Task IsPathfindingServiceAvailableAsync_NoService_ReturnsFalse()
    {
        var config = new IntegrationTestConfig
        {
            PathfindingServiceIp = "127.0.0.1",
            PathfindingServicePort = 1,
            HealthCheckTimeoutMs = 500
        };

        var result = await _checker.IsPathfindingServiceAvailableAsync(config);
        Assert.False(result);
    }
}
