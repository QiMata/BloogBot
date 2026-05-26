using System;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public sealed class PathfindingFixtureConfigurationTests
{
    [Fact]
    public void ResolveCurrentPathfindingEndpoint_UsesCurrentEnvironmentOverrides()
    {
        using var ipScope = new EnvironmentVariableScope("WWOW_TEST_PATHFINDING_IP", "127.0.0.42");
        using var portScope = new EnvironmentVariableScope("WWOW_TEST_PATHFINDING_PORT", "9020");

        var endpoint = BotServiceFixture.ResolveCurrentPathfindingEndpoint();

        Assert.Equal("127.0.0.42", endpoint.IpAddress);
        Assert.Equal(9020, endpoint.Port);
    }

    [Fact]
    public void ResolveCurrentPathfindingEndpoint_DefaultsToDockerPortWhenUnset()
    {
        using var ipScope = new EnvironmentVariableScope("WWOW_TEST_PATHFINDING_IP", null);
        using var portScope = new EnvironmentVariableScope("WWOW_TEST_PATHFINDING_PORT", null);

        var endpoint = BotServiceFixture.ResolveCurrentPathfindingEndpoint();

        Assert.Equal("127.0.0.1", endpoint.IpAddress);
        Assert.Equal(9002, endpoint.Port);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
            => Environment.SetEnvironmentVariable(_name, _previousValue);
    }
}
