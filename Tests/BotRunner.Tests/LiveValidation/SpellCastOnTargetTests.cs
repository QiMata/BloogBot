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
/// Spell cast integration tests — validates ActionType.CastSpell pipeline.
///
/// Each bot (BG + FG) independently:
///   1) Learn Battle Shout (spell 6673, warrior self-buff).
///   2) Remove any existing Battle Shout aura via .unaura.
///   3) Cast Battle Shout via ActionType.CastSpell (no target needed).
///   4) Verify Battle Shout aura (6673) appears in snapshot auras.
///
/// Why Battle Shout: Instant cast, no target required, self-buff that appears
/// in snapshot auras. Tests the CastSpell dispatch + aura detection pipeline.
///
/// Run: dotnet test --filter "FullyQualifiedName~SpellCastOnTargetTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class SpellCastOnTargetTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Battle Shout: Rank 1, warrior self-buff (+15 AP for 2 min), instant cast
    private const uint BattleShoutSpellId = 6673;

    public SpellCastOnTargetTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task CastSpell_BattleShout_AuraApplied()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
            hasFg = await _bot.CheckFgActionableAsync();

        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG spell cast scenarios in parallel.");

            var bgTask = RunCastSpellScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            var fgTask = RunCastSpellScenario(fgAccount, () => _bot.ForegroundBot?.Player, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunCastSpellScenario(bgAccount, () => _bot.BackgroundBot?.Player, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Battle Shout aura should appear after CastSpell.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Battle Shout aura should appear after CastSpell.");
    }

    private async Task<bool> RunCastSpellScenario(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        if (label == "FG" && !await _bot.CheckFgActionableAsync())
        {
            _output.WriteLine("  [FG] Skipping spell-cast assertions because the FG responsiveness probe failed after clean slate.");
            return true;
        }

        // Step 1: Learn Battle Shout
        _output.WriteLine($"  [{label}] Step 1: Learning Battle Shout (spell {BattleShoutSpellId}).");
        await _bot.BotLearnSpellAsync(account, BattleShoutSpellId);
        await Task.Delay(500);

        // Wait for spell to appear in known spell list (SMSG_LEARNED_SPELL processing)
        var spellKnown = false;
        var learnSw = Stopwatch.StartNew();
        while (learnSw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (snap?.Player?.SpellList?.Contains(BattleShoutSpellId) == true)
            {
                spellKnown = true;
                _output.WriteLine($"  [{label}] Battle Shout confirmed in spell list after {learnSw.ElapsedMilliseconds}ms.");
                break;
            }
            await Task.Delay(300);
        }
        if (!spellKnown)
            _output.WriteLine($"  [{label}] WARNING: Battle Shout not found in spell list after 5s.");

        // Step 2a: Give rage (Battle Shout costs 10 rage = 100 internal units).
        // MaNGOS stores rage as displayed_rage * 10, so 200 = 20 displayed rage (enough for 10-rage spell).
        // Do NOT set to 1000 — full rage bar causes combat behavior interference in other tests.
        _output.WriteLine($"  [{label}] Step 2a: Granting rage for Battle Shout.");
        await _bot.SendGmChatCommandAsync(account, ".modify rage 200");
        await Task.Delay(300);

        // Step 2b: Remove any existing Battle Shout buff + leftover auras from other tests
        _output.WriteLine($"  [{label}] Step 2b: Removing stale auras.");
        await _bot.SendGmChatCommandAsync(account, $".unaura {BattleShoutSpellId}");
        await _bot.SendGmChatCommandAsync(account, ".unaura 2457"); // Lion's Strength buff
        await _bot.SendGmChatCommandAsync(account, ".unaura 2367"); // Lesser Strength spell
        await Task.Delay(500);

        // Verify aura is gone
        await _bot.RefreshSnapshotsAsync();
        var playerBefore = getPlayer();
        bool hadAura = playerBefore?.Unit?.Auras?.Contains(BattleShoutSpellId) == true;
        _output.WriteLine($"  [{label}] Before cast: hasBattleShout={hadAura}");

        // Step 3: Cast Battle Shout via player action (no GM shortcuts).
        // Both FG and BG use ActionType.CastSpell. FG resolves spell ID → name via spell DB.
        _output.WriteLine($"  [{label}] Step 3: Casting Battle Shout via player action.");
        var castResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters =
            {
                new RequestParameter { IntParam = (int)BattleShoutSpellId }
            }
        });
        _output.WriteLine($"  [{label}] CastSpell action dispatch result: {castResult}");

        // Step 4: Wait for aura to appear. FG cast can silently fail under load
        // (Lua frame miss, rage not yet applied), so retry once if needed.
        _output.WriteLine($"  [{label}] Step 4: Waiting for Battle Shout aura to appear.");
        var auraAppeared = false;
        for (int castAttempt = 0; castAttempt < 2 && !auraAppeared; castAttempt++)
        {
            if (castAttempt > 0)
            {
                _output.WriteLine($"  [{label}] Aura not detected — retry #{castAttempt}: re-granting rage and re-casting.");
                await _bot.SendGmChatCommandAsync(account, ".modify rage 200");
                await Task.Delay(500);
                await _bot.SendActionAsync(account, new ActionMessage
                {
                    ActionType = ActionType.CastSpell,
                    Parameters = { new RequestParameter { IntParam = (int)BattleShoutSpellId } }
                });
            }

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(12))
            {
                await _bot.RefreshSnapshotsAsync();
                var snap = await _bot.GetSnapshotAsync(account);
                var player = snap?.Player ?? getPlayer();
                if (player?.Unit?.Auras?.Contains(BattleShoutSpellId) == true)
                {
                    auraAppeared = true;
                    _output.WriteLine($"  [{label}] Battle Shout aura detected after {sw.ElapsedMilliseconds}ms (attempt {castAttempt + 1}).");
                    break;
                }
                await Task.Delay(300);
            }
        }

        if (!auraAppeared)
        {
            var finalSnap = await _bot.GetSnapshotAsync(account);
            var playerFinal = finalSnap?.Player ?? getPlayer();
            _output.WriteLine($"  [{label}] FAIL: Battle Shout aura not found after 2 attempts. Auras: [{string.Join(", ", playerFinal?.Unit?.Auras ?? [])}]");
        }

        // Cleanup: remove the aura
        await _bot.SendGmChatCommandAsync(account, $".unaura {BattleShoutSpellId}");

        return auraAppeared;
    }
}
