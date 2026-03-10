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
/// Crafting profession integration test (First Aid) - dual-client validation.
///
/// Each bot (BG + FG) independently:
///   1) Apply only missing setup deltas (alive/location/known spells/input items).
///   2) Craft Linen Bandage.
///   3) Verify crafted bandage appears in ActivitySnapshot bag contents.
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~CraftingProfessionTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class CraftingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint FirstAidApprentice = 3273;
    private const uint LinenBandageRecipe = 3275;
    private const uint LinenBandageItem = 1251;

    // Orgrimmar (safe zone) for setup/casting.
    private const int OrgrimmarMap = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;
    private const float SetupArrivalDistance = 45f;

    public CraftingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task FirstAid_LearnAndCraft_ProducesLinenBandage()
    {
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
        {
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ===");
            _output.WriteLine("[PARITY] Running BG and FG crafting scenarios in parallel.");

            var bgTask = RunCraftingScenario(_bot.BgAccountName!, "BG");
            var fgTask = RunCraftingScenario(_bot.FgAccountName!, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunCraftingScenario(_bot.BgAccountName!, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Crafting should produce Linen Bandage item in bag snapshot.");
        if (hasFg)
        {
            Assert.True(fgPassed, "FG bot: Crafting should produce Linen Bandage item in bag snapshot.");
        }
    }

    private async Task<bool> RunCraftingScenario(string account, string label)
    {
        // Standardized setup: revive if dead, teleport to Orgrimmar, GM on (BT-SETUP-001).
        await _bot.EnsureCleanSlateAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return false;

        // Step 2: Learn only missing First Aid spells.
        var hasFirstAid = snap.Player?.SpellList?.Contains(FirstAidApprentice) == true;
        var hasBandageRecipe = snap.Player?.SpellList?.Contains(LinenBandageRecipe) == true;
        if (!hasFirstAid || !hasBandageRecipe)
        {
            _output.WriteLine($"  [{label}] Learning missing First Aid spells: apprentice={!hasFirstAid}, recipe={!hasBandageRecipe}");
            if (!hasFirstAid)
                await _bot.BotLearnSpellAsync(account, FirstAidApprentice);
            if (!hasBandageRecipe)
                await _bot.BotLearnSpellAsync(account, LinenBandageRecipe);
            // Poll for SMSG_LEARNED_SPELL to be processed so the bot's
            // internal Spells collection is updated before CastSpell action.
            var learnSw = Stopwatch.StartNew();
            while (learnSw.ElapsedMilliseconds < 5000)
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                var learnSnap = await _bot.GetSnapshotAsync(account);
                var learnedFirstAid = learnSnap?.Player?.SpellList?.Contains(FirstAidApprentice) == true;
                var learnedRecipe = learnSnap?.Player?.SpellList?.Contains(LinenBandageRecipe) == true;
                if (learnedFirstAid && learnedRecipe)
                {
                    _output.WriteLine($"  [{label}] Spells confirmed in snapshot after {learnSw.ElapsedMilliseconds}ms");
                    break;
                }
            }
        }
        else
        {
            _output.WriteLine($"  [{label}] First Aid spells already learned.");
        }

        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return false;

        // Bag snapshot has item IDs only (not stack quantities). For deterministic verification,
        // clear bags only when preexisting bandages or near-full inventory would mask outcome.
        var bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
        var linenClothSlotsBefore = bagItems.Count(itemId => itemId == LiveBotFixture.TestItems.LinenCloth);
        var bandageSlotsBefore = bagItems.Count(itemId => itemId == LinenBandageItem);
        var bagItemCount = snap.Player?.BagContents?.Count ?? 0;
        if (bandageSlotsBefore > 0 || (linenClothSlotsBefore == 0 && bagItemCount >= 15))
        {
            _output.WriteLine(
                $"  [{label}] Clearing inventory for deterministic craft verification (bandageSlots={bandageSlotsBefore}, clothSlots={linenClothSlotsBefore}, bagItems={bagItemCount}).");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            // Poll for inventory clear instead of fixed 1500ms delay.
            var clearSw = Stopwatch.StartNew();
            while (clearSw.Elapsed < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                var currentBagCount = snap.Player?.BagContents?.Count ?? bagItemCount;
                if (currentBagCount < bagItemCount)
                    break;
            }
            bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
            linenClothSlotsBefore = bagItems.Count(itemId => itemId == LiveBotFixture.TestItems.LinenCloth);
            bandageSlotsBefore = bagItems.Count(itemId => itemId == LinenBandageItem);
        }

        if (linenClothSlotsBefore == 0)
        {
            _output.WriteLine($"  [{label}] Adding Linen Cloth for craft input.");
            await _bot.BotAddItemAsync(account, LiveBotFixture.TestItems.LinenCloth, 1);
            // Poll for linen cloth to appear in bags instead of fixed 1500ms delay.
            var addClothSw = Stopwatch.StartNew();
            while (addClothSw.Elapsed < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
                linenClothSlotsBefore = bagItems.Count(itemId => itemId == LiveBotFixture.TestItems.LinenCloth);
                if (linenClothSlotsBefore > 0)
                    break;
            }
        }
        else
        {
            _output.WriteLine($"  [{label}] Linen Cloth already present (slots={linenClothSlotsBefore}).");
        }

        if (linenClothSlotsBefore == 0)
        {
            _output.WriteLine($"  [{label}] Missing Linen Cloth after setup; cannot craft.");
            return false;
        }

        // Step 3: Cast recipe via player action (no GM shortcuts).
        // Both FG and BG use ActionType.CastSpell. FG resolves spell ID → name via spell DB.
        _output.WriteLine($"  [{label}] Casting Linen Bandage recipe (spell {LinenBandageRecipe})");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)LinenBandageRecipe } }
        }, delayMs: 500);

        // Poll for bandage to appear in bags (crafting channel ~3.5s server-side).
        WoWActivitySnapshot? after = null;
        int bandageSlotsAfter = 0;
        bool crafted = false;
        var craftSw = Stopwatch.StartNew();
        while (craftSw.Elapsed < TimeSpan.FromSeconds(8))
        {
            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            bandageSlotsAfter = after?.Player?.BagContents?.Values.Count(itemId => itemId == LinenBandageItem) ?? 0;
            crafted = bandageSlotsAfter > bandageSlotsBefore;
            if (crafted)
            {
                _output.WriteLine($"  [{label}] Bandage detected after {craftSw.ElapsedMilliseconds}ms");
                break;
            }
            await Task.Delay(200);
        }

        _output.WriteLine($"  [{label}] Bandage slots before={bandageSlotsBefore}, after={bandageSlotsAfter}, crafted={crafted}");

        if (!crafted)
            _bot.DumpSnapshotDiagnostics(after, label);

        return crafted;
    }

    private async Task<bool> EnsureNearSetupLocationAsync(string account, string label)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos != null)
        {
            var dist = LiveBotFixture.Distance3D(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
            if (dist <= SetupArrivalDistance)
            {
                _output.WriteLine($"  [{label}] Already near setup location (dist={dist:F1}y); skipping teleport.");
                return false;
            }
        }

        _output.WriteLine($"  [{label}] Teleporting to setup location.");
        await _bot.BotTeleportAsync(account, OrgrimmarMap, OrgX, OrgY, OrgZ);
        return true;
    }

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
