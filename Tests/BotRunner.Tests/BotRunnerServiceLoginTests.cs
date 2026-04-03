namespace BotRunner.Tests;

public class BotRunnerServiceLoginTests
{
    [Theory]
    [InlineData("ABBOTA14", 0, "ABBOTA14")]
    [InlineData("ABBOTA14", 1, "ABBOTA14:1")]
    [InlineData("ABBOTA14", 2, "ABBOTA14:2")]
    public void BuildCharacterUniquenessSeed_AppendsRetrySuffixAfterFirstAttempt(
        string accountName,
        int createAttempts,
        string expected)
    {
        var seed = BotRunnerService.BuildCharacterUniquenessSeed(accountName, createAttempts);

        Assert.Equal(expected, seed);
    }

    [Fact]
    public void BuildCharacterUniquenessSeed_KeepsBlankSeedBlank()
    {
        Assert.Null(BotRunnerService.BuildCharacterUniquenessSeed(null, 3));
        Assert.Equal(string.Empty, BotRunnerService.BuildCharacterUniquenessSeed(string.Empty, 3));
    }
}
