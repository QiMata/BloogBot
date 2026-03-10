using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Death and corpse-run integration test.
///
/// Flow: teleport to Orgrimmar → kill → release → ghost runs back to corpse → retrieve → alive.
/// Uses the "Orgrimmar" named .tele location (on top of the bank). Simple flat terrain, short run.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class DeathCorpseRunTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float RetrieveRange = 39.0f;
    private const int MaxRunbackSeconds = 60;
    private const int MaxReclaimWaitSeconds = 45;

    public DeathCorpseRunTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService not available on port 5001.");
    }

    [SkippableFact]
    public async Task Death_ReleaseAndRetrieve_ResurrectsPlayer()
    {
        var bgAccount = _bot.BgAccountName;
        var bgChar = _bot.BgCharacterName;
        var fgAccount = _bot.FgAccountName;
        var fgChar = _bot.FgCharacterName;

        var bgAvailable = !string.IsNullOrWhiteSpace(bgAccount) && !string.IsNullOrWhiteSpace(bgChar);
        var fgAvailable = _bot.IsFgActionable && !string.IsNullOrWhiteSpace(fgAccount) && !string.IsNullOrWhiteSpace(fgChar);

        global::Tests.Infrastructure.Skip.If(!bgAvailable, "No BG bot available.");

        // Run scenarios
        if (bgAvailable && fgAvailable)
        {
            _output.WriteLine($"[PARITY] BG={bgChar}, FG={fgChar} — running in parallel.");
            var bgTask = RunCorpseRunScenario(bgAccount!, bgChar!, "BG");
            var fgTask = RunCorpseRunScenario(fgAccount!, fgChar!, "FG");
            await Task.WhenAll(bgTask, fgTask);

            var (bgPass, bgReason) = await bgTask;
            var (fgPass, fgReason) = await fgTask;

            // Cleanup: revive and teleport back regardless of outcome
            await CleanupAsync(bgAccount!, bgChar!, fgAccount!, fgChar!);

            Assert.True(bgPass, $"[BG] {bgReason}");
            Assert.True(fgPass, $"[FG] {fgReason}");
        }
        else
        {
            _output.WriteLine($"[BG only] {bgChar}");
            var (bgPass, bgReason) = await RunCorpseRunScenario(bgAccount!, bgChar!, "BG");
            await CleanupAsync(bgAccount!, bgChar!);
            Assert.True(bgPass, $"[BG] {bgReason}");
        }
    }

    private async Task<(bool passed, string reason)> RunCorpseRunScenario(string account, string charName, string label)
    {
        // Step 1: Ensure alive
        _output.WriteLine($"  [{label}] Step 1: Ensure alive");
        await _bot.EnsureCleanSlateAsync(account, label);

        // Step 2: Teleport to Orgrimmar (named location — on top of bank)
        _output.WriteLine($"  [{label}] Step 2: Teleport to Orgrimmar");
        await _bot.BotTeleportToNamedAsync(account, charName, "Orgrimmar");
        // Poll for position to appear (named teleport doesn't have target coords for WaitForTeleportSettledAsync)
        await _bot.WaitForSnapshotConditionAsync(account,
            s => s.Player?.Unit?.GameObject?.Base?.Position != null, TimeSpan.FromSeconds(5));

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return (false, "No position after teleport to Orgrimmar");
        _output.WriteLine($"  [{label}] Position after teleport: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

        // Step 3: Kill
        _output.WriteLine($"  [{label}] Step 3: Kill");
        var deathResult = await _bot.InduceDeathForTestAsync(account, charName,
            timeoutMs: 15000, requireCorpseTransition: true);
        _output.WriteLine($"  [{label}] Kill result: {deathResult.Succeeded}, cmd={deathResult.Command}");
        if (!deathResult.Succeeded)
            return (false, $"Kill failed: {deathResult.Details}");

        var corpsePos = deathResult.ObservedCorpsePosition;
        if (corpsePos == null)
            return (false, "Corpse position not captured");
        _output.WriteLine($"  [{label}] Corpse at: ({corpsePos.X:F1}, {corpsePos.Y:F1}, {corpsePos.Z:F1})");

        // Step 4: Release corpse
        _output.WriteLine($"  [{label}] Step 4: Release corpse");
        var releaseResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.ReleaseCorpse });
        if (releaseResult != ResponseResult.Success)
            return (false, $"ReleaseCorpse failed: {releaseResult}");

        // Wait for ghost state
        var ghostConfirmed = await _bot.WaitForSnapshotConditionAsync(account,
            s => (s.Player?.PlayerFlags & 0x10) != 0, TimeSpan.FromSeconds(10));
        if (!ghostConfirmed)
            return (false, "Never transitioned to ghost state");
        _output.WriteLine($"  [{label}] Ghost confirmed");

        // Wait for graveyard teleport to settle (position changes after ghost release)
        await _bot.WaitForSnapshotConditionAsync(account,
            s => s.Player?.Unit?.GameObject?.Base?.Position != null, TimeSpan.FromSeconds(5));
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
        var ghostPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (ghostPos == null)
            return (false, "No ghost position");

        var distToCorpse = LiveBotFixture.Distance2D(ghostPos.X, ghostPos.Y, corpsePos.X, corpsePos.Y);
        _output.WriteLine($"  [{label}] Ghost at ({ghostPos.X:F1}, {ghostPos.Y:F1}, {ghostPos.Z:F1}), {distToCorpse:F0}y from corpse");

        // Step 5: Run back to corpse (dispatch RetrieveCorpse which triggers pathfinding)
        bool alreadyNearCorpse = distToCorpse <= RetrieveRange;
        if (!alreadyNearCorpse)
        {
            _output.WriteLine($"  [{label}] Step 5: Running back to corpse ({distToCorpse:F0}y)");
            await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });

            // Observe runback — poll every 2s, log progress
            var runSw = Stopwatch.StartNew();
            var lastLogTime = Stopwatch.StartNew();
            while (runSw.Elapsed < TimeSpan.FromSeconds(MaxRunbackSeconds))
            {
                await Task.Delay(2000);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account);
                var curPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                if (curPos == null) continue;

                distToCorpse = LiveBotFixture.Distance2D(curPos.X, curPos.Y, corpsePos.X, corpsePos.Y);

                // Log every 10s
                if (lastLogTime.Elapsed.TotalSeconds >= 10)
                {
                    _output.WriteLine($"  [{label}] Runback: ({curPos.X:F1},{curPos.Y:F1},{curPos.Z:F1}), {distToCorpse:F0}y from corpse");
                    lastLogTime.Restart();
                }

                if (distToCorpse <= RetrieveRange)
                {
                    alreadyNearCorpse = true;
                    _output.WriteLine($"  [{label}] Reached corpse range ({distToCorpse:F0}y) in {runSw.Elapsed.TotalSeconds:F0}s");
                    break;
                }
            }

            if (!alreadyNearCorpse)
                return (false, $"Did not reach corpse within {MaxRunbackSeconds}s (last dist={distToCorpse:F0}y)");
        }
        else
        {
            _output.WriteLine($"  [{label}] Already within retrieve range — skipping runback");
        }

        // Step 6: Wait for reclaim delay to reach 0
        _output.WriteLine($"  [{label}] Step 6: Wait for reclaim delay");
        var reclaimReady = await _bot.WaitForSnapshotConditionAsync(account,
            s => (s.Player?.CorpseRecoveryDelaySeconds ?? 99) <= 0,
            TimeSpan.FromSeconds(MaxReclaimWaitSeconds));
        if (!reclaimReady)
            return (false, "Reclaim delay never reached 0");

        // Step 7: Retrieve corpse
        _output.WriteLine($"  [{label}] Step 7: Retrieve corpse");
        var retrieveResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });
        if (retrieveResult != ResponseResult.Success)
            return (false, $"RetrieveCorpse failed: {retrieveResult}");

        // Step 8: Confirm alive
        _output.WriteLine($"  [{label}] Step 8: Confirm alive");
        var alive = await _bot.WaitForSnapshotConditionAsync(account,
            LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(15));
        if (!alive)
            return (false, "Not alive after corpse retrieval");

        _output.WriteLine($"  [{label}] PASSED — full death/release/runback/retrieve cycle complete");
        return (true, string.Empty);
    }

    private async Task CleanupAsync(string bgAccount, string bgChar, string? fgAccount = null, string? fgChar = null)
    {
        // Revive anyone still dead
        await _bot.RevivePlayerAsync(bgChar);
        if (!string.IsNullOrWhiteSpace(fgChar))
            await _bot.RevivePlayerAsync(fgChar!);

        await Task.Delay(1000);

        // Teleport back to safe zone
        await _bot.BotTeleportToNamedAsync(bgAccount, bgChar, "Orgrimmar");
        if (!string.IsNullOrWhiteSpace(fgAccount) && !string.IsNullOrWhiteSpace(fgChar))
            await _bot.BotTeleportToNamedAsync(fgAccount!, fgChar!, "Orgrimmar");
        await Task.Delay(1000);
    }
}
