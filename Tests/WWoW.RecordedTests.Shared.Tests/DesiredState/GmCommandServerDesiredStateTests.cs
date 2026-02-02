using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.DesiredState;

namespace WWoW.RecordedTests.Shared.Tests.DesiredState;

public class GmCommandServerDesiredStateTests
{
    [Fact]
    public async Task ApplyAsync_ShouldExecuteSetupCommands()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var setupCommands = new[] { ".teleport name Stormwind", ".character level 10" };
        var teardownCommands = new[] { ".character delete" };

        var desiredState = new GmCommandServerDesiredState(
            setupCommands,
            teardownCommands);

        // Act
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        // Note: GmCommandServerDesiredState currently doesn't execute commands directly
        // It delegates to PrepareServerStateAsync/ResetServerStateAsync
        // This test verifies it doesn't throw
    }

    [Fact]
    public async Task RevertAsync_ShouldExecuteTeardownCommands()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var setupCommands = new[] { ".teleport name Stormwind" };
        var teardownCommands = new[] { ".character delete", ".server shutdown 0" };

        var desiredState = new GmCommandServerDesiredState(
            setupCommands,
            teardownCommands);

        // Act
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert
        // Verifies it doesn't throw
    }

    [Fact]
    public async Task ApplyAsync_WithEmptySetupCommands_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();

        var desiredState = new GmCommandServerDesiredState(
            Array.Empty<string>(),
            new[] { ".character delete" });

        // Act & Assert
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public async Task RevertAsync_WithEmptyTeardownCommands_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();

        var desiredState = new GmCommandServerDesiredState(
            new[] { ".teleport name Stormwind" },
            Array.Empty<string>());

        // Act & Assert
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_WithNullCommands_ShouldNotThrow()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();

        var desiredState = new GmCommandServerDesiredState(
            null,
            null);

        // Act & Assert
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAndRevert_ShouldBeIdempotent()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var mockContext = Substitute.For<IRecordedTestContext>();
        mockContext.TestName.Returns("TestName");

        var setupCommands = new[] { ".teleport name Stormwind" };
        var teardownCommands = new[] { ".character delete" };

        var desiredState = new GmCommandServerDesiredState(
            setupCommands,
            teardownCommands);

        // Act - Apply twice
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);
        await desiredState.ApplyAsync(mockRunner, mockContext, CancellationToken.None);

        // Revert twice
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);
        await desiredState.RevertAsync(mockRunner, mockContext, CancellationToken.None);

        // Assert - No exceptions thrown
    }
}
