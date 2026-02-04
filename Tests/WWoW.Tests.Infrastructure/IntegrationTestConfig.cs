using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Configuration for integration test environments.
/// Override these values using environment variables for CI/CD pipelines.
/// </summary>
public class IntegrationTestConfig
{
    #region WoW Server Settings

    /// <summary>
    /// WoW Auth server IP address.
    /// Override: WWOW_TEST_AUTH_IP
    /// </summary>
    public string AuthServerIp { get; init; } = 
        Environment.GetEnvironmentVariable("WWOW_TEST_AUTH_IP") ?? "127.0.0.1";

    /// <summary>
    /// WoW Auth server port.
    /// Override: WWOW_TEST_AUTH_PORT
    /// </summary>
    public int AuthServerPort { get; init; } = 
        int.Parse(Environment.GetEnvironmentVariable("WWOW_TEST_AUTH_PORT") ?? "3724");

    /// <summary>
    /// WoW World server port.
    /// Override: WWOW_TEST_WORLD_PORT
    /// </summary>
    public int WorldServerPort { get; init; } = 
        int.Parse(Environment.GetEnvironmentVariable("WWOW_TEST_WORLD_PORT") ?? "8085");

    #endregion

    #region PathfindingService Settings

    /// <summary>
    /// PathfindingService IP address.
    /// Override: WWOW_TEST_PATHFINDING_IP
    /// </summary>
    public string PathfindingServiceIp { get; init; } = 
        Environment.GetEnvironmentVariable("WWOW_TEST_PATHFINDING_IP") ?? "127.0.0.1";

    /// <summary>
    /// PathfindingService port.
    /// Override: WWOW_TEST_PATHFINDING_PORT
    /// </summary>
    public int PathfindingServicePort { get; init; } = 
        int.Parse(Environment.GetEnvironmentVariable("WWOW_TEST_PATHFINDING_PORT") ?? "5001");

    #endregion

    #region Test Account Settings

    /// <summary>
    /// Test account username. Should have GM level 3.
    /// Override: WWOW_TEST_USERNAME
    /// </summary>
    public string TestUsername { get; init; } = 
        Environment.GetEnvironmentVariable("WWOW_TEST_USERNAME") ?? "TESTBOT1";

    /// <summary>
    /// Test account password.
    /// Override: WWOW_TEST_PASSWORD
    /// </summary>
    public string TestPassword { get; init; } = 
        Environment.GetEnvironmentVariable("WWOW_TEST_PASSWORD") ?? "PASSWORD";

    #endregion

    #region Timeout Settings

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// Service health check timeout in milliseconds.
    /// </summary>
    public int HealthCheckTimeoutMs { get; init; } = 2000;

    /// <summary>
    /// Polling interval for async operations in milliseconds.
    /// </summary>
    public int PollingIntervalMs { get; init; } = 100;

    #endregion

    /// <summary>
    /// Creates a default configuration from environment variables.
    /// </summary>
    public static IntegrationTestConfig FromEnvironment() => new();
}

/// <summary>
/// Service availability checker for integration tests.
/// </summary>
public class ServiceHealthChecker(ILogger<ServiceHealthChecker>? logger = null)
{
    private readonly ILogger<ServiceHealthChecker>? _logger = logger;

    /// <summary>
    /// Checks if a TCP service is available at the specified endpoint.
    /// </summary>
    public async Task<bool> IsServiceAvailableAsync(string ip, int port, int timeoutMs = 2000)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));

            if (connectTask.IsCompletedSuccessfully && client.Connected)
            {
                _logger?.LogDebug("Service at {Ip}:{Port} is available", ip, port);
                return true;
            }

            _logger?.LogDebug("Service at {Ip}:{Port} is NOT available (timeout)", ip, port);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Service at {Ip}:{Port} is NOT available (error)", ip, port);
            return false;
        }
    }

    /// <summary>
    /// Checks if the WoW auth server is available.
    /// </summary>
    public Task<bool> IsWoWServerAvailableAsync(IntegrationTestConfig config)
        => IsServiceAvailableAsync(config.AuthServerIp, config.AuthServerPort, config.HealthCheckTimeoutMs);

    /// <summary>
    /// Checks if the PathfindingService is available.
    /// </summary>
    public Task<bool> IsPathfindingServiceAvailableAsync(IntegrationTestConfig config)
        => IsServiceAvailableAsync(config.PathfindingServiceIp, config.PathfindingServicePort, config.HealthCheckTimeoutMs);
}

/// <summary>
/// Required services for different types of integration tests.
/// </summary>
[Flags]
public enum RequiredServices
{
    None = 0,
    WoWServer = 1,
    PathfindingService = 2,
    All = WoWServer | PathfindingService
}

/// <summary>
/// Attribute to mark tests that require specific external services.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequiresServicesAttribute(RequiredServices services) : Attribute
{
    public RequiredServices Services { get; } = services;
}
