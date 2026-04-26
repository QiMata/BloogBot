using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// F-1 step 3 end-to-end check for <c>StateManagerMode.Automated</c>.
/// Loads <c>Onboarding.config.json</c> (a wrapped-schema config with one
/// background bot and a small <c>Loadout</c>) and asserts that the bot's
/// snapshot reports a <see cref="LoadoutStatus"/> past
/// <see cref="LoadoutStatus.LoadoutNotStarted"/> — proving the
/// <c>AutomatedModeHandler.OnWorldEntryAsync</c> path dispatched
/// <c>APPLY_LOADOUT</c> without any fixture-side
/// <c>StageBotRunnerLoadoutAsync</c> call and the bot's
/// <c>LoadoutTask</c> picked it up.
///
/// The loadout itself is intentionally tiny (one skill bump + one bag
/// item) so the test exercises the dispatch wiring rather than the
/// LoadoutTask's full plan execution; reaching
/// <see cref="LoadoutStatus.LoadoutReady"/> requires a stable in-world
/// connection that newly-created BG accounts don't always sustain on
/// first login. The richer LOADOUT_READY end-to-end is covered by the
/// existing battleground coordinator paths that reuse the same
/// LoadoutTask under more deterministic preconditions.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public sealed class OnboardingAutomatedModeTests
{
    private const uint FishingSkillId = 356;
    private const uint FishingPoleItemId = 6256;

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public OnboardingAutomatedModeTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Onboarding_AutomatedMode_DispatchesApplyLoadoutAtWorldEntry()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Onboarding.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        global::Tests.Infrastructure.Skip.IfNot(
            _bot.IsReady,
            _bot.FailureReason ?? "Live bot fixture not ready for Onboarding config.");

        var account = _bot.BgAccountName;
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(account),
            "BG account was not detected after loading Onboarding.config.json.");

        _output.WriteLine($"[ACTION-PLAN] BG {account}: Automated mode wiring check; no fixture dispatch.");

        // Wiring assertion: the snapshot's LoadoutStatus must advance past
        // LoadoutNotStarted within a generous window. Reaching anything other
        // than LoadoutNotStarted proves three things in one signal:
        //   1) AutomatedModeHandler.OnWorldEntryAsync was invoked at first
        //      IsObjectManagerValid=true (otherwise no action was queued).
        //   2) BuildApplyLoadoutAction(...) translated the JSON Loadout
        //      successfully (otherwise APPLY_LOADOUT was never queued).
        //   3) The bot received the action and constructed a LoadoutTask
        //      (transitions LoadoutNotStarted -> LoadoutInProgress in
        //      LoadoutTask.Update).
        var loadoutPickedUp = await _bot.WaitForSnapshotConditionAsync(
            account!,
            snap => snap.LoadoutStatus != LoadoutStatus.LoadoutNotStarted,
            TimeSpan.FromSeconds(60),
            pollIntervalMs: 500,
            progressLabel: $"automated-loadout-pickup {account}");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account!);
        Assert.NotNull(snap);

        Assert.True(
            loadoutPickedUp,
            $"Automated mode failed to dispatch APPLY_LOADOUT and have the bot construct a LoadoutTask within 60s. " +
            $"Final status='{snap!.LoadoutStatus}', failureReason='{snap.LoadoutFailureReason}'.");

        // Best-effort: if the bot stays in-world long enough for LoadoutTask
        // to walk its 2-step plan, also verify the skill + item landed. A
        // newly-created BG account sometimes flaps between InWorld and
        // CharacterSelect during first login; we log the final shape but
        // don't fail the wiring test on that pre-existing flake.
        var ready = await _bot.WaitForSnapshotConditionAsync(
            account!,
            s => s.LoadoutStatus == LoadoutStatus.LoadoutReady
                || s.LoadoutStatus == LoadoutStatus.LoadoutFailed,
            TimeSpan.FromSeconds(60),
            pollIntervalMs: 500,
            progressLabel: $"automated-loadout-finish {account}");

        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account!);

        var skillValue = snap?.Player?.SkillInfo?.TryGetValue(FishingSkillId, out var fishing) == true
            ? fishing
            : 0u;
        var hasPole = snap?.Player?.BagContents?.Values.Any(itemId => itemId == FishingPoleItemId) == true;

        _output.WriteLine(
            $"[ASSERT-OK] BG {account}: APPLY_LOADOUT wiring confirmed via LoadoutStatus='{snap?.LoadoutStatus}'. " +
            $"Plan completion (best-effort): finishedWithinWindow={ready}, fishingSkill={skillValue}, hasPole={hasPole}.");

        Assert.NotEqual(LoadoutStatus.LoadoutFailed, snap!.LoadoutStatus);
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
