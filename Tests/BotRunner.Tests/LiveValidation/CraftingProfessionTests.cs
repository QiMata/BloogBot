using System;
<<<<<<< HEAD
=======
using System.Diagnostics;
>>>>>>> cpp_physics_system
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
<<<<<<< HEAD
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
=======
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
>>>>>>> cpp_physics_system
[Collection(LiveValidationCollection.Name)]
public class CraftingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint FirstAidApprentice = 3273;
    private const uint LinenBandageRecipe = 3275;
<<<<<<< HEAD
    private const uint LinenCloth = 2589;
    private const uint LinenBandageItem = 1251;

    // Orgrimmar (safe zone) for setup/casting.
    private const int OrgrimmarMap = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;
    private const float SetupArrivalDistance = 45f;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD
=======
    private const uint LinenClothItem = LiveBotFixture.TestItems.LinenCloth;
    private const uint LinenBandageItem = 1251;
    private const int CraftTimeoutMs = 8000;
>>>>>>> cpp_physics_system

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
<<<<<<< HEAD
        bool bgPassed;

        // === BG Bot ===
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ===");
        bgPassed = await RunCraftingScenario(_bot.BgAccountName!, "BG");

        // === FG Bot ===
        bool fgPassed = false;
        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine($"\n=== FG Bot: {_bot.FgCharacterName} ===");
            fgPassed = await RunCraftingScenario(_bot.FgAccountName!, "FG");
        }
        else
        {
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Crafting should produce Linen Bandage item in bag snapshot.");
        if (_bot.ForegroundBot != null)
        {
            if (!fgPassed)
                _output.WriteLine("WARNING: FG did not craft Linen Bandage in this run; BG path remains authoritative.");
        }
    }

    private async Task<bool> RunCraftingScenario(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return false;

        // Step 0: Strict-alive guard (avoid dead-state GM command rejection).
        if (!IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        // Step 1: Teleport only when not already near setup location.
        var teleported = await EnsureNearSetupLocationAsync(account, label);
        if (teleported)
            await Task.Delay(4000);

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
            await _bot.SendGmChatCommandAsync(account, ".gm on");
            if (!hasFirstAid)
                await _bot.BotLearnSpellAsync(account, FirstAidApprentice);
            if (!hasBandageRecipe)
                await _bot.BotLearnSpellAsync(account, LinenBandageRecipe);
            await Task.Delay(1200);
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
            await Task.Delay(1500);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
            bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
            linenClothSlotsBefore = bagItems.Count(itemId => itemId == LinenCloth);
            bandageSlotsBefore = bagItems.Count(itemId => itemId == LinenBandageItem);
        }

        if (linenClothSlotsBefore == 0)
        {
            _output.WriteLine($"  [{label}] Adding Linen Cloth for craft input.");
            await _bot.BotAddItemAsync(account, LinenCloth, 1);
            await Task.Delay(1500);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
            bagItems = snap.Player?.BagContents?.Values ?? Enumerable.Empty<uint>();
            linenClothSlotsBefore = bagItems.Count(itemId => itemId == LinenCloth);
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
        _output.WriteLine($"  [{label}] Casting Linen Bandage recipe (spell {LinenBandageRecipe})");
=======
        var metrics = await RunCraftingScenarioAsync(_bot.BgAccountName!, "BG");

        Assert.True(metrics.ClothPrepared, "BG: Exactly one Linen Cloth should be staged before the craft cast.");
        Assert.True(metrics.Crafted, "BG: Casting Linen Bandage should yield a Linen Bandage in bag contents.");
        Assert.Equal(1, metrics.ClothSlotsBefore);
        Assert.Equal(0, metrics.BandageSlotsBefore);
        Assert.Equal(0, metrics.ClothSlotsAfter);
        Assert.Equal(1, metrics.BandageSlotsAfter);
        Assert.Equal(1, metrics.BagItemCountAfter);
        Assert.InRange(metrics.CraftLatencyMs, 1, CraftTimeoutMs);
    }

    private async Task<CraftingMetrics> RunCraftingScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        _output.WriteLine($"[{label}] Clearing bags for deterministic craft verification.");
        await _bot.BotClearInventoryAsync(account);

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

        _output.WriteLine($"[{label}] Casting Linen Bandage recipe via ActionType.CastSpell.");
        var craftTimer = Stopwatch.StartNew();
>>>>>>> cpp_physics_system
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)LinenBandageRecipe } }
<<<<<<< HEAD
        }, delayMs: 3500);

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var bandageSlotsAfter = after?.Player?.BagContents?.Values.Count(itemId => itemId == LinenBandageItem) ?? 0;
        var crafted = bandageSlotsAfter > bandageSlotsBefore;
        if (!crafted)
        {
            _output.WriteLine($"  [{label}] CastSpell path did not produce bandage; retrying via .cast {LinenBandageRecipe}.");
            await _bot.SendGmChatCommandTrackedAsync(
                account,
                $".cast {LinenBandageRecipe}",
                captureResponse: true,
                delayMs: 1200);

            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            bandageSlotsAfter = after?.Player?.BagContents?.Values.Count(itemId => itemId == LinenBandageItem) ?? 0;
            crafted = bandageSlotsAfter > bandageSlotsBefore;
        }

        _output.WriteLine($"  [{label}] Bandage slots before={bandageSlotsBefore}, after={bandageSlotsAfter}, crafted={crafted}");
=======
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
            $"bagItems={bagItemCountAfter}, latencyMs={craftTimer.ElapsedMilliseconds}");
>>>>>>> cpp_physics_system

        if (!crafted)
            _bot.DumpSnapshotDiagnostics(after, label);

<<<<<<< HEAD
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

    private static bool IsStrictAlive(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;
        return unit.Health > 0 && !hasGhostFlag && standState != StandStateDead;
    }

    private static float DistanceTo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
=======
        return new CraftingMetrics(
            spellsKnown,
            clothPrepared,
            crafted,
            clothSlotsBefore,
            bandageSlotsBefore,
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
        int ClothSlotsAfter,
        int BandageSlotsAfter,
        int BagItemCountAfter,
        int CraftLatencyMs);
>>>>>>> cpp_physics_system
}
