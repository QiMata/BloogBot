using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests;

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
        _serverInfo = new ServerInfo("localhost", 3724, "Test Realm");
        _tempArtifactsRoot = Path.Combine(Path.GetTempPath(), $"test-artifacts-{Guid.NewGuid()}");
    }

    private IRecordedTestContext CreateContext(string testName = "Test Pathing")
    {
        var sanitized = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
        var now = DateTimeOffset.UtcNow;
        var testRunDir = Path.Combine(_tempArtifactsRoot, sanitized, now.ToString("yyyyMMdd_HHmmss"));
        return new RecordedTestContext(
            testName,
            sanitized,
            _serverInfo,
            now,
            _tempArtifactsRoot,
            Path.Combine(_tempArtifactsRoot, sanitized),
            testRunDir);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessPath_ExecutesInCorrectOrder()
    {
        // Arrange
        var callOrder = new List<string>();
        var context = CreateContext();

        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("ForegroundConnect"));

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("BackgroundConnect"));

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderLaunch"));

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"))
            .AndDoes(_ => callOrder.Add("GetRecordingTarget"));

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderConfigure"));

        _desiredState.ApplyAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("StateApply"));

        _recorder.StartAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderStart"));

        _backgroundRunner.RunTestAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RunTest"));

        _recorder.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("RecorderStop"));

        _desiredState.RevertAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("StateRevert"));

        _recorder.MoveLastRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestArtifact("test.mkv", context.TestRunDirectory))
            .AndDoes(_ => callOrder.Add("RecorderMove"));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("ForegroundShutdown"));

        _foregroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _backgroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert — verify key steps happened in order
        callOrder.Should().ContainInOrder(
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
            "ForegroundShutdown"
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRecorder_SkipsRecorderSteps()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            recorder: null,
            _logger
        );

        // Act
        var result = await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        await _recorder.DidNotReceive().LaunchAsync(Arg.Any<CancellationToken>());
        await _recorder.DidNotReceive().StartAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>());
        await _recorder.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ConnectsWithCorrectServerInfo()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _foregroundRunner.Received(1).ConnectAsync(_serverInfo, Arg.Any<CancellationToken>());
        await _backgroundRunner.Received(1).ConnectAsync(_serverInfo, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AppliesDesiredStateWithForegroundRunner()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _desiredState.Received().ApplyAsync(_foregroundRunner, Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RevertsDesiredStateAfterTest()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _desiredState.Received().RevertAsync(_foregroundRunner, Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ConfiguresRecorderWithForegroundRunnerTarget()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var expectedTarget = new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW");
        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(expectedTarget);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _recorder.Received(1).ConfigureTargetAsync(expectedTarget, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringApply_ReturnsFailed()
    {
        // Arrange
        var context = CreateContext();

        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"));

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.ApplyAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("GM command failed"));

        _foregroundRunner.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _backgroundRunner.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _recorder.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _desiredState.RevertAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _foregroundRunner.ResetServerStateAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        var result = await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert — the implementation catches exceptions and returns a failure result
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("GM command failed");
    }

    [Fact]
    public async Task ExecuteAsync_RunsTestWithBackgroundRunner()
    {
        // Arrange
        var context = CreateContext();
        SetupDefaultHappyPath(context);

        var testDescription = new DefaultRecordedWoWTestDescription(
            context,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _backgroundRunner.Received(1).RunTestAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>());
    }

    private void SetupDefaultHappyPath(IRecordedTestContext context)
    {
        _foregroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ConnectAsync(Arg.Any<ServerInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.LaunchAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"));

        _recorder.ConfigureTargetAsync(Arg.Any<RecordingTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.ApplyAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.StartAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.RunTestAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.RevertAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _recorder.MoveLastRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestArtifact("test.mkv", context.TestRunDirectory));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.PrepareServerStateAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.ResetServerStateAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }
}
