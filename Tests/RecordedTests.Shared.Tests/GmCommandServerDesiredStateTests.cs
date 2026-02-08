using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using Xunit;

namespace RecordedTests.Shared.Tests;

public sealed class GmCommandServerDesiredStateTests
{
    [Fact]
    public async Task ApplyAsync_ExecutesCommandsInOrder()
    {
        var logger = new CapturingLogger();
        var commands = new[]
        {
            new GmCommandServerDesiredState.GmCommandStep(".announce Recording started"),
            new GmCommandServerDesiredState.GmCommandStep(ctx => $".summon {ctx.TestName}", "Summon test character")
        };
        var desiredState = new GmCommandServerDesiredState("ScenarioSetup", commands, logger);
        var runner = new CapturingGmRunner();
        var context = new StubRecordedTestContext("Test Character");

        await desiredState.ApplyAsync(runner, context, CancellationToken.None);

        Assert.Equal(
            new[]
            {
                ".announce Recording started",
                ".summon Test Character"
            },
            runner.ExecutedCommands);

        Assert.Contains(
            "[DesiredState:ScenarioSetup] Executing GM command .announce Recording started",
            logger.InfoMessages);
        Assert.Contains(
            "[DesiredState:ScenarioSetup] Executing GM command Summon test character: .summon Test Character",
            logger.InfoMessages);
    }

    [Fact]
    public async Task ApplyAsync_CommandResolvesToEmpty_Throws()
    {
        var desiredState = new GmCommandServerDesiredState(
            "Invalid",
            new[]
            {
                new GmCommandServerDesiredState.GmCommandStep(_ => " ")
            });
        var runner = new CapturingGmRunner();
        var context = new StubRecordedTestContext("Tester");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => desiredState.ApplyAsync(runner, context, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_RunnerDoesNotSupportCommands_Throws()
    {
        var desiredState = new GmCommandServerDesiredState(
            "Scenario",
            new[]
            {
                new GmCommandServerDesiredState.GmCommandStep(".reset all")
            });
        var runner = new NonCommandRunner();
        var context = new StubRecordedTestContext("Tester");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => desiredState.ApplyAsync(runner, context, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_CommandFails_Throws()
    {
        var desiredState = new GmCommandServerDesiredState(
            "Scenario",
            new[]
            {
                new GmCommandServerDesiredState.GmCommandStep(".reset all")
            });
        var runner = new FailingGmRunner();
        var context = new StubRecordedTestContext("Tester");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => desiredState.ApplyAsync(runner, context, CancellationToken.None));
    }

    private sealed class CapturingGmRunner : IBotRunner, IGmCommandHost
    {
        private readonly List<string> _commands = new();

        public IReadOnlyList<string> ExecutedCommands => _commands;

        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GmCommandExecutionResult> ExecuteGmCommandAsync(string command, CancellationToken cancellationToken)
        {
            _commands.Add(command);
            return Task.FromResult(GmCommandExecutionResult.Succeeded);
        }

        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RecordingTarget(RecordingTargetType.Screen, ScreenIndex: 0));

        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ShutdownUiAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<TResult> AcceptVisitorAsync<TResult>(IBotRunnerVisitor<TResult> visitor, CancellationToken cancellationToken)
        {
            if (visitor is IGmCommandRunnerVisitor<TResult> gmVisitor)
            {
                return gmVisitor.VisitAsync(this, cancellationToken);
            }

            return visitor.VisitAsync(this, cancellationToken);
        }
    }

    private sealed class NonCommandRunner : IBotRunner
    {
        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RecordingTarget(RecordingTargetType.Screen, ScreenIndex: 0));

        public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ShutdownUiAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingGmRunner : IBotRunner, IGmCommandHost
    {
        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RecordingTarget(RecordingTargetType.Screen, ScreenIndex: 0));

        public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ShutdownUiAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<GmCommandExecutionResult> ExecuteGmCommandAsync(string command, CancellationToken cancellationToken)
            => Task.FromResult(GmCommandExecutionResult.Failed("Simulated failure"));

        public Task<TResult> AcceptVisitorAsync<TResult>(IBotRunnerVisitor<TResult> visitor, CancellationToken cancellationToken)
        {
            if (visitor is IGmCommandRunnerVisitor<TResult> gmVisitor)
            {
                return gmVisitor.VisitAsync(this, cancellationToken);
            }

            return visitor.VisitAsync(this, cancellationToken);
        }
    }

    private sealed class StubRecordedTestContext : IRecordedTestContext
    {
        public StubRecordedTestContext(string testName)
        {
            TestName = testName;
            SanitizedTestName = ArtifactPathHelper.SanitizeName(testName);
            ArtifactsRootDirectory = "/tmp";
            TestRootDirectory = "/tmp/root";
            TestRunDirectory = "/tmp/root/run";
            Server = new ServerInfo("localhost", 3724, "Test");
            StartedAt = DateTimeOffset.UtcNow;
        }

        public string TestName { get; }

        public string SanitizedTestName { get; }

        public ServerInfo Server { get; }

        public DateTimeOffset StartedAt { get; }

        public string ArtifactsRootDirectory { get; }

        public string TestRootDirectory { get; }

        public string TestRunDirectory { get; }
    }

    private sealed class CapturingLogger : ITestLogger
    {
        private readonly List<string> _info = new();

        public IReadOnlyList<string> InfoMessages => _info;

        public void Error(string message, Exception? ex = null)
        {
        }

        public void Info(string message)
        {
            _info.Add(message);
        }

        public void Warn(string message)
        {
        }
    }
}
