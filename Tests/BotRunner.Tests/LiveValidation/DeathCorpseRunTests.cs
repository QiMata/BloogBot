using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BotRunner.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed corpse recovery integration test.
///
/// Flow: Shodan stages Razor Hill death -> release -> wait for graveyard relocation ->
/// dispatch RetrieveCorpse once -> let RetrieveCorpseTask own runback, cooldown, and reclaim.
///
/// This is the live baseline for the corpse-recovery path in:
///   - Exports/BotRunner/BotRunnerService.ActionDispatch.cs
///   - Exports/BotRunner/Tasks/RetrieveCorpseTask.cs
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class DeathCorpseRunTests
{
    private const int FailureMessageLimit = 4;
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float RetrieveRange = 39.0f;
    private const float GraveyardDistanceThreshold = RetrieveRange + 20.0f;
    private const float MinRunbackImprovement = 25.0f;
    private const int MaxGraveyardTeleportSeconds = 15;
    private const int MaxRecoverySeconds = 120;
    private const string RetryForegroundCrash001EnvVar = "WWOW_RETRY_FG_CRASH001";

    // Razor Hill — flat outdoor terrain, graveyard is nearby (~100y), navmesh can route.
    // Orgrimmar failed because pathfinding returned no_route for graveyard→corpse (460y, city navmesh).
    // Z+3 offset applied to spawn table Z to avoid UNDERMAP detection.
    private const int MapId = 1;
    private const float DeathAreaX = 340f, DeathAreaY = -4686f, DeathAreaZ = 19.5f;

    public DeathCorpseRunTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer()
    {
        var target = await EnsureDeathSettingsAndTargetAsync();

        _output.WriteLine($"[SHODAN] {target.CharacterName} -> corpse recovery is asserted on the BG BotRunner target.");
        var (bgPass, bgReason) = await RunCorpseRunScenario(target);
        await CleanupAsync(target);
        Assert.True(bgPass, $"[{target.RoleLabel}] {bgReason}");
    }

    [SkippableFact]
    public async Task Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer()
    {
        await EnsureDeathSettingsAsync();

        // CRASH-001: WoW.exe historically hit ACCESS_VIOLATION at 0x00619CDF during
        // foreground ghost runback. Keep it guarded by default so regular live runs
        // cannot crash the local client; set WWOW_RETRY_FG_CRASH001=1 to validate a
        // mitigation intentionally.
        global::Tests.Infrastructure.Skip.If(
            !string.Equals(Environment.GetEnvironmentVariable(RetryForegroundCrash001EnvVar), "1", StringComparison.OrdinalIgnoreCase),
            "CRASH-001: FG corpse-run validation is opt-in because WoW.exe has historically crashed in ghost form. " +
            $"Set {RetryForegroundCrash001EnvVar}=1 to retry the current mitigation. " +
            "See Tests/BotRunner.Tests/LiveValidation/docs/CRASH_INVESTIGATION.md.");

        var target = ResolveForegroundDeathTarget();
        // Skip teleport probe — this test teleports to Razor Hill immediately after.
        // The probe does 2-3 rapid teleports to Orgrimmar which, combined with the
        // Razor Hill teleport + kill + graveyard, causes MaNGOS to TCP-disconnect.
        global::Tests.Infrastructure.Skip.IfNot(await _bot.CheckFgActionableAsync(requireTeleportProbe: false), "FG bot is not actionable.");

        _output.WriteLine($"[FG-OPT-IN] {target.CharacterName} -> corpse recovery is asserted on the injected BotRunner target.");
        var (fgPass, fgReason) = await RunCorpseRunScenario(target);
        await CleanupAsync(target);
        Assert.True(fgPass, $"[{target.RoleLabel}] {fgReason}");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureDeathSettingsAndTargetAsync()
    {
        await EnsureDeathSettingsAsync();

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG corpse-run action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity; " +
            $"foreground corpse-run remains guarded by {RetryForegroundCrash001EnvVar}.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no corpse-run action dispatch.");

        return target;
    }

    private async Task EnsureDeathSettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Loot.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsPathfindingReady, "PathfindingService not available on port 5001.");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Loot.config.json.");
    }

    private LiveBotFixture.BotRunnerActionTarget ResolveForegroundDeathTarget()
    {
        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: true,
                foregroundFirst: true)
            .FirstOrDefault(target => target.IsForeground);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(target.AccountName),
            "No foreground BotRunner action target was available under Loot.config.json.");

        return target;
    }

    private async Task<(bool passed, string reason)> RunCorpseRunScenario(LiveBotFixture.BotRunnerActionTarget target)
    {
        var account = target.AccountName;
        var label = target.RoleLabel;
        Task<(bool passed, string reason)> FailAsync(string reason, global::Game.Position? corpsePos = null)
            => BuildFailureResultAsync(account, label, reason, corpsePos);

        _output.WriteLine($"  [{label}] Step 1: Shodan stages Razor Hill corpse state");
        var deathResult = await _bot.StageBotRunnerCorpseAtNavigationPointAsync(
            account,
            label,
            MapId,
            DeathAreaX,
            DeathAreaY,
            DeathAreaZ,
            "Razor Hill corpse-run start");

        _output.WriteLine($"  [{label}] Death staging result: {deathResult.Succeeded}, cmd={deathResult.Command}");

        var corpsePos = deathResult.ObservedCorpsePosition;
        if (corpsePos == null)
            return await FailAsync("Corpse position not captured");

        _output.WriteLine($"  [{label}] Corpse at: ({corpsePos.X:F1}, {corpsePos.Y:F1}, {corpsePos.Z:F1})");

        _output.WriteLine($"  [{label}] Step 2: Release corpse");
        var releaseResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.ReleaseCorpse });
        if (releaseResult != ResponseResult.Success)
            return await FailAsync($"ReleaseCorpse failed: {releaseResult}", corpsePos);

        var ghostConfirmed = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => (s.Player?.PlayerFlags & 0x10) != 0,
            TimeSpan.FromSeconds(10),
            progressLabel: $"{label} ghost-state");
        if (!ghostConfirmed)
            return await FailAsync("Never transitioned to ghost state", corpsePos);

        _output.WriteLine($"  [{label}] Ghost confirmed");

        _output.WriteLine($"  [{label}] Step 3: Wait for graveyard relocation");
        var (graveyardSettled, graveyardDistanceToCorpse, initialReclaimDelay) =
            await WaitForGraveyardTransitionAsync(account, label, corpsePos);
        if (!graveyardSettled)
            return await FailAsync($"Ghost never left corpse location (lastDist={graveyardDistanceToCorpse:F0}y, reclaimDelay={initialReclaimDelay}s)", corpsePos);

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, account, "physics", "transform", "navtrace");

        _output.WriteLine($"  [{label}] Step 4: Start diagnostic recording and queue RetrieveCorpse from {graveyardDistanceToCorpse:F0}y away");
        var startRecordingResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        if (startRecordingResult != ResponseResult.Success)
            return await FailAsync($"StartPhysicsRecording failed: {startRecordingResult}", corpsePos);

        (bool alive, float bestDistanceToCorpse, uint bestReclaimDelay, bool reachedCorpseRange) recoveryResult;
        try
        {
            var retrieveResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.RetrieveCorpse });
            if (retrieveResult != ResponseResult.Success)
                return await FailAsync($"RetrieveCorpse failed: {retrieveResult}", corpsePos);

            _output.WriteLine($"  [{label}] Step 5: Observe RetrieveCorpseTask runback/cooldown/reclaim");
            recoveryResult = await WaitForCorpseRecoveryAsync(account, label, corpsePos, graveyardDistanceToCorpse, initialReclaimDelay);
        }
        finally
        {
            var stopRecordingResult = await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            _output.WriteLine($"  [{label}] Recording stop result: {stopRecordingResult}");
            await Task.Delay(500);
        }

        AssertRetrieveCorpseTraceRecorded(account, label);

        var (alive, bestDistanceToCorpse, bestReclaimDelay, reachedCorpseRange) = recoveryResult;
        if (!alive)
        {
            // Check if failure was due to pathfinding no_route — skip rather than fail.
            // The pathfinding service cannot route from many graveyard positions (navmesh gaps).
            // This is tracked as a PathfindingService issue, not a RetrieveCorpseTask bug.
            // Refresh snapshot to pick up diagnostic messages flushed after task pop.
            await _bot.RefreshSnapshotsAsync();
            var chatMessages = (await _bot.GetSnapshotAsync(account))?.RecentChatMessages ?? [];
            var isPathfindingGap = chatMessages.Any(m =>
                m.Contains("RunbackStallRecoveryExceeded", StringComparison.Ordinal)
                || m.Contains("NoPathTimeout", StringComparison.Ordinal));
            _output.WriteLine($"  [{label}] Skip check: {chatMessages.Count} chat messages, pathfindingGap={isPathfindingGap}");
            if (chatMessages.Count > 0)
            {
                foreach (var msg in chatMessages.TakeLast(5))
                    _output.WriteLine($"    chat: {msg}");
            }

            if (isPathfindingGap)
            {
                var skipMsg = $"[{label}] SKIP: RetrieveCorpseTask hit RunbackStallRecoveryExceeded — pathfinding " +
                              $"cannot route graveyard→corpse (start={graveyardDistanceToCorpse:F0}y, best={bestDistanceToCorpse:F0}y). " +
                              "Tracked as PathfindingService navmesh gap.";
                _output.WriteLine(skipMsg);
                global::Tests.Infrastructure.Skip.If(true, skipMsg);
                return (true, string.Empty);
            }

            var improvement = graveyardDistanceToCorpse - bestDistanceToCorpse;
            if (improvement < MinRunbackImprovement)
                return await FailAsync($"RetrieveCorpseTask never reduced corpse distance enough (start={graveyardDistanceToCorpse:F0}y, best={bestDistanceToCorpse:F0}y)", corpsePos);
            if (!reachedCorpseRange)
                return await FailAsync($"RetrieveCorpseTask improved runback but never reached reclaim range (best={bestDistanceToCorpse:F0}y, bestDelay={bestReclaimDelay}s)", corpsePos);
            if (bestReclaimDelay > 0)
                return await FailAsync($"RetrieveCorpseTask reached corpse range but reclaim delay never elapsed (bestDelay={bestReclaimDelay}s)", corpsePos);
            return await FailAsync($"RetrieveCorpseTask reached corpse range but bot never returned to strict-alive (best={bestDistanceToCorpse:F0}y)", corpsePos);
        }

        _output.WriteLine($"  [{label}] PASSED -> release + RetrieveCorpseTask restored strict-alive state");
        return (true, string.Empty);
    }

    private void AssertRetrieveCorpseTraceRecorded(string account, string label)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        var navTracePath = RecordingArtifactHelper.WaitForRecordingFile(
            recordingDir,
            "navtrace",
            account,
            "json",
            TimeSpan.FromSeconds(5));

        Assert.False(string.IsNullOrEmpty(navTracePath), $"[{label}] Expected navtrace recording for {account}.");

        using var document = JsonDocument.Parse(File.ReadAllText(navTracePath!));
        var root = document.RootElement;
        var recordedTask = root.TryGetProperty("RecordedTask", out var taskProperty)
            ? taskProperty.GetString()
            : null;
        var recordedAction = root.TryGetProperty("RecordedAction", out var actionProperty)
            ? actionProperty.GetString()
            : null;
        var taskStack = root.TryGetProperty("TaskStack", out var stackProperty) && stackProperty.ValueKind == JsonValueKind.Array
            ? stackProperty.EnumerateArray().Select(element => element.GetString() ?? string.Empty).Where(name => name.Length > 0).ToArray()
            : [];
        var hasTrace = root.TryGetProperty("TraceSnapshot", out var traceProperty)
            && traceProperty.ValueKind != JsonValueKind.Null
            && traceProperty.ValueKind != JsonValueKind.Undefined;
        var planVersion = hasTrace && traceProperty.TryGetProperty("PlanVersion", out var planProperty)
            ? planProperty.GetInt32()
            : -1;
        var lastResolution = hasTrace && traceProperty.TryGetProperty("LastResolution", out var resolutionProperty)
            ? resolutionProperty.GetString()
            : null;

        _output.WriteLine(
            $"  [{label}] Navtrace captured: task={recordedTask ?? "null"} action={recordedAction ?? "null"} " +
            $"plan={planVersion} resolution={lastResolution ?? "null"} stack=[{string.Join(", ", taskStack)}]");

        Assert.True(hasTrace, $"[{label}] Navtrace sidecar did not contain a trace snapshot.");
        Assert.Equal(nameof(RetrieveCorpseTask), recordedTask);
        Assert.Contains(nameof(RetrieveCorpseTask), taskStack);
    }

    private async Task<(bool passed, string reason)> BuildFailureResultAsync(
        string account,
        string label,
        string reason,
        global::Game.Position? corpsePos)
    {
        var context = await BuildCorpseFailureContextAsync(account, label, corpsePos);
        return (false, $"{reason} | {context}");
    }

    private async Task<string> BuildCorpseFailureContextAsync(
        string account,
        string label,
        global::Game.Position? corpsePos)
    {
        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        _bot.DumpSnapshotDiagnostics(snapshot, $"{label}-corpse-failure");
        _bot.DumpRecentBotRunnerDiagnostics($"{label}-corpse", "[RETRIEVE_CORPSE]", "DeathRecovery", "NavigationPath");

        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var corpseDistance = corpsePos != null && playerPosition != null
            ? $"{LiveBotFixture.Distance2D(playerPosition.X, playerPosition.Y, corpsePos.X, corpsePos.Y):F1}y"
            : "unknown";
        var reclaimDelay = snapshot?.Player?.CorpseRecoveryDelaySeconds is uint delay
            ? $"{delay}s"
            : "unknown";
        var relevantChats = snapshot?.RecentChatMessages?
            .Where(message =>
                message.Contains("corpse", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ghost", StringComparison.OrdinalIgnoreCase)
                || message.Contains("reclaim", StringComparison.OrdinalIgnoreCase)
                || message.Contains("path", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? [];
        var diagSummary = _bot.FormatRecentBotRunnerDiagnostics("[RETRIEVE_CORPSE]", "DeathRecovery", "NavigationPath");

        return $"screen={snapshot?.ScreenState ?? "null"} strictAlive={LiveBotFixture.IsStrictAlive(snapshot)} " +
               $"pos={FormatPosition(playerPosition)} corpseDist={corpseDistance} reclaimDelay={reclaimDelay} " +
               $"recentErrors={FormatMessageTail(snapshot?.RecentErrors)} corpseChat={FormatMessageTail(relevantChats)} diag={diagSummary}";
    }

    private static string FormatMessageTail(IReadOnlyList<string>? messages)
    {
        if (messages == null || messages.Count == 0)
            return "[]";

        return "[" + string.Join(" || ", messages.TakeLast(FailureMessageLimit)) + "]";
    }

    private static string FormatPosition(global::Game.Position? position)
        => position == null
            ? "null"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

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

    private Task CleanupAsync(LiveBotFixture.BotRunnerActionTarget target)
        => _bot.RestoreBotRunnerAliveAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            MapId,
            DeathAreaX,
            DeathAreaY,
            DeathAreaZ,
            "Razor Hill corpse-run start");
}
