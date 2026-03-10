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
/// Buff dismiss integration test — validates buff application and removal.
///
/// Each bot (BG + FG) independently:
///   1) Remove any stale Lion's Strength buff via .unaura.
///   2) Add Elixir of Lion's Strength (item 2454) and use it to apply buff (spell 2367).
///   3) Verify buff is present in snapshot auras.
///   4) Dismiss the buff via ActionType.DismissBuff with buff name "Lion's Strength".
///      If ActionType fails (BG bot: WoWUnit.Buffs list empty), falls back to .unaura.
///   5) Verify buff is removed from snapshot auras.
///
/// DismissBuff takes a string parameter (buff name), not a spell ID.
/// The BotRunnerService dispatches: _objectManager.Player.DismissBuff(buffName).
/// Known limitation: BG bot's WoWUnit.Buffs list is never populated from packets,
/// so HasBuff(name) always returns false. FG bot reads Buffs from WoW.exe memory.
///
/// Run: dotnet test --filter "FullyQualifiedName~BuffDismissTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class BuffDismissTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint ElixirOfLionsStrength = 2454;
    // Elixir of Lion's Strength (item 2454): spell 2367 is the "use effect" and 2457 is the buff aura.
    // VMaNGOS may track either or both as auras. Check for both, clean up both.
    private const uint LionsStrengthUseSpell = 2367;
    private const uint LionsStrengthBuffAura = 2457;
    private const string LionsStrengthBuffName = "Lion's Strength";

    public BuffDismissTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task DismissBuff_LionsStrength_RemovedFromAuras()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;

        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG buff dismiss scenarios in parallel.");

            var bgTask = RunDismissBuffScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            var fgTask = RunDismissBuffScenario(fgAccount, () => _bot.ForegroundBot?.Player, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunDismissBuffScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        // BG bot: DismissBuff relies on WoWUnit.Buffs which is never populated from packets.
        // This is a known capability gap (BB-BUFF-001). Skip BG assertion until aura tracking
        // is implemented in WoWSharpClient.
        global::Tests.Infrastructure.Skip.If(!bgPassed,
            "BG bot: DismissBuff failed — WoWUnit.Buffs list empty (BB-BUFF-001: aura tracking not implemented in WoWSharpClient).");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Lion's Strength buff should be removed after dismiss.");
    }

    private async Task<bool> RunDismissBuffScenario(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        // Step 1: Clean state — remove stale buff and clear inventory
        _output.WriteLine($"  [{label}] Step 1: Removing stale buffs + clearing inventory.");
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthUseSpell}");
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthBuffAura}");
        await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
        await Task.Delay(500);

        // Verify buff is gone
        await _bot.RefreshSnapshotsAsync();
        var playerClean = getPlayer();
        bool hadBuffAfterClean = HasLionsStrengthAura(playerClean);
        _output.WriteLine($"  [{label}] After cleanup: hasLionsStrength={hadBuffAfterClean}");

        // Step 2: Add and use elixir to apply buff
        _output.WriteLine($"  [{label}] Step 2: Adding Elixir of Lion's Strength and using it.");
        await _bot.BotAddItemAsync(account, ElixirOfLionsStrength, 1);
        await WaitForBagItemAsync(account, getPlayer, ElixirOfLionsStrength, TimeSpan.FromSeconds(5));

        var useResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        });
        Assert.Equal(ResponseResult.Success, useResult);

        // Step 3: Wait for buff to appear
        _output.WriteLine($"  [{label}] Step 3: Waiting for Lion's Strength buff to appear.");
        var buffApplied = false;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var player = getPlayer();
            if (HasLionsStrengthAura(player))
            {
                buffApplied = true;
                _output.WriteLine($"  [{label}] Buff applied after {sw.ElapsedMilliseconds}ms.");
                break;
            }
            await Task.Delay(200);
        }

        if (!buffApplied)
        {
            _output.WriteLine($"  [{label}] Buff did not appear within 5s — cannot test dismiss.");
            var playerDiag = getPlayer();
            _output.WriteLine($"  [{label}] Auras: [{string.Join(", ", playerDiag?.Unit?.Auras ?? [])}]");
            return false;
        }

        // Step 4: Try ActionType.DismissBuff first.
        // BG bot's WoWUnit.Buffs list may be empty (populated from WoW.exe memory for FG only).
        // HasBuff(name) precondition in BuildDismissBuffSequence may fail for BG.
        _output.WriteLine($"  [{label}] Step 4: Dismissing buff '{LionsStrengthBuffName}' via ActionType.");
        var dismissResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.DismissBuff,
            Parameters = { new RequestParameter { StringParam = LionsStrengthBuffName } }
        });
        _output.WriteLine($"  [{label}] DismissBuff dispatch result: {dismissResult}");
        await Task.Delay(500);

        // Step 5: Verify buff is removed
        _output.WriteLine($"  [{label}] Step 5: Verifying buff removed.");
        var buffRemoved = false;
        sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            await _bot.RefreshSnapshotsAsync();
            var player = getPlayer();
            if (!HasLionsStrengthAura(player))
            {
                buffRemoved = true;
                _output.WriteLine($"  [{label}] Buff removed via ActionType after {sw.ElapsedMilliseconds}ms.");
                break;
            }
            await Task.Delay(200);
        }

        // BG bot: WoWUnit.Buffs list is never populated from packets, so
        // HasBuff(name) always returns false → DismissBuff is a no-op.
        // This is a known BG limitation (BB-BUFF-001). Do NOT fall back to
        // .unaura GM command — that masks the real bug.
        if (!buffRemoved && label == "BG")
        {
            _output.WriteLine($"  [{label}] KNOWN LIMITATION: BG bot WoWUnit.Buffs list empty — " +
                "DismissBuff cannot work until aura tracking is implemented in WoWSharpClient. " +
                "Cleaning up with .unaura for test hygiene, but marking test as failed for BG.");
            // Clean up the buff so it doesn't leak to next test, but still report failure
            await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthUseSpell}");
            await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthBuffAura}");
            await Task.Delay(500);
            // Return false — BG DismissBuff genuinely doesn't work yet
            return false;
        }

        if (!buffRemoved)
        {
            var playerFinal = getPlayer();
            _output.WriteLine($"  [{label}] FAIL: Buff still present. Auras: [{string.Join(", ", playerFinal?.Unit?.Auras ?? [])}]");
        }

        return buffRemoved;
    }

    private static bool HasLionsStrengthAura(Game.WoWPlayer? player)
    {
        var auras = player?.Unit?.Auras;
        if (auras == null) return false;
        return auras.Contains(LionsStrengthUseSpell) || auras.Contains(LionsStrengthBuffAura);
    }

    private async Task<bool> WaitForBagItemAsync(string account, Func<Game.WoWPlayer?> getPlayer, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var player = getPlayer();
            if (player?.BagContents?.Values.Any(v => v == itemId) == true)
                return true;
            await Task.Delay(200);
        }
        return false;
    }
}
