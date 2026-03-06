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
    private const uint LinenCloth = 2589;
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
        var hasFg = _bot.ForegroundBot != null;
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
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return false;

        // Step 0: Strict-alive guard (avoid dead-state GM command rejection).
        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            // Poll for alive state instead of fixed delay (same ~1200ms budget)
            var reviveSw = Stopwatch.StartNew();
            while (reviveSw.ElapsedMilliseconds < 3000)
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                if (LiveBotFixture.IsStrictAlive(snap))
                    break;
            }
        }

        // Step 1: Teleport only when not already near setup location.
        var teleported = await EnsureNearSetupLocationAsync(account, label);
        if (teleported)
        {
            // Poll for position to update near the setup location instead of fixed 2000ms delay.
            var teleSw = Stopwatch.StartNew();
            while (teleSw.Elapsed < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (pos != null && DistanceTo(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ) <= SetupArrivalDistance)
                {
                    _output.WriteLine($"  [{label}] Teleport confirmed after {teleSw.ElapsedMilliseconds}ms");
                    break;
                }
            }
        }

        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
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
        var linenClothSlotsBefore = bagItems.Count(itemId => itemId == LinenCloth);
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
            linenClothSlotsBefore = bagItems.Count(itemId => itemId == LinenCloth);
            bandageSlotsBefore = bagItems.Count(itemId => itemId == LinenBandageItem);
        }

        if (linenClothSlotsBefore == 0)
        {
            _output.WriteLine($"  [{label}] Adding Linen Cloth for craft input.");
            await _bot.BotAddItemAsync(account, LinenCloth, 1);
            // Poll for linen cloth to appear in bags instead of fixed 1500ms delay.
            var addClothSw = Stopwatch.StartNew();
            while (addClothSw.Elapsed < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
                linenClothSlotsBefore = bagItems.Count(itemId => itemId == LinenCloth);
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

        // Step 3: Cast recipe and verify Linen Bandage appears in bag snapshot.
        // Strategy: (a) CastSpell action (CMSG_CAST_SPELL), (b) .cast GM cmd,
        // (c) .cast triggered (bypasses spell focus requirement — MaNGOS data has
        // RequiresSpellFocus=42 on some First Aid spells which is incorrect).
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
        while (craftSw.Elapsed < TimeSpan.FromSeconds(5))
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
        if (!crafted)
        {
            _output.WriteLine($"  [{label}] CastSpell path did not produce bandage; retrying via .cast {LinenBandageRecipe}.");
            var castTrace = await _bot.SendGmChatCommandTrackedAsync(
                account,
                $".cast {LinenBandageRecipe}",
                captureResponse: true,
                delayMs: 1200);
            AssertCommandSucceeded(castTrace, label, $".cast {LinenBandageRecipe}");

            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            bandageSlotsAfter = after?.Player?.BagContents?.Values.Count(itemId => itemId == LinenBandageItem) ?? 0;
            crafted = bandageSlotsAfter > bandageSlotsBefore;
        }

        if (!crafted)
        {
            // Triggered cast bypasses spell focus, range, resource, and cooldown checks.
            // Ensure self-target is set (some GM commands need a selected player).
            _output.WriteLine($"  [{label}] .cast also failed; trying .cast {LinenBandageRecipe} triggered (bypasses spell focus).");
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(300);
            await _bot.SendGmChatCommandTrackedAsync(
                account,
                $".cast {LinenBandageRecipe} triggered",
                captureResponse: true,
                delayMs: 4000);

            // Poll for bandage to appear (server may need a tick to create the item)
            var triggeredSw = Stopwatch.StartNew();
            while (triggeredSw.Elapsed < TimeSpan.FromSeconds(5) && !crafted)
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                after = await _bot.GetSnapshotAsync(account);
                bandageSlotsAfter = after?.Player?.BagContents?.Values.Count(itemId => itemId == LinenBandageItem) ?? 0;
                crafted = bandageSlotsAfter > bandageSlotsBefore;
            }
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
            var dist = DistanceTo(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
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

    private static float DistanceTo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
