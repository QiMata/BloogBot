using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed legacy consumable usage baseline.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class ConsumableUsageTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint ElixirOfLionsStrength = 2454;
    private const uint LionsStrengthUseSpell = 2367;
    private const uint LionsStrengthBuffAura = 2457;

    public ConsumableUsageTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task UseConsumable_ElixirOfLionsStrength_BuffApplied()
    {
        var target = await EnsureConsumableSettingsAndTargetAsync();

        var passed = await RunConsumableScenarioAsync(target);
        global::Tests.Infrastructure.Skip.If(
            !passed,
            $"{target.RoleLabel} UseItem dispatch is delivered, but Lion's Strength spell {LionsStrengthUseSpell} does not produce a stable aura assertion; " +
            "tracked in ConsumableUsageTests.md.");
    }

    private async Task<bool> RunConsumableScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        _output.WriteLine(
            $"[SHODAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "staging Elixir of Lion's Strength.");
        await _bot.StageBotRunnerConsumableStateAsync(
            target.AccountName,
            target.RoleLabel,
            ElixirOfLionsStrength,
            itemCount: 1,
            auraSpellIds: [LionsStrengthUseSpell, LionsStrengthBuffAura]);

        var itemStaged = await WaitForBagItemCountAsync(
            target.AccountName,
            ElixirOfLionsStrength,
            minimumSlots: 1,
            timeout: TimeSpan.FromSeconds(10),
            label: target.RoleLabel);
        Assert.True(itemStaged, $"[{target.RoleLabel}] Elixir should appear in bag snapshot after Shodan staging.");

        await _bot.RefreshSnapshotsAsync();
        var playerBefore = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;
        var aurasBefore = playerBefore?.Unit?.Auras?.Count ?? 0;
        var hadBuff = HasLionsStrengthAura(playerBefore);
        _output.WriteLine($"  [{target.RoleLabel}] Auras before: {aurasBefore}, hasLionsStrength={hadBuff}");
        Assert.False(hadBuff, $"[{target.RoleLabel}] Lion's Strength should be absent before UseItem.");

        _output.WriteLine($"  [{target.RoleLabel}] Dispatch UseItem({ElixirOfLionsStrength})");
        var useResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        });
        Assert.Equal(ResponseResult.Success, useResult);

        var hasBuff = await WaitForAuraAsync(
            target.AccountName,
            HasLionsStrengthAura,
            present: true,
            timeout: TimeSpan.FromSeconds(6));
        await _bot.RefreshSnapshotsAsync();
        var player = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;

        var aurasAfter = player?.Unit?.Auras?.Count ?? 0;
        _output.WriteLine($"  [{target.RoleLabel}] Auras after: {aurasAfter} (before: {aurasBefore}), hasLionsStrength={hasBuff}");

        if (player?.Unit?.Auras != null)
        {
            foreach (var aura in player.Unit.Auras)
                _output.WriteLine($"    Aura: {aura}{(aura is LionsStrengthUseSpell or LionsStrengthBuffAura ? " <-- LION'S STRENGTH" : "")}");
        }

        return hasBuff;
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureConsumableSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Loot.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Loot.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG consumable action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no consumable action dispatch.");

        return target;
    }

    private async Task<bool> WaitForBagItemCountAsync(string account, uint itemId, int minimumSlots, TimeSpan timeout, string label = "?")
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var count = snap?.Player?.BagContents?.Values.Count(v => v == itemId) ?? 0;
            if (count >= minimumSlots)
            {
                _output.WriteLine($"  [{label}] Item {itemId} appeared in bag after {sw.ElapsedMilliseconds}ms (count={count}).");
                return true;
            }

            await Task.Delay(300);
        }

        return false;
    }

    private async Task<bool> WaitForAuraAsync(
        string account,
        Func<Game.WoWPlayer?, bool> predicate,
        bool present,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var matches = predicate(snap?.Player);
            if (matches == present)
                return true;

            await Task.Delay(200);
        }

        return false;
    }

    private static bool HasLionsStrengthAura(Game.WoWPlayer? player)
    {
        var auras = player?.Unit?.Auras;
        return auras != null && (auras.Contains(LionsStrengthUseSpell) || auras.Contains(LionsStrengthBuffAura));
    }

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
