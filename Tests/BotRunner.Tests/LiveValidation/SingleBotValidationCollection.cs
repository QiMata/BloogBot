using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Collection for live suites backed by a single headless background bot.
/// </summary>
[CollectionDefinition(Name)]
public class SingleBotValidationCollection : ICollectionFixture<SingleBotFixture>
{
    public const string Name = "SingleBotValidation";
}
