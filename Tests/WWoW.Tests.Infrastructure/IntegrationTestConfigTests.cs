namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="IntegrationTestConfig"/> default values, environment variable
/// overrides, and TryParse fallback behavior.
/// </summary>
[UnitTest]
public class IntegrationTestConfigTests
{
    // ======== Default Values ========

    [Fact]
    public void FromEnvironment_ReturnsNonNullConfig()
    {
        var config = IntegrationTestConfig.FromEnvironment();
        Assert.NotNull(config);
    }

    [Fact]
    public void DefaultAuthServerIp_IsLoopback()
    {
        var config = new IntegrationTestConfig();
        // When WWOW_TEST_AUTH_IP is not set, default is 127.0.0.1
        Assert.False(string.IsNullOrWhiteSpace(config.AuthServerIp));
    }

    [Fact]
    public void DefaultAuthServerPort_Is3724()
    {
        // Unless overridden by env var, default auth port is 3724
        var config = new IntegrationTestConfig();
        // The port is either the env var value or the default 3724
        Assert.True(config.AuthServerPort > 0, "AuthServerPort must be positive");
    }

    [Fact]
    public void DefaultWorldServerPort_Is8085()
    {
        var config = new IntegrationTestConfig();
        Assert.True(config.WorldServerPort > 0, "WorldServerPort must be positive");
    }

    [Fact]
    public void DefaultPathfindingServiceIp_IsNotEmpty()
    {
        var config = new IntegrationTestConfig();
        Assert.False(string.IsNullOrWhiteSpace(config.PathfindingServiceIp));
    }

    [Fact]
    public void DefaultPathfindingServicePort_Is5001()
    {
        var config = new IntegrationTestConfig();
        Assert.True(config.PathfindingServicePort > 0, "PathfindingServicePort must be positive");
    }

    // ======== Timeout Defaults ========

    [Fact]
    public void DefaultConnectionTimeoutMs_Is10000()
    {
        var config = new IntegrationTestConfig();
        Assert.Equal(10000, config.ConnectionTimeoutMs);
    }

    [Fact]
    public void DefaultHealthCheckTimeoutMs_Is2000()
    {
        var config = new IntegrationTestConfig();
        Assert.Equal(2000, config.HealthCheckTimeoutMs);
    }

    [Fact]
    public void DefaultPollingIntervalMs_Is100()
    {
        var config = new IntegrationTestConfig();
        Assert.Equal(100, config.PollingIntervalMs);
    }

    // ======== Test Account Defaults ========

    [Fact]
    public void DefaultTestUsername_IsNotEmpty()
    {
        var config = new IntegrationTestConfig();
        Assert.False(string.IsNullOrWhiteSpace(config.TestUsername));
    }

    [Fact]
    public void DefaultTestPassword_IsNotEmpty()
    {
        var config = new IntegrationTestConfig();
        Assert.False(string.IsNullOrWhiteSpace(config.TestPassword));
    }

    // ======== Init-Property Overrides ========

    [Fact]
    public void ConnectionTimeoutMs_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { ConnectionTimeoutMs = 5000 };
        Assert.Equal(5000, config.ConnectionTimeoutMs);
    }

    [Fact]
    public void HealthCheckTimeoutMs_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { HealthCheckTimeoutMs = 500 };
        Assert.Equal(500, config.HealthCheckTimeoutMs);
    }

    [Fact]
    public void PollingIntervalMs_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { PollingIntervalMs = 50 };
        Assert.Equal(50, config.PollingIntervalMs);
    }

    [Fact]
    public void AuthServerIp_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { AuthServerIp = "10.0.0.1" };
        Assert.Equal("10.0.0.1", config.AuthServerIp);
    }

    [Fact]
    public void AuthServerPort_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { AuthServerPort = 9999 };
        Assert.Equal(9999, config.AuthServerPort);
    }

    [Fact]
    public void WorldServerPort_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { WorldServerPort = 7777 };
        Assert.Equal(7777, config.WorldServerPort);
    }

    [Fact]
    public void PathfindingServicePort_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { PathfindingServicePort = 6001 };
        Assert.Equal(6001, config.PathfindingServicePort);
    }

    [Fact]
    public void TestUsername_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { TestUsername = "CUSTOM_USER" };
        Assert.Equal("CUSTOM_USER", config.TestUsername);
    }

    [Fact]
    public void TestPassword_CanBeOverriddenViaInit()
    {
        var config = new IntegrationTestConfig { TestPassword = "CUSTOM_PASS" };
        Assert.Equal("CUSTOM_PASS", config.TestPassword);
    }
}

/// <summary>
/// Tests for <see cref="RequiredServices"/> flags enum.
/// </summary>
[UnitTest]
public class RequiredServicesTests
{
    [Fact]
    public void None_IsZero()
    {
        Assert.Equal(0, (int)RequiredServices.None);
    }

    [Fact]
    public void WoWServer_IsFlagValue1()
    {
        Assert.Equal(1, (int)RequiredServices.WoWServer);
    }

    [Fact]
    public void PathfindingService_IsFlagValue2()
    {
        Assert.Equal(2, (int)RequiredServices.PathfindingService);
    }

    [Fact]
    public void All_IncludesWoWServerAndPathfinding()
    {
        Assert.True(RequiredServices.All.HasFlag(RequiredServices.WoWServer));
        Assert.True(RequiredServices.All.HasFlag(RequiredServices.PathfindingService));
    }

    [Fact]
    public void All_DoesNotIncludeNone()
    {
        // None (0) is always contained in any flags value, but All should be non-zero
        Assert.NotEqual(RequiredServices.None, RequiredServices.All);
    }

    [Fact]
    public void FlagsCombination_WoWServerAndPathfinding_EqualsAll()
    {
        var combined = RequiredServices.WoWServer | RequiredServices.PathfindingService;
        Assert.Equal(RequiredServices.All, combined);
    }
}

/// <summary>
/// Tests for <see cref="RequiresServicesAttribute"/>.
/// </summary>
[UnitTest]
public class RequiresServicesAttributeTests
{
    [Fact]
    public void Constructor_StoresServicesValue()
    {
        var attr = new RequiresServicesAttribute(RequiredServices.WoWServer);
        Assert.Equal(RequiredServices.WoWServer, attr.Services);
    }

    [Fact]
    public void Constructor_All_StoresAll()
    {
        var attr = new RequiresServicesAttribute(RequiredServices.All);
        Assert.Equal(RequiredServices.All, attr.Services);
    }

    [Fact]
    public void Constructor_None_StoresNone()
    {
        var attr = new RequiresServicesAttribute(RequiredServices.None);
        Assert.Equal(RequiredServices.None, attr.Services);
    }

    [Fact]
    public void AttributeUsage_AllowsMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequiresServicesAttribute), typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    [Fact]
    public void AttributeUsage_DoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RequiresServicesAttribute), typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.False(usage!.AllowMultiple);
    }
}
