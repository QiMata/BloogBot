using FluentAssertions;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
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
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("unknown-release", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReleaseAsync_InspectReturnsNoSuchObject_ReturnsNotRunning()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, "", "Error: No such object: mangosd-dev"));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task GetReleaseAsync_RunningContainer_ReturnsIsRunning()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724,
            host: "127.0.0.1",
            realm: "Dev Realm"
        );

        var inspectJson = """
        [
            {
                "State": {
                    "Running": true,
                    "Status": "running"
                }
            }
        ]
        """;

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(static args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, ""));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsRunning.Should().BeTrue();
        result.Host.Should().Be("127.0.0.1");
        result.Port.Should().Be(3724);
        result.Realm.Should().Be("Dev Realm");
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerRunning_DoesNotStartAgain()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        var inspectJson = """
        [
            {
                "State": {
                    "Running": true,
                    "Status": "running"
                }
            }
        ]
        """;

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, ""));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert - inspect was called, but no start or run commands
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerExistsButStopped_CallsDockerStart()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        var inspectJson = """
        [
            {
                "State": {
                    "Running": false,
                    "Status": "exited"
                }
            }
        ]
        """;

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, ""));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("start")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, "", ""));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("start")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_ContainerDoesNotExist_CallsDockerRun()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, "", "Error: No such object"));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, "", ""));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => ((IReadOnlyList<string>)args).Contains("run")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LocalMangosDockerConfiguration_EffectiveContainerName_UsesProvidedName()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724,
            containerName: "custom-name"
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
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        // Act
        var effectiveName = config.EffectiveContainerName;

        // Assert
        effectiveName.Should().Be("mangos-mangosd-dev");
    }

    [Fact]
    public void Dispose_MultipleDisposeCalls_DoesNotThrow()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
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
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);
        client.Dispose();

        // Act
        var act = async () => await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
