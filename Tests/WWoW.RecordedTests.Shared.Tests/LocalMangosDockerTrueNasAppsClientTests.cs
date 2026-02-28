using FluentAssertions;
using NSubstitute;
using System.Text.Json;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests;

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
    public async Task GetReleaseAsync_InspectReturnsNoSuchObject_ReturnsReleaseNotRunning()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a.Contains("inspect"))),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object: mangosd-dev"));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var result = await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert - implementation returns a release with IsRunning=false when container doesn't exist
        result.Should().NotBeNull();
        result!.IsRunning.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GetReleaseAsync_ContainerState_MapsToIsRunning(bool running, bool expectedIsRunning)
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

        var inspectJson = $$"""
        [
            {
                "State": {
                    "Running": {{running.ToString().ToLowerInvariant()}},
                    "Status": "{{(running ? "running" : "exited")}}"
                }
            }
        ]
        """;

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, string.Empty));

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
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, string.Empty));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert - inspect was called, but no start or run commands
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>());

        await _dockerCli.DidNotReceive().RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "start") || args.Any(a => a == "run")),
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
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, inspectJson, string.Empty));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "start")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, string.Empty, string.Empty));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("start")),
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
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, string.Empty, string.Empty));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("run")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_DockerRun_BuildsCorrectArguments()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724,
            containerName: "custom-mangos",
            environment: new Dictionary<string, string>
            {
                ["DB_HOST"] = "localhost",
                ["DB_PORT"] = "3306"
            },
            volumeMappings: new List<string>
            {
                "/host/data:/container/data",
                "/host/config:/container/config"
            },
            additionalArguments: new[] { "--network=bridge" },
            command: new[] { "/usr/bin/mangosd", "--config=/etc/mangosd.conf" }
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, string.Empty, string.Empty));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args =>
                args.Contains("run") &&
                args.Contains("--detach") &&
                args.Contains("custom-mangos") &&
                args.Contains("mangosd:latest") &&
                args.Contains("--network=bridge")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_MinimalConfig_BuildsBasicArguments()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(0, string.Empty, string.Empty));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args =>
                args.Contains("run") &&
                args.Contains("--detach") &&
                args.Contains("mangosd:latest")),
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
    public async Task GetReleaseAsync_UsesEffectiveContainerName()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724,
            containerName: "custom-container"
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("custom-container")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        await client.GetReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await _dockerCli.Received(1).RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("custom-container")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReleaseAsync_DockerRunFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "mangosd-dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Any(a => a == "inspect")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

        _dockerCli.RunAsync(
            Arg.Is<IReadOnlyList<string>>(args => args.Contains("run")),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(125, "", "docker: Error response from daemon"));

        var client = new LocalMangosDockerTrueNasAppsClient(new[] { config }, _dockerCli);

        // Act
        var act = async () => await client.StartReleaseAsync("mangosd-dev", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*failed*");
    }

    [Fact]
    public void Constructor_CaseInsensitiveReleaseLookup_FindsRelease()
    {
        // Arrange
        var config = new LocalMangosDockerConfiguration(
            releaseName: "MangosD-Dev",
            image: "mangosd:latest",
            hostPort: 3724,
            containerPort: 3724
        );

        _dockerCli.RunAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new LocalMangosDockerTrueNasAppsClient.DockerCliResult(1, string.Empty, "Error: No such object"));

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
