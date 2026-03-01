using System;
using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Factories;

namespace WWoW.RecordedTests.Shared.Tests;

public class DelegateBotRunnerFactoryTests
{
    [Fact]
    public void Constructor_NullDelegate_ThrowsArgumentNullException()
    {
        var act = () => new DelegateBotRunnerFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ReturnsResultFromDelegate()
    {
        var mockRunner = Substitute.For<IBotRunner>();
        var factory = new DelegateBotRunnerFactory(() => mockRunner);

        var result = factory.Create();

        result.Should().BeSameAs(mockRunner);
    }

    [Fact]
    public void Create_CalledMultipleTimes_InvokesDelegateEachTime()
    {
        int callCount = 0;
        var factory = new DelegateBotRunnerFactory(() =>
        {
            callCount++;
            return Substitute.For<IBotRunner>();
        });

        factory.Create();
        factory.Create();
        factory.Create();

        callCount.Should().Be(3);
    }
}

public class DelegateScreenRecorderFactoryTests
{
    [Fact]
    public void Constructor_NullDelegate_ThrowsArgumentNullException()
    {
        var act = () => new DelegateScreenRecorderFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ReturnsResultFromDelegate()
    {
        var mockRecorder = Substitute.For<IScreenRecorder>();
        var factory = new DelegateScreenRecorderFactory(() => mockRecorder);

        var result = factory.Create();

        result.Should().BeSameAs(mockRecorder);
    }
}

public class BotRunnerFactoryHelpersTests
{
    [Fact]
    public void FromDelegate_NullDelegate_ThrowsArgumentNullException()
    {
        var act = () => BotRunnerFactoryHelpers.FromDelegate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromDelegate_ValidDelegate_ReturnsFactory()
    {
        var mockRunner = Substitute.For<IBotRunner>();
        var factory = BotRunnerFactoryHelpers.FromDelegate(() => mockRunner);

        factory.Should().NotBeNull();
    }

    [Fact]
    public void FromDelegate_Factory_CreateReturnsDelegateResult()
    {
        var mockRunner = Substitute.For<IBotRunner>();
        var factory = BotRunnerFactoryHelpers.FromDelegate(() => mockRunner);

        var result = factory.Create();

        result.Should().BeSameAs(mockRunner);
    }
}
