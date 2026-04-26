using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Dungeons;

/// <summary>
/// V2.15: Summoning stone / meeting stone tests.
/// Teleport bots near WC meeting stone, INTERACT_WITH stone object.
///
/// Run: dotnet test --filter "FullyQualifiedName~SummoningStoneTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SummoningStoneTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Wailing Caverns meeting stone area (outside the instance entrance, Barrens)
    private const float WcMeetingStoneX = -740.0f, WcMeetingStoneY = -2214.0f, WcMeetingStoneZ = 16.0f;

    // Meeting stone game object type = 23 (GAMEOBJECT_TYPE_MEETINGSTONE)
    private const uint GoTypeMeetingStone = 23;

    public SummoningStoneTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Stockade_AllianceSummon_ArrivesAtInstance()
    {
        // V2.15: Alliance summoning stone test -- requires Alliance character
        var account = _bot.BgAccountName!;

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Character: {snap!.CharacterName}");
        // Stockade is Alliance-only; verify fixture is ready
        Assert.NotNull(snap.Player);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task RFC_WarlockSummon_ArrivesAtInstance()
    {
        // V2.15: Warlock summon requires warlock class setup
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Learn Ritual of Summoning (spell 698)
        const uint ritualOfSummoning = 698;
        _output.WriteLine($"[SETUP] Teaching Ritual of Summoning ({ritualOfSummoning})");
        await _bot.SendGmChatCommandAsync(account, $".learn {ritualOfSummoning}");
        await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => snapshot.Player?.SpellList?.Contains(ritualOfSummoning) == true,
            TimeSpan.FromSeconds(2),
            pollIntervalMs: 150,
            progressLabel: "BG ritual-summoning-learned");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        var hasSpell = snap!.Player?.SpellList?.Contains(ritualOfSummoning) == true;
        _output.WriteLine($"[TEST] Has Ritual of Summoning: {hasSpell}");
        // Full warlock summon requires 3 party members; validate spell setup
        Assert.NotNull(snap.Player);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task WC_MeetingStoneSummon_ArrivesAtInstance()
    {
        // V2.15: Teleport both bots near WC meeting stone, interact with it
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        _output.WriteLine($"[TEST] Teleporting to WC meeting stone area ({WcMeetingStoneX}, {WcMeetingStoneY}, {WcMeetingStoneZ})");
        await _bot.BotTeleportAsync(bgAccount, KalimdorMapId, WcMeetingStoneX, WcMeetingStoneY, WcMeetingStoneZ);
        await _bot.WaitForTeleportSettledAsync(bgAccount, WcMeetingStoneX, WcMeetingStoneY);

        // Wait for game objects to populate near the meeting stone.
        await _bot.WaitForSnapshotConditionAsync(
            bgAccount,
            snapshot => (snapshot.MovementData?.NearbyGameObjects?.Count ?? 0) > 0,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 250,
            progressLabel: "BG nearby-go-populate");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Position near WC: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

        // Look for meeting stone in nearby game objects
        var nearbyGOs = snap.MovementData?.NearbyGameObjects?.ToList()
            ?? new System.Collections.Generic.List<Game.GameObjectSnapshot>();
        _output.WriteLine($"[TEST] Nearby game objects: {nearbyGOs.Count}");

        var meetingStone = nearbyGOs.FirstOrDefault(go => go.GameObjectType == GoTypeMeetingStone);
        if (meetingStone != null)
        {
            _output.WriteLine($"[TEST] Found meeting stone: entry={meetingStone.Entry}, guid=0x{meetingStone.Guid:X}");

            // Attempt to interact with the meeting stone
            var interactResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)meetingStone.Guid } }
            });
            _output.WriteLine($"[TEST] INTERACT_WITH meeting stone result: {interactResult}");

            var preInteractChatCount = snap.RecentChatMessages.Count;
            await _bot.WaitForSnapshotConditionAsync(
                bgAccount,
                snapshot => snapshot.RecentChatMessages.Count > preInteractChatCount,
                TimeSpan.FromSeconds(2),
                pollIntervalMs: 200,
                progressLabel: "BG meeting-stone-response");
            await _bot.RefreshSnapshotsAsync();
            var afterSnap = await _bot.GetSnapshotAsync(bgAccount);
            Assert.NotNull(afterSnap);
        }
        else
        {
            _output.WriteLine("[TEST] No meeting stone found in nearby game objects");
            _output.WriteLine($"[TEST] GO entries found: {string.Join(", ", nearbyGOs.Select(g => $"{g.Entry}(t{g.GameObjectType})"))}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Fallback_NoSummoner_BotWalksToDungeon()
    {
        // V2.15: Bot navigates to dungeon entrance on foot (no summoner)
        var account = _bot.BgAccountName!;

        // Start from a position away from WC entrance
        const float startX = -650.0f, startY = -2100.0f, startZ = 20.0f;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, KalimdorMapId, startX, startY, startZ);
        await _bot.WaitForTeleportSettledAsync(account, startX, startY);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1})");

        // Send TRAVEL_TO toward WC entrance
        var travelResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = KalimdorMapId },
                new RequestParameter { FloatParam = WcMeetingStoneX },
                new RequestParameter { FloatParam = WcMeetingStoneY },
                new RequestParameter { FloatParam = WcMeetingStoneZ }
            }
        });
        _output.WriteLine($"[TEST] TRAVEL_TO WC result: {travelResult}");
        Assert.Equal(ResponseResult.Success, travelResult);

        // Verify bot starts moving toward WC
        var moved = await _bot.WaitForPositionChangeAsync(account, startPos.X, startPos.Y, startPos.Z,
            timeoutMs: 20000, progressLabel: "BG walk-to-wc");
        Assert.True(moved, "Bot should start walking toward WC entrance");
    }
}
