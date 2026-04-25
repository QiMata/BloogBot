using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed First Aid crafting baseline.
///
/// FG is launched for topology parity, but ActionType.CastSpell-by-id is the
/// BG-compatible behavior surface for this suite. SHODAN stages recipe, skill,
/// and reagent setup; only BG receives the crafting action dispatch.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class CraftingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint FirstAidApprentice = 3273;
    private const uint LinenBandageRecipe = 3275;
    private const uint LinenClothItem = LiveBotFixture.TestItems.LinenCloth;
    private const uint LinenBandageItem = 1251;
    private const int FirstAidSkillValue = 1;
    private const int FirstAidSkillMax = 75;
    private const int CraftTimeoutMs = 8000;

    public CraftingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task FirstAid_LearnAndCraft_ProducesLinenBandage()
    {
        await EnsureCraftingSettingsAsync();
        var target = ResolveCraftingTarget();
        var metrics = await RunCraftingScenarioAsync(target);

        Assert.True(metrics.SkillPrepared, "BG: First Aid skill should be staged before the craft cast.");
        Assert.True(metrics.ClothPrepared, "BG: Exactly one Linen Cloth should be staged before the craft cast.");
        Assert.True(metrics.Crafted, "BG: Casting Linen Bandage should yield a Linen Bandage in bag contents.");
        Assert.Equal(1, metrics.ClothSlotsBefore);
        Assert.Equal(0, metrics.BandageSlotsBefore);
        Assert.Equal(0, metrics.ClothSlotsAfter);
        Assert.Equal(1, metrics.BandageSlotsAfter);
        Assert.Equal(metrics.BagItemCountBefore, metrics.BagItemCountAfter);
        Assert.InRange(metrics.CraftLatencyMs, 1, CraftTimeoutMs);
    }

    private async Task<string> EnsureCraftingSettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Crafting.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Crafting.config.json.");

        return settingsPath;
    }

    private LiveBotFixture.BotRunnerActionTarget ResolveCraftingTarget()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no crafting action dispatch.");

        foreach (var target in targets)
        {
            var action = target.IsForeground
                ? "idle for this test method (FG CastSpell-by-id crafting is not the validated path)"
                : "stage First Aid loadout + dispatch CastSpell";
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: {action}.");
        }

        var selected = targets.FirstOrDefault(target => !target.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(selected.AccountName),
            "BG bot not available.");

        return selected;
    }

    private async Task<CraftingMetrics> RunCraftingScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var account = target.AccountName;
        var label = target.RoleLabel;

        await _bot.RefreshSnapshotsAsync();
        _output.WriteLine($"[{label}] Staging First Aid recipe, skill, and one Linen Cloth through Shodan loadout helper.");
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { FirstAidApprentice, LinenBandageRecipe },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(CraftingData.FirstAidSkillId, FirstAidSkillValue, FirstAidSkillMax) },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(LinenClothItem, 1) });

        var spellsKnown = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => snapshot.Player?.SpellList?.Contains(FirstAidApprentice) == true
                && snapshot.Player?.SpellList?.Contains(LinenBandageRecipe) == true,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 250,
            progressLabel: $"{label} first-aid spells");

        var skillPrepared = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => snapshot.Player?.SkillInfo.TryGetValue(CraftingData.FirstAidSkillId, out var skill) == true
                && skill >= FirstAidSkillValue,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 250,
            progressLabel: $"{label} first-aid skill");

        var clothPrepared = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, LinenClothItem) == 1
                && CountItemSlots(snapshot, LinenBandageItem) == 0,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 250,
            progressLabel: $"{label} linen cloth");

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var clothSlotsBefore = CountItemSlots(before, LinenClothItem);
        var bandageSlotsBefore = CountItemSlots(before, LinenBandageItem);
        var bagItemCountBefore = before?.Player?.BagContents?.Count ?? 0;

        _output.WriteLine($"[{label}] Casting Linen Bandage recipe via ActionType.CastSpell.");
        var craftTimer = Stopwatch.StartNew();
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)LinenBandageRecipe } }
        }, delayMs: 500);

        var crafted = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, LinenBandageItem) == 1
                && CountItemSlots(snapshot, LinenClothItem) == 0,
            TimeSpan.FromMilliseconds(CraftTimeoutMs),
            pollIntervalMs: 200,
            progressLabel: $"{label} linen bandage");

        craftTimer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var clothSlotsAfter = CountItemSlots(after, LinenClothItem);
        var bandageSlotsAfter = CountItemSlots(after, LinenBandageItem);
        var bagItemCountAfter = after?.Player?.BagContents?.Count ?? 0;

        _output.WriteLine(
            $"[{label}] craft metrics: spellListSynced={spellsKnown}, clothPrepared={clothPrepared}, crafted={crafted}, " +
            $"cloth {clothSlotsBefore}->{clothSlotsAfter}, bandage {bandageSlotsBefore}->{bandageSlotsAfter}, " +
            $"bagItems {bagItemCountBefore}->{bagItemCountAfter}, latencyMs={craftTimer.ElapsedMilliseconds}");

        if (!crafted)
            _bot.DumpSnapshotDiagnostics(after, label);

        return new CraftingMetrics(
            spellsKnown,
            skillPrepared,
            clothPrepared,
            crafted,
            clothSlotsBefore,
            bandageSlotsBefore,
            bagItemCountBefore,
            clothSlotsAfter,
            bandageSlotsAfter,
            bagItemCountAfter,
            (int)craftTimer.ElapsedMilliseconds);
    }

    private static int CountItemSlots(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private sealed record CraftingMetrics(
        bool SpellsKnown,
        bool SkillPrepared,
        bool ClothPrepared,
        bool Crafted,
        int ClothSlotsBefore,
        int BandageSlotsBefore,
        int BagItemCountBefore,
        int ClothSlotsAfter,
        int BandageSlotsAfter,
        int BagItemCountAfter,
        int CraftLatencyMs);

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
