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
/// Fixture for Warsong Gulch tests. Launches 20 bots: 10 Horde (1 FG + 9 BG) and
/// 10 Alliance BG clients.
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

    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => index == 0 ? HordeLeaderAccount : $"WSGBOT{index + 1}")
        .ToArray();

    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => index == 0 ? AllianceLeaderAccount : $"WSGBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "WarsongGulch.settings.json";

    protected override string FixtureLabel => "WSG";

    protected virtual bool UseForegroundHordeLeader => true;

    protected virtual bool UseForegroundAllianceLeader => false;

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(10);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromMinutes(3);

    protected override int LaunchThrottleActivationBotCountOverride => 2;

    protected override int MaxPendingStartupBotsOverride => 6;

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

    // WSG uses faction group queue.
    // Prep only stages each faction at the battlemasters; StateManager/BotRunner reconcile grouping.

    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindWsg.MapId,
        BattlemasterData.StormwindWsg.Position.X,
        BattlemasterData.StormwindWsg.Position.Y,
        BattlemasterData.StormwindWsg.Position.Z);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var roster = LoadCharacterSettingsFromConfig("WarsongGulch.config.json").ToList();

        foreach (var setting in roster)
        {
            if (setting.AccountName.Equals(HordeLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundHordeLeader ? BotRunnerType.Foreground : BotRunnerType.Background;
            else if (setting.AccountName.Equals(AllianceLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundAllianceLeader ? BotRunnerType.Foreground : BotRunnerType.Background;

            // P3.6: stamp the per-bot loadout onto settings so the BattlegroundCoordinator
            // hands it off via ApplyLoadout once each bot reaches the staging area. The
            // legacy fixture-driven prep helpers below remain in place as a safety net
            // until live validation confirms the hand-off path covers every BG fixture.
            setting.Loadout ??= AlteracValleyLoadoutPlan.BuildLoadoutSpecSettings(setting);
        }

        return roster;
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
        await RunLoadoutPrepAsync();
    }

    /// <summary>
    /// Runs the per-account loadout work (gear, riding, mount, elixirs) without first
    /// calling <see cref="CoordinatorFixtureBase.EnsurePreparedAsync"/>, so it is safe
    /// to invoke from <c>AfterPrepareAsync</c> (which already runs inside the base
    /// prep task and would otherwise deadlock on the prep-task lock).
    /// </summary>
    protected async Task RunLoadoutPrepAsync()
    {
        await SetCoordinatorEnabledAsync(false);

        try
        {
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
        finally
        {
            await SetCoordinatorEnabledAsync(true);
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
        {
            var snapshot = await GetSnapshotAsync(accountName);
            var bagItemIds = snapshot?.Player?.BagContents?.Values?.ToArray() ?? Array.Empty<uint>();
            var remainingEquip = loadout.EquipItemIds.Where(itemId => bagItemIds.Contains(itemId)).Distinct().ToArray();
            var remainingElixirs = loadout.ElixirItemIds.Where(itemId => bagItemIds.Contains(itemId)).Distinct().ToArray();
            var bagPreview = bagItemIds.Take(12).ToArray();
            Console.WriteLine(
                $"[LOADOUT-WARN] Loadout not fully applied for '{accountName}' - " +
                $"remainingEquip=[{string.Join(",", remainingEquip)}], " +
                $"remainingElixirs=[{string.Join(",", remainingElixirs)}], " +
                $"bagCount={bagItemIds.Length}, bagPreview=[{string.Join(",", bagPreview)}]. Continuing.");
        }
    }

    internal Task<ResponseResult> SetRuntimeCoordinatorEnabledAsync(bool enabled)
        => SetCoordinatorEnabledAsync(enabled);

    internal Task ResetTrackedBattlegroundStateAsync(string label)
        => ResetBattlegroundStateAsync(AccountNames, label);

    internal async Task AttemptMountSpellsAsync(IEnumerable<string> accountNames)
    {
        foreach (var accountName in accountNames
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mountSpellId = HordeAccountsOrdered.Contains(accountName, StringComparer.OrdinalIgnoreCase) ? 23509 : 23510;
            var result = await SendSilentActionAsync(accountName, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = mountSpellId } }
            });
            if (result != ResponseResult.Success)
            {
                throw new InvalidOperationException(
                    $"WSG mount spell dispatch failed for '{accountName}' (spell={mountSpellId}, result={result}).");
            }

            await Task.Delay(100);
        }
    }
}

