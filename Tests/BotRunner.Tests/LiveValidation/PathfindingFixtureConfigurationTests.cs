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

    [Fact]
    public void ResolveStateManagerDataDirectory_PrefersExplicitTestDataRoot()
    {
        using var dataRoot = new NavDataRootScope();
        using var otherRoot = new NavDataRootScope();
        using var testDataScope = new EnvironmentVariableScope("WWOW_TEST_DATA_DIR", dataRoot.RootPath);
        using var dataScope = new EnvironmentVariableScope("WWOW_DATA_DIR", otherRoot.RootPath);

        var resolved = BotServiceFixture.ResolveStateManagerDataDirectory();

        Assert.Equal(dataRoot.RootPath, resolved);
    }

    [Fact]
    public void ResolveStateManagerDataDirectory_FallsBackToExistingWwowDataDirWhenTestRootUnset()
    {
        using var dataRoot = new NavDataRootScope();
        using var testDataScope = new EnvironmentVariableScope("WWOW_TEST_DATA_DIR", null);
        using var dataScope = new EnvironmentVariableScope("WWOW_DATA_DIR", dataRoot.RootPath);

        var resolved = BotServiceFixture.ResolveStateManagerDataDirectory();

        Assert.Equal(dataRoot.RootPath, resolved);
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

    private sealed class NavDataRootScope : IDisposable
    {
        public string RootPath { get; }

        public NavDataRootScope()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "wwow-nav-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(RootPath, "mmaps"));
            Directory.CreateDirectory(Path.Combine(RootPath, "maps"));
            Directory.CreateDirectory(Path.Combine(RootPath, "vmaps"));
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
