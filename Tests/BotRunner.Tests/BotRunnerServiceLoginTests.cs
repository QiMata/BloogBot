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

    [Theory]
    [InlineData("ABBOTA14", 0, 1, "ABBOTA14:1")]
    [InlineData("ABBOTA14", 1, 2, "ABBOTA14:3")]
    [InlineData("ABBOTA14", 0, 0, "ABBOTA14")]
    public void BuildCharacterUniquenessSeed_AppliesAttemptOffsetBeforeRetrySuffix(
        string accountName,
        int createAttempts,
        int attemptOffset,
        string expected)
    {
        var seed = BotRunnerService.BuildCharacterUniquenessSeed(accountName, createAttempts, attemptOffset);

        Assert.Equal(expected, seed);
    }

    [Fact]
    public void ResolveCharacterNameAttemptOffset_UsesNonNegativeEnvironmentValue()
    {
        const string variableName = "WWOW_CHARACTER_NAME_ATTEMPT_OFFSET";
        var originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "4");
            Assert.Equal(4, BotRunnerService.ResolveCharacterNameAttemptOffset());

            Environment.SetEnvironmentVariable(variableName, "-1");
            Assert.Equal(0, BotRunnerService.ResolveCharacterNameAttemptOffset());

            Environment.SetEnvironmentVariable(variableName, "invalid");
            Assert.Equal(0, BotRunnerService.ResolveCharacterNameAttemptOffset());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }
}
