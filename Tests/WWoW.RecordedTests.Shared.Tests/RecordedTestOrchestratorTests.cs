using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests;

public class RecordedTestOrchestratorTests
{
    private readonly IServerAvailabilityChecker _serverChecker;
    private readonly ITestDescription _testDescription;
    private readonly ITestLogger _logger;
    private readonly OrchestrationOptions _options;

    public RecordedTestOrchestratorTests()
    {
        _serverChecker = Substitute.For<IServerAvailabilityChecker>();
        _testDescription = Substitute.For<ITestDescription>();
        _testDescription.Name.Returns("TestDescription");
        _logger = Substitute.For<ITestLogger>();
        _options = new OrchestrationOptions
        {
            ServerAvailabilityTimeout = TimeSpan.FromMinutes(5),
            ArtifactsRootDirectory = "./TestLogs"
        };
    }

    [Fact]
    public async Task RunAsync_SuccessPath_ReturnsSuccessResult()
    {
        // Arrange
        var expectedServer = new ServerInfo("localhost", 3724, "Test Realm");
        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedServer);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult(true, "Test succeeded"));

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        var result = await orchestrator.RunAsync(_testDescription, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        await _serverChecker.Received(1).WaitForAvailableAsync(
            _options.ServerAvailabilityTimeout,
            Arg.Any<CancellationToken>());

        await _testDescription.Received(1).ExecuteAsync(
            Arg.Any<IRecordedTestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NoServerAvailable_ReturnsFailureResult()
    {
        // Arrange
        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns((ServerInfo?)null);

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        var result = await orchestrator.RunAsync(_testDescription, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not available");

        await _testDescription.DidNotReceive().ExecuteAsync(
            Arg.Any<IRecordedTestContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ReturnsFailureResultWithCancellationMessage()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var expectedServer = new ServerInfo("localhost", 3724, "Test Realm");
        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedServer);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<OrchestrationResult>>(callInfo =>
            {
                cts.Cancel();
                throw new OperationCanceledException();
            });

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        var result = await orchestrator.RunAsync(_testDescription, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("canceled");
    }

    [Fact]
    public async Task RunAsync_TestDescriptionThrowsException_ReturnsFailureResultWithExceptionMessage()
    {
        // Arrange
        var expectedServer = new ServerInfo("localhost", 3724, "Test Realm");
        var expectedException = new InvalidOperationException("Bot runner failed to connect");

        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedServer);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        var result = await orchestrator.RunAsync(_testDescription, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("failed");
        result.Message.Should().Contain("Bot runner failed to connect");
    }

    [Fact]
    public async Task RunAsync_UsesNullTestLoggerWhenNoneProvided()
    {
        // Arrange
        var expectedServer = new ServerInfo("localhost", 3724, "Test Realm");
        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedServer);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult(true, "Test succeeded"));

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, logger: null);

        // Act
        var act = async () => await orchestrator.RunAsync(_testDescription, CancellationToken.None);

        // Assert - should not throw NullReferenceException
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_PassesCancellationTokenToServerChecker()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            token)
            .Returns(new ServerInfo("localhost", 3724));

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult(true, "Test succeeded"));

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        await orchestrator.RunAsync(_testDescription, token);

        // Assert
        await _serverChecker.Received(1).WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            token);
    }

    [Fact]
    public async Task RunAsync_PassesCancellationTokenToTestDescription()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var server = new ServerInfo("localhost", 3724);

        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(server);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), token)
            .Returns(new OrchestrationResult(true, "Test succeeded"));

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        await orchestrator.RunAsync(_testDescription, token);

        // Assert
        await _testDescription.Received(1).ExecuteAsync(Arg.Any<IRecordedTestContext>(), token);
    }

    [Fact]
    public async Task RunAsync_LogsServerWaitStart()
    {
        // Arrange
        var server = new ServerInfo("localhost", 3724);
        _serverChecker.WaitForAvailableAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(server);

        _testDescription.ExecuteAsync(Arg.Any<IRecordedTestContext>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationResult(true, "Test succeeded"));

        var orchestrator = new RecordedTestOrchestrator(_serverChecker, _options, _logger);

        // Act
        await orchestrator.RunAsync(_testDescription, CancellationToken.None);

        // Assert
        _logger.Received(1).Info(Arg.Is<string>(s => s.Contains("Waiting for server")));
    }
}
