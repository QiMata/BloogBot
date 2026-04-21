using FluentAssertions;
using NSubstitute;
using RecordedTests.PathingTests.Configuration;
using RecordedTests.PathingTests.Runners;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.PathingTests.Tests;

public class ForegroundRecordedTestRunnerTests
{
    [Fact]
    public async Task GetRecordingTargetAsync_ConfigWindowTitleOverridesEnvironment()
    {
        // Arrange
        var runner = CreateRunner(new TestConfiguration { WowWindowTitle = "Config WoW" });

        using var _ = WithRecordingTargetEnvironment(processId: "1234");

        // Act
        var target = await runner.GetRecordingTargetAsync(CancellationToken.None);

        // Assert
        target.TargetType.Should().Be(RecordingTargetType.WindowByTitle);
        target.WindowTitle.Should().Be("Config WoW");
        target.ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordingTargetAsync_ConfigProcessIdOverridesEnvironmentTitle_WhenConfigTitleMissing()
    {
        // Arrange
        var runner = CreateRunner(new TestConfiguration { WowProcessId = 5678 });

        using var _ = WithRecordingTargetEnvironment(windowTitle: "Env WoW");

        // Act
        var target = await runner.GetRecordingTargetAsync(CancellationToken.None);

        // Assert
        target.TargetType.Should().Be(RecordingTargetType.ProcessId);
        target.ProcessId.Should().Be(5678);
        target.WindowTitle.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordingTargetAsync_EnvironmentUsesTitleBeforeProcessIdBeforeHandle()
    {
        // Arrange
        var runner = CreateRunner();

        using var _ = WithRecordingTargetEnvironment(
            windowTitle: "Env WoW",
            processId: "1234",
            windowHandle: "5678");

        // Act
        var target = await runner.GetRecordingTargetAsync(CancellationToken.None);

        // Assert
        target.TargetType.Should().Be(RecordingTargetType.WindowByTitle);
        target.WindowTitle.Should().Be("Env WoW");
        target.ProcessId.Should().BeNull();
        target.WindowHandle.Should().BeNull();
    }

    [Fact]
    public async Task DisconnectAsync_DisconnectsWorldAuthAndDisposesInOrder()
    {
        // Arrange
        var order = new List<string>();
        var runner = CreateRunner();
        runner.DisconnectWorldOverride = _ =>
        {
            order.Add("world");
            return Task.CompletedTask;
        };
        runner.DisconnectAuthOverride = _ =>
        {
            order.Add("auth");
            return Task.CompletedTask;
        };
        runner.DisposeOrchestratorOverride = () => order.Add("orchestrator");

        // Act
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        order.Should().Equal("world", "auth", "orchestrator");
    }

    [Fact]
    public async Task DisconnectAsync_IsIdempotent()
    {
        // Arrange
        var calls = 0;
        var runner = CreateRunner();
        runner.DisconnectWorldOverride = _ =>
        {
            calls++;
            return Task.CompletedTask;
        };
        runner.DisconnectAuthOverride = _ =>
        {
            calls++;
            return Task.CompletedTask;
        };
        runner.DisposeOrchestratorOverride = () => calls++;

        // Act
        await runner.DisconnectAsync(CancellationToken.None);
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        calls.Should().Be(3);
    }

    [Fact]
    public async Task DisconnectAsync_DisposesOrchestrator_WhenWorldDisconnectFails()
    {
        // Arrange
        var order = new List<string>();
        var runner = CreateRunner();
        runner.DisconnectWorldOverride = _ =>
        {
            order.Add("world");
            throw new InvalidOperationException("world failed");
        };
        runner.DisconnectAuthOverride = _ =>
        {
            order.Add("auth");
            return Task.CompletedTask;
        };
        runner.DisposeOrchestratorOverride = () => order.Add("orchestrator");

        // Act
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        order.Should().Equal("world", "auth", "orchestrator");
    }

    private static ForegroundRecordedTestRunner CreateRunner(TestConfiguration? config = null)
        => new("GM", "PASSWORD", "Gmchar", Substitute.For<ITestLogger>(), config);

    private static EnvironmentScope WithRecordingTargetEnvironment(
        string? windowTitle = null,
        string? processId = null,
        string? windowHandle = null)
        => new(
            ("WWOW_WOW_WINDOW_TITLE", windowTitle),
            ("WWOW_WOW_PROCESS_ID", processId),
            ("WWOW_WOW_WINDOW_HANDLE", windowHandle));

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly (string Key, string? Value)[] _previous;

        public EnvironmentScope(params (string Key, string? Value)[] values)
        {
            _previous = values
                .Select(value => (value.Key, Environment.GetEnvironmentVariable(value.Key)))
                .ToArray();

            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
