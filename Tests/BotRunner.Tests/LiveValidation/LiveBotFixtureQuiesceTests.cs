using Communication;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public sealed class LiveBotFixtureQuiesceTests
{
    [Fact]
    public void DescribeBlockingActionState_NullSnapshot_DoesNotBlock()
    {
        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", null);
        Assert.Null(result);
    }

    [Fact]
    public void DescribeBlockingActionState_DisconnectedBot_DoesNotBlock()
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ACC1",
            ScreenState = "LoginScreen",
            ConnectionState = BotConnectionState.BotDisconnected,
            IsMapTransition = true,
        };

        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", snapshot);
        Assert.Null(result);
    }

    [Fact]
    public void DescribeBlockingActionState_InWorldWithWaitAction_DoesNotBlock()
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ACC1",
            ScreenState = "InWorld",
            ConnectionState = BotConnectionState.BotInWorld,
            CurrentAction = new ActionMessage { ActionType = ActionType.Wait },
        };

        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", snapshot);
        Assert.Null(result);
    }

    [Fact]
    public void DescribeBlockingActionState_InWorldWithActiveGoto_Blocks()
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ACC1",
            ScreenState = "InWorld",
            ConnectionState = BotConnectionState.BotInWorld,
            CurrentAction = new ActionMessage { ActionType = ActionType.Goto },
        };

        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", snapshot);
        Assert.NotNull(result);
        Assert.Contains("current=Goto", result);
    }

    [Fact]
    public void DescribeBlockingActionState_MapTransition_Blocks()
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ACC1",
            ScreenState = "InWorld",
            ConnectionState = BotConnectionState.BotInWorld,
            IsMapTransition = true,
        };

        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", snapshot);
        Assert.NotNull(result);
        Assert.Contains("transition=True", result);
    }

    [Fact]
    public void DescribeBlockingActionState_EnteringWorldButNotYetInWorld_Blocks()
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ACC1",
            ScreenState = "CharacterSelect",
            ConnectionState = BotConnectionState.BotEnteringWorld,
        };

        var result = LiveBotFixture.DescribeBlockingActionState("ACC1", snapshot);
        Assert.NotNull(result);
        Assert.Contains("conn=BotEnteringWorld", result);
    }
}
