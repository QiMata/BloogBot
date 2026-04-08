using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Travel;
using Communication;
using WoWStateManager.Settings;
using BotRunnerType = WoWStateManager.Settings.BotRunnerType;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Warsong Gulch tests. Launches 20 bots: 10 Horde + 10 Alliance (all BG headless).
/// Level 60, PvP gear loadout, elixirs, mount, talents — full combat-ready prep.
/// </summary>
public class WarsongGulchFixture : BattlegroundCoordinatorFixtureBase
{
    private readonly object _loadoutPrepareLock = new();
    private Task? _loadoutPrepareTask;

    public const int HordeBotCount = 10;
    public const int AllianceBotCount = 10;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint WsgMapId = 489;
    public const string HordeLeaderAccount = "WSGBOT1";
    public const string AllianceLeaderAccount = "WSGBOTA1";

    private static readonly (string Class, string Race)[] HordeTemplates =
    [
        ("Warrior", "Orc"), ("Shaman", "Orc"), ("Druid", "Tauren"), ("Priest", "Undead"),
        ("Warlock", "Undead"), ("Hunter", "Orc"), ("Rogue", "Undead"), ("Mage", "Troll"),
        ("Warrior", "Tauren"), ("Shaman", "Troll"),
    ];

    private static readonly (string Class, string Race)[] AllianceTemplates =
    [
        ("Paladin", "Human"), ("Warrior", "Human"), ("Druid", "NightElf"), ("Priest", "Human"),
        ("Warlock", "Human"), ("Hunter", "NightElf"), ("Rogue", "Human"), ("Mage", "Gnome"),
        ("Warrior", "Dwarf"), ("Paladin", "Dwarf"),
    ];

    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => $"WSGBOT{index + 1}")
        .ToArray();

    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => $"WSGBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "WarsongGulch.settings.json";

    protected override string FixtureLabel => "WSG";

    protected override uint BattlegroundTypeId => 2;

    protected override uint BattlegroundMapId => WsgMapId;

    protected override int TargetLevel => 60;

    protected override bool PrepareDuringInitialization => false;

    protected override IReadOnlyCollection<string> HordeAccounts => HordeAccountsOrdered;

    protected override IReadOnlyCollection<string> AllianceAccounts => AllianceAccountsOrdered;

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarWsg.MapId,
        BattlemasterData.OrgrimmarWsg.Position.X,
        BattlemasterData.OrgrimmarWsg.Position.Y,
        BattlemasterData.OrgrimmarWsg.Position.Z);

    // WSG uses group queue (leader queues for the raid).
    // BattlegroundCoordinatorFixtureBase.PrepareBotsAsync handles raid formation + staging.

    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindWsg.MapId,
        BattlemasterData.StormwindWsg.Position.X,
        BattlemasterData.StormwindWsg.Position.Y,
        BattlemasterData.StormwindWsg.Position.Z);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var bots = new List<CharacterSettings>(TotalBotCount);

        for (var index = 0; index < HordeBotCount; index++)
        {
            var template = HordeTemplates[index];
            bots.Add(CreateCharacterSetting(
                accountName: $"WSGBOT{index + 1}",
                characterClass: template.Class,
                characterRace: template.Race,
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: BotRunnerType.Background));
        }

        for (var index = 0; index < AllianceBotCount; index++)
        {
            var template = AllianceTemplates[index];
            bots.Add(CreateCharacterSetting(
                accountName: $"WSGBOTA{index + 1}",
                characterClass: template.Class,
                characterRace: template.Race,
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: BotRunnerType.Background));
        }

        return bots;
    }

    // ---- Loadout prep (reuses AV loadout plan for level 60 PvP gear) ----

    internal async Task EnsureLoadoutPreparedAsync()
    {
        Task prepareTask;
        lock (_loadoutPrepareLock)
        {
            _loadoutPrepareTask ??= PrepareLoadoutsOnceAsync();
            prepareTask = _loadoutPrepareTask;
        }
        await prepareTask;
    }

    private async Task PrepareLoadoutsOnceAsync()
    {
        await EnsurePreparedAsync();

        var loadouts = CharacterSettings.ToDictionary(
            s => s.AccountName,
            AlteracValleyLoadoutPlan.ResolveLoadout,
            StringComparer.OrdinalIgnoreCase);

        // Batch 4 at a time to avoid overwhelming the server
        foreach (var batch in CharacterSettings.Chunk(4))
        {
            var batchTasks = batch.Select(async settings =>
            {
                if (!loadouts.TryGetValue(settings.AccountName, out var loadout))
                    throw new InvalidOperationException($"WSG loadout missing for '{settings.AccountName}'.");
                await PrepareLoadoutAsync(settings.AccountName, loadout);
            }).ToArray();

            await Task.WhenAll(batchTasks);
            await Task.Delay(300);
        }
    }

    private async Task PrepareLoadoutAsync(
        string accountName,
        AlteracValleyLoadoutPlan.AlteracValleyLoadout loadout)
    {
        var supplementalItemIds = AlteracValleyFixture.BuildSupplementalItemIds(loadout);

        await SendSilentGmChatCommandAsync(accountName, ".reset items");
        await WaitForSnapshotConditionAsync(
            accountName,
            snapshot => (snapshot.Player?.BagContents?.Count ?? 1) == 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 250);

        // Learn riding + mount spell
        await SendSilentGmChatCommandAsync(accountName, $".learn {AlteracValleyLoadoutPlan.ApprenticeRidingSpellId}");
        await Task.Delay(75);
        await SendSilentGmChatCommandAsync(
            accountName,
            $".setskill {AlteracValleyLoadoutPlan.RidingSkillId} {AlteracValleyLoadoutPlan.EpicRidingSkill} {AlteracValleyLoadoutPlan.EpicRidingSkill}");
        await Task.Delay(75);
        var mountSpellId = HordeAccountsOrdered.Contains(accountName, StringComparer.OrdinalIgnoreCase) ? 23509 : 23510;
        await SendSilentGmChatCommandAsync(accountName, $".learn {mountSpellId}");
        await Task.Delay(75);

        // Add armor set + supplemental items
        await SendSilentGmChatCommandAsync(accountName, $".additemset {loadout.ArmorSetId}");
        await Task.Delay(500);

        foreach (var itemId in supplementalItemIds)
        {
            await SendSilentGmChatCommandAsync(accountName, $".additem {itemId}");
            await Task.Delay(75);
        }

        // Fire-and-forget equip
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
            Console.WriteLine($"[LOADOUT-WARN] Loadout not fully applied for '{accountName}' — continuing.");
    }

    internal async Task MountAllBotsAsync()
    {
        foreach (var settings in CharacterSettings)
        {
            var isHorde = HordeAccountsOrdered.Contains(settings.AccountName, StringComparer.OrdinalIgnoreCase);
            var mountSpellId = isHorde ? 23509 : 23510;
            await SendSilentGmChatCommandAsync(settings.AccountName, ".gm on");
            await Task.Delay(50);
            await SendSilentGmChatCommandAsync(settings.AccountName, ".targetself");
            await Task.Delay(50);
            await SendSilentGmChatCommandAsync(settings.AccountName, $".cast {mountSpellId}");
            await Task.Delay(50);
        }
    }
}
