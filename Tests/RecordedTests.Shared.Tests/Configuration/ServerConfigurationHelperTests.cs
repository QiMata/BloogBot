using FluentAssertions;
using RecordedTests.Shared.Configuration;

namespace RecordedTests.Shared.Tests.Configuration;

public class ServerConfigurationHelperTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();

    public void Dispose()
    {
        foreach (var varName in _envVarsToCleanup)
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    private void SetTestEnvVar(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
    }

    [Fact]
    public void ResolveServerDefinitions_WithCliValue_ShouldParseSemicolonSeparated()
    {
        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions(
            cliServerDefinitions: "server1|host1|3724;server2|host2|3725|Alliance");

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("server1|host1|3724");
        result[1].Should().Be("server2|host2|3725|Alliance");
    }

    [Fact]
    public void ResolveServerDefinitions_WithEnvValue_ShouldParseEnvVariable()
    {
        // Arrange
        SetTestEnvVar("SERVER_DEFINITIONS", "env-server|env-host|3726;env-server2|env-host2|3727");

        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions();

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("env-server|env-host|3726");
        result[1].Should().Be("env-server2|env-host2|3727");
    }

    [Fact]
    public void ResolveServerDefinitions_WithConfigValue_ShouldReturnConfigArray()
    {
        // Arrange
        var configDefs = new[] { "config-server|config-host|3728", "config-server2|config-host2|3729" };

        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions(
            configServerDefinitions: configDefs);

        // Assert
        result.Should().BeEquivalentTo(configDefs);
    }

    [Fact]
    public void ResolveServerDefinitions_WithNothingSet_ShouldReturnDefaultLocal()
    {
        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions();

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("mangos-local|127.0.0.1|3724");
    }

    [Fact]
    public void ResolveServerDefinitions_WithCliValue_ShouldTrimWhitespace()
    {
        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions(
            cliServerDefinitions: " server1|host1|3724 ; server2|host2|3725 ");

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("server1|host1|3724");
        result[1].Should().Be("server2|host2|3725");
    }

    [Fact]
    public void ResolveServerDefinitions_WithEmptyStrings_ShouldBeFiltered()
    {
        // Act
        var result = ServerConfigurationHelper.ResolveServerDefinitions(
            cliServerDefinitions: "server1|host1|3724;;;server2|host2|3725");

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("server1|host1|3724");
        result[1].Should().Be("server2|host2|3725");
    }

    [Fact]
    public void ResolveMangosClie_WithTrueNasApiConfigured_ShouldReturnTrueNasClient()
    {
        // Act
        var client = ServerConfigurationHelper.ResolveMangosClie(
            cliTrueNasApi: "https://truenas.example.com/api",
            cliTrueNasApiKey: "test-api-key");

        // Assert
        client.Should().NotBeNull();
        client.Should().BeOfType<TrueNasAppsClient>();
    }

    [Fact]
    public void ResolveMangosClie_WithoutTrueNasApi_AndFallbackEnabled_ShouldReturnLocalDockerClient()
    {
        // Act
        var client = ServerConfigurationHelper.ResolveMangosClie(
            useLocalDockerFallback: true);

        // Assert
        client.Should().NotBeNull();
        client.Should().BeOfType<LocalMangosDockerTrueNasAppsClient>();
    }

    [Fact]
    public void ResolveMangosClie_WithoutTrueNasApi_AndFallbackDisabled_ShouldThrow()
    {
        // Act
        var act = () => ServerConfigurationHelper.ResolveMangosClie(
            useLocalDockerFallback: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No Mangos client configuration found*");
    }

    [Fact]
    public void ResolveMangosClie_WithEnvVariables_ShouldUseTrueNasClient()
    {
        // Arrange
        SetTestEnvVar("TRUENAS_API", "https://env-truenas.example.com/api");
        SetTestEnvVar("TRUENAS_API_KEY", "env-api-key");

        // Act
        var client = ServerConfigurationHelper.ResolveMangosClie();

        // Assert
        client.Should().NotBeNull();
        client.Should().BeOfType<TrueNasAppsClient>();
    }

    [Fact]
    public void CreateServerAvailabilityChecker_ShouldCreateWithResolvedDefinitions()
    {
        // Arrange
        var dockerConfig = new LocalMangosDockerConfiguration(
            releaseName: "test-server",
            image: "test:latest",
            hostPort: 3724,
            containerPort: 3724);
        using var mangosClient = new LocalMangosDockerTrueNasAppsClient(new[] { dockerConfig });

        // Act
        var checker = ServerConfigurationHelper.CreateServerAvailabilityChecker(
            mangosClient: mangosClient,
            cliServerDefinitions: "server1|host1|3724");

        // Assert
        checker.Should().NotBeNull();
        checker.Should().BeOfType<TrueNasAppServerAvailabilityChecker>();
    }
}
