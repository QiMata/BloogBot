namespace BotRunner.Tests.LiveValidation;

public class LiveBotFixtureBotChatTests
{
    [Fact]
    public void GetTrackedChatCommandDelayMs_ExtendsPoolSpawnsCaptureWindow()
    {
        var delayMs = LiveBotFixture.GetTrackedChatCommandDelayMs(".pool spawns 2620", 750);

        Assert.Equal(3500, delayMs);
    }

    [Fact]
    public void GetTrackedChatCommandPostActionTailMs_ExtendsPoolSpawnsTail()
    {
        var tailMs = LiveBotFixture.GetTrackedChatCommandPostActionTailMs(".pool spawns 2620");

        Assert.Equal(2500, tailMs);
    }

    [Fact]
    public void GetTrackedChatCommandDelayMs_ExtendsPoolUpdateCaptureWindow()
    {
        var delayMs = LiveBotFixture.GetTrackedChatCommandDelayMs(".pool update 2620", 750);
        var tailMs = LiveBotFixture.GetTrackedChatCommandPostActionTailMs(".pool update 2620");

        Assert.Equal(2500, delayMs);
        Assert.Equal(1500, tailMs);
    }

    [Fact]
    public void GetTrackedChatCommandDelayMs_LeavesOtherCommandsUnchanged()
    {
        var delayMs = LiveBotFixture.GetTrackedChatCommandDelayMs(".learn 7731", 1000);
        var tailMs = LiveBotFixture.GetTrackedChatCommandPostActionTailMs(".learn 7731");

        Assert.Equal(1000, delayMs);
        Assert.Equal(1000, tailMs);
    }

    [Fact]
    public void GetTrackedChatCommandDelayMs_ExtendsTaxiCheatCaptureWindow()
    {
        var delayMs = LiveBotFixture.GetTrackedChatCommandDelayMs(".taxicheat on", 1000);
        var tailMs = LiveBotFixture.GetTrackedChatCommandPostActionTailMs(".taxicheat on");
        var settleMs = LiveBotFixture.GetTrackedChatCommandResponseSettleMs(".taxicheat on");

        Assert.Equal(4500, delayMs);
        Assert.Equal(2500, tailMs);
        Assert.Equal(1200, settleMs);
    }

    [Theory]
    [InlineData("[SYSTEM] |cffffffff|Hplayer:Thokzugshvrg|h[Thokzugshvrg]|h|r has access to all taxi nodes now (until logout).", true)]
    [InlineData("[CHAT:CHAT_MSG_SYSTEM] : GM mode is OFF", false)]
    [InlineData("[SYSTEM] Cheat: Taxi: Attempt to use unknown node.", false)]
    public void ContainsTaxiNodesGrantedMessage_MatchesOnlyGrantConfirmation(string message, bool expected)
    {
        Assert.Equal(expected, LiveBotFixture.ContainsTaxiNodesGrantedMessage(message));
    }
}
