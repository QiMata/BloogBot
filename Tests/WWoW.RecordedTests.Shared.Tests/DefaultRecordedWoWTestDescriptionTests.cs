using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests;

public class DefaultRecordedWoWTestDescriptionTests
{
    private readonly IBotRunner _foregroundRunner;
    private readonly IBotRunner _backgroundRunner;
    private readonly IScreenRecorder _recorder;
    private readonly IServerDesiredState _desiredState;
    private readonly ITestLogger _logger;
    private readonly ServerInfo _serverInfo;
    private readonly string _tempArtifactsRoot;

    public DefaultRecordedWoWTestDescriptionTests()
    {
        _foregroundRunner = Substitute.For<IBotRunner>();
        _backgroundRunner = Substitute.For<IBotRunner>();
        _recorder = Substitute.For<IScreenRecorder>();
        _desiredState = Substitute.For<IServerDesiredState>();
        _logger = Substitute.For<ITestLogger>();
        _serverInfo = new ServerInfo("test-server", "localhost", 3724, "Test Realm");
        _tempArtifactsRoot = Path.Combine(Path.GetTempPath(), $"test-artifacts-{Guid.NewGuid()}");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessPath_ExecutesInCorrectOrder()
    {
        // Arrange
        var callOrder = new List<string>();

        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("ForegroundConnect"));

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("BackgroundConnect"));

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderLaunch"));

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderConfigure"));

        _desiredState.ApplyAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("StateApply"));

        _recorder.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderStart"));

        _backgroundRunner.RunTestAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RunTest"));

        _recorder.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderStop"));

        _desiredState.RevertAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("StateRevert"));

        _recorder.MoveLastRecordingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestArtifact("test.mkv", "video/x-matroska", 1024))
            .AndDoes(_ => callOrder.Add("RecorderMove"));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("ForegroundShutdown"));

        _backgroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("BackgroundShutdown"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        callOrder.Should().Equal(
            "ForegroundConnect",
            "BackgroundConnect",
            "RecorderLaunch",
            "RecorderConfigure",
            "StateApply",
            "RecorderStart",
            "RunTest",
            "RecorderStop",
            "StateRevert",
            "RecorderMove",
            "ForegroundShutdown",
            "BackgroundShutdown"
        );
    }

    [Fact]
    public async Task ExecuteAsync_DoubleStopEnabled_CallsStopTwice()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: true,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        await _recorder.Received(2).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoubleStopDisabled_CallsStopOnce()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        await _recorder.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesArtifactDirectoryWithSanitizedName()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testNameWithInvalidChars = "Test/Pathing\\With:Invalid*Chars?";
        var testDescription = new DefaultRecordedWoWTestDescription(
            testNameWithInvalidChars,
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        // Verify the move was called with a sanitized path
        await _recorder.Received(1).MoveLastRecordingAsync(
            Arg.Is<string>(path => !path.Contains('/') && !path.Contains('\\') && !path.Contains(':')),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_CreatesTimestampedFolder()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        // Verify the move was called with a path containing a timestamp pattern
        await _recorder.Received(1).MoveLastRecordingAsync(
            Arg.Is<string>(path => System.Text.RegularExpressions.Regex.IsMatch(
                path,
                @"\d{8}_\d{6}")), // yyyyMMdd_HHmmss pattern
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringTest_CleansUpResources()
    {
        // Arrange
        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.ApplyAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("GM command failed"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => testDescription.ExecuteAsync(_serverInfo, CancellationToken.None));

        // Verify cleanup was attempted
        await _foregroundRunner.Received().DisposeAsync();
        await _backgroundRunner.Received().DisposeAsync();
        await _recorder.Received().DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CleanupExceptionsSuppressed_DoesNotThrow()
    {
        // Arrange
        SetupDefaultHappyPath();

        _foregroundRunner.DisposeAsync()
            .ThrowsAsync(new InvalidOperationException("Cleanup failed"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        var act = async () => await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert - should not throw despite disposal exception
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ConfiguresRecorderWithForegroundRunnerTarget()
    {
        // Arrange
        SetupDefaultHappyPath();

        var expectedTarget = new RecordingTarget(RecordingTargetType.WindowTitle, "WoW");
        _foregroundRunner.GetRecordingTarget().Returns(expectedTarget);

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        await _recorder.Received(1).ConfigureTargetAsync(expectedTarget, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesServerInfoToRunners()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            () => _foregroundRunner,
            () => _backgroundRunner,
            () => _recorder,
            _desiredState,
            _tempArtifactsRoot,
            doubleStopRecorder: false,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_serverInfo, CancellationToken.None);

        // Assert
        await _foregroundRunner.Received(1).ConnectAsync(_serverInfo, Arg.Any<CancellationToken>());
        await _backgroundRunner.Received(1).ConnectAsync(_serverInfo, Arg.Any<CancellationToken>());
    }

    private void SetupDefaultHappyPath()
    {
        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.ApplyAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.RunTestAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.RevertAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.MoveLastRecordingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestArtifact("test.mkv", "video/x-matroska", 1024));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.GetRecordingTarget()
            .Returns(new RecordingTarget(RecordingTargetType.WindowTitle, "WoW"));
    }
}
