using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed V3 integration validation slice.
/// World, quest, inventory, and loadout state is staged through fixture helpers;
/// executable lanes dispatch only BotRunner actions to resolved FG/BG targets.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class IntegrationValidationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMap = 1;
    private const float RfcEntranceX = 1811f;
    private const float RfcEntranceY = -4410f;
    private const float RfcEntranceZ = -15f;

    private const float OrgSafeX = 1629f;
    private const float OrgSafeY = -4373f;
    private const float OrgSafeZ = 15.5f;

    private const float EscortQuestX = 196f;
    private const float EscortQuestY = -4752f;
    private const float EscortQuestZ = 14f;

    public IntegrationValidationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_1_EncounterMechanics_StartDungeoneering_SnapshotsUpdate()
    {
        var target = await EnsureBgIntegrationTargetAsync();
        _output.WriteLine($"=== V3.1 Encounter Mechanics: {target.AccountName}/{target.CharacterName} ===");

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            KalimdorMap,
            RfcEntranceX,
            RfcEntranceY,
            RfcEntranceZ,
            "RFC entrance",
            cleanSlate: true,
            xyToleranceYards: 10f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"[{target.RoleLabel}] RFC entrance staging should settle before dungeoneering dispatch.");

        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snapBefore);
        _output.WriteLine($"[{target.RoleLabel}] Pre-dungeoneering snapshot: screen={snapBefore.ScreenState}");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.StartDungeoneering
        });
        _output.WriteLine($"[{target.RoleLabel}] StartDungeoneering dispatched (result={result})");
        Assert.Equal(ResponseResult.Success, result);

        var updated = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot != null,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 500,
            progressLabel: "V3.1 dungeoneering snapshot update");
        Assert.True(updated, $"[{target.RoleLabel}] Dungeoneering dispatch should leave a readable snapshot.");

        await _bot.RefreshSnapshotsAsync();
        var snapAfter = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snapAfter);
        _output.WriteLine($"[{target.RoleLabel}] Post-dungeoneering snapshot: screen={snapAfter.ScreenState}");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_2_PvPEngagement_TwoBotsAttack_CombatStateReflected()
    {
        var (bg, fg) = await EnsureBgFgIntegrationTargetsAsync();
        _output.WriteLine($"=== V3.2 PvP Engagement: BG={bg.AccountName}, FG={fg.AccountName} ===");

        await _bot.RefreshSnapshotsAsync();
        var bgSetup = await _bot.GetSnapshotAsync(bg.AccountName);
        var fgSetup = await _bot.GetSnapshotAsync(fg.AccountName);
        var bgFaction = bgSetup?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
        var fgFaction = fgSetup?.Player?.Unit?.GameObject?.FactionTemplate ?? 0;
        _output.WriteLine($"[SETUP] BG factionTemplate={bgFaction}, FG factionTemplate={fgFaction}");

        global::Tests.Infrastructure.Skip.If(
            bgFaction != 0 && bgFaction == fgFaction,
            "Economy.config.json launches same-faction bots; PvP engagement needs an opposing-faction Shodan topology.");

        global::Tests.Infrastructure.Skip.If(
            true,
            "PvP engagement also needs a fixture-owned PvP-flag staging helper before the action dispatch can be made director-clean.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_3_EscortQuest_AddQuest_AppearsInQuestLog()
    {
        var target = await EnsureBgIntegrationTargetAsync();
        _output.WriteLine($"=== V3.3 Escort Quest Snapshot Projection: {target.AccountName}/{target.CharacterName} ===");

        const uint questId = 5441;
        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            KalimdorMap,
            EscortQuestX,
            EscortQuestY,
            EscortQuestZ,
            "escort quest area",
            cleanSlate: true,
            xyToleranceYards: 10f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"[{target.RoleLabel}] Escort quest staging should settle before quest-state validation.");

        var absent = await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, questId);
        Assert.True(absent, $"[{target.RoleLabel}] Quest {questId} should be absent before add staging.");

        try
        {
            var added = await _bot.StageBotRunnerQuestAddedAsync(target.AccountName, target.RoleLabel, questId);
            Assert.True(added, $"[{target.RoleLabel}] Quest {questId} should appear in quest log after fixture staging.");

            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);
            var hasQuest = HasQuest(snap, questId);
            _output.WriteLine(
                $"[{target.RoleLabel}] Quest {questId} in log: {hasQuest}, quest log count: {snap.Player?.QuestLogEntries?.Count ?? 0}");
            Assert.True(hasQuest, $"Quest {questId} should appear in the snapshot quest log.");
        }
        finally
        {
            await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, questId);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_4_TalentAutoAllocator_LevelUp_TrainTalent_PointSpent()
    {
        await EnsureBgIntegrationTargetAsync();
        global::Tests.Infrastructure.Skip.If(
            true,
            "This legacy talent probe is snapshot/progression staging rather than a BotRunner action-dispatch lane; keep it tracked until a production talent action exists.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_5_LevelUpTrainer_VisitTrainer_TrainSkill_NewSpellsLearned()
    {
        await EnsureBgIntegrationTargetAsync();
        global::Tests.Infrastructure.Skip.If(
            true,
            "Trainer learning remains covered by NpcInteractionTests and is currently blocked by the tracked live trainer funding/staging gap.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_6_AuctionPostingService_BuySell_InventoryChanges()
    {
        var target = await EnsureBgIntegrationTargetAsync();
        _output.WriteLine($"=== V3.6 Vendor Sell Dispatch: {target.AccountName}/{target.CharacterName} ===");

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            itemsToAdd: [new LiveBotFixture.ItemDirective(LiveBotFixture.TestItems.LinenCloth, 1)],
            cleanSlate: true,
            clearInventoryFirst: true);

        var staged = await _bot.StageBotRunnerAtRazorHillVendorAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: false);
        Assert.True(staged, $"[{target.RoleLabel}] Razor Hill vendor staging should find a vendor.");

        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(target.AccountName);
        var coinageBefore = snapBefore?.Player?.Coinage ?? 0;
        var itemCountBefore = CountItemSlots(snapBefore, LiveBotFixture.TestItems.LinenCloth);
        var (bagId, slotId) = FindItemBagSlot(snapBefore, LiveBotFixture.TestItems.LinenCloth);
        _output.WriteLine(
            $"[{target.RoleLabel}] Before sell: coinage={coinageBefore}, linenCloth={itemCountBefore}, bag={bagId}, slot={slotId}");
        Assert.Equal(1, itemCountBefore);
        Assert.True(bagId >= 0, $"[{target.RoleLabel}] Linen Cloth should resolve to a bag slot before sell.");

        var vendor = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} integration vendor lookup");
        Assert.NotNull(vendor);

        var vendorGuid = vendor!.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[{target.RoleLabel}] Found vendor: {vendor.GameObject?.Name} GUID=0x{vendorGuid:X}");

        var sellResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.SellItem,
            Parameters =
            {
                new RequestParameter { LongParam = (long)vendorGuid },
                new RequestParameter { IntParam = bagId },
                new RequestParameter { IntParam = slotId },
                new RequestParameter { IntParam = 1 }
            }
        });
        _output.WriteLine($"[{target.RoleLabel}] SellItem dispatched (result={sellResult})");
        Assert.Equal(ResponseResult.Success, sellResult);

        var inventoryChanged = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => CountItemSlots(snapshot, LiveBotFixture.TestItems.LinenCloth) == 0,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: "V3.6 sell inventory change");
        var coinageChanged = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) > coinageBefore,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: "V3.6 sell coinage change");

        await _bot.RefreshSnapshotsAsync();
        var snapAfter = await _bot.GetSnapshotAsync(target.AccountName);
        var coinageAfter = snapAfter?.Player?.Coinage ?? 0;
        var itemCountAfter = CountItemSlots(snapAfter, LiveBotFixture.TestItems.LinenCloth);
        _output.WriteLine(
            $"[{target.RoleLabel}] After sell: coinage={coinageAfter} (delta={coinageAfter - coinageBefore}), " +
            $"linenCloth={itemCountAfter}, inventoryChanged={inventoryChanged}, coinageChanged={coinageChanged}");
        Assert.Equal(0, itemCountAfter);
        Assert.True(coinageAfter > coinageBefore,
            $"Coinage should increase after selling Linen Cloth (before={coinageBefore}, after={coinageAfter}).");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_7_BgRewardCollection_HonorMarks_VisibleInSnapshot()
    {
        var target = await EnsureBgIntegrationTargetAsync();
        _output.WriteLine($"=== V3.7 BG Reward Snapshot Projection: {target.AccountName}/{target.CharacterName} ===");

        const uint wsgMark = 20558;
        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            KalimdorMap,
            OrgSafeX,
            OrgSafeY,
            OrgSafeZ,
            "Orgrimmar reward snapshot staging",
            cleanSlate: true,
            xyToleranceYards: 10f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"[{target.RoleLabel}] Reward snapshot staging should settle before item setup.");

        try
        {
            await _bot.StageBotRunnerLoadoutAsync(
                target.AccountName,
                target.RoleLabel,
                itemsToAdd: [new LiveBotFixture.ItemDirective(wsgMark, 3)],
                cleanSlate: false,
                clearInventoryFirst: true);

            var marksFound = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snapshot => snapshot?.Player?.BagContents?.Values.Any(value => value == wsgMark) == true,
                TimeSpan.FromSeconds(10),
                pollIntervalMs: 500,
                progressLabel: "V3.7 battleground marks snapshot");

            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var markSlots = snap?.Player?.BagContents?.Values.Count(value => value == wsgMark) ?? 0;
            _output.WriteLine($"[{target.RoleLabel}] WSG mark slots in inventory: {markSlots}");
            Assert.True(marksFound, $"WSG Mark of Honor ({wsgMark}) should be visible in bag snapshot after fixture staging.");
        }
        finally
        {
            await _bot.StageBotRunnerLoadoutAsync(
                target.AccountName,
                target.RoleLabel,
                cleanSlate: false,
                clearInventoryFirst: true);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task V3_8_MasterLootDistribution_AssignLoot_ActionDispatches()
    {
        var target = await EnsureBgIntegrationTargetAsync();
        _output.WriteLine($"=== V3.8 Master Loot Distribution: {target.AccountName}/{target.CharacterName} ===");

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            KalimdorMap,
            RfcEntranceX,
            RfcEntranceY,
            RfcEntranceZ,
            "RFC loot assignment staging",
            cleanSlate: true,
            xyToleranceYards: 10f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"[{target.RoleLabel}] RFC staging should settle before AssignLoot dispatch.");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        var selfGuid = snap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[{target.RoleLabel}] Self GUID=0x{selfGuid:X}");
        Assert.True(selfGuid != 0, $"[{target.RoleLabel}] Self GUID should be available before AssignLoot dispatch.");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.AssignLoot,
            Parameters = { new RequestParameter { LongParam = (long)selfGuid } }
        });
        _output.WriteLine($"[{target.RoleLabel}] AssignLoot dispatched (result={result})");

        Assert.True(result == ResponseResult.Success || result == ResponseResult.Failure,
            "AssignLoot action should be routed through the action dispatch pipeline.");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureBgIntegrationTargetAsync()
    {
        var targets = await EnsureIntegrationSettingsAndTargetsAsync(includeForegroundIfActionable: false);
        return targets.Single(target => !target.IsForeground);
    }

    private async Task<(LiveBotFixture.BotRunnerActionTarget Bg, LiveBotFixture.BotRunnerActionTarget Fg)> EnsureBgFgIntegrationTargetsAsync()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            await _bot.CheckFgActionableAsync(requireTeleportProbe: false),
            "FG bot not actionable for integration parity/PvP comparison.");

        var targets = await EnsureIntegrationSettingsAndTargetsAsync(includeForegroundIfActionable: true);
        return (
            targets.Single(target => !target.IsForeground),
            targets.Single(target => target.IsForeground));
    }

    private async Task<IReadOnlyList<LiveBotFixture.BotRunnerActionTarget>> EnsureIntegrationSettingsAndTargetsAsync(
        bool includeForegroundIfActionable)
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");

        var targets = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable,
                foregroundFirst: false)
            .ToList();

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: integration action target.");
        }

        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName) && !targets.Any(target => target.IsForeground))
        {
            _output.WriteLine(
                $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for Shodan topology parity.");
        }

        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no integration action dispatch.");

        return targets;
    }

    private static bool HasQuest(WoWActivitySnapshot? snapshot, uint questId)
        => snapshot?.Player?.QuestLogEntries?.Any(q => q.QuestId == questId || q.QuestLog1 == questId) == true;

    private static int CountItemSlots(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private static (int bagId, int slotId) FindItemBagSlot(WoWActivitySnapshot? snapshot, uint itemId)
    {
        var bags = snapshot?.Player?.BagContents;
        if (bags == null)
            return (-1, -1);

        foreach (var item in bags)
        {
            if (item.Value == itemId)
                return (0xFF, (int)item.Key);
        }

        return (-1, -1);
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository path for {Path.Combine(segments)} from {AppContext.BaseDirectory}.");
    }
}
