using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// BG-first First Aid crafting baseline.
///
/// Current production path under test:
/// - Exports/BotRunner/BotRunnerService.ActionDispatch.cs
/// - Exports/BotRunner/BotRunnerService.Sequences.Combat.cs
/// - Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs
///
/// FG parity is intentionally excluded here. Foreground crafting still depends on Lua spell-name
/// resolution and is not the behavior surface we are overhauling first.
/// </summary>
[Collection(BgOnlyValidationCollection.Name)]
public class CraftingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint FirstAidApprentice = 3273;
    private const uint LinenBandageRecipe = 3275;
    private const uint LinenClothItem = LiveBotFixture.TestItems.LinenCloth;
    private const uint LinenBandageItem = 1251;
    private const int CraftTimeoutMs = 8000;

    public CraftingProfessionTests(BgOnlyBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task FirstAid_LearnAndCraft_ProducesLinenBandage()
    {
        var metrics = await RunCraftingScenarioAsync(_bot.BgAccountName!, "BG");

        Assert.True(metrics.ClothPrepared, "BG: Exactly one Linen Cloth should be staged before the craft cast.");
        Assert.True(metrics.Crafted, "BG: Casting Linen Bandage should yield a Linen Bandage in bag contents.");
        Assert.Equal(1, metrics.ClothSlotsBefore);
        Assert.Equal(0, metrics.BandageSlotsBefore);
        Assert.Equal(0, metrics.ClothSlotsAfter);
        Assert.Equal(1, metrics.BandageSlotsAfter);
        Assert.Equal(metrics.BagItemCountBefore, metrics.BagItemCountAfter);
        Assert.InRange(metrics.CraftLatencyMs, 1, CraftTimeoutMs);
    }

    private async Task<CraftingMetrics> RunCraftingScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var baseline = await _bot.GetSnapshotAsync(account);
        var charName = baseline?.CharacterName ?? string.Empty;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(charName), $"{label}: character name not available for .reset items.");

        _output.WriteLine($"[{label}] Resetting items for deterministic craft verification.");
        await _bot.ResetItemsAsync(charName);
        await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.BagContents?.Count ?? 1) == 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 250,
            progressLabel: $"{label} reset items");

        _output.WriteLine($"[{label}] Teaching First Aid apprentice + Linen Bandage recipe.");
        await _bot.BotLearnSpellAsync(account, FirstAidApprentice);
        await _bot.BotLearnSpellAsync(account, LinenBandageRecipe);

        var spellsKnown = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => snapshot.Player?.SpellList?.Contains(FirstAidApprentice) == true
                && snapshot.Player?.SpellList?.Contains(LinenBandageRecipe) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 250,
            progressLabel: $"{label} first-aid spells");

        _output.WriteLine($"[{label}] Staging exactly one Linen Cloth.");
        await _bot.BotAddItemAsync(account, LinenClothItem, 1);

        var clothPrepared = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, LinenClothItem) == 1
                && CountItemSlots(snapshot, LinenBandageItem) == 0,
            TimeSpan.FromSeconds(5),
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
        bool ClothPrepared,
        bool Crafted,
        int ClothSlotsBefore,
        int BandageSlotsBefore,
        int BagItemCountBefore,
        int ClothSlotsAfter,
        int BandageSlotsAfter,
        int BagItemCountAfter,
        int CraftLatencyMs);
}
