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
/// Talent allocation integration test - dual-client validation.
///
/// Flow per client:
///   1) Ensure strict-alive setup.
///   2) Ensure level >= 10 (only if needed).
///   3) Ensure target talent spell is absent (only if needed).
///   4) Learn talent via GM command.
///   5) Assert spell appears in snapshot spell list.
/// </summary>
<<<<<<< HEAD
[RequiresMangosStack]
=======
>>>>>>> cpp_physics_system
[Collection(LiveValidationCollection.Name)]
public class TalentAllocationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint Deflection1 = 16462;
    private const uint MinTalentLevel = 10;
<<<<<<< HEAD
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD
=======
>>>>>>> cpp_physics_system

    public TalentAllocationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Talent_LearnViaGM_SpellAppearsInKnownSpells()
    {
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ===");
<<<<<<< HEAD
        var bgLearned = await RunTalentScenario(_bot.BgAccountName!, "BG");
        Assert.True(bgLearned, "[BG] Spell 16462 should appear in snapshot spell list after .learn.");

        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine($"\n=== FG Bot: {_bot.FgCharacterName} ===");
            var fgLearned = await RunTalentScenario(_bot.FgAccountName!, "FG");
            if (!fgLearned)
                _output.WriteLine("[FG] WARNING: spell 16462 not visible in FG snapshot spell list; BG path remains authoritative.");
        }
        else
        {
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
=======

        bool bgLearned, fgLearned = false;
        var hasFg = _bot.IsFgActionable;
        if (hasFg)
        {
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ===");
            _output.WriteLine("[PARITY] Running BG and FG talent scenarios in parallel.");

            var bgTask = RunTalentScenario(_bot.BgAccountName!, "BG");
            var fgTask = RunTalentScenario(_bot.FgAccountName!, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgLearned = await bgTask;
            fgLearned = await fgTask;
        }
        else
        {
            bgLearned = await RunTalentScenario(_bot.BgAccountName!, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgLearned, "[BG] Spell 16462 should appear in snapshot spell list after .learn.");
        if (hasFg)
        {
            Assert.True(fgLearned, "[FG] Spell 16462 should appear in FG snapshot spell list after .learn.");
        }
>>>>>>> cpp_physics_system
    }

    private async Task<bool> RunTalentScenario(string account, string label)
    {
<<<<<<< HEAD
        await EnsureStrictAliveAsync(account, label);
        await EnsureLevelAtLeastAsync(account, label, MinTalentLevel);
        _ = await TryEnsureSpellAbsentAsync(account, label, Deflection1);

        _output.WriteLine($"  [{label}] Learning spell {Deflection1}");
        var learnTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".learn {Deflection1}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(learnTrace, label, ".learn");

=======
        await _bot.EnsureCleanSlateAsync(account, label);
        await EnsureLevelAtLeastAsync(account, label, MinTalentLevel);
        var spellCleared = await TryEnsureSpellAbsentAsync(account, label, Deflection1);
        Assert.True(spellCleared,
            $"[{label}] .unlearn {Deflection1} failed — spell still known. " +
            "Cannot validate .learn if the spell is already present (test would trivially pass).");

        _output.WriteLine($"  [{label}] Learning spell {Deflection1}");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
        var learnTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".learn {Deflection1}", captureResponse: true, delayMs: 1000);

        // If the action was dropped (bot in dead/ghost state at dispatch time), re-confirm alive and retry once.
        // This handles the case where a prior test crash left the FG bot's snapshot showing health=0.
        if (learnTrace.DispatchResult == ResponseResult.Failure)
        {
            _output.WriteLine($"  [{label}] .learn was dropped (dead/ghost state at dispatch); ensuring alive and retrying...");
            await _bot.EnsureCleanSlateAsync(account, label);
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(300);
            learnTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".learn {Deflection1}", captureResponse: true, delayMs: 1000);
            Assert.True(learnTrace.DispatchResult == ResponseResult.Success,
                $"[{label}] .learn retry also failed (DispatchResult={learnTrace.DispatchResult}). Bot may be stuck in dead/ghost state.");
        }

        AssertCommandSucceeded(learnTrace, label, ".learn");

        // Diagnostic: dump snapshot state immediately after .learn dispatch
        {
            var diagSnap = await _bot.GetSnapshotAsync(account);
            var spellCount = diagSnap?.Player?.SpellList?.Count ?? -1;
            var health = diagSnap?.Player?.Unit?.Health ?? 0;
            var screen = diagSnap?.ScreenState ?? "(null)";
            var charName = diagSnap?.CharacterName ?? "(null)";
            var hasSpell = diagSnap?.Player?.SpellList?.Contains(Deflection1) == true;
            _output.WriteLine($"  [{label}] POST-LEARN snapshot: screen={screen}, char={charName}, health={health}, spells={spellCount}, has16462={hasSpell}");
        }

>>>>>>> cpp_physics_system
        var learned = await WaitForSpellPresenceAsync(account, Deflection1, shouldExist: true, TimeSpan.FromSeconds(12));
        return learned;
    }

<<<<<<< HEAD
    private async Task EnsureStrictAliveAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (IsStrictAlive(snap))
            return;

        var characterName = snap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(characterName), $"{label}: missing character name for revive setup.");

        _output.WriteLine($"  [{label}] Not strict-alive; reviving before talent setup.");
        await _bot.RevivePlayerAsync(characterName!);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account);
            if (IsStrictAlive(snap))
                return;
        }

        global::Tests.Infrastructure.Skip.If(true, $"{label}: could not establish strict-alive setup state.");
    }
=======
    private Task EnsureStrictAliveAsync(string account, string label)
        => _bot.EnsureStrictAliveAsync(account, label);
>>>>>>> cpp_physics_system

    private async Task EnsureLevelAtLeastAsync(string account, string label, uint minLevel)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var level = snap?.Player?.Unit?.GameObject?.Level ?? 0;
        if (level >= minLevel)
            return;

        _output.WriteLine($"  [{label}] Level {level} < {minLevel}; setting level.");
        var levelTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".character level {minLevel}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(levelTrace, label, ".character level");

        var leveled = await WaitForLevelAtLeastAsync(account, minLevel, TimeSpan.FromSeconds(15));
        Assert.True(leveled, $"[{label}] Player level should reach >= {minLevel} after level setup command.");
    }

    private async Task<bool> TryEnsureSpellAbsentAsync(string account, string label, uint spellId)
    {
<<<<<<< HEAD
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var hasSpell = snap?.Player?.SpellList?.Contains(spellId) == true;
        if (!hasSpell)
            return true;

        _output.WriteLine($"  [{label}] Spell {spellId} already known; unlearning for clean setup.");
        var unlearnTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".unlearn {spellId}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(unlearnTrace, label, ".unlearn");

        var removed = await WaitForSpellPresenceAsync(account, spellId, shouldExist: false, TimeSpan.FromSeconds(10));
        if (!removed)
        {
            _output.WriteLine($"  [{label}] WARNING: spell {spellId} remained known after .unlearn; continuing with learn assertion.");
            return false;
        }

=======
        // Always send .unlearn regardless of snapshot state.
        // The server may have the spell even if the client memory scan doesn't show it
        // (client-server desync from prior sessions). By always unlearning:
        //   1. We guarantee the server has the spell removed.
        //   2. When .learn is sent next, SMSG_LEARNED_SPELL fires → WoW.exe memory updated → RefreshSpells picks it up.
        _output.WriteLine($"  [{label}] Ensuring spell {spellId} absent on server (always unlearn for clean state).");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
        // Use tracked version so we can detect if the action was silently dropped (e.g. stale dead-state).
        // Ignore "you haven't learned that spell" response — that just means it was already absent.
        var unlearn = await _bot.SendGmChatCommandTrackedAsync(account, $".unlearn {spellId}", captureResponse: false, delayMs: 1000);
        if (unlearn.DispatchResult == ResponseResult.Failure)
        {
            _output.WriteLine($"  [{label}] WARNING: .unlearn {spellId} was DROPPED (bot in dead/ghost state at dispatch); ensuring alive and retrying.");
            await _bot.EnsureCleanSlateAsync(account, label);
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(300);
            unlearn = await _bot.SendGmChatCommandTrackedAsync(account, $".unlearn {spellId}", captureResponse: false, delayMs: 1000);
        }
        if (unlearn.DispatchResult == ResponseResult.Failure)
        {
            _output.WriteLine($"  [{label}] ERROR: .unlearn {spellId} dropped twice — cannot guarantee clean state.");
            return false;
        }
>>>>>>> cpp_physics_system
        return true;
    }

    private async Task<bool> WaitForSpellPresenceAsync(string account, uint spellId, bool shouldExist, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
<<<<<<< HEAD
=======
        int pollCount = 0;
>>>>>>> cpp_physics_system
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var hasSpell = snap?.Player?.SpellList?.Contains(spellId) == true;
<<<<<<< HEAD
=======
            var spellCount = snap?.Player?.SpellList?.Count ?? -1;
            var health = snap?.Player?.Unit?.Health ?? 0;
            var screen = snap?.ScreenState ?? "(null)";
            if (pollCount % 4 == 0 || hasSpell == shouldExist) // log every 2s or on success
                _output.WriteLine($"  [{account}] poll#{pollCount} {sw.Elapsed.TotalSeconds:F1}s: screen={screen}, health={health}, spells={spellCount}, has{spellId}={hasSpell}");
            pollCount++;
>>>>>>> cpp_physics_system
            if (hasSpell == shouldExist)
                return true;

            await Task.Delay(500);
        }

        return false;
    }

    private async Task<bool> WaitForLevelAtLeastAsync(string account, uint minLevel, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var level = snap?.Player?.Unit?.GameObject?.Level ?? 0;
            if (level >= minLevel)
                return true;

            await Task.Delay(500);
        }

        return false;
    }

    private void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

<<<<<<< HEAD
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }

    private static bool ContainsCommandRejection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("no such command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no such subcommand", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unknown command", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not available to you", StringComparison.OrdinalIgnoreCase);
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
=======
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }

>>>>>>> cpp_physics_system
}
