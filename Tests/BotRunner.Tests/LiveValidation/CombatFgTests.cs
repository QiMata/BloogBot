using System;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// FG combat test: COMBATTEST is the FG bot (injected WoW.exe) — gold standard.
/// Uses CombatFg.settings.json via CombatFgBotFixture — no runtime settings switching.
/// </summary>
[Collection(CombatFgValidationCollection.Name)]
public class CombatFgTests
{
    private readonly CombatFgBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public CombatFgTests(CombatFgBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Combat_FG_AutoAttacksMob_DealsDamageInMeleeRange()
    {
        var combatAccount = _bot.CombatTestAccountName;
        Assert.NotNull(combatAccount);
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        Assert.True(string.Equals(combatAccount, _bot.FgAccountName, StringComparison.OrdinalIgnoreCase),
            $"COMBATTEST ({combatAccount}) should be the FG bot after CombatFg config");

        _output.WriteLine($"=== FG Combat Test: {_bot.CombatTestCharacterName} ({combatAccount}) ===");
        _output.WriteLine("COMBATTEST on FG (injected WoW.exe) — gold standard combat validation");

        var killed = await CombatTestHelpers.RunCombatScenarioAsync(_bot, _output, combatAccount, observerAccount: null);
        Assert.True(killed, "FG COMBATTEST must approach, face, and auto-attack a mob to death.");
    }
}
