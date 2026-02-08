using FluentAssertions;
using NSubstitute;
using System.Text.Json;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Tests;

public class LocalMangosDockerTrueNasAppsClientTests
{
    private readonly LocalMangosDockerTrueNasAppsClient.IDockerCli _dockerCli;

    public LocalMangosDockerTrueNasAppsClientTests()
    {
        _dockerCli = Substitute.For<LocalMangosDockerTrueNasAppsClient.IDockerCli>();
    }

    [Fact]
    public void Constructor_EmptyConfigurations_ThrowsArgumentException()
    {
        // Act
        var act = () => new LocalMangosDockerTrueNasAppsClient(
            Array.Empty<LocalMangosDockerConfiguration>(),
            _dockerCli
        );

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void Constructor_ValidConfigurations_Succeeds()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        // Act
        var act = () => new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetReleaseAsync_UnknownRelease_ReturnsNull()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("unknown-release", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReleaseAsync_InspectReturnsNoSuchObject_ReturnsNullRelease()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object: mangosd-dev", string.Empty, 1));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("running", true)]
    [InlineData("exited", false)]
    [InlineData("created", false)]
    [InlineData("paused", false)]
    public async Task GetReleaseAsync_ContainerState_MapsToIsRunning(string state, bool expectedIsRunning)
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724,
            Host: "127.0.0.1",
            Realm: "Dev Realm"
        );

        var inspectJson = $$"""
        [
            {
                "State": {
                    "Status": "{{state}}"
                }
            }
        ]
        """;

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns((inspectJson, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsRunning.Should().Be(expectedIsRunning);
        result.Host.Should().Be("127.0.0.1");
        result.Port.Should().Be(3724);
        result.Realm.Should().Be("Dev Realm");
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerRunning_DoesNotStartAgain()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        var inspectJson = """
        [
            {
                "State": {
                    "Status": "running"
                }
            }
        ]
        """;

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns((inspectJson, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert - inspect was called, but no start or run commands
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>());

        await _dockerCli.DidNotReceive().ExecuteAsync(
            Arg.Is<string>(s => s.Contains("start") || s.Contains("run")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerExistsButStopped_CallsDockerStart()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        var inspectJson = """
        [
            {
                "State": {
                    "Status": "exited"
                }
            }
        ]
        """;

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns((inspectJson, string.Empty, 0));

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("start")),
            Arg.Any<CancellationToken>())
            .Returns((string.Empty, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(s => s.Contains("start") && s.Contains("mangosd-dev")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerDoesNotExist_CallsDockerRun()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns((string.Empty, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(s => s.Contains("run")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_DockerRun_BuildsCorrectArguments()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724,
            ContainerName: "custom-mangos",
            Environment: new Dictionary<string, string>
            {
                ["DB_HOST"] = "localhost",
                ["DB_PORT"] = "3306"
            },
            VolumeMappings: new List<string>
            {
                "/host/data:/container/data",
                "/host/config:/container/config"
            },
            AdditionalArguments: "--network=bridge",
            Command: "/usr/bin/mangosd --config=/etc/mangosd.conf"
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns((string.Empty, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(args =>
                args.Contains("--detach") &&
                args.Contains("--name custom-mangos") &&
                args.Contains("--pull missing") &&
                args.Contains("-p 3724:3724") &&
                args.Contains("-e DB_HOST=localhost") &&
                args.Contains("-e DB_PORT=3306") &&
                args.Contains("-v /host/data:/container/data") &&
                args.Contains("-v /host/config:/container/config") &&
                args.Contains("--network=bridge") &&
                args.Contains("mangosd:latest") &&
                args.Contains("/usr/bin/mangosd --config=/etc/mangosd.conf")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_MinimalConfig_BuildsBasicArguments()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns((string.Empty, string.Empty, 0));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(args =>
                args.Contains("--detach") &&
                args.Contains("--name mangos-mangosd-dev") &&  // Default container name
                args.Contains("--pull missing") &&
                args.Contains("-p 3724:3724") &&
                args.Contains("mangosd:latest") &&
                !args.Contains("-e ") &&  // No environment variables
                !args.Contains("-v ")),   // No volumes
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LocalMangosDockerConfiguration_EffectiveContainerName_UsesProvidedName()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724,
            ContainerName: "custom-name"
        );

        // Act
        var effectiveName = config.EffectiveContainerName;

        // Assert
        effectiveName.Should().Be("custom-name");
    }

    [Fact]
    public void LocalMangosDockerConfiguration_EffectiveContainerName_GeneratesDefaultName()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        // Act
        var effectiveName = config.EffectiveContainerName;

        // Assert
        effectiveName.Should().Be("mangos-mangosd-dev");
    }

    [Fact]
    public async Task GetReleaseAsync_UsesEffectiveContainerName()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724,
            ContainerName: "custom-container"
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect") && s.Contains("custom-container")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).ExecuteAsync(
            Arg.Is<string>(s => s.Contains("custom-container")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_DockerRunFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        _dockerCli.ExecuteAsync(
            Arg.Is<string>(s => s.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(("", "docker: Error response from daemon", 125));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var act = async () => await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*docker run*failed*");
    }

    [Fact]
    public void Constructor_CaseInsensitiveReleaseLookup_FindsRelease()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "MangosD-Dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        _dockerCli.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(("Error: No such object", string.Empty, 1));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var act = async () => await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);  // lowercase

        // Assert - should not throw, should attempt to inspect
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_MultipleDisposeCalls_DoesNotThrow()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        client.Dispose();
        var act = () => client.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetReleaseAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            ReleaseName: "mangosd-dev",
            Image: "mangosd:latest",
            HostPort: 3724,
            ContainerPort: 3724
        );

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);
        client.Dispose();

        // Act
        var act = async () => await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
