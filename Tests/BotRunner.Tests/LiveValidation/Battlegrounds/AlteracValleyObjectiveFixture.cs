using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Objective-focused AV fixture. Keeps the roster background-only and allows the
/// live objective tests to proceed once an objective-ready majority of the 80-bot
/// roster is hydrated, instead of waiting for the long-tail world-entry stragglers.
/// </summary>
public class AlteracValleyObjectiveFixture : AlteracValleyFixture
{
    internal const int ObjectiveReadyMinimumBotCount = 60;

    protected override string SettingsFileName => "AlteracValleyObjective.settings.json";

    protected override string FixtureLabel => "AVOBJ";

    protected override bool UseForegroundHordeLeader => false;

    protected override bool UseForegroundAllianceLeader => false;

    protected override int MinimumBotCount => ObjectiveReadyMinimumBotCount;
}

public class AlteracValleyObjectiveCollection : ICollectionFixture<AlteracValleyObjectiveFixture>
{
    public const string Name = "AlteracValleyObjectiveValidation";
}
