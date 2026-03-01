using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.States;

public sealed class BotActivityTests
{
    [Fact]
    public void AllValues_AreDefined()
    {
        var values = Enum.GetValues<BotActivity>();

        Assert.Contains(BotActivity.Resting, values);
        Assert.Contains(BotActivity.Combat, values);
        Assert.Contains(BotActivity.Grinding, values);
        Assert.Contains(BotActivity.Questing, values);
        Assert.Contains(BotActivity.Battlegrounding, values);
        Assert.Contains(BotActivity.Dungeoning, values);
        Assert.Contains(BotActivity.Raiding, values);
        Assert.Contains(BotActivity.Trading, values);
        Assert.Contains(BotActivity.Mailing, values);
        Assert.Contains(BotActivity.Auction, values);
    }

    [Fact]
    public void TotalCount_Is26()
    {
        Assert.Equal(26, Enum.GetValues<BotActivity>().Length);
    }
}
