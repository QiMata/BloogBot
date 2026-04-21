using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Travel;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Arathi Basin tests. Launches 20 background bots: 10 Horde and
/// 10 Alliance.
/// Fixture prep handles revive/level/teleport/GM-off; StateManager/BotRunner handle faction grouping, queue, and entry.
/// </summary>
public class ArathiBasinFixture : BattlegroundCoordinatorFixtureBase
{
    public const int HordeBotCount = 10;
    public const int AllianceBotCount = 10;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AbMapId = 529;
    public const string HordeLeaderAccount = "ABBOT1";
    public const string AllianceLeaderAccount = "ABBOTA1";
    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => $"ABBOT{index + 1}")
        .ToArray();
    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => $"ABBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "ArathiBasin.settings.json";

    protected override string FixtureLabel => "AB";

    // Foreground battleground transfers are still unstable for this fixture even
    // with packet hooks disabled, so keep the full AB roster on the BG runner path.
    protected virtual bool UseForegroundHordeLeader => false;

    protected virtual bool UseForegroundAllianceLeader => false;

    // Historical AB live evidence shows the 20-bot cold start can take well over
    // the old 8-minute window before the full roster hydrates into world snapshots.
    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(12);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromMinutes(4);

    // The generic launch throttle blocks the back half of the AB roster while the
    // first wave is still at CharacterSelect. Disable it for the 20-bot AB fixture.
    protected override int LaunchThrottleActivationBotCountOverride => TotalBotCount + 1;

    protected override int MaxPendingStartupBotsOverride => TotalBotCount + 1;

    protected override uint BattlegroundTypeId => 3;

    protected override uint BattlegroundMapId => AbMapId;

    protected override int TargetLevel => BattlemasterData.GetMinimumLevel(BattlemasterData.BattlegroundType.ArathiBasin);

    protected override IReadOnlyCollection<string> HordeAccounts
        => HordeAccountsOrdered;

    protected override IReadOnlyCollection<string> AllianceAccounts
        => AllianceAccountsOrdered;

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarAb.MapId,
        BattlemasterData.OrgrimmarAb.Position.X,
        BattlemasterData.OrgrimmarAb.Position.Y,
        BattlemasterData.OrgrimmarAb.Position.Z + 3f);

    // Alliance AB battlemaster is indoors in Champion's Hall.
    // Z+3 lands on the upper floor (Z≈127), which strands bots above Lady Hoteshem.
    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindAb.MapId,
        BattlemasterData.StormwindAb.Position.X,
        BattlemasterData.StormwindAb.Position.Y,
        BattlemasterData.StormwindAb.Position.Z);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var roster = LoadCharacterSettingsFromConfig("ArathiBasin.config.json").ToList();

        foreach (var setting in roster)
        {
            if (setting.AccountName.Equals(HordeLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundHordeLeader ? BotRunnerType.Foreground : BotRunnerType.Background;
            else if (setting.AccountName.Equals(AllianceLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundAllianceLeader ? BotRunnerType.Foreground : BotRunnerType.Background;
        }

        return roster;
    }

    internal Task<Communication.ResponseResult> SetRuntimeCoordinatorEnabledAsync(bool enabled)
        => SetCoordinatorEnabledAsync(enabled);
}

[CollectionDefinition(Name)]
public class ArathiBasinCollection : ICollectionFixture<ArathiBasinFixture>
{
    public const string Name = "ArathiBasinValidation";
}
