using System;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Group formation integration test - dual-client validation.
///
/// Scenario:
///   1) Start from snapshot-confirmed ungrouped state.
///   2) FG sends invite to BG by character name.
///   3) BG accepts invite.
///   4) Assert both snapshots report same non-zero PartyLeaderGuid.
///   5) Deterministically clean up group state.
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~GroupFormationTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class GroupFormationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public GroupFormationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp()
    {
        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG account not available.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available.");
        global::Tests.Infrastructure.Skip.If(_bot.ForegroundBot == null, "FG snapshot not available; requires dual-client run.");

        // Step 1: deterministic clean start from snapshot state (no GM chat disband).
        await EnsureNotGroupedAsync(bgAccount!, "BG");
        await EnsureNotGroupedAsync(fgAccount!, "FG");

        await _bot.RefreshSnapshotsAsync();
        var bgStart = await _bot.GetSnapshotAsync(bgAccount!);
        var fgStart = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(bgStart);
        Assert.NotNull(fgStart);

        Assert.Equal(0UL, bgStart!.PartyLeaderGuid);
        Assert.Equal(0UL, fgStart!.PartyLeaderGuid);

        var bgName = bgStart.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgName), "BG character name missing; cannot send invite by name.");

        // Step 2: FG invites BG by name.
        _output.WriteLine($"[GROUP] FG invites BG by name: {bgName}");
        await _bot.SendActionAndWaitAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.SendGroupInvite,
            Parameters = { new RequestParameter { StringParam = bgName } }
        }, delayMs: 1200);

        // Step 3: BG accepts invite.
        _output.WriteLine("[GROUP] BG accepts invite");
        await _bot.SendActionAndWaitAsync(bgAccount!, new ActionMessage
        {
            ActionType = ActionType.AcceptGroupInvite
        }, delayMs: 1500);

        // Step 4: assert group state from snapshots.
        var formed = await WaitForGroupFormationAsync(fgAccount!, bgAccount!, timeoutMs: 20000);
        Assert.True(formed.formed, formed.details);

        // Step 5: deterministic cleanup and verification.
        await EnsureNotGroupedAsync(bgAccount!, "BG");
        await EnsureNotGroupedAsync(fgAccount!, "FG");

        await _bot.RefreshSnapshotsAsync();
        var bgEnd = await _bot.GetSnapshotAsync(bgAccount!);
        var fgEnd = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(bgEnd);
        Assert.NotNull(fgEnd);

        Assert.Equal(0UL, bgEnd!.PartyLeaderGuid);
        Assert.Equal(0UL, fgEnd!.PartyLeaderGuid);
    }

    private async Task EnsureNotGroupedAsync(string account, string label)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (snap == null)
                return;

            if (snap.PartyLeaderGuid == 0)
                return;

            var selfGuid = snap.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
            var action = selfGuid != 0 && snap.PartyLeaderGuid == selfGuid
                ? ActionType.DisbandGroup
                : ActionType.LeaveGroup;

            _output.WriteLine($"[{label}] grouped (leader=0x{snap.PartyLeaderGuid:X}); sending {action} (attempt {attempt}/5)");
            await _bot.SendActionAndWaitAsync(account, new ActionMessage { ActionType = action }, delayMs: 1000);
        }
    }

    private async Task<(bool formed, string details)> WaitForGroupFormationAsync(string fgAccount, string bgAccount, int timeoutMs)
    {
        string last = "group formation not observed";
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            await _bot.RefreshSnapshotsAsync();
            var fg = await _bot.GetSnapshotAsync(fgAccount);
            var bg = await _bot.GetSnapshotAsync(bgAccount);
            if (fg != null && bg != null)
            {
                var fgLeader = fg.PartyLeaderGuid;
                var bgLeader = bg.PartyLeaderGuid;
                var fgGuid = fg.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;

                last = $"fgLeader=0x{fgLeader:X}, bgLeader=0x{bgLeader:X}, fgGuid=0x{fgGuid:X}";
                if (fgLeader != 0 && fgLeader == bgLeader && fgLeader == fgGuid)
                    return (true, $"group formed: {last}");
            }

            await Task.Delay(1000);
        }

        return (false, $"Timed out waiting for group formation ({last})");
    }
}
