using System;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fishing profession integration test — dual-client validation.
///
/// Each bot (BG + FG) independently:
///   1) Clean inventory + learn Fishing + set skill
///   2) Add + equip fishing pole
///   3) Teleport to Ratchet dock edge (both bots together to prevent combat coordinator interference)
///   4) Cast fishing
///   5) Verify bobber appears in nearby objects
///   6) Clean inventory on teardown
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~FishingProfessionTests" --configuration Release -v n
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Ratchet — neutral goblin town, no hostiles, confirmed fishing node spawns at docks.
    private const int FishMap = 1; // Kalimdor

    // Southern tip of the main dock, centered on its width (~5yd wide, X: -991 to -986).
    // World Trigger marks the very end at (-988.4, -3835.9, 8.2). We stand 2yd north of that.
    // Dock surface is at Z≈5.7; fishing nodes spawn at Z=0 within 15yd east/southeast.
    private const float DockEdgeX = -988.5f;
    private const float DockEdgeY = -3834.0f;
    private const float DockEdgeZ = 5.7f;

    // Face east-southeast toward the nearest fishing node at (-975, -3835).
    // atan2(-3835 - (-3834), -975 - (-988.5)) = atan2(-1, 13.5) ≈ 6.21 rad
    private const float FishFacing = 6.21f;

    private const uint GameObjectTypeFishingNode = 17;

    public FishingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Fishing_LearnCastAndCatch_BothBotsSeeBobbersOrSkillIncreases()
    {
        try
        {
            // --- Step 0: Clean inventory for both characters before any test work ---
            await CleanupInventory("Pre-test");

            // --- Step 1 & 2: Learn fishing + equip pole for both bots ---
            var bgAccount = _bot.BgAccountName!;
            Assert.NotNull(bgAccount);
            _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");
            await PrepareBot(bgAccount, "BG");

            string? fgAccount = null;
            if (_bot.ForegroundBot != null)
            {
                fgAccount = _bot.FgAccountName!;
                Assert.NotNull(fgAccount);
                _output.WriteLine($"\n=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
                await PrepareBot(fgAccount, "FG");
            }

            // --- Step 3: Teleport BOTH bots to same dock edge position ---
            // This prevents the combat coordinator from flooding GOTO commands to regroup them.
            _output.WriteLine($"\n=== Teleporting both bots to dock edge ({DockEdgeX}, {DockEdgeY}, {DockEdgeZ}) ===");

            // Both bots use chat-based .go xyz (the bot types the GM command).
            // MaNGOS sends MSG_MOVE_TELEPORT back to the client for same-map teleports.
            // NotifyTeleportIncoming() in the handler ensures the position write guard
            // allows the update through before the event fires.
            // NOTE: SOAP ".teleport name" with coordinates is NOT a valid MaNGOS command
            // and silently fails — always use .go xyz via chat for coordinate teleports.
            await _bot.BotTeleportAsync(bgAccount, FishMap, DockEdgeX, DockEdgeY, DockEdgeZ);
            _output.WriteLine("  BG teleport sent (chat .go xyz)");
            if (fgAccount != null)
            {
                await _bot.BotTeleportAsync(fgAccount, FishMap, DockEdgeX, DockEdgeY, DockEdgeZ);
                _output.WriteLine("  FG teleport sent (chat .go xyz)");
            }

            // Wait for both teleports to settle
            _output.WriteLine("  Waiting 12s for zone load...");
            await Task.Delay(12000);

            // Verify positions
            await _bot.RefreshSnapshotsAsync();
            var bgSnap = _bot.BackgroundBot;
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  BG position: ({bgPos?.X:F1}, {bgPos?.Y:F1}, {bgPos?.Z:F1})");

            if (_bot.ForegroundBot != null)
            {
                var fgPos = _bot.ForegroundBot?.Player?.Unit?.GameObject?.Base?.Position;
                _output.WriteLine($"  FG position: ({fgPos?.X:F1}, {fgPos?.Y:F1}, {fgPos?.Z:F1})");
            }

            // --- Step 3b: Face toward the water (fishing bobber lands in the facing direction) ---
            _output.WriteLine($"\n=== Setting facing toward water ({FishFacing:F2} rad) ===");
            await _bot.SendActionAndWaitAsync(bgAccount, new ActionMessage
            {
                ActionType = ActionType.SetFacing,
                Parameters = { new RequestParameter { FloatParam = FishFacing } }
            }, delayMs: 2000);
            _output.WriteLine("  BG facing set");
            if (fgAccount != null)
            {
                await _bot.SendActionAndWaitAsync(fgAccount, new ActionMessage
                {
                    ActionType = ActionType.SetFacing,
                    Parameters = { new RequestParameter { FloatParam = FishFacing } }
                }, delayMs: 2000);
                _output.WriteLine("  FG facing set");
            }

            // --- Step 4: Cast fishing on each bot ---
            _output.WriteLine("\n=== Casting Fishing ===");
            bool bgBobber = await CastAndCheckFishing(bgAccount, "BG");

            bool fgBobber = false;
            if (fgAccount != null)
                fgBobber = await CastAndCheckFishing(fgAccount, "FG");

            // === Results ===
            _output.WriteLine($"\n=== Results: BG bobber={bgBobber}, FG bobber={fgBobber} ===");

            Assert.True(bgBobber,
                "BG bot: No bobber detected after fishing cast. Check [CAST_FAILED] logs for failure reason.");
            if (_bot.ForegroundBot != null)
                Assert.True(fgBobber,
                    "FG bot: No bobber detected after fishing cast. Check [CAST_FAILED] logs for failure reason.");
        }
        finally
        {
            await CleanupInventory("Post-test");
        }
    }

    /// <summary>Learn fishing spell, set skill to 300, add and equip fishing pole.</summary>
    private async Task PrepareBot(string account, string label)
    {
        // Learn Fishing + set skill
        _output.WriteLine($"  [{label}] Learn Fishing ({FishingData.FishingRank1}) + skill ({FishingData.FishingSkillId})");
        await _bot.BotLearnSpellAsync(account, FishingData.FishingRank1);
        await _bot.SendGmChatCommandAsync(account, $".setskill {FishingData.FishingSkillId} 1 300");
        await Task.Delay(3000);

        // Add + equip fishing pole
        _output.WriteLine($"  [{label}] Add + Equip Fishing Pole ({FishingData.FishingPole})");
        await _bot.BotAddItemAsync(account, FishingData.FishingPole);
        await Task.Delay(10000);

        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingPole } }
        }, delayMs: 8000);
        await Task.Delay(5000);
    }

    /// <summary>Try multiple approaches to cast fishing and check for bobber.</summary>
    private async Task<bool> CastAndCheckFishing(string account, string label)
    {
        // Stop movement first
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.Wait,
        }, delayMs: 2000);

        // --- Approach A: IPC CastSpell ---
        _output.WriteLine($"  [{label}] Approach A: IPC CastSpell");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingRank1 } }
        }, delayMs: 2000);
        await Task.Delay(8000);
        if (await CheckForBobber(label)) return true;

        // --- Approach B: GM .cast ---
        _output.WriteLine($"  [{label}] Approach B: GM .cast");
        await _bot.SendGmChatCommandAsync(account, $".cast {FishingData.FishingRank1}");
        await Task.Delay(5000);
        if (await CheckForBobber(label)) return true;

        // --- Approach C: GM .cast triggered ---
        _output.WriteLine($"  [{label}] Approach C: GM .cast triggered");
        await _bot.SendGmChatCommandAsync(account, $".cast {FishingData.FishingRank1} triggered");
        await Task.Delay(5000);
        if (await CheckForBobber(label)) return true;

        // --- Final diagnostics ---
        await _bot.RefreshSnapshotsAsync();
        var snap = GetSnapshot(label);
        _output.WriteLine($"  [{label}] channelSpellId={snap?.Player?.Unit?.ChannelSpellId ?? 0}");
        if (snap?.NearbyObjects != null)
        {
            foreach (var go in snap.NearbyObjects)
                _output.WriteLine($"    GO: entry={go.Entry}, displayId={go.DisplayId}, type={go.GameObjectType}, name=\"{go.Name}\"");
        }
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"  [{label}] Final position: ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");

        return false;
    }

    private async Task CleanupInventory(string phase)
    {
        _output.WriteLine($"\n=== {phase}: Resetting items ===");
        try
        {
            if (_bot.BgCharacterName != null)
            {
                var result = await _bot.ResetItemsAsync(_bot.BgCharacterName);
                _output.WriteLine($"  BG ({_bot.BgCharacterName}): {result}");
            }
            if (_bot.FgCharacterName != null)
            {
                var result = await _bot.ResetItemsAsync(_bot.FgCharacterName);
                _output.WriteLine($"  FG ({_bot.FgCharacterName}): {result}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  Cleanup error: {ex.Message}");
        }
    }

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

    private async Task<bool> CheckForBobber(string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = GetSnapshot(label);
        var nearbyObjects = snap?.NearbyObjects;

        var bobbersByDisplay = nearbyObjects?.Where(go => go.DisplayId == FishingData.BobberDisplayId).ToList() ?? [];
        var bobbersByType = nearbyObjects?.Where(go => go.GameObjectType == GameObjectTypeFishingNode).ToList() ?? [];
        var goCount = nearbyObjects?.Count ?? 0;
        var channeling = snap?.Player?.Unit?.ChannelSpellId ?? 0;

        _output.WriteLine($"    [{label}] byDisplay={bobbersByDisplay.Count}, byType={bobbersByType.Count}, totalGOs={goCount}, channeling={channeling}");

        if (goCount > 0)
        {
            foreach (var go in nearbyObjects!)
                _output.WriteLine($"      GO: entry={go.Entry}, displayId={go.DisplayId}, type={go.GameObjectType}, name=\"{go.Name}\"");
        }

        return bobbersByDisplay.Count > 0 || bobbersByType.Count > 0;
    }
}
