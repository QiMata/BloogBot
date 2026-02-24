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
/// Required flow (state-driven):
///   1) strict alive from snapshot,
///   2) setup position once,
///   3) kill once (setup only),
///   4) release via client action,
///   5) confirm ghost in snapshot,
///   6) observe pathfinding corpse run (no teleport shortcut),
///   7) wait reclaim delay == 0 from snapshot,
///   8) retrieve via client action,
///   9) assert alive/non-ghost snapshot state.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class DeathCorpseRunTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD
    private const uint MoveFlagForward = 0x1;
    private const float RetrieveRange = 39.0f;
    private const float MinimumGhostToCorpseRunbackDistance = 45.0f;
    private const float MinimumGhostTravelDistance = 12.0f;
    private const float MinimumDistanceImprovement = 10.0f;
    private const int RequiredImprovementTicks = 3;

    private static readonly TimeSpan CorpseRunObservationWindow = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ReleaseToGhostTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ReclaimTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan AliveAfterRetrieveTimeout = TimeSpan.FromSeconds(20);

    public DeathCorpseRunTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    private readonly record struct LifeState(uint Health, bool Ghost, bool StandDead);

    private sealed class CorpsePhaseEvidence
    {
        public CorpsePhaseEvidence(string label) => Label = label;

        public string Label { get; }
        public bool Succeeded { get; set; }
        public bool DeadCorpsePhaseObserved { get; set; }
        public bool GhostPhaseObserved { get; set; }
        public bool MovingToCorpsePhaseObserved { get; set; }
        public bool ImmediateCorpseRangePhaseObserved { get; set; }
        public bool ReclaimReadyPhaseObserved { get; set; }
        public bool AlivePhaseObserved { get; set; }
        public bool StaleForwardFlagObserved { get; set; }
        public int PostDeathGmChatCommands { get; set; }
        public string KillCommand { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public float InitialDistanceToCorpse2D { get; set; }
        public float BestDistanceToCorpse2D { get; set; }
        public float CumulativeGhostTravel { get; set; }
        public int DistanceImprovementTicks { get; set; }
    }

    private static LifeState GetLifeState(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return new LifeState(0, false, false);

        var standState = unit.Bytes1 & StandStateMask;
        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        return new LifeState(unit.Health, hasGhostFlag, standState == StandStateDead);
    }

    private static bool IsStrictAlive(LifeState state) => state.Health > 0 && !state.Ghost && !state.StandDead;
    private static bool IsCorpseState(LifeState state) => !state.Ghost && (state.Health == 0 || state.StandDead);
    private static bool IsDeadOrGhost(LifeState state) => state.Health == 0 || state.Ghost || state.StandDead;

    private static float DistanceTo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float DistanceTo2D(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static int GetReclaimDelaySeconds(WoWActivitySnapshot? snap)
        => (int)(snap?.Player?.CorpseRecoveryDelaySeconds ?? 0);

    private static uint GetMovementFlags(WoWActivitySnapshot? snap)
        => snap?.Player?.Unit?.MovementFlags ?? snap?.MovementData?.MovementFlags ?? 0;

    [SkippableFact]
    public async Task Death_ReleaseAndRetrieve_ResurrectsPlayer()
    {
        var bgAccount = _bot.BgAccountName;
        var bgChar = _bot.BgCharacterName;
        var fgAccount = _bot.FgAccountName;
        var fgChar = _bot.FgCharacterName;

        var bgAvailable = !string.IsNullOrWhiteSpace(bgAccount) && !string.IsNullOrWhiteSpace(bgChar);
        var fgAvailable = !string.IsNullOrWhiteSpace(fgAccount) && !string.IsNullOrWhiteSpace(fgChar);

        global::Tests.Infrastructure.Skip.If(!bgAvailable && !fgAvailable, "No in-world bot account/character available.");

        if (bgAvailable && fgAvailable)
        {
            _output.WriteLine($"=== BG Bot: {bgChar} ({bgAccount}) ===");
            _output.WriteLine($"=== FG Bot: {fgChar} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG corpse scenarios concurrently.");

            var bgTask = RunDeathScenario(bgAccount!, bgChar!, () => _bot.BackgroundBot, "BG");
            var fgTask = RunDeathScenario(fgAccount!, fgChar!, () => _bot.ForegroundBot, "FG");
            await Task.WhenAll(bgTask, fgTask);

            var bgEvidence = await bgTask;
            var fgEvidence = await fgTask;

            AssertScenario(bgEvidence);
            AssertScenario(fgEvidence);

            Assert.Equal(bgEvidence.DeadCorpsePhaseObserved, fgEvidence.DeadCorpsePhaseObserved);
            Assert.Equal(bgEvidence.GhostPhaseObserved, fgEvidence.GhostPhaseObserved);
            Assert.Equal(
                bgEvidence.MovingToCorpsePhaseObserved || bgEvidence.ImmediateCorpseRangePhaseObserved,
                fgEvidence.MovingToCorpsePhaseObserved || fgEvidence.ImmediateCorpseRangePhaseObserved);
            Assert.Equal(bgEvidence.ReclaimReadyPhaseObserved, fgEvidence.ReclaimReadyPhaseObserved);
            Assert.Equal(bgEvidence.AlivePhaseObserved, fgEvidence.AlivePhaseObserved);
            return;
        }

        if (bgAvailable)
        {
            _output.WriteLine($"=== BG Bot: {bgChar} ({bgAccount}) ===");
            var bgEvidenceOnly = await RunDeathScenario(bgAccount!, bgChar!, () => _bot.BackgroundBot, "BG");
            AssertScenario(bgEvidenceOnly);
            _output.WriteLine("\nFG Bot: NOT AVAILABLE (WoW.exe injection required for parity check).");
            return;
        }

        _output.WriteLine($"=== FG Bot: {fgChar} ({fgAccount}) ===");
        var fgEvidenceOnly = await RunDeathScenario(fgAccount!, fgChar!, () => _bot.ForegroundBot, "FG");
        AssertScenario(fgEvidenceOnly);
        _output.WriteLine("\nBG Bot: NOT AVAILABLE (headless account not present in this run).");
    }

    private void AssertScenario(CorpsePhaseEvidence evidence)
    {
        Assert.True(evidence.Succeeded, $"[{evidence.Label}] scenario failed: {evidence.FailureReason}");
        Assert.True(evidence.DeadCorpsePhaseObserved, $"[{evidence.Label}] dead-corpse phase was not observed.");
        Assert.True(evidence.GhostPhaseObserved, $"[{evidence.Label}] ghost phase was not observed.");
        Assert.True(evidence.MovingToCorpsePhaseObserved || evidence.ImmediateCorpseRangePhaseObserved,
            $"[{evidence.Label}] corpse-approach phase not proven (improvementTicks={evidence.DistanceImprovementTicks}, initial2D={evidence.InitialDistanceToCorpse2D:F1}, best2D={evidence.BestDistanceToCorpse2D:F1}, travel={evidence.CumulativeGhostTravel:F1}, immediateRange={evidence.ImmediateCorpseRangePhaseObserved}).");
        Assert.True(evidence.ReclaimReadyPhaseObserved, $"[{evidence.Label}] reclaim-ready phase was not observed.");
        Assert.True(evidence.AlivePhaseObserved, $"[{evidence.Label}] alive phase was not observed.");
        Assert.False(evidence.StaleForwardFlagObserved, $"[{evidence.Label}] stale MOVEFLAG_FORWARD detected while not moving.");
        Assert.True(evidence.PostDeathGmChatCommands == 0,
            $"[{evidence.Label}] GM chat commands were sent after death during corpse behavior (delta={evidence.PostDeathGmChatCommands}).");
    }

    private async Task<CorpsePhaseEvidence> RunDeathScenario(
        string account,
        string characterName,
        Func<WoWActivitySnapshot?> getSnap,
        string label)
    {
        var evidence = new CorpsePhaseEvidence(label);
        var postDeathGmBaseline = -1;
        async Task<WoWActivitySnapshot?> GetSnapshotForAccountAsync()
        {
            await _bot.RefreshSnapshotsAsync();
            return await _bot.GetSnapshotAsync(account) ?? getSnap();
        }

        bool ValidateNoPostDeathGmCommands(string phase)
        {
            if (postDeathGmBaseline < 0)
                return true;

            var currentTotal = _bot.GetTrackedChatCommandTotal(account);
            var delta = Math.Max(0, currentTotal - postDeathGmBaseline);
            evidence.PostDeathGmChatCommands = delta;
            if (delta <= 0)
                return true;

            evidence.FailureReason = $"post-death GM chat command leak during {phase} (delta={delta})";
            _output.WriteLine($"  [{label}] {evidence.FailureReason}");
            return false;
        }

        async Task<CorpsePhaseEvidence> FailAsync(string reason, bool cleanupRevive = false)
        {
            if (string.IsNullOrWhiteSpace(evidence.FailureReason))
                evidence.FailureReason = reason;

            _output.WriteLine($"  [{label}] FAIL: {evidence.FailureReason}");
            if (cleanupRevive)
            {
                _output.WriteLine($"  [{label}] TEARDOWN: SOAP revive cleanup");
                await _bot.RevivePlayerAsync(characterName);
                await Task.Delay(1000);
            }

            if (postDeathGmBaseline >= 0)
            {
                var currentTotal = _bot.GetTrackedChatCommandTotal(account);
                evidence.PostDeathGmChatCommands = Math.Max(0, currentTotal - postDeathGmBaseline);
            }

            return evidence;
        }

        async Task<CorpsePhaseEvidence> RetryOrFailRunbackAsync(string reason)
        {
            _output.WriteLine($"  [{label}] Runback failure observed; failing immediately for snapshot comparison: {reason}");
            return await FailAsync(reason);
        }

        // Step 1: strict-alive setup from snapshot; SOAP revive only as fallback.
        _output.WriteLine($"  [{label}] Step 1: Ensure strict-alive snapshot state");
        WoWActivitySnapshot? snap = await GetSnapshotForAccountAsync();
        var setupState = GetLifeState(snap);

        if (!IsStrictAlive(setupState))
        {
            _output.WriteLine($"  [{label}] Setup not strict-alive; SOAP revive fallback once.");
            await _bot.RevivePlayerAsync(characterName);
            await Task.Delay(1000);
        }

        var setupSw = Stopwatch.StartNew();
        while (setupSw.Elapsed < TimeSpan.FromSeconds(20))
        {
            snap = await GetSnapshotForAccountAsync();
            setupState = GetLifeState(snap);
            if (IsStrictAlive(setupState))
                break;

            await Task.Delay(1000);
        }

        if (!IsStrictAlive(setupState))
            return await FailAsync("unable to establish strict-alive setup state before death test");

        // Step 2: force deterministic setup in Orgrimmar before kill.
        _output.WriteLine($"  [{label}] Step 2: Teleport to Orgrimmar setup before kill");
        var teleportResult = await _bot.TeleportToNamedAsync(characterName, "Orgrimmar");
        _output.WriteLine($"  [{label}] Teleport result: {teleportResult}");
        if (string.IsNullOrWhiteSpace(teleportResult)
            || teleportResult.StartsWith("FAULT", StringComparison.OrdinalIgnoreCase)
            || teleportResult.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || teleportResult.Contains("syntax", StringComparison.OrdinalIgnoreCase))
        {
            return await FailAsync("unable to execute Orgrimmar named teleport setup");
        }

        await Task.Delay(2000);
        snap = await GetSnapshotForAccountAsync();
        setupState = GetLifeState(snap);
        var setupPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (!IsStrictAlive(setupState) || setupPos == null)
            return await FailAsync("invalid setup after Orgrimmar teleport");

        _output.WriteLine($"  [{label}] Setup position: ({setupPos.X:F1}, {setupPos.Y:F1}, {setupPos.Z:F1})");
        _output.WriteLine($"  [{label}] Setup life-state: HP={setupState.Health}, Ghost={setupState.Ghost}, StandDead={setupState.StandDead}");

        // Step 3: kill once (setup only).
        _output.WriteLine($"  [{label}] Step 3: Kill setup (single transition path with fixture fallback)");

        var deathResult = await _bot.InduceDeathForTestAsync(
            account,
            characterName,
            timeoutMs: 20000,
            requireCorpseTransition: true,
            singleCommandOnly: true);
        evidence.KillCommand = deathResult.Command;
        _output.WriteLine($"  [{label}] Death setup: success={deathResult.Succeeded}, command='{deathResult.Command}', details='{deathResult.Details}'");
        if (!deathResult.Succeeded)
            return await FailAsync("kill setup did not produce dead/ghost transition");

        snap = await GetSnapshotForAccountAsync();
        var postKillState = GetLifeState(snap);
        if (!IsDeadOrGhost(postKillState))
            return await FailAsync("post-kill snapshot is not dead/ghost");
        if (postKillState.Ghost)
            return await FailAsync("post-kill state is already ghost before explicit ReleaseCorpse action");

        evidence.DeadCorpsePhaseObserved =
            IsCorpseState(postKillState)
            || deathResult.ObservedCorpseState
            || deathResult.UsedCorpsePositionFallback;

        var observedCorpsePosition = deathResult.ObservedCorpsePosition;
        if (observedCorpsePosition == null)
            return await FailAsync("corpse position was not captured from snapshot");

        var corpseX = observedCorpsePosition.X;
        var corpseY = observedCorpsePosition.Y;
        var corpseZ = observedCorpsePosition.Z;
        _output.WriteLine($"  [{label}] Corpse position: ({corpseX:F1}, {corpseY:F1}, {corpseZ:F1})");

        // After death setup, no GM chat commands are allowed during corpse behavior phase.
        postDeathGmBaseline = _bot.GetTrackedChatCommandTotal(account);
        evidence.PostDeathGmChatCommands = 0;

        // Step 4: release via client action.
        _output.WriteLine($"  [{label}] Step 4: Release corpse via client action");
        var releaseDispatch = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.ReleaseCorpse });
        _output.WriteLine($"  [{label}] ReleaseCorpse dispatch result: {releaseDispatch}");
        if (releaseDispatch != ResponseResult.Success)
            return await FailAsync("ReleaseCorpse action dispatch failed");
        if (!ValidateNoPostDeathGmCommands("release"))
            return await FailAsync(evidence.FailureReason);

        // Step 5: confirm ghost from snapshot.
        _output.WriteLine($"  [{label}] Step 5: Confirm ghost state from snapshot");
        var ghostSw = Stopwatch.StartNew();
        while (ghostSw.Elapsed < ReleaseToGhostTimeout)
        {
            await Task.Delay(1000);
            snap = await GetSnapshotForAccountAsync();
            if (!ValidateNoPostDeathGmCommands("ghost-confirmation"))
                return await FailAsync(evidence.FailureReason);

            var ghostState = GetLifeState(snap);
            if (ghostState.Ghost)
            {
                evidence.GhostPhaseObserved = true;
                break;
            }
        }

        if (!evidence.GhostPhaseObserved)
            return await FailAsync("ReleaseCorpse did not transition to ghost state");

        var ghostPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (ghostPos == null)
            return await FailAsync("missing ghost position in snapshot");

        // Let post-release graveyard teleport/state settle before seeding runback.
        // Issuing Goto during this transition causes noisy teleport-sized jumps and non-actionable retries.
        var settleSw = Stopwatch.StartNew();
        var settlePrev = ghostPos;
        var settleStableSamples = 0;
        while (settleSw.Elapsed < TimeSpan.FromSeconds(8))
        {
            await Task.Delay(1000);
            snap = await GetSnapshotForAccountAsync();
            if (!ValidateNoPostDeathGmCommands("ghost-settle"))
                return await FailAsync(evidence.FailureReason);

            var settlePos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (settlePos == null)
                continue;

            var settleStep = DistanceTo(settlePos.X, settlePos.Y, settlePos.Z, settlePrev.X, settlePrev.Y, settlePrev.Z);
            if (settleStep >= 80f)
            {
                _output.WriteLine($"  [{label}] Ghost settle: observed teleport-sized jump ({settleStep:F1}y), waiting for stabilization.");
                settleStableSamples = 0;
            }
            else if (settleStep < 0.2f)
            {
                settleStableSamples++;
            }
            else
            {
                settleStableSamples = 0;
            }

            ghostPos = settlePos;
            settlePrev = settlePos;
            if (settleStableSamples >= 2)
                break;
        }

        var initialDist2D = DistanceTo2D(ghostPos.X, ghostPos.Y, corpseX, corpseY);
        evidence.InitialDistanceToCorpse2D = initialDist2D;
        evidence.BestDistanceToCorpse2D = initialDist2D;
        var shortRunbackExpected = initialDist2D <= MinimumGhostToCorpseRunbackDistance;
        var startedWithinRetrieveRange = initialDist2D <= RetrieveRange;
        if (startedWithinRetrieveRange)
            evidence.ImmediateCorpseRangePhaseObserved = true;
        _output.WriteLine($"  [{label}] Ghost->corpse initial distance2D: {initialDist2D:F1}y");
        if (shortRunbackExpected)
        {
            _output.WriteLine($"  [{label}] Ghost starts near corpse; continuing without reseed/retry loop.");
        }
        if (startedWithinRetrieveRange)
        {
            _output.WriteLine($"  [{label}] Ghost already inside retrieve radius; runback movement is not required.");
        }

        // Step 6: pathfind/navigate to corpse (no teleport).
        _output.WriteLine($"  [{label}] Step 6: Observe pathfinding runback to corpse");
        var startRunbackDispatch = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });
        _output.WriteLine($"  [{label}] RetrieveCorpse(runback start) dispatch result: {startRunbackDispatch}");
        if (startRunbackDispatch != ResponseResult.Success)
            return await FailAsync("RetrieveCorpse runback-start action dispatch failed");
        if (!ValidateNoPostDeathGmCommands("runback-start"))
            return await FailAsync(evidence.FailureReason);

        var runSw = Stopwatch.StartNew();
        var previousPos = ghostPos;
        var noMovementTicks = 0;
        var staleForwardTicks = 0;
        var ignoredInitialTeleportJump = false;
        var arrivedNearCorpse = false;
        while (runSw.Elapsed < CorpseRunObservationWindow)
        {
            await Task.Delay(3000);

            snap = await GetSnapshotForAccountAsync();
            if (!ValidateNoPostDeathGmCommands("runback-observation"))
                return await FailAsync(evidence.FailureReason);

            var currentPos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (currentPos == null)
                continue;

            var stepDistance = DistanceTo(currentPos.X, currentPos.Y, currentPos.Z, previousPos.X, previousPos.Y, previousPos.Z);
            var distanceToCorpse2D = DistanceTo2D(currentPos.X, currentPos.Y, corpseX, corpseY);
            var reclaimDelay = GetReclaimDelaySeconds(snap);
            var movementFlags = GetMovementFlags(snap);

            _output.WriteLine($"  [{label}] Run tick: dist2D={distanceToCorpse2D:F1}, step={stepDistance:F1}, reclaimDelay={reclaimDelay}s, moveFlags=0x{movementFlags:X}");

            if (!ignoredInitialTeleportJump && stepDistance >= 80f)
            {
                ignoredInitialTeleportJump = true;
                _output.WriteLine($"  [{label}] Ignoring initial teleport-sized jump ({stepDistance:F1}y) from graveyard transition.");
                previousPos = currentPos;
                continue;
            }

            Assert.True(stepDistance < 80f, $"{label}: corpse run used teleport-like shortcut ({stepDistance:F1}y)");

            if (stepDistance >= 0.2f)
                evidence.CumulativeGhostTravel += stepDistance;

            if (distanceToCorpse2D + 0.5f < evidence.BestDistanceToCorpse2D)
            {
                evidence.BestDistanceToCorpse2D = distanceToCorpse2D;
                evidence.DistanceImprovementTicks++;
            }

            if ((movementFlags & MoveFlagForward) != 0 && stepDistance < 0.2f)
                staleForwardTicks++;
            else
                staleForwardTicks = 0;

            if (staleForwardTicks >= 10)
            {
                evidence.StaleForwardFlagObserved = true;
                return await RetryOrFailRunbackAsync("stale MOVEFLAG_FORWARD persisted while no displacement during runback");
            }

            noMovementTicks = stepDistance < 0.2f ? noMovementTicks + 1 : 0;
            if (noMovementTicks >= 15 && !arrivedNearCorpse && evidence.CumulativeGhostTravel < MinimumGhostTravelDistance)
                return await RetryOrFailRunbackAsync($"corpse run stalled with minimal movement (travel={evidence.CumulativeGhostTravel:F1}y, moveFlags=0x{movementFlags:X})");

            if (distanceToCorpse2D <= RetrieveRange)
                arrivedNearCorpse = true;

            var totalImprovement = evidence.InitialDistanceToCorpse2D - evidence.BestDistanceToCorpse2D;
            var strongImprovement =
                evidence.DistanceImprovementTicks >= RequiredImprovementTicks
                && totalImprovement >= MinimumDistanceImprovement;
            var meaningfulTravel = evidence.CumulativeGhostTravel >= MinimumGhostTravelDistance
                && totalImprovement >= (MinimumDistanceImprovement / 2f);
            evidence.MovingToCorpsePhaseObserved =
                strongImprovement
                || meaningfulTravel
                || (arrivedNearCorpse && totalImprovement >= 8f);
            if (arrivedNearCorpse && shortRunbackExpected)
                evidence.ImmediateCorpseRangePhaseObserved = true;

            if (reclaimDelay <= 0)
                evidence.ReclaimReadyPhaseObserved = true;

            if (arrivedNearCorpse && (evidence.MovingToCorpsePhaseObserved || evidence.ImmediateCorpseRangePhaseObserved))
            {
                previousPos = currentPos;
                break;
            }

            previousPos = currentPos;
        }

        if (!evidence.MovingToCorpsePhaseObserved && !evidence.ImmediateCorpseRangePhaseObserved && !shortRunbackExpected)
            return await RetryOrFailRunbackAsync($"movement toward corpse not proven (improvementTicks={evidence.DistanceImprovementTicks}, initial2D={evidence.InitialDistanceToCorpse2D:F1}, best2D={evidence.BestDistanceToCorpse2D:F1})");
        if (!arrivedNearCorpse)
            return await RetryOrFailRunbackAsync($"did not reach corpse reclaim range during runback (best2D={evidence.BestDistanceToCorpse2D:F1}y)");

        // Step 7: wait until reclaim delay is zero from snapshot.
        _output.WriteLine($"  [{label}] Step 7: Wait until reclaim delay reaches zero");
        if (!evidence.ReclaimReadyPhaseObserved)
        {
            var reclaimSw = Stopwatch.StartNew();
            while (reclaimSw.Elapsed < ReclaimTimeout)
            {
                await Task.Delay(3000);
                snap = await GetSnapshotForAccountAsync();
                if (!ValidateNoPostDeathGmCommands("reclaim-ready-wait"))
                    return await FailAsync(evidence.FailureReason);

                var reclaimDelay = GetReclaimDelaySeconds(snap);
                _output.WriteLine($"  [{label}] Reclaim wait tick: reclaimDelay={reclaimDelay}s");
                if (reclaimDelay <= 0)
                {
                    evidence.ReclaimReadyPhaseObserved = true;
                    break;
                }
            }
        }

        if (!evidence.ReclaimReadyPhaseObserved)
            return await FailAsync("reclaim delay never reached zero during wait window");

        // Step 8: retrieve corpse via client action.
        _output.WriteLine($"  [{label}] Step 8: Retrieve corpse via client action");
        var retrieveDispatch = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });
        _output.WriteLine($"  [{label}] RetrieveCorpse dispatch result: {retrieveDispatch}");
        if (retrieveDispatch != ResponseResult.Success)
            return await FailAsync("RetrieveCorpse action dispatch failed");
        if (!ValidateNoPostDeathGmCommands("retrieve"))
            return await FailAsync(evidence.FailureReason);

        // Step 9: assert alive/non-ghost snapshot state.
        _output.WriteLine($"  [{label}] Step 9: Wait for strict-alive non-ghost state");
        var aliveSw = Stopwatch.StartNew();
        while (aliveSw.Elapsed < AliveAfterRetrieveTimeout)
        {
            await Task.Delay(2000);
            snap = await GetSnapshotForAccountAsync();
            if (!ValidateNoPostDeathGmCommands("alive-confirmation"))
                return await FailAsync(evidence.FailureReason);

            var finalState = GetLifeState(snap);
            if (IsStrictAlive(finalState))
            {
                evidence.AlivePhaseObserved = true;
                break;
            }
        }

        // Guard the timeout boundary: if strict-alive is reached exactly at timeout,
        // take one final snapshot before failing.
        if (!evidence.AlivePhaseObserved)
        {
            snap = await GetSnapshotForAccountAsync();
            if (!ValidateNoPostDeathGmCommands("alive-confirmation-final"))
                return await FailAsync(evidence.FailureReason);

            var finalState = GetLifeState(snap);
            if (IsStrictAlive(finalState))
                evidence.AlivePhaseObserved = true;
        }

        if (!evidence.AlivePhaseObserved)
            return await FailAsync("character did not return to strict-alive state after retrieve", cleanupRevive: true);

        evidence.PostDeathGmChatCommands = Math.Max(0, _bot.GetTrackedChatCommandTotal(account) - postDeathGmBaseline);
        evidence.Succeeded = true;
        return evidence;
    }
}
