using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Factories;

namespace WWoW.RecordedTests.Shared.Tests.Factories;

public class BotRunnerFactoryHelpersTests
{
    private class TestBotRunner : IBotRunner
    {
        public int InstanceId { get; }

        public TestBotRunner()
        {
            InstanceId = Random.Shared.Next();
        }

        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new RecordingTarget(RecordingTargetType.Screen));
        public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ShutdownUiAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void FromDelegate_WithNullDelegate_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => BotRunnerFactoryHelpers.FromDelegate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromDelegate_WithValidDelegate_ShouldCreateFactory()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        Func<IBotRunner> factoryFunc = () => mockRunner;

        // Act
        var factory = BotRunnerFactoryHelpers.FromDelegate(factoryFunc);

        // Assert
        factory.Should().NotBeNull();
        factory.Should().BeAssignableTo<IBotRunnerFactory>();
    }

    [Fact]
    public void FromDelegate_WhenFactoryCreates_ShouldReturnBotRunner()
    {
        // Arrange
        var mockRunner = Substitute.For<IBotRunner>();
        var factory = BotRunnerFactoryHelpers.FromDelegate(() => mockRunner);

        // Act
        var runner = factory.Create();

        // Assert
        runner.Should().BeSameAs(mockRunner);
    }

    [Fact]
    public void FromDelegate_CalledMultipleTimes_ShouldInvokeDelegateEachTime()
    {
        // Arrange
        var callCount = 0;
        var factory = BotRunnerFactoryHelpers.FromDelegate(() =>
        {
            callCount++;
            return Substitute.For<IBotRunner>();
        });

        // Act
        var runner1 = factory.Create();
        var runner2 = factory.Create();

        // Assert
        callCount.Should().Be(2);
        runner1.Should().NotBeSameAs(runner2);
    }

    [Fact]
    public void FromType_ShouldCreateFactory()
    {
        // Act
        var factory = BotRunnerFactoryHelpers.FromType<TestBotRunner>();

        // Assert
        factory.Should().NotBeNull();
        factory.Should().BeAssignableTo<IBotRunnerFactory>();
    }

    [Fact]
    public void FromType_WhenFactoryCreates_ShouldReturnNewInstance()
    {
        // Arrange
        var factory = BotRunnerFactoryHelpers.FromType<TestBotRunner>();

        // Act
        var runner = factory.Create();

        // Assert
        runner.Should().NotBeNull();
        runner.Should().BeOfType<TestBotRunner>();
    }

    [Fact]
    public void FromType_CalledMultipleTimes_ShouldCreateNewInstancesEachTime()
    {
        // Arrange
        var factory = BotRunnerFactoryHelpers.FromType<TestBotRunner>();

        // Act
        var runner1 = factory.Create() as TestBotRunner;
        var runner2 = factory.Create() as TestBotRunner;

        // Assert
        runner1.Should().NotBeNull();
        runner2.Should().NotBeNull();
        runner1!.InstanceId.Should().NotBe(runner2!.InstanceId);
    }

    [Fact]
    public void FromType_WithDifferentTypes_ShouldCreateDifferentFactories()
    {
        // Act
        var factory1 = BotRunnerFactoryHelpers.FromType<TestBotRunner>();
        var factory2 = BotRunnerFactoryHelpers.FromType<TestBotRunner>();

        // Assert
        factory1.Should().NotBeSameAs(factory2); // Different factory instances
    }
}
