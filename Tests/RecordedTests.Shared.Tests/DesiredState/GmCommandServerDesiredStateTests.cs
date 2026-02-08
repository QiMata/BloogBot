using FluentAssertions;
using NSubstitute;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Tests.DesiredState;

public class GmCommandServerDesiredStateTests
{
    [Fact]
    public async Task ApplyAsync_WithGmCommandHost_ShouldExecuteCommands()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner, IGmCommandHost>();
        var mockGmHost = (IGmCommandHost)mockRunner;
        mockGmHost.ExecuteGmCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GmCommandExecutionResult.Succeeded);

        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var commands = new[]
        {
            new GmCommandServerDesiredState.GmCommandStep(".teleport name Stormwind", "Teleport"),
            new GmCommandServerDesiredState.GmCommandStep(".character level 10", "Set level")
        };

        var desiredState = new GmCommandServerDesiredState("TestState", commands);

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        await mockGmHost.Received(2).ExecuteGmCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevertAsync_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var commands = new[]
        {
            new GmCommandServerDesiredState.GmCommandStep(".teleport name Stormwind")
        };

        var desiredState = new GmCommandServerDesiredState("TestState", commands);

        // Act & Assert - RevertAsync is a no-op by default
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public void Constructor_WithEmptyCommands_ShouldThrow()
    {
        // Act
        var act = () => new GmCommandServerDesiredState(
            "TestState",
            Array.Empty<GmCommandServerDesiredState.GmCommandStep>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullCommands_ShouldThrow()
    {
        // Act
        var act = () => new GmCommandServerDesiredState(
            "TestState",
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyAsync_WithoutGmCommandHost_ShouldThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>(); // Not an IGmCommandHost
        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var commands = new[]
        {
            new GmCommandServerDesiredState.GmCommandStep(".teleport name Stormwind")
        };

        var desiredState = new GmCommandServerDesiredState("TestState", commands);

        // Act
        var act = async () => await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApplyAndRevert_ShouldBeIdempotent()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner, IGmCommandHost>();
        var mockGmHost = (IGmCommandHost)mockRunner;
        mockGmHost.ExecuteGmCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GmCommandExecutionResult.Succeeded);

        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var commands = new[]
        {
            new GmCommandServerDesiredState.GmCommandStep(".teleport name Stormwind")
        };

        var desiredState = new GmCommandServerDesiredState("TestState", commands);

        // Act - Apply twice
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Revert twice
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert - No exceptions thrown
    }
}
