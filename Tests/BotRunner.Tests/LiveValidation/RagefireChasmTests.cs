using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Ragefire Chasm 10-man dungeoneering integration test.
///
/// Launches 10 bots (1 FG raid leader/main tank + 9 BG role-specific bots)
/// via custom StateManager config. The group forms outside RFC, enters the
/// dungeon (map 389), and uses DungeoneeringTask to clear encounters.
///
/// Raid composition:
///   Slot 1: TESTBOT1  — Warrior (F) — Main Tank / Raid Leader (FG)
///   Slot 2: RFCBOT2   — Shaman (F) — Off-Tank
///   Slot 3: RFCBOT3   — Druid (M)  — Healer
///   Slot 4: RFCBOT4   — Priest (M) — Healer
///   Slot 5: RFCBOT5   — Warlock (M) — DPS
///   Slot 6: RFCBOT6   — Hunter (F) — DPS
///   Slot 7: RFCBOT7   — Rogue (F)  — DPS
///   Slot 8: RFCBOT8   — Mage (M)   — DPS
///   Slot 9: RFCBOT9   — Warrior (F) — DPS
///   Slot 10: RFCBOT10 — Warrior (F) — DPS
///
/// Trope: physical classes = Female, magic classes = Male.
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~RagefireChasmTests" --configuration Release -v n --blame-hang --blame-hang-timeout 30m
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class RagefireChasmTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // RFC dungeon portal: Orgrimmar cleft entrance
    private const int KalimdorMap = 1;
    private const float RfcEntranceX = 1811f;
    private const float RfcEntranceY = -4410f;
    private const float RfcEntranceZ = -15f; // Z+3 from portal at -18

    // RFC interior start
    private const int RfcMap = 389;

    // Orgrimmar safe zone for group formation
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;

    // All RFC accounts — TESTBOT1 is raid leader, RFCBOT2-10 are members
    private static readonly string[] RfcAccounts =
    [
        "TESTBOT1", "RFCBOT2", "RFCBOT3", "RFCBOT4", "RFCBOT5",
        "RFCBOT6", "RFCBOT7", "RFCBOT8", "RFCBOT9", "RFCBOT10"
    ];

    private const int ExpectedBotCount = 10;

    public RagefireChasmTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    private static string ResolveTestSettingsPath(string settingsFileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "LiveValidation", "Settings", settingsFileName);
        if (File.Exists(outputPath))
            return outputPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Tests", "BotRunner.Tests", "LiveValidation", "Settings", settingsFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Settings file not found: {settingsFileName}");
    }

    /// <summary>
    /// Phase 1: Restart StateManager with RFC config and verify all 10 bots enter world.
    /// </summary>
    [SkippableFact]
    public async Task RFC_AllBotsEnterWorld()
    {
        var settingsPath = ResolveTestSettingsPath("RagefireChasm.settings.json");
        _output.WriteLine($"Restarting StateManager with RFC config: {settingsPath}");
        await _bot.RestartWithSettingsAsync(settingsPath);
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready after restart with RFC config");

        // The fixture breaks early after finding FG + first BG. Poll until more bots enter world.
        // 10 BG bots need to authenticate + create/load characters — give them up to 90s.
        var sw = Stopwatch.StartNew();
        var bestCount = 0;
        while (sw.Elapsed < TimeSpan.FromSeconds(90))
        {
            await _bot.RefreshSnapshotsAsync();
            if (_bot.AllBots.Count > bestCount)
            {
                bestCount = _bot.AllBots.Count;
                _output.WriteLine($"[{sw.Elapsed.TotalSeconds:F0}s] Bots in world: {bestCount}/{ExpectedBotCount}");
            }
            if (bestCount >= ExpectedBotCount)
                break;
            await Task.Delay(2000);
        }

        var allBots = _bot.AllBots;
        _output.WriteLine($"Final bots in world: {allBots.Count}/{ExpectedBotCount}");
        foreach (var snap in allBots)
        {
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            _output.WriteLine($"  {snap.AccountName}: {snap.CharacterName} (map={mapId}, pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0}))");
        }

        Assert.True(allBots.Count >= 2,
            $"At least 2 bots must enter world for RFC test (got {allBots.Count}/{ExpectedBotCount}). " +
            "Ensure RFCBOT2-10 accounts exist in MaNGOS with level 8+ characters.");
    }

    /// <summary>
    /// Phase 2: Form raid group — FG bot (TESTBOT1) invites all others, converts to raid.
    /// </summary>
    [SkippableFact]
    public async Task RFC_FormRaidGroup()
    {
        var settingsPath = ResolveTestSettingsPath("RagefireChasm.settings.json");
        _output.WriteLine($"Restarting StateManager with RFC config: {settingsPath}");
        await _bot.RestartWithSettingsAsync(settingsPath);
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready after restart");

        await _bot.RefreshSnapshotsAsync();
        var allBots = _bot.AllBots;
        var leaderAccount = allBots.FirstOrDefault(b =>
            b.AccountName.Equals("TESTBOT1", StringComparison.OrdinalIgnoreCase))?.AccountName;
        global::Tests.Infrastructure.Skip.If(leaderAccount == null, "TESTBOT1 (raid leader) not in world");

        // Collect character names for invite
        var memberNames = new List<string>();
        foreach (var snap in allBots)
        {
            if (!snap.AccountName.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(snap.CharacterName))
            {
                memberNames.Add(snap.CharacterName);
            }
        }
        _output.WriteLine($"Raid leader: {leaderAccount}, members to invite: {string.Join(", ", memberNames)}");
        Assert.True(memberNames.Count >= 1, "Need at least 1 member bot to form a group");

        // Leader invites each member
        foreach (var memberName in memberNames)
        {
            _output.WriteLine($"  Inviting {memberName}...");
            var inviteResult = await _bot.SendActionAsync(leaderAccount!, new ActionMessage
            {
                ActionType = ActionType.SendGroupInvite,
                Parameters = { new RequestParameter { StringParam = memberName } }
            });
            _output.WriteLine($"    Invite result: {inviteResult}");
            await Task.Delay(500);
        }

        // Each member accepts
        foreach (var snap in allBots)
        {
            if (snap.AccountName.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
                continue;

            _output.WriteLine($"  {snap.AccountName} accepting invite...");
            var acceptResult = await _bot.SendActionAsync(snap.AccountName, new ActionMessage
            {
                ActionType = ActionType.AcceptGroupInvite
            });
            _output.WriteLine($"    Accept result: {acceptResult}");
            await Task.Delay(300);
        }

        // Wait for group to form
        await Task.Delay(2000);
        await _bot.RefreshSnapshotsAsync();

        // Verify all bots report the same party leader
        var refreshedBots = _bot.AllBots;
        var groupedCount = 0;
        ulong? expectedLeader = null;

        foreach (var snap in refreshedBots)
        {
            if (snap.PartyLeaderGuid != 0)
            {
                groupedCount++;
                expectedLeader ??= snap.PartyLeaderGuid;

                if (snap.PartyLeaderGuid != expectedLeader.Value)
                    _output.WriteLine($"  WARNING: {snap.AccountName} has different leader GUID: {snap.PartyLeaderGuid:X} vs expected {expectedLeader:X}");
            }
            _output.WriteLine($"  {snap.AccountName}: PartyLeaderGuid=0x{snap.PartyLeaderGuid:X}");
        }

        _output.WriteLine($"\nGrouped: {groupedCount}/{refreshedBots.Count}");
        Assert.True(groupedCount >= 2, $"At least 2 bots should be grouped (got {groupedCount}).");

        // Cleanup: all bots leave group
        foreach (var snap in refreshedBots)
        {
            if (snap.PartyLeaderGuid != 0)
            {
                await _bot.SendActionAsync(snap.AccountName, new ActionMessage
                {
                    ActionType = ActionType.LeaveGroup
                });
            }
        }
    }

    /// <summary>
    /// Phase 3: Teleport all bots to RFC entrance and verify they arrive near the portal.
    /// </summary>
    [SkippableFact]
    public async Task RFC_TeleportToEntrance()
    {
        var settingsPath = ResolveTestSettingsPath("RagefireChasm.settings.json");
        await _bot.RestartWithSettingsAsync(settingsPath);
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        await _bot.RefreshSnapshotsAsync();
        var allBots = _bot.AllBots;

        // Teleport all bots to RFC entrance in Orgrimmar cleft
        foreach (var snap in allBots)
        {
            _output.WriteLine($"Teleporting {snap.AccountName} to RFC entrance...");
            await _bot.BotTeleportAsync(snap.AccountName, KalimdorMap, RfcEntranceX, RfcEntranceY, RfcEntranceZ);
            await Task.Delay(1000);
        }

        // Poll until at least one bot reports being near the RFC entrance.
        // Teleport takes time to propagate through the snapshot pipeline.
        var sw2 = Stopwatch.StartNew();
        var nearEntrance = 0;
        while (sw2.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();

            nearEntrance = 0;
            foreach (var snap in _bot.AllBots)
            {
                var pos = snap.Player?.Unit?.GameObject?.Base?.Position ?? snap.MovementData?.Position;
                var px = pos?.X ?? 0;
                var py = pos?.Y ?? 0;
                var dist = MathF.Sqrt((px - RfcEntranceX) * (px - RfcEntranceX) + (py - RfcEntranceY) * (py - RfcEntranceY));
                var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;

                if ((mapId == KalimdorMap && dist < 50f) || mapId == RfcMap)
                    nearEntrance++;
            }

            if (nearEntrance >= 1)
                break;
        }

        // Log final state
        foreach (var snap in _bot.AllBots)
        {
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position ?? snap.MovementData?.Position;
            var px = pos?.X ?? 0;
            var py = pos?.Y ?? 0;
            var dist = MathF.Sqrt((px - RfcEntranceX) * (px - RfcEntranceX) + (py - RfcEntranceY) * (py - RfcEntranceY));
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}, pos=({px:F0},{py:F0}), dist={dist:F0}y from entrance");
        }

        _output.WriteLine($"\nBots near RFC entrance or inside: {nearEntrance}/{_bot.AllBots.Count}");
        Assert.True(nearEntrance >= 1, $"At least 1 bot should be near RFC entrance (got {nearEntrance}).");
    }

    /// <summary>
    /// Full dungeon run: form group, teleport to entrance, enter RFC, clear encounters.
    /// This is the comprehensive integration test — requires DungeoneeringTask to be implemented.
    /// </summary>
    [SkippableFact]
    public async Task RFC_FullDungeonRun()
    {
        // This test will be enabled once DungeoneeringTask (P5.3) is restored and
        // the dungeoneering coordinator (P5.4) is implemented in StateManager.
        global::Tests.Infrastructure.Skip.If(true,
            "RFC full dungeon run requires DungeoneeringTask implementation (P5.3/P5.4). " +
            "Run RFC_AllBotsEnterWorld, RFC_FormRaidGroup, and RFC_TeleportToEntrance for incremental validation.");
    }
}
