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
}
