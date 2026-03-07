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
        var hasFg = _bot.ForegroundBot != null;
        if (hasFg)
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
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Expected aura increase after using Elixir of Lion's Strength.");
    }

    private const uint LionsStrengthSpellId = 2367; // Spell applied by Elixir of Lion's Strength

    private async Task<bool> RunConsumableScenario(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        // --- Step 0: Remove stale Lion's Strength buff and clear inventory ---
        _output.WriteLine($"  [{label}] Step 0: Remove stale buffs + clear inventory");
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthSpellId}");
        await _bot.BotClearInventoryAsync(account, includeExtraBags: false);

        // Record aura state BEFORE
        await _bot.RefreshSnapshotsAsync();
        var playerBefore = getPlayer();
        Assert.NotNull(playerBefore);
        int aurasBefore = playerBefore?.Unit?.Auras?.Count ?? 0;
        bool hadBuff = playerBefore?.Unit?.Auras?.Contains(LionsStrengthSpellId) == true;
        _output.WriteLine($"  [{label}] Auras before: {aurasBefore}, hasLionsStrength={hadBuff}");

        // --- Step 1: Add elixir via GM chat ---
        _output.WriteLine($"  [{label}] Step 1: Add Elixir of Lion's Strength (item {ElixirOfLionsStrength})");
        await _bot.BotAddItemAsync(account, ElixirOfLionsStrength, 3);

        // Poll for item to appear in snapshot (FG injection client needs more time
        // for WoW.exe memory to reflect GM-added items).
        bool hasElixir = false;
        for (int poll = 0; poll < 15 && !hasElixir; poll++)
        {
            await Task.Delay(200);
            await _bot.RefreshSnapshotsAsync();
            var playerCheck = getPlayer();
            if (playerCheck?.BagContents != null)
            {
                foreach (var kvp in playerCheck.BagContents)
                {
                    if (kvp.Value == ElixirOfLionsStrength)
                    {
                        _output.WriteLine($"  [{label}] Found elixir at bag slot [{kvp.Key}] (poll {poll + 1})");
                        hasElixir = true;
                        break;
                    }
                }
            }
        }
        if (!hasElixir)
        {
            var playerAfterAdd = getPlayer();
            _output.WriteLine($"  [{label}] WARNING: Elixir {ElixirOfLionsStrength} not found in bags after 15 polls (bags count={playerAfterAdd?.BagContents.Count ?? 0})!");
        }

        // --- Step 2: Use elixir via action forwarding ---
        _output.WriteLine($"  [{label}] Step 2: Use elixir via UseItem action");
        var useResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        });
        // AST-4: Verify dispatch was accepted — silently rejected UseItem masks real failures.
        Assert.Equal(ResponseResult.Success, useResult);
        await Task.Delay(500);

        // --- Step 3: Poll for buff (buff application is near-instant after item use) ---
        _output.WriteLine($"  [{label}] Step 3: Polling for Lion's Strength buff (spell {LionsStrengthSpellId})");
        bool hasBuff = false;
        Game.WoWPlayer? player = null;
        var buffSw = Stopwatch.StartNew();
        while (buffSw.Elapsed < TimeSpan.FromSeconds(3))
        {
            await _bot.RefreshSnapshotsAsync();
            player = getPlayer();
            if (player?.Unit?.Auras?.Contains(LionsStrengthSpellId) == true)
            {
                hasBuff = true;
                _output.WriteLine($"  [{label}] Buff detected after {buffSw.ElapsedMilliseconds}ms");
                break;
            }
            await Task.Delay(200);
        }

        if (player == null)
        {
            _output.WriteLine($"  [{label}] Player snapshot null — skipping");
            return false;
        }

        int aurasAfter = player.Unit?.Auras?.Count ?? 0;
        _output.WriteLine($"  [{label}] Auras after: {aurasAfter} (before: {aurasBefore}), hasLionsStrength={hasBuff}");

        if (player.Unit?.Auras != null)
        {
            foreach (var aura in player.Unit.Auras)
                _output.WriteLine($"    Aura: {aura}{(aura == LionsStrengthSpellId ? " <-- LION'S STRENGTH" : "")}");
        }

        return hasBuff;
    }
}
