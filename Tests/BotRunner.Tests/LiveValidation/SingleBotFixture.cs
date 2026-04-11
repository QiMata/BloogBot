using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for live suites that need one background bot and no foreground client.
/// Reuses the lightweight single-account settings so environment tests stay focused.
/// </summary>
public class SingleBotFixture : BgOnlyBotFixture
{
}
