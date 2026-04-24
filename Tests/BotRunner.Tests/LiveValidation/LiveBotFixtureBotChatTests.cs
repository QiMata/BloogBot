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
    public void GetTrackedChatCommandDelayMs_ExtendsPoolInfoCaptureWindow()
    {
        var delayMs = LiveBotFixture.GetTrackedChatCommandDelayMs(".pool info 2628", 750);
        var tailMs = LiveBotFixture.GetTrackedChatCommandPostActionTailMs(".pool info 2628");
        var settleMs = LiveBotFixture.GetTrackedChatCommandResponseSettleMs(".pool info 2628");

        Assert.Equal(3500, delayMs);
        Assert.Equal(2500, tailMs);
        Assert.Equal(800, settleMs);
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

    [Fact]
    public void TryParseGameObjectTargetLine_ParsesSelectedObjectDetail()
    {
        const string response = "[SYSTEM] Selected object:\n|cffffffff|Hgameobject:19479|h[Floating Debris]|h|r GUID: 19479 ID: 180655\nX: -957.177979 Y: -3778.919922 Z: 0.000000 MapId: 1\nOrientation: 0.000000";

        var parsed = LiveBotFixture.TryParseGameObjectTargetLine(response, out var result);

        Assert.True(parsed);
        Assert.True(result.Found);
        Assert.Equal((uint)19479, result.Guid);
        Assert.Equal((uint)180655, result.Entry);
        Assert.Equal(-957.177979f, result.X);
        Assert.Equal(-3778.919922f, result.Y);
        Assert.Equal(0f, result.Z);
    }

    [Fact]
    public void TryParseGameObjectTargetLine_RejectsNonTargetResponses()
    {
        var parsed = LiveBotFixture.TryParseGameObjectTargetLine(
            "[CHAT:CHAT_MSG_SYSTEM] : Pool #2620: 1 objects spawned [limit = 1]",
            out var result);

        Assert.False(parsed);
        Assert.False(result.Found);
    }

    [Fact]
    public void TryParsePoolInfoChildPoolLine_ParsesActiveChildPool()
    {
        const string response = "[SYSTEM] 2620 - |cffffffff|Hpool:2620|h[Barrens Coast Fishing 14]|h|r AutoSpawn: 1 MaxLimit: 1 Creatures: 0 GameObjecs: 2 Pools 0  [active]";

        var parsed = LiveBotFixture.TryParsePoolInfoChildPoolLine(response, out var result);

        Assert.True(parsed);
        Assert.True(result.Parsed);
        Assert.Equal((uint)2620, result.PoolId);
        Assert.True(result.Active);
    }

    [Fact]
    public void TryParsePoolInfoChildPoolLine_ParsesInactiveChildPool()
    {
        const string response = "[SYSTEM] 2619 - |cffffffff|Hpool:2619|h[Barrens Coast Fishing 13]|h|r AutoSpawn: 1 MaxLimit: 1 Creatures: 0 GameObjecs: 2 Pools 0";

        var parsed = LiveBotFixture.TryParsePoolInfoChildPoolLine(response, out var result);

        Assert.True(parsed);
        Assert.True(result.Parsed);
        Assert.Equal((uint)2619, result.PoolId);
        Assert.False(result.Active);
    }
}
