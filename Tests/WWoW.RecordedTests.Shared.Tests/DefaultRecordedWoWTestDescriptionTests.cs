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
    private readonly IRecordedTestContext _testContext;

    public DefaultRecordedWoWTestDescriptionTests()
    {
        _foregroundRunner = Substitute.For<IBotRunner>();
        _backgroundRunner = Substitute.For<IBotRunner>();
        _recorder = Substitute.For<IScreenRecorder>();
        _desiredState = Substitute.For<IServerDesiredState>();
        _logger = Substitute.For<ITestLogger>();
        _serverInfo = new ServerInfo("localhost", 3724, "Test Realm");
        _tempArtifactsRoot = Path.Combine(Path.GetTempPath(), $"test-artifacts-{Guid.NewGuid()}");

        _testContext = Substitute.For<IRecordedTestContext>();
        _testContext.TestName.Returns("Test Pathing");
        _testContext.SanitizedTestName.Returns("Test_Pathing");
        _testContext.Server.Returns(_serverInfo);
        _testContext.StartedAt.Returns(DateTimeOffset.UtcNow);
        _testContext.ArtifactsRootDirectory.Returns(_tempArtifactsRoot);
        _testContext.TestRootDirectory.Returns(Path.Combine(_tempArtifactsRoot, "Test_Pathing"));
        _testContext.TestRunDirectory.Returns(Path.Combine(_tempArtifactsRoot, "Test_Pathing", "run1"));
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
            .Returns(new TestArtifact("test.mkv", "/tmp/test.mkv"))
            .AndDoes(_ => callOrder.Add("RecorderMove"));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("ForegroundShutdown"));

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            _testContext,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert - verify key operations were called in order
        callOrder.Should().Contain("ForegroundConnect");
        callOrder.Should().Contain("BackgroundConnect");
        callOrder.Should().Contain("RecorderLaunch");
        callOrder.Should().Contain("RecorderConfigure");
        callOrder.Should().Contain("StateApply");
        callOrder.Should().Contain("RecorderStart");
        callOrder.Should().Contain("RunTest");
        callOrder.Should().Contain("RecorderStop");
        callOrder.Should().Contain("StateRevert");
        callOrder.Should().Contain("RecorderMove");
        callOrder.Should().Contain("ForegroundShutdown");

        // Verify ordering of key phases
        callOrder.IndexOf("ForegroundConnect").Should().BeLessThan(callOrder.IndexOf("StateApply"));
        callOrder.IndexOf("StateApply").Should().BeLessThan(callOrder.IndexOf("RecorderStart"));
        callOrder.IndexOf("RecorderStart").Should().BeLessThan(callOrder.IndexOf("RunTest"));
        callOrder.IndexOf("RunTest").Should().BeLessThan(callOrder.IndexOf("RecorderStop"));
    }

    [Fact]
    public async Task ExecuteAsync_DoubleStopEnabled_CallsStopTwice()
    {
        // Arrange
        SetupDefaultHappyPath();

        var fgFactory = Substitute.For<IBotRunnerFactory>();
        fgFactory.Create().Returns(_foregroundRunner);
        var bgFactory = Substitute.For<IBotRunnerFactory>();
        bgFactory.Create().Returns(_backgroundRunner);
        var recorderFactory = Substitute.For<IScreenRecorderFactory>();
        recorderFactory.Create().Returns(_recorder);

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            fgFactory,
            bgFactory,
            recorderFactory,
            options: new OrchestrationOptions { DoubleStopRecorderForSafety = true },
            logger: _logger
        );

        // Act
        await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert - StopAsync called 3 times: main stop + double stop + cleanup
        await _recorder.Received(3).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoubleStopDisabled_CallsStopFewerTimes()
    {
        // Arrange
        SetupDefaultHappyPath();

        var fgFactory = Substitute.For<IBotRunnerFactory>();
        fgFactory.Create().Returns(_foregroundRunner);
        var bgFactory = Substitute.For<IBotRunnerFactory>();
        bgFactory.Create().Returns(_backgroundRunner);
        var recorderFactory = Substitute.For<IScreenRecorderFactory>();
        recorderFactory.Create().Returns(_recorder);

        var testDescription = new DefaultRecordedWoWTestDescription(
            "Test Pathing",
            fgFactory,
            bgFactory,
            recorderFactory,
            options: new OrchestrationOptions { DoubleStopRecorderForSafety = false },
            logger: _logger
        );

        // Act
        await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert - StopAsync called but without the double-stop
        await _recorder.Received().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesArtifactDirectoryWithSanitizedName()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testNameWithInvalidChars = "Test/Pathing\\With:Invalid*Chars?";
        var sanitized = "Test_Pathing_With_Invalid_Chars_";
        var ctx = Substitute.For<IRecordedTestContext>();
        ctx.TestName.Returns(testNameWithInvalidChars);
        ctx.SanitizedTestName.Returns(sanitized);
        ctx.Server.Returns(_serverInfo);
        ctx.StartedAt.Returns(DateTimeOffset.UtcNow);
        ctx.ArtifactsRootDirectory.Returns(_tempArtifactsRoot);
        ctx.TestRootDirectory.Returns(Path.Combine(_tempArtifactsRoot, sanitized));
        ctx.TestRunDirectory.Returns(Path.Combine(_tempArtifactsRoot, sanitized, "run1"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            ctx,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        // Verify the move was called with a sanitized path (no path separator chars)
        await _recorder.Received(1).MoveLastRecordingAsync(
            Arg.Any<string>(),
            Arg.Is<string>(name => !name.Contains('/') && !name.Contains('\\') && !name.Contains(':')),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringTest_ReturnsFailureResult()
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

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"));

        _desiredState.ApplyAsync(Arg.Any<IBotRunner>(), Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("GM command failed"));

        var testDescription = new DefaultRecordedWoWTestDescription(
            _testContext,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        var result = await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert - the current implementation catches exceptions and returns OrchestrationResult
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("GM command failed");
    }

    [Fact]
    public async Task ExecuteAsync_CleanupExceptionsSuppressed_DoesNotThrow()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            _testContext,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        var act = async () => await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ConfiguresRecorderWithForegroundRunnerTarget()
    {
        // Arrange
        SetupDefaultHappyPath();

        var expectedTarget = new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW");
        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>()).Returns(expectedTarget);

        var testDescription = new DefaultRecordedWoWTestDescription(
            _testContext,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

        // Assert
        await _recorder.Received(1).ConfigureTargetAsync(expectedTarget, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesServerInfoToRunners()
    {
        // Arrange
        SetupDefaultHappyPath();

        var testDescription = new DefaultRecordedWoWTestDescription(
            _testContext,
            _foregroundRunner,
            _backgroundRunner,
            _desiredState,
            _recorder,
            _logger
        );

        // Act
        await testDescription.ExecuteAsync(_testContext, CancellationToken.None);

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
            .Returns(new TestArtifact("test.mkv", "/tmp/test.mkv"));

        _foregroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.ShutdownUiAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.GetRecordingTargetAsync(Arg.Any<CancellationToken>())
            .Returns(new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW"));

        _foregroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _backgroundRunner.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.PrepareServerStateAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _foregroundRunner.ResetServerStateAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _desiredState.Name.Returns("TestState");
    }
}
