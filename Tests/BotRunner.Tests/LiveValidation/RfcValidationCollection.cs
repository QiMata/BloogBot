using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Separate collection for Ragefire Chasm tests — launches StateManager directly
/// with the 10-bot RFC config and coordinator enabled. No default config, no restarts.
/// </summary>
[CollectionDefinition(Name)]
public class RfcValidationCollection : ICollectionFixture<RfcBotFixture>
{
    public const string Name = "RfcValidation";
}
