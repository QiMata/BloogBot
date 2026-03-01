using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Consumable usage integration test — dual-client validation.
///
/// Each bot (BG + FG) independently:
///   1) Add Elixir of Lion's Strength (item 2454) to bags.
///   2) Use the elixir via ActionType.UseItem.
///   3) Verify aura count increased (buff applied).
///
/// Known IDs:
///   Elixir of Lion's Strength: item 2454 (spell 2367, +4 Strength for 1h)
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~ConsumableUsageTests" --configuration Release -v n
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class ConsumableUsageTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint ElixirOfLionsStrength = 2454;

    public ConsumableUsageTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task UseConsumable_ElixirOfLionsStrength_BuffApplied()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        if (_bot.ForegroundBot != null)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG consumable scenarios in parallel.");

            var bgTask = RunConsumableScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            var fgTask = RunConsumableScenario(fgAccount, () => _bot.ForegroundBot?.Player, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunConsumableScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Expected aura increase after using Elixir of Lion's Strength.");
        if (_bot.ForegroundBot != null)
            Assert.True(fgPassed, "FG bot: Expected aura increase after using Elixir of Lion's Strength.");
    }

    private const uint LionsStrengthSpellId = 2367; // Spell applied by Elixir of Lion's Strength

    private async Task<bool> RunConsumableScenario(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        // Enable GM mode for setup safety (invulnerability, no mob aggro).
        await _bot.SendGmChatCommandAsync(account, ".gm on");

        // --- Step 0: Remove stale Lion's Strength buff and clear inventory ---
        _output.WriteLine($"  [{label}] Step 0: Remove stale buffs + clear inventory");
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthSpellId}");
        await _bot.BotClearInventoryAsync(account, includeExtraBags: false);

        // Record aura state BEFORE
        await _bot.RefreshSnapshotsAsync();
        var playerBefore = getPlayer();
        int aurasBefore = playerBefore?.Unit?.Auras?.Count ?? 0;
        bool hadBuff = playerBefore?.Unit?.Auras?.Contains(LionsStrengthSpellId) == true;
        _output.WriteLine($"  [{label}] Auras before: {aurasBefore}, hasLionsStrength={hadBuff}");

        // --- Step 1: Add elixir via GM chat ---
        _output.WriteLine($"  [{label}] Step 1: Add Elixir of Lion's Strength (item {ElixirOfLionsStrength})");
        await _bot.BotAddItemAsync(account, ElixirOfLionsStrength, 3);
        _output.WriteLine($"  [{label}] Waiting for item to arrive...");
        await Task.Delay(2000);

        // Verify elixir was added
        await _bot.RefreshSnapshotsAsync();
        var playerAfterAdd = getPlayer();
        if (playerAfterAdd != null)
        {
            bool hasElixir = false;
            foreach (var kvp in playerAfterAdd.BagContents)
            {
                if (kvp.Value == ElixirOfLionsStrength)
                {
                    _output.WriteLine($"  [{label}] Found elixir at bag slot [{kvp.Key}]");
                    hasElixir = true;
                    break;
                }
            }
            if (!hasElixir)
                _output.WriteLine($"  [{label}] WARNING: Elixir {ElixirOfLionsStrength} not found in bags (bags may be full, count={playerAfterAdd.BagContents.Count})!");
        }

        // --- Step 2: Use elixir via action forwarding ---
        _output.WriteLine($"  [{label}] Step 2: Use elixir via UseItem action");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        }, delayMs: 5000);

        // --- Step 3: Verify buff is active (check for specific spell ID, not count increase) ---
        _output.WriteLine($"  [{label}] Step 3: Check auras for Lion's Strength buff (spell {LionsStrengthSpellId})");
        await _bot.RefreshSnapshotsAsync();
        var player = getPlayer();
        if (player == null)
        {
            _output.WriteLine($"  [{label}] Player snapshot null — skipping");
            return false;
        }

        int aurasAfter = player.Unit?.Auras?.Count ?? 0;
        bool hasBuff = player.Unit?.Auras?.Contains(LionsStrengthSpellId) == true;
        _output.WriteLine($"  [{label}] Auras after: {aurasAfter} (before: {aurasBefore}), hasLionsStrength={hasBuff}");

        if (player.Unit?.Auras != null)
        {
            foreach (var aura in player.Unit.Auras)
                _output.WriteLine($"    Aura: {aura}{(aura == LionsStrengthSpellId ? " <-- LION'S STRENGTH" : "")}");
        }

        return hasBuff;
    }
}
