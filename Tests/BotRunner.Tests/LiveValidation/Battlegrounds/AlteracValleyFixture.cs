using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Travel;
using Communication;
using WoWStateManager.Settings;
using Xunit;
using BotRunnerType = WoWStateManager.Settings.BotRunnerType;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Alterac Valley tests. Launches 80 bots: 2 FG leaders (one per faction)
/// plus 78 BG clients so test runs keep an in-world visual reference while preserving scale.
/// Fixture prep handles revive/level/teleport/GM-off; the coordinator handles queue and entry only.
/// </summary>
public class AlteracValleyFixture : BattlegroundCoordinatorFixtureBase
{
    // Weapon/armor proficiency spells moved to ClassLoadoutSpells (P3.7) and
    // are taught explicitly via CharacterSettings.Loadout.SpellIdsToLearn.

    public const int HordeBotCount = 40;
    public const int AllianceBotCount = 40;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AvMapId = 30;
    public const string HordeLeaderAccount = "AVBOT1";
    public const string AllianceLeaderAccount = "AVBOTA1";
    internal const int LoadoutPreparationBatchSize = 4;

    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => index == 0 ? HordeLeaderAccount : $"AVBOT{index + 1}")
        .ToArray();

    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => $"AVBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "AlteracValley.settings.json";

    protected override string FixtureLabel => "AV";

    protected virtual bool UseForegroundHordeLeader => true;

    protected virtual bool UseForegroundAllianceLeader => true;

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(10);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromMinutes(2);

    // The generic battleground launch throttle starves the back half of the AV
    // roster while the first wave is still progressing through first-login world
    // hydration. Disable it for the 80-bot cold-start fixture.
    protected override int LaunchThrottleActivationBotCountOverride => TotalBotCount + 1;

    protected override int MaxPendingStartupBotsOverride => TotalBotCount + 1;

    // AV requires all configured bots in-world before prep so both FG leaders and the full BG roster participate.
    protected override int MinimumBotCount => TotalBotCount;

    protected override uint BattlegroundTypeId => 1;

    protected override uint BattlegroundMapId => AvMapId;

    protected override int TargetLevel => AlteracValleyLoadoutPlan.TargetLevel;

    protected override bool PrepareDuringInitialization => false;

    protected override IReadOnlyCollection<string> HordeAccounts
        => HordeAccountsOrdered;

    protected override IReadOnlyCollection<string> AllianceAccounts
        => AllianceAccountsOrdered;

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarAv.MapId,
        BattlemasterData.OrgrimmarAv.Position.X,
        BattlemasterData.OrgrimmarAv.Position.Y,
        BattlemasterData.OrgrimmarAv.Position.Z + 3f);

    // Alliance battlemaster is INDOORS (Champion's Hall). Z+3 lands on upper floor (Z≈127).
    // Use Z+0 — the server handles ground placement for indoor teleports.
    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindAv.MapId,
        BattlemasterData.StormwindAv.Position.X,
        BattlemasterData.StormwindAv.Position.Y,
        BattlemasterData.StormwindAv.Position.Z);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var roster = LoadCharacterSettingsFromConfig("AlteracValley.config.json").ToList();

        foreach (var setting in roster)
        {
            if (setting.AccountName.Equals(HordeLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundHordeLeader ? BotRunnerType.Foreground : BotRunnerType.Background;
            else if (setting.AccountName.Equals(AllianceLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundAllianceLeader ? BotRunnerType.Foreground : BotRunnerType.Background;

            // P3.6: stamp the per-bot loadout onto settings so the BattlegroundCoordinator
            // hands it off via ApplyLoadout once each bot reaches the staging area. AV's
            // objective-specific class/proficiency teaching still runs through the legacy
            // PrepareObjectiveReadyLoadoutAsync fixture path for now — the LoadoutSpec
            // schema can't yet express `.learn all_myclass` / `.learn all_myspells`.
            setting.Loadout ??= AlteracValleyLoadoutPlan.BuildLoadoutSpecSettings(setting);
        }

        return roster;
    }

    // BattlegroundCoordinatorFixtureBase.PrepareOfflineAccountStateAsync now handles
    // PvP honor-rank hydration for every battleground fixture. No AV-specific
    // override needed.

    internal IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.AlteracValleyLoadout> BuildLoadoutMap()
    {
        return CharacterSettings.ToDictionary(
            settings => settings.AccountName,
            AlteracValleyLoadoutPlan.ResolveLoadout,
            StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> BuildFirstObjectiveAssignments()
        => AlteracValleyLoadoutPlan.BuildFirstObjectiveAssignments(CharacterSettings);

    internal IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> BuildAdaptiveFirstObjectiveAssignments(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        Action<string>? log = null)
        => AlteracValleyLoadoutPlan.BuildAdaptiveFirstObjectiveAssignments(CharacterSettings, snapshots, log);

    internal static IReadOnlyList<uint> BuildSupplementalItemIds(AlteracValleyLoadoutPlan.AlteracValleyLoadout loadout)
    {
        var supplementalItemIds = new List<uint>(loadout.EquipItemIds.Count + loadout.ElixirItemIds.Count + 1);
        var armorItemIds = new HashSet<uint>(loadout.ArmorItemIds);

        foreach (var itemId in loadout.EquipItemIds)
        {
            if (!armorItemIds.Contains(itemId) && !supplementalItemIds.Contains(itemId))
                supplementalItemIds.Add(itemId);
        }

        if (!supplementalItemIds.Contains(loadout.MountItemId))
            supplementalItemIds.Add(loadout.MountItemId);

        foreach (var elixirItemId in loadout.ElixirItemIds)
        {
            if (!supplementalItemIds.Contains(elixirItemId))
                supplementalItemIds.Add(elixirItemId);
        }

        return supplementalItemIds;
    }

    internal Task<ResponseResult> SetCoordinatorEnabledForObjectivePushAsync(bool enabled)
        => SetCoordinatorEnabledAsync(enabled);

    protected override async Task PrepareBotsAsync()
    {
        await ReviveAndLevelBotsAsync(TargetLevel);

        // AV uses individual queue — VMaNGOS anticheat rejects BG queue from grouped players.
        // Stage bots at battlemasters WITHOUT forming raids. Raids form automatically inside AV.
        await ResetBattlegroundStateAsync(AccountNames.ToList(), "BgResetPreStage");
        await EnsureAccountsStagedAtLocationAsync(HordeAccounts, HordeQueueLocation, "HordeStage");
        await EnsureAccountsStagedAtLocationAsync(AllianceAccounts, AllianceQueueLocation, "AllianceStage");
        await ResetBattlegroundStateAsync(AccountNames.ToList(), "BgResetPostStage");

        foreach (var account in AccountNames)
            await SendGmChatCommandAsync(account, ".gm off");
        await Task.Delay(1000);
    }

    // EnsureObjectivePreparedAsync / PrepareObjectiveLoadoutOnceAsync /
    // PrepareObjectiveReadyLoadoutAsync were removed in P3.7. AV loadout prep now
    // runs through the BattlegroundCoordinator's ApplyingLoadouts state: each
    // bot receives an ActionType.ApplyLoadout with the full LoadoutSpec
    // (explicit per-(class, race) spell IDs, proficiencies, armor set, gear,
    // elixirs) that BotRunner's LoadoutTask executes at its own pace.

    internal async Task MountRaidForFirstObjectiveAsync()
    {
        // Apply the mount aura directly through SOAP so AV prep never toggles runtime GM mode.
        // 23509 = Frostwolf Howler (Horde), 23510 = Stormpike Battle Charger (Alliance)
        await SeedExpectedCharacterNamesFromDatabaseAsync();
        for (var pass = 1; pass <= 3; pass++)
        {
            var snapshots = await QueryAllSnapshotsAsync();
            var snapshotsByAccount = snapshots
                .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
                .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            var pendingAccounts = CharacterSettings
                .Select(settings => new
                {
                    Settings = settings,
                    Snapshot = snapshotsByAccount.GetValueOrDefault(settings.AccountName)
                })
                .Where(candidate =>
                {
                    var snapshot = candidate.Snapshot;
                    if (snapshot == null)
                        return false;

                    var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                    if (mapId != AvMapId)
                        return false;

                    return (snapshot.Player?.Unit?.MountDisplayId ?? 0) == 0;
                })
                .ToArray();

            if (pendingAccounts.Length == 0)
                break;

            foreach (var pending in pendingAccounts)
            {
                var settings = pending.Settings;
                var snapshot = pending.Snapshot!;
                var characterName = snapshot.CharacterName;
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    Console.WriteLine($"[AV] Mount aura skipped for {settings.AccountName}: snapshot character name unavailable.");
                    continue;
                }

                var isHorde = settings.CharacterRace != null && (
                    settings.CharacterRace.Equals("Orc", StringComparison.OrdinalIgnoreCase)
                    || settings.CharacterRace.Equals("Undead", StringComparison.OrdinalIgnoreCase)
                    || settings.CharacterRace.Equals("Tauren", StringComparison.OrdinalIgnoreCase)
                    || settings.CharacterRace.Equals("Troll", StringComparison.OrdinalIgnoreCase));
                var mountSpellId = isHorde ? 23509 : 23510;
                var auraResult = await ExecuteGMCommandAsync($".aura {mountSpellId} {characterName}");
                if (string.IsNullOrWhiteSpace(auraResult) || auraResult.StartsWith("FAULT:", StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"[AV] Mount aura failed for {characterName} ({settings.AccountName}): {auraResult}");

                await Task.Delay(90);
            }

            await Task.Delay(800);
        }
    }

}

[CollectionDefinition(Name)]
public class AlteracValleyCollection : ICollectionFixture<AlteracValleyFixture>
{
    public const string Name = "AlteracValleyValidation";
}

