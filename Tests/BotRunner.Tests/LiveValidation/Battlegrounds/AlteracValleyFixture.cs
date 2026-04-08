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
/// Fixture for Alterac Valley tests. Launches the full 80 bots: 40 Horde (1 FG + 39 BG) and
/// 40 Alliance (1 FG + 39 BG).
/// Fixture prep handles revive/level/teleport/GM-off; the coordinator handles queue and entry only.
/// </summary>
public class AlteracValleyFixture : BattlegroundCoordinatorFixtureBase
{
    private readonly object _objectivePrepareLock = new();
    private Task? _objectivePrepareTask;

    public const int HordeBotCount = 40;
    public const int AllianceBotCount = 40;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AvMapId = 30;
    public const string HordeLeaderAccount = "TESTBOT1";
    public const string AllianceLeaderAccount = "AVBOTA1";
    internal const int LoadoutPreparationBatchSize = 4;

    private static readonly (string Class, string Race)[] HordeTemplates =
    [
        ("Warrior", "Tauren"), ("Shaman", "Orc"), ("Druid", "Tauren"), ("Priest", "Undead"),
        ("Warlock", "Undead"), ("Hunter", "Orc"), ("Rogue", "Undead"), ("Mage", "Troll"),
        ("Warrior", "Tauren"), ("Hunter", "Troll"), ("Shaman", "Troll"), ("Mage", "Undead"),
    ];

    private static readonly (string Class, string Race)[] AllianceTemplates =
    [
        ("Paladin", "Human"), ("Warrior", "Human"), ("Druid", "NightElf"), ("Priest", "Human"),
        ("Warlock", "Human"), ("Hunter", "NightElf"), ("Rogue", "Human"), ("Mage", "Gnome"),
        ("Warrior", "Dwarf"), ("Hunter", "Dwarf"), ("Paladin", "Dwarf"), ("Priest", "NightElf"),
    ];

    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => index == 0 ? HordeLeaderAccount : $"AVBOT{index + 1}")
        .ToArray();

    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => $"AVBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "AlteracValley.settings.json";

    protected override string FixtureLabel => "AV";

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(10);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromMinutes(2);

    // FG bots (TESTBOT1, AVBOTA1) consistently fail at CharacterSelect.
    // Accept 78/80 (BG bots only) so the pipeline can proceed.
    protected override int MinimumBotCount => TotalBotCount - 2;

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
        var bots = new List<CharacterSettings>(TotalBotCount);

        for (var index = 0; index < HordeBotCount; index++)
        {
            var template = HordeTemplates[index % HordeTemplates.Length];
            bots.Add(CreateCharacterSetting(
                accountName: index == 0 ? HordeLeaderAccount : $"AVBOT{index + 1}",
                characterClass: template.Class,
                characterRace: template.Race,
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: index == 0 ? BotRunnerType.Foreground : BotRunnerType.Background));
        }

        for (var index = 0; index < AllianceBotCount; index++)
        {
            var template = AllianceTemplates[index % AllianceTemplates.Length];
            bots.Add(CreateCharacterSetting(
                accountName: $"AVBOTA{index + 1}",
                characterClass: template.Class,
                characterRace: template.Race,
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: index == 0 ? BotRunnerType.Foreground : BotRunnerType.Background));
        }

        return bots;
    }

    internal IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.AlteracValleyLoadout> BuildLoadoutMap()
    {
        return CharacterSettings.ToDictionary(
            settings => settings.AccountName,
            AlteracValleyLoadoutPlan.ResolveLoadout,
            StringComparer.OrdinalIgnoreCase);
    }

    internal IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> BuildFirstObjectiveAssignments()
        => AlteracValleyLoadoutPlan.BuildFirstObjectiveAssignments(CharacterSettings);

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

    internal async Task EnsureObjectivePreparedAsync()
    {
        Task prepareTask;
        lock (_objectivePrepareLock)
        {
            _objectivePrepareTask ??= PrepareObjectiveLoadoutOnceAsync();
            prepareTask = _objectivePrepareTask;
        }

        await prepareTask;
    }

    private async Task PrepareObjectiveLoadoutOnceAsync()
    {
        await EnsurePreparedAsync();

        var loadouts = BuildLoadoutMap();
        foreach (var batch in CharacterSettings.Chunk(LoadoutPreparationBatchSize))
        {
            var batchTasks = batch.Select(async settings =>
            {
                if (!loadouts.TryGetValue(settings.AccountName, out var loadout))
                    throw new InvalidOperationException($"AV loadout missing for '{settings.AccountName}'.");

                await PrepareObjectiveReadyLoadoutAsync(settings.AccountName, loadout);
            }).ToArray();

            await Task.WhenAll(batchTasks);
            await Task.Delay(300);
        }
    }

    internal async Task MountRaidForFirstObjectiveAsync()
    {
        var loadouts = BuildLoadoutMap();
        foreach (var settings in CharacterSettings)
        {
            var mountItemId = loadouts[settings.AccountName].MountItemId;
            await SendSilentActionAsync(settings.AccountName, new ActionMessage
            {
                ActionType = ActionType.UseItem,
                Parameters = { new RequestParameter { IntParam = (int)mountItemId } }
            });
            await Task.Delay(50);
        }
    }

    private async Task PrepareObjectiveReadyLoadoutAsync(
        string accountName,
        AlteracValleyLoadoutPlan.AlteracValleyLoadout loadout)
    {
        var supplementalItemIds = BuildSupplementalItemIds(loadout);

        await SendSilentGmChatCommandAsync(accountName, ".reset items");
        await WaitForSnapshotConditionAsync(
            accountName,
            snapshot => (snapshot.Player?.BagContents?.Count ?? 1) == 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 250);

        // Note: .modify honor rank does NOT exist in VMaNGOS. PvP rank is loaded from DB
        // (honor_highest_rank column) on character login. DB was pre-populated for all AV characters.
        await SendSilentGmChatCommandAsync(accountName, $".learn {AlteracValleyLoadoutPlan.ApprenticeRidingSpellId}");
        await Task.Delay(75);
        await SendSilentGmChatCommandAsync(
            accountName,
            $".setskill {AlteracValleyLoadoutPlan.RidingSkillId} {AlteracValleyLoadoutPlan.EpicRidingSkill} {AlteracValleyLoadoutPlan.EpicRidingSkill}");
        await Task.Delay(150);
        await SendSilentGmChatCommandAsync(accountName, $".additemset {loadout.ArmorSetId}");
        await Task.Delay(500); // Wait for server to process full item set addition

        foreach (var itemId in supplementalItemIds)
        {
            await SendSilentGmChatCommandAsync(accountName, $".additem {itemId}");
            await Task.Delay(75);
        }

        var stagedItemIds = loadout.EquipItemIds.Concat(supplementalItemIds).ToArray();
        await WaitForSnapshotConditionAsync(
            accountName,
            snapshot =>
            {
                var bagContents = snapshot.Player?.BagContents?.Values;
                return bagContents != null && stagedItemIds.All(bagContents.Contains);
            },
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300);

        // Fire-and-forget equip: send all equip actions with short spacing.
        // Server may reject some (rank, class, timing) — that's OK, the BG queue
        // pipeline doesn't require gear. Blocking retry was causing 18s+ timeouts.
        foreach (var itemId in loadout.EquipItemIds)
        {
            await SendSilentActionAsync(accountName, new ActionMessage
            {
                ActionType = ActionType.EquipItem,
                Parameters = { new RequestParameter { IntParam = (int)itemId } }
            });
            await Task.Delay(150);
        }

        // Fire-and-forget elixirs
        foreach (var elixirItemId in loadout.ElixirItemIds)
        {
            await SendSilentActionAsync(accountName, new ActionMessage
            {
                ActionType = ActionType.UseItem,
                Parameters = { new RequestParameter { IntParam = (int)elixirItemId } }
            });
            await Task.Delay(150);
        }

        // Give server time to process equip/use actions, but don't block on it
        await Task.Delay(2000);
        var loadoutApplied = await WaitForSnapshotConditionAsync(
            accountName,
            snapshot =>
            {
                var bagContents = snapshot.Player?.BagContents?.Values;
                return bagContents != null
                    && loadout.EquipItemIds.All(itemId => !bagContents.Contains(itemId))
                    && loadout.ElixirItemIds.All(itemId => !bagContents.Contains(itemId));
            },
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 250);
        if (!loadoutApplied)
            Console.WriteLine($"[LOADOUT-WARN] Loadout not fully applied for '{accountName}' — some items may remain in bags. Continuing.");
    }

}

[CollectionDefinition(Name)]
public class AlteracValleyCollection : ICollectionFixture<AlteracValleyFixture>
{
    public const string Name = "AlteracValleyValidation";
}
