using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Tests.TestInfrastructure;
using Xunit;

namespace WWoW.RecordedTests.Shared.Tests;

public sealed class DefaultRecordedWoWTestDescriptionTests
{
    [Fact]
    public async Task ExecuteAsync_AppliesDesiredStatesOnSuccess()
    {
        using var tempDir = new TempDirectory();
        var context = FakeRecordedTestContext.Create(tempDir.Path);

        var foregroundRunner = new FakeBotRunner();
        var backgroundRunner = new FakeBotRunner();

        var initialAppliedBeforePrepare = false;
        var resetCallsBeforeBase = 0;
        var baseAppliedAfterReset = false;

        var initialState = new CallbackDesiredState("Initial", (runner, ctx, token) =>
        {
            initialAppliedBeforePrepare = true;
            return Task.CompletedTask;
        });

        foregroundRunner.OnPrepare = (ctx, token) =>
        {
            Assert.True(initialAppliedBeforePrepare);
            return Task.CompletedTask;
        };

        foregroundRunner.OnReset = (ctx, token) =>
        {
            resetCallsBeforeBase++;
            return Task.CompletedTask;
        };

        var baseState = new CallbackDesiredState("Base", (runner, ctx, token) =>
        {
            baseAppliedAfterReset = resetCallsBeforeBase > 0;
            return Task.CompletedTask;
        });

        var description = new DefaultRecordedWoWTestDescription(
            "Test Scenario",
            new DelegateBotRunnerFactory(() => foregroundRunner),
            new DelegateBotRunnerFactory(() => backgroundRunner),
            initialDesiredState: initialState,
            baseDesiredState: baseState);

        var result = await description.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(context.TestRunDirectory));

        Assert.Equal(1, initialState.ApplyCount);
        Assert.Equal(1, baseState.ApplyCount);
        Assert.True(baseAppliedAfterReset);

        Assert.Equal(1, foregroundRunner.ConnectCallCount);
        Assert.Equal(1, foregroundRunner.PrepareCallCount);
        Assert.Equal(2, foregroundRunner.ResetCallCount); // Execution + cleanup.
        Assert.Equal(1, foregroundRunner.ShutdownCallCount);
        Assert.Equal(1, foregroundRunner.DisconnectCallCount);
        Assert.Equal(1, foregroundRunner.DisposeCallCount);

        Assert.Equal(1, backgroundRunner.ConnectCallCount);
        Assert.Equal(1, backgroundRunner.RunCallCount);
        Assert.Equal(0, backgroundRunner.ResetCallCount);
        Assert.Equal(1, backgroundRunner.DisconnectCallCount);
        Assert.Equal(1, backgroundRunner.DisposeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBackgroundRunFails_RestoresBaseStateDuringCleanup()
    {
        using var tempDir = new TempDirectory();
        var context = FakeRecordedTestContext.Create(tempDir.Path);

        var foregroundRunner = new FakeBotRunner();
        var backgroundRunner = new FakeBotRunner()
        {
            OnRun = (ctx, token) => throw new InvalidOperationException("Run failure"),
        };

        var initialAppliedBeforePrepare = false;
        var resetCallsBeforeBase = 0;
        var baseAppliedAfterReset = false;

        var initialState = new CallbackDesiredState("Initial", (runner, ctx, token) =>
        {
            initialAppliedBeforePrepare = true;
            return Task.CompletedTask;
        });

        foregroundRunner.OnPrepare = (ctx, token) =>
        {
            Assert.True(initialAppliedBeforePrepare);
            return Task.CompletedTask;
        };

        foregroundRunner.OnReset = (ctx, token) =>
        {
            resetCallsBeforeBase++;
            return Task.CompletedTask;
        };

        var baseState = new CallbackDesiredState("Base", (runner, ctx, token) =>
        {
            baseAppliedAfterReset = resetCallsBeforeBase > 0;
            return Task.CompletedTask;
        });

        var description = new DefaultRecordedWoWTestDescription(
            "Failing Scenario",
            new DelegateBotRunnerFactory(() => foregroundRunner),
            new DelegateBotRunnerFactory(() => backgroundRunner),
            initialDesiredState: initialState,
            baseDesiredState: baseState);

        var result = await description.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(Directory.Exists(context.TestRunDirectory));

        Assert.Equal(1, initialState.ApplyCount);
        Assert.Equal(1, baseState.ApplyCount);
        Assert.True(baseAppliedAfterReset);
        Assert.Equal(1, resetCallsBeforeBase);

        Assert.Equal(1, foregroundRunner.ConnectCallCount);
        Assert.Equal(1, foregroundRunner.PrepareCallCount);
        Assert.Equal(1, foregroundRunner.ResetCallCount); // Cleanup reset only.
        Assert.Equal(1, foregroundRunner.ShutdownCallCount);
        Assert.Equal(1, foregroundRunner.DisconnectCallCount);
        Assert.Equal(1, foregroundRunner.DisposeCallCount);

        Assert.Equal(1, backgroundRunner.ConnectCallCount);
        Assert.Equal(1, backgroundRunner.RunCallCount);
        Assert.Equal(1, backgroundRunner.DisconnectCallCount);
        Assert.Equal(1, backgroundRunner.DisposeCallCount);
    }

    private sealed class FakeBotRunner : IBotRunner
    {
        public FakeBotRunner()
        {
        }

        public int ConnectCallCount { get; private set; }
        public int DisconnectCallCount { get; private set; }
        public int PrepareCallCount { get; private set; }
        public int ResetCallCount { get; private set; }
        public int RunCallCount { get; private set; }
        public int ShutdownCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public Func<ServerInfo, CancellationToken, Task>? OnConnect { get; set; }
        public Func<CancellationToken, Task>? OnDisconnect { get; set; }
        public Func<IRecordedTestContext, CancellationToken, Task>? OnPrepare { get; set; }
        public Func<IRecordedTestContext, CancellationToken, Task>? OnReset { get; set; }
        public Func<IRecordedTestContext, CancellationToken, Task>? OnRun { get; set; }
        public Func<CancellationToken, Task>? OnShutdown { get; set; }

        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken)
        {
            ConnectCallCount++;
            return OnConnect?.Invoke(server, cancellationToken) ?? Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            DisconnectCallCount++;
            return OnDisconnect?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            PrepareCallCount++;
            return OnPrepare?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
        }

        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            ResetCallCount++;
            return OnReset?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
        }

        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new RecordingTarget(RecordingTargetType.Screen, ScreenIndex: 0));
        }

        public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            RunCallCount++;
            return OnRun?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
        }

        public Task ShutdownUiAsync(CancellationToken cancellationToken)
        {
            ShutdownCallCount++;
            return OnShutdown?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CallbackDesiredState : IServerDesiredState
    {
        private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> _callback;

        public CallbackDesiredState(string name, Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> callback)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public string Name { get; }

        public int ApplyCount { get; private set; }

        public Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken)
        {
            ApplyCount++;
            return _callback(gmRunner, context, cancellationToken);
        }
    }

    private sealed class FakeRecordedTestContext : IRecordedTestContext
    {
        private FakeRecordedTestContext(string artifactsRoot, string testName, string sanitizedName)
        {
            ArtifactsRootDirectory = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));
            TestName = testName ?? throw new ArgumentNullException(nameof(testName));
            SanitizedTestName = sanitizedName ?? throw new ArgumentNullException(nameof(sanitizedName));
            TestRootDirectory = Path.Combine(ArtifactsRootDirectory, SanitizedTestName);
            TestRunDirectory = Path.Combine(TestRootDirectory, "run");
            Server = new ServerInfo("localhost", 3724, "TestRealm");
            StartedAt = DateTimeOffset.UtcNow;
        }

        public static FakeRecordedTestContext Create(string artifactsRoot, string? testName = null)
        {
            Directory.CreateDirectory(artifactsRoot);
            testName ??= "Test Scenario";
            var sanitized = ArtifactPathHelper.SanitizeName(testName);
            return new FakeRecordedTestContext(artifactsRoot, testName, sanitized);
        }

        public string TestName { get; }
        public string SanitizedTestName { get; }
        public ServerInfo Server { get; }
        public DateTimeOffset StartedAt { get; }
        public string ArtifactsRootDirectory { get; }
        public string TestRootDirectory { get; }
        public string TestRunDirectory { get; }
    }
}
