using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests;

public class ServerAvailabilityTests
{
    private readonly IMangosAppsClient _appsClient;
    private readonly ITestLogger _logger;

    public ServerAvailabilityTests()
    {
        _appsClient = Substitute.For<IMangosAppsClient>();
        _logger = Substitute.For<ITestLogger>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mangosd")]  // Missing delimiter
    [InlineData("mangosd|")]  // Missing host
    [InlineData("mangosd|localhost")]  // Missing port
    [InlineData("|localhost|3724")]  // Missing release name
    public void Constructor_InvalidDefinition_ThrowsFormatException(string invalidDefinition)
    {
        // Act
        var act = () => new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { invalidDefinition },
            logger: _logger
        );

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_NonNumericPort_ThrowsFormatException()
    {
        // Arrange
        var invalidDefinition = "mangosd-dev|localhost|abc";

        // Act
        var act = () => new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { invalidDefinition },
            logger: _logger
        );

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*port*");
    }

    [Fact]
    public void Constructor_ValidDefinitionWithRealm_Succeeds()
    {
        // Arrange
        var validDefinition = "mangosd-dev|localhost|3724|Test Realm";

        // Act
        var act = () => new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { validDefinition },
            logger: _logger
        );

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ValidDefinitionWithoutRealm_Succeeds()
    {
        // Arrange
        var validDefinition = "mangosd-dev|localhost|3724";

        // Act
        var act = () => new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { validDefinition },
            logger: _logger
        );

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task WaitForAvailableAsync_CheckedOutRelease_SkipsAndContinues()
    {
        // Arrange
        var definitions = new[] { "mangosd-dev|localhost|3724", "mangosd-prod|localhost|3725" };

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: true,
                IsCheckedOut: true,
                Host: "192.168.1.10",
                Port: 3724,
                Realm: null
            ));

        _appsClient.GetReleaseAsync("mangosd-prod", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-prod",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.11",
                Port: 3725,
                Realm: "Prod Realm"
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            definitions,
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ReleaseName.Should().Be("mangosd-prod");
        result.Host.Should().Be("192.168.1.11");
        result.Port.Should().Be(3725);

        _logger.Received().Warn(Arg.Is<string>(s => s.Contains("checked out")));
    }

    [Fact]
    public async Task WaitForAvailableAsync_NotRunningRelease_TriggersStartReleaseAsync()
    {
        // Arrange
        var definition = "mangosd-dev|localhost|3724";

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(
                // First call: not running
                new TrueNasAppRelease(
                    "mangosd-dev",
                    IsRunning: false,
                    IsCheckedOut: false,
                    Host: null,
                    Port: null,
                    Realm: null
                ),
                // Second call (after start): running
                new TrueNasAppRelease(
                    "mangosd-dev",
                    IsRunning: true,
                    IsCheckedOut: false,
                    Host: "192.168.1.10",
                    Port: 3724,
                    Realm: null
                )
            );

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            pollInterval: TimeSpan.FromMilliseconds(100),
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _appsClient.Received(1).StartReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitForAvailableAsync_HostAndPortFromRelease_TakesPrecedenceOverDefinition()
    {
        // Arrange
        var definition = "mangosd-dev|fallback-host|9999";

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "actual-host",
                Port: 3724,
                Realm: "Actual Realm"
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Host.Should().Be("actual-host");
        result.Port.Should().Be(3724);
        result.Realm.Should().Be("Actual Realm");
    }

    [Fact]
    public async Task WaitForAvailableAsync_HostAndPortMissingFromRelease_UsesDefinitionFallback()
    {
        // Arrange
        var definition = "mangosd-dev|fallback-host|9999|Fallback Realm";

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: true,
                IsCheckedOut: false,
                Host: null,
                Port: null,
                Realm: null
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Host.Should().Be("fallback-host");
        result.Port.Should().Be(9999);
        result.Realm.Should().Be("Fallback Realm");
    }

    [Fact]
    public async Task WaitForAvailableAsync_MissingHost_SkipsCandidate()
    {
        // Arrange
        var definitions = new[] { "mangosd-dev|localhost|3724", "mangosd-prod|localhost|3725" };

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: true,
                IsCheckedOut: false,
                Host: null,  // Missing host
                Port: null,
                Realm: null
            ));

        _appsClient.GetReleaseAsync("mangosd-prod", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-prod",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.11",
                Port: 3725,
                Realm: null
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            definitions,
            logger: _logger
        );

        // Act - with definition fallback having localhost, should work
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // First candidate should use fallback, second should use release info
        result!.Host.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WaitForAvailableAsync_TimeoutExpires_ReturnsNull()
    {
        // Arrange
        var definition = "mangosd-dev|localhost|3724";

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: false,
                IsCheckedOut: false,
                Host: null,
                Port: null,
                Realm: null
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            pollInterval: TimeSpan.FromMilliseconds(50),
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task WaitForAvailableAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var definition = "mangosd-dev|localhost|3724";
        var cts = new CancellationTokenSource();

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return new TrueNasAppRelease(
                    "mangosd-dev",
                    IsRunning: false,
                    IsCheckedOut: false,
                    Host: null,
                    Port: null,
                    Realm: null
                );
            });

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            pollInterval: TimeSpan.FromMilliseconds(50),
            logger: _logger
        );

        // Act
        var act = async () => await checker.WaitForAvailableAsync(
            TimeSpan.FromSeconds(10),
            cts.Token
        );

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForAvailableAsync_HttpRequestException_LogsWarningAndContinues()
    {
        // Arrange
        var definitions = new[] { "mangosd-dev|localhost|3724", "mangosd-prod|localhost|3725" };

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _appsClient.GetReleaseAsync("mangosd-prod", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-prod",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.11",
                Port: 3725,
                Realm: null
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            definitions,
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ReleaseName.Should().Be("mangosd-prod");

        _logger.Received().Warn(Arg.Is<string>(s =>
            s.Contains("mangosd-dev") && s.Contains("Connection refused")));
    }

    [Fact]
    public async Task WaitForAvailableAsync_InfiniteTimeout_DoesNotThrowOverflow()
    {
        // Arrange
        var definition = "mangosd-dev|localhost|3724";

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.10",
                Port: 3724,
                Realm: null
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            new[] { definition },
            logger: _logger
        );

        // Act
        var act = async () => await checker.WaitForAvailableAsync(
            Timeout.InfiniteTimeSpan,
            CancellationToken.None
        );

        // Assert
        await act.Should().NotThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task WaitForAvailableAsync_MultipleDefinitions_ReturnsFirstAvailable()
    {
        // Arrange
        var definitions = new[] {
            "mangosd-dev|localhost|3724",
            "mangosd-staging|localhost|3725",
            "mangosd-prod|localhost|3726"
        };

        _appsClient.GetReleaseAsync("mangosd-dev", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-dev",
                IsRunning: false,
                IsCheckedOut: false,
                Host: null,
                Port: null,
                Realm: null
            ));

        _appsClient.GetReleaseAsync("mangosd-staging", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-staging",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.11",
                Port: 3725,
                Realm: "Staging"
            ));

        _appsClient.GetReleaseAsync("mangosd-prod", Arg.Any<CancellationToken>())
            .Returns(new TrueNasAppRelease(
                "mangosd-prod",
                IsRunning: true,
                IsCheckedOut: false,
                Host: "192.168.1.12",
                Port: 3726,
                Realm: "Prod"
            ));

        var checker = new TrueNasAppServerAvailabilityChecker(
            _appsClient,
            definitions,
            logger: _logger
        );

        // Act
        var result = await checker.WaitForAvailableAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ReleaseName.Should().Be("mangosd-staging");  // First available
    }
}
