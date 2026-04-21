using BotRunner.Clients;
using FluentAssertions;
using GameData.Core.Models;
using NSubstitute;
using RecordedTests.PathingTests.Models;
using RecordedTests.PathingTests.Runners;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.PathingTests.Tests;

public class BackgroundRecordedTestRunnerTests
{
    [Fact]
    public async Task RunTestAsync_Timeout_ThrowsTimeoutExceptionAndStopsMovement()
    {
        // Arrange
        var stopCalls = 0;
        var runner = CreateRunner(CreatePathingTest(TimeSpan.FromMilliseconds(10)));
        runner.DelayAsync = (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token);
        runner.StopMovementOverride = () => stopCalls++;

        // Act
        var act = () => runner.RunTestAsync(Substitute.For<IRecordedTestContext>(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*maximum duration*");
        stopCalls.Should().Be(1);
    }

    [Fact]
    public async Task RunTestAsync_NavigationFailure_PreservesExceptionAndStopsMovement()
    {
        // Arrange
        var stopCalls = 0;
        var runner = CreateRunner(CreatePathingTest(TimeSpan.FromSeconds(30)));
        runner.DelayAsync = (_, _) => Task.CompletedTask;
        runner.NavigateToDestinationOverride = (_, _, _) => throw new InvalidOperationException("navigation failed");
        runner.StopMovementOverride = () => stopCalls++;

        // Act
        var act = () => runner.RunTestAsync(Substitute.For<IRecordedTestContext>(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("navigation failed");
        stopCalls.Should().Be(1);
    }

    [Fact]
    public async Task RunTestAsync_SuccessfulNavigation_ValidatesPositionAndStopsMovement()
    {
        // Arrange
        var stopCalls = 0;
        var definition = CreatePathingTest(TimeSpan.FromSeconds(30));
        var runner = CreateRunner(definition);
        runner.DelayAsync = (_, _) => Task.CompletedTask;
        runner.NavigateToDestinationOverride = (_, _, _) => Task.CompletedTask;
        runner.CurrentPositionOverride = () => definition.EndPosition!;
        runner.CurrentMapIdOverride = () => definition.EndMapId ?? definition.MapId;
        runner.StopMovementOverride = () => stopCalls++;

        // Act
        await runner.RunTestAsync(Substitute.For<IRecordedTestContext>(), CancellationToken.None);

        // Assert
        stopCalls.Should().Be(1);
    }

    [Fact]
    public async Task DisconnectAsync_StopsGameLoopBeforeDisconnectingAndDisposing()
    {
        // Arrange
        var order = new List<string>();
        var runner = CreateRunner(CreatePathingTest(TimeSpan.FromSeconds(30)));
        runner.StopGameLoopOverride = () => order.Add("stop-loop");
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
        runner.DisposeWowClientOverride = () => order.Add("wow-client");

        // Act
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        order.Should().Equal("stop-loop", "world", "auth", "orchestrator", "wow-client");
    }

    [Fact]
    public async Task DisconnectAsync_IsIdempotent()
    {
        // Arrange
        var calls = 0;
        var runner = CreateRunner(CreatePathingTest(TimeSpan.FromSeconds(30)));
        runner.StopGameLoopOverride = () => calls++;
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
        runner.DisposeWowClientOverride = () => calls++;

        // Act
        await runner.DisconnectAsync(CancellationToken.None);
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        calls.Should().Be(5);
    }

    [Fact]
    public async Task DisconnectAsync_DisposesRemainingResources_WhenWorldDisconnectFails()
    {
        // Arrange
        var order = new List<string>();
        var runner = CreateRunner(CreatePathingTest(TimeSpan.FromSeconds(30)));
        runner.StopGameLoopOverride = () => order.Add("stop-loop");
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
        runner.DisposeWowClientOverride = () => order.Add("wow-client");

        // Act
        await runner.DisconnectAsync(CancellationToken.None);

        // Assert
        order.Should().Equal("stop-loop", "world", "auth", "orchestrator", "wow-client");
    }

    private static BackgroundRecordedTestRunner CreateRunner(PathingTestDefinition definition)
        => new(
            definition,
            new PathfindingClient(),
            "TEST",
            "PASSWORD",
            "Tester",
            Substitute.For<ITestLogger>());

    private static PathingTestDefinition CreatePathingTest(TimeSpan expectedDuration)
        => new(
            "TimeoutTest",
            "Unit",
            "test",
            1,
            new Position(0, 0, 0),
            new Position(10, 0, 0),
            Array.Empty<string>(),
            Array.Empty<string>(),
            expectedDuration);
}
