using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// BG-only corpse recovery integration test.
///
/// Flow: teleport to Orgrimmar -> kill -> release -> wait for graveyard relocation ->
/// dispatch RetrieveCorpse once -> let RetrieveCorpseTask own runback, cooldown, and reclaim.
///
/// This is the live baseline for the corpse-recovery path in:
///   - Exports/BotRunner/BotRunnerService.ActionDispatch.cs
///   - Exports/BotRunner/Tasks/RetrieveCorpseTask.cs
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class DeathCorpseRunTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float RetrieveRange = 39.0f;
    private const float GraveyardDistanceThreshold = RetrieveRange + 20.0f;
    private const float MinRunbackImprovement = 25.0f;
    private const int MaxGraveyardTeleportSeconds = 15;
    private const int MaxRecoverySeconds = 120;

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

        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount) || string.IsNullOrWhiteSpace(bgChar), "No BG bot available.");

        _output.WriteLine($"[BG-ONLY] {bgChar} -> corpse recovery is asserted on the headless bot.");
        var (bgPass, bgReason) = await RunCorpseRunScenario(bgAccount!, bgChar!, "BG");
        await CleanupAsync(bgAccount!, bgChar!);
        Assert.True(bgPass, $"[BG] {bgReason}");
    }

    private async Task<(bool passed, string reason)> RunCorpseRunScenario(string account, string charName, string label)
    {
        _output.WriteLine($"  [{label}] Step 1: Ensure alive");
        await _bot.EnsureCleanSlateAsync(account, label);

        _output.WriteLine($"  [{label}] Step 2: Teleport to Orgrimmar");
        await _bot.BotTeleportToNamedAsync(account, charName, "Orgrimmar");
        await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.Player?.Unit?.GameObject?.Base?.Position != null,
            TimeSpan.FromSeconds(5));

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return (false, "No position after teleport to Orgrimmar");

        _output.WriteLine($"  [{label}] Position after teleport: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

        _output.WriteLine($"  [{label}] Step 3: Kill");
        var deathResult = await _bot.InduceDeathForTestAsync(
            account,
            charName,
            timeoutMs: 15000,
            requireCorpseTransition: true);
        _output.WriteLine($"  [{label}] Kill result: {deathResult.Succeeded}, cmd={deathResult.Command}");
        if (!deathResult.Succeeded)
            return (false, $"Kill failed: {deathResult.Details}");

        var corpsePos = deathResult.ObservedCorpsePosition;
        if (corpsePos == null)
            return (false, "Corpse position not captured");

        _output.WriteLine($"  [{label}] Corpse at: ({corpsePos.X:F1}, {corpsePos.Y:F1}, {corpsePos.Z:F1})");

        _output.WriteLine($"  [{label}] Step 4: Release corpse");
        var releaseResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.ReleaseCorpse });
        if (releaseResult != ResponseResult.Success)
            return (false, $"ReleaseCorpse failed: {releaseResult}");

        var ghostConfirmed = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => (s.Player?.PlayerFlags & 0x10) != 0,
            TimeSpan.FromSeconds(10),
            progressLabel: $"{label} ghost-state");
        if (!ghostConfirmed)
            return (false, "Never transitioned to ghost state");

        _output.WriteLine($"  [{label}] Ghost confirmed");

        _output.WriteLine($"  [{label}] Step 5: Wait for graveyard relocation");
        var (graveyardSettled, graveyardDistanceToCorpse, initialReclaimDelay) =
            await WaitForGraveyardTransitionAsync(account, label, corpsePos);
        if (!graveyardSettled)
            return (false, $"Ghost never left corpse location (lastDist={graveyardDistanceToCorpse:F0}y, reclaimDelay={initialReclaimDelay}s)");

        _output.WriteLine($"  [{label}] Step 6: Queue RetrieveCorpse task from {graveyardDistanceToCorpse:F0}y away");
        var retrieveResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });
        if (retrieveResult != ResponseResult.Success)
            return (false, $"RetrieveCorpse failed: {retrieveResult}");

        _output.WriteLine($"  [{label}] Step 7: Observe RetrieveCorpseTask runback/cooldown/reclaim");
        var (alive, bestDistanceToCorpse, bestReclaimDelay, reachedCorpseRange) =
            await WaitForCorpseRecoveryAsync(account, label, corpsePos, graveyardDistanceToCorpse, initialReclaimDelay);
        if (!alive)
        {
            var improvement = graveyardDistanceToCorpse - bestDistanceToCorpse;
            if (improvement < MinRunbackImprovement)
                return (false, $"RetrieveCorpseTask never reduced corpse distance enough (start={graveyardDistanceToCorpse:F0}y, best={bestDistanceToCorpse:F0}y)");
            if (!reachedCorpseRange)
                return (false, $"RetrieveCorpseTask improved runback but never reached reclaim range (best={bestDistanceToCorpse:F0}y, bestDelay={bestReclaimDelay}s)");
            if (bestReclaimDelay > 0)
                return (false, $"RetrieveCorpseTask reached corpse range but reclaim delay never elapsed (bestDelay={bestReclaimDelay}s)");
            return (false, $"RetrieveCorpseTask reached corpse range but bot never returned to strict-alive (best={bestDistanceToCorpse:F0}y)");
        }

        _output.WriteLine($"  [{label}] PASSED -> release + RetrieveCorpseTask restored strict-alive state");
        return (true, string.Empty);
    }

    private async Task<(bool settled, float distanceToCorpse, uint reclaimDelay)> WaitForGraveyardTransitionAsync(
        string account,
        string label,
        global::Game.Position corpsePos)
    {
        var sw = Stopwatch.StartNew();
        float lastDistanceToCorpse = 0f;
        uint lastReclaimDelay = 0;

        while (sw.Elapsed < TimeSpan.FromSeconds(MaxGraveyardTeleportSeconds))
        {
            await Task.Delay(500);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var ghostPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (ghostPos == null)
                continue;

            lastDistanceToCorpse = LiveBotFixture.Distance2D(ghostPos.X, ghostPos.Y, corpsePos.X, corpsePos.Y);
            lastReclaimDelay = snap?.Player?.CorpseRecoveryDelaySeconds ?? 0;
            if (lastDistanceToCorpse <= GraveyardDistanceThreshold)
                continue;

            _output.WriteLine(
                $"  [{label}] Graveyard settled at ({ghostPos.X:F1}, {ghostPos.Y:F1}, {ghostPos.Z:F1}), {lastDistanceToCorpse:F0}y from corpse, reclaimDelay={lastReclaimDelay}s");
            return (true, lastDistanceToCorpse, lastReclaimDelay);
        }

        return (false, lastDistanceToCorpse, lastReclaimDelay);
    }

    private async Task<(bool alive, float bestDistanceToCorpse, uint bestReclaimDelay, bool reachedCorpseRange)> WaitForCorpseRecoveryAsync(
        string account,
        string label,
        global::Game.Position corpsePos,
        float startDistanceToCorpse,
        uint initialReclaimDelay)
    {
        var sw = Stopwatch.StartNew();
        var lastLogTime = Stopwatch.StartNew();
        var bestDistanceToCorpse = startDistanceToCorpse;
        var bestReclaimDelay = initialReclaimDelay;
        var reachedCorpseRange = false;

        while (sw.Elapsed < TimeSpan.FromSeconds(MaxRecoverySeconds))
        {
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);

            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                var distanceToCorpse = LiveBotFixture.Distance2D(pos.X, pos.Y, corpsePos.X, corpsePos.Y);
                if (distanceToCorpse < bestDistanceToCorpse)
                    bestDistanceToCorpse = distanceToCorpse;
                if (distanceToCorpse <= RetrieveRange)
                    reachedCorpseRange = true;
            }

            var reclaimDelay = snap?.Player?.CorpseRecoveryDelaySeconds ?? 0;
            if (reclaimDelay < bestReclaimDelay)
                bestReclaimDelay = reclaimDelay;

            if (LiveBotFixture.IsStrictAlive(snap))
            {
                _output.WriteLine(
                    $"  [{label}] Alive after {sw.Elapsed.TotalSeconds:F0}s (bestDist={bestDistanceToCorpse:F0}y, bestDelay={bestReclaimDelay}s)");
                return (true, bestDistanceToCorpse, bestReclaimDelay, reachedCorpseRange);
            }

            if (lastLogTime.Elapsed.TotalSeconds >= 10)
            {
                var currentPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                _output.WriteLine(
                    $"  [{label}] Recovery: pos=({currentPos?.X ?? 0:F1},{currentPos?.Y ?? 0:F1},{currentPos?.Z ?? 0:F1}) bestDist={bestDistanceToCorpse:F0}y reclaimDelay={reclaimDelay}s inRange={reachedCorpseRange}");
                lastLogTime.Restart();
            }
        }

        return (false, bestDistanceToCorpse, bestReclaimDelay, reachedCorpseRange);
    }

    private async Task CleanupAsync(string bgAccount, string bgChar)
    {
        await _bot.RevivePlayerAsync(bgChar);
        await Task.Delay(1000);

        await _bot.BotTeleportToNamedAsync(bgAccount, bgChar, "Orgrimmar");
        await Task.Delay(1000);
    }
}
