using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Collection definition that shares a single LiveBotFixture across all live validation tests.
/// This avoids multiple login attempts to the MaNGOS server (which rejects duplicate sessions).
/// All test classes in this collection share the same connected bot.
/// </summary>
[CollectionDefinition(Name)]
public class LiveValidationCollection : ICollectionFixture<LiveBotFixture>
{
    public const string Name = "LiveValidation";
}
