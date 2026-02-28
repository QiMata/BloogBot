using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions.I;
using DelegateState = WWoW.RecordedTests.Shared.DesiredState.DelegateServerDesiredState;

namespace WWoW.RecordedTests.Shared.Tests.DesiredState;

public class DelegateServerDesiredStateTests
{
    [Fact]
    public async Task ApplyAsync_WithProvidedDelegate_ShouldInvokeDelegate()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var applyCalled = false;

        var desiredState = new DelegateState(
            name: "TestState",
            applyAsync: (runner, context, ct) =>
            {
                applyCalled = true;
                return Task.CompletedTask;
            });

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        applyCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RevertAsync_WithProvidedDelegate_ShouldInvokeDelegate()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var revertCalled = false;

        var desiredState = new DelegateState(
            name: "TestState",
            revertAsync: (runner, context, ct) =>
            {
                revertCalled = true;
                return Task.CompletedTask;
            });

        // Act
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        revertCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WithNullDelegate_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();

        var desiredState = new DelegateState(name: "TestState", applyAsync: null);

        // Act & Assert
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public async Task RevertAsync_WithNullDelegate_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();

        var desiredState = new DelegateState(name: "TestState", revertAsync: null);

        // Act & Assert
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_ShouldPassCorrectParametersToDelegate()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var cts = new CancellationTokenSource();

        IBotRunner? capturedRunner = null;
        IRecordedTestContext? capturedContext = null;
        CancellationToken capturedToken = default;

        var desiredState = new DelegateState(
            name: "TestState",
            applyAsync: (runner, context, ct) =>
            {
                capturedRunner = runner;
                capturedContext = context;
                capturedToken = ct;
                return Task.CompletedTask;
            });

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, cts.Token);

        // Assert
        capturedRunner.Should().BeSameAs(mockRunner);
        capturedContext.Should().BeSameAs(mockContext);
        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task RevertAsync_ShouldPassCorrectParametersToDelegate()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var cts = new CancellationTokenSource();

        IBotRunner? capturedRunner = null;
        IRecordedTestContext? capturedContext = null;
        CancellationToken capturedToken = default;

        var desiredState = new DelegateState(
            name: "TestState",
            revertAsync: (runner, context, ct) =>
            {
                capturedRunner = runner;
                capturedContext = context;
                capturedToken = ct;
                return Task.CompletedTask;
            });

        // Act
        await desiredState.RevertAsync(mockRunner, mockContext, cts.Token);

        // Assert
        capturedRunner.Should().BeSameAs(mockRunner);
        capturedContext.Should().BeSameAs(mockContext);
        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ApplyAndRevert_WithBothDelegates_ShouldInvokeBoth()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var applyCallCount = 0;
        var revertCallCount = 0;

        var desiredState = new DelegateState(
            name: "TestState",
            applyAsync: (runner, context, ct) =>
            {
                applyCallCount++;
                return Task.CompletedTask;
            },
            revertAsync: (runner, context, ct) =>
            {
                revertCallCount++;
                return Task.CompletedTask;
            });

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        applyCallCount.Should().Be(1);
        revertCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_WithAsyncDelegate_ShouldAwaitCompletion()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var completed = false;

        var desiredState = new DelegateState(
            name: "TestState",
            applyAsync: async (runner, context, ct) =>
            {
                await Task.Delay(50, ct);
                completed = true;
            });

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WithCancellation_ShouldPropagateCancellation()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var desiredState = new DelegateState(
            name: "TestState",
            applyAsync: async (runner, context, ct) =>
            {
                await Task.Delay(1000, ct);
            });

        // Act
        var act = async () => await desiredState.ApplyAsync(mockRunner, mockContext, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
