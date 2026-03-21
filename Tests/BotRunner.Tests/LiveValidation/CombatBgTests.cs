using System;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// BG combat test: COMBATTEST is the BG bot (headless), with FG bot (TESTBOT1)
/// positioned nearby as a GM camera so the human can observe.
/// Uses CombatBg.settings.json via CombatBgBotFixture — no runtime settings switching.
/// </summary>
[Collection(CombatBgValidationCollection.Name)]
public class CombatBgTests
{
    private readonly CombatBgBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public CombatBgTests(CombatBgBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Combat_BG_AutoAttacksMob_WithFgObserver()
    {
        var combatAccount = _bot.CombatTestAccountName;
        Assert.NotNull(combatAccount);
        Assert.True(!string.Equals(combatAccount, _bot.FgAccountName, StringComparison.OrdinalIgnoreCase),
            $"COMBATTEST ({combatAccount}) should NOT be the FG bot after CombatBg config");

        _output.WriteLine($"=== BG Combat Test: {_bot.CombatTestCharacterName} ({combatAccount}) ===");
        _output.WriteLine($"FG observer: {_bot.FgAccountName} (GM on, camera)");

        var killed = await CombatTestHelpers.RunCombatScenarioAsync(_bot, _output, combatAccount, observerAccount: _bot.FgAccountName);
        Assert.True(killed, "BG COMBATTEST must approach, face, and auto-attack a mob to death.");
    }
}
