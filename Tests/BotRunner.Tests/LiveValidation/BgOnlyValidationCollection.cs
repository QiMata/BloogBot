using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Collection for BG-only live suites backed by a single headless bot.
/// </summary>
[CollectionDefinition(Name)]
public class BgOnlyValidationCollection : ICollectionFixture<BgOnlyBotFixture>
{
    public const string Name = "BgOnlyValidation";
}
