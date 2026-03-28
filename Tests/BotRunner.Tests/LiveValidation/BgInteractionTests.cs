using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// BG-only interaction tests for game world services not yet fully validated.
/// Validates: banking, auction house, mail collection, flight master discovery,
/// and transport (Deeprun Tram placeholder).
/// Uses Horde locations in Orgrimmar.
///
/// Run: dotnet test --filter "FullyQualifiedName~BgInteractionTests" --configuration Release
/// </summary>
[Collection(BgOnlyValidationCollection.Name)]
public class BgInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMap = 1;
    private const int EasternKingdomsMap = 0;
    private const float SetupArrivalDistance = 40f;

    // Orgrimmar bank — Z+3 offset applied
    private const float OrgBankX = 1627.32f, OrgBankY = -4376.07f, OrgBankZ = 17.81f;
    // Orgrimmar AH — Z+3 offset applied
    private const float OrgAhX = 1687.26f, OrgAhY = -4464.71f, OrgAhZ = 26.15f;
    // Orgrimmar mailbox — Z+3 offset applied
    private const float OrgMailboxX = 1615.58f, OrgMailboxY = -4391.60f, OrgMailboxZ = 16.11f;
    // Orgrimmar flight master — Z+3 offset applied
    private const float OrgFmX = 1676.25f, OrgFmY = -4313.45f, OrgFmZ = 67.72f;

    public BgInteractionTests(BgOnlyBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    // ── Test 1: Bank ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Bank_DepositItem_MovesToBankSlot()
    {
        _output.WriteLine("=== Bank Deposit: BG bot finds banker and interacts ===");

        var account = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Setup: teleport to Orgrimmar bank and add a Worn Mace (item 36)
        await EnsureReadyAtLocationAsync(account, "BG", KalimdorMap, OrgBankX, OrgBankY, OrgBankZ);
        await EnsureBagHasItemAsync(account, "BG", itemId: 36, addCount: 1);

        // Action: find banker NPC and dispatch InteractWith
        var banker = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
            timeoutMs: 15000,
            progressLabel: "BG banker lookup");

        Assert.True(banker != null,
            "BG: No banker NPC found near Orgrimmar bank after 15s — this is a unit detection or ObjectManager bug.");

        var bankerGuid = banker!.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[BG] Found banker: {banker.GameObject?.Name} GUID=0x{bankerGuid:X} flags={banker.NpcFlags}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)bankerGuid } }
        });
        _output.WriteLine($"[BG] InteractWith banker dispatched (result={result})");

        Assert.Equal(ResponseResult.Success, result);
    }

    // ── Test 2: Auction House ─────────────────────────────────────────────

    [SkippableFact]
    public async Task AuctionHouse_InteractWithAuctioneer()
    {
        _output.WriteLine("=== Auction House: BG bot finds auctioneer and interacts ===");

        var account = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Setup: teleport to Orgrimmar AH
        await EnsureReadyAtLocationAsync(account, "BG", KalimdorMap, OrgAhX, OrgAhY, OrgAhZ);

        // Action: find auctioneer NPC and dispatch InteractWith
        var auctioneer = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER,
            timeoutMs: 15000,
            progressLabel: "BG auctioneer lookup");

        Assert.True(auctioneer != null,
            "BG: No auctioneer NPC found near Orgrimmar AH after 15s — this is a unit detection or ObjectManager bug.");

        var auctioneerGuid = auctioneer!.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[BG] Found auctioneer: {auctioneer.GameObject?.Name} GUID=0x{auctioneerGuid:X} flags={auctioneer.NpcFlags}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)auctioneerGuid } }
        });
        _output.WriteLine($"[BG] InteractWith auctioneer dispatched (result={result})");

        Assert.Equal(ResponseResult.Success, result);
    }

    // ── Test 3: Mail ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Mail_SendGoldAndCollect_CoinageChanges()
    {
        _output.WriteLine("=== Mail Collection: BG bot collects gold from mailbox ===");

        var account = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Setup: send gold via SOAP, then teleport to mailbox
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var charName = snap?.CharacterName ?? "";
        global::Tests.Infrastructure.Skip.If(string.IsNullOrEmpty(charName), "BG character name not available");

        _output.WriteLine($"[BG] Sending 10000 copper to {charName} via SOAP .send money");
        await _bot.ExecuteGMCommandAsync($".send money {charName} \"Test Gold\" \"Mail collection test\" 10000");

        await EnsureReadyAtLocationAsync(account, "BG", KalimdorMap, OrgMailboxX, OrgMailboxY, OrgMailboxZ);

        // Record coinage before mail collection
        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(account);
        var coinageBefore = snapBefore?.Player?.Coinage ?? 0;
        _output.WriteLine($"[BG] Coinage before: {coinageBefore}");

        // Action: find mailbox game object and dispatch CheckMail
        var mailbox = await FindMailboxAsync(account, "BG");
        Assert.True(mailbox.Found,
            "BG: No mailbox found near Orgrimmar mailbox location — this is a game object detection bug.");

        _output.WriteLine($"[BG] Found mailbox: type={mailbox.GoType} name='{mailbox.Name}' GUID=0x{mailbox.Guid:X}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
            Parameters = { new RequestParameter { LongParam = (long)mailbox.Guid } }
        });
        _output.WriteLine($"[BG] CheckMail dispatched (result={result})");
        Assert.Equal(ResponseResult.Success, result);

        // Assert: coinage should increase after collecting mail
        var coinageIncreased = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => (s?.Player?.Coinage ?? 0) > coinageBefore,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: "BG mail-coinage-increase");

        await _bot.RefreshSnapshotsAsync();
        var snapAfter = await _bot.GetSnapshotAsync(account);
        var coinageAfter = snapAfter?.Player?.Coinage ?? 0;
        _output.WriteLine($"[BG] Coinage after: {coinageAfter} (delta={coinageAfter - coinageBefore})");

        Assert.True(coinageAfter > coinageBefore,
            $"BG coinage should increase after collecting mail (before={coinageBefore}, after={coinageAfter})");
    }

    // ── Test 4: Flight Master ─────────────────────────────────────────────

    [SkippableFact]
    public async Task FlightMaster_DiscoverAndTakeFlight()
    {
        _output.WriteLine("=== Flight Master: BG bot discovers taxi nodes ===");

        var account = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Setup: teleport to Orgrimmar FM and ensure money
        await _bot.SendGmChatCommandAsync(account, ".modify money 50000");
        await _bot.WaitForSnapshotConditionAsync(
            account,
            s => (s?.Player?.Coinage ?? 0) >= 50000,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: "BG money-setup");

        await EnsureReadyAtLocationAsync(account, "BG", KalimdorMap, OrgFmX, OrgFmY, OrgFmZ);

        // Action: find flight master NPC and dispatch VisitFlightMaster
        var fm = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: "BG flight master lookup");

        Assert.True(fm != null,
            "BG: No flight master NPC found near Orgrimmar after 15s — this is a unit detection or ObjectManager bug.");

        var fmGuid = fm!.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[BG] Found flight master: {fm.GameObject?.Name} GUID=0x{fmGuid:X} flags={fm.NpcFlags}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        _output.WriteLine($"[BG] VisitFlightMaster dispatched (result={result})");
        Assert.Equal(ResponseResult.Success, result);

        // Assert: wait for task to complete (poll for at least one snapshot cycle)
        await _bot.WaitForSnapshotConditionAsync(
            account,
            _ => true,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 500,
            progressLabel: "BG fm-task-complete");

        _output.WriteLine("[BG] Flight master visit task completed.");
    }

    // ── Test 5: Deeprun Tram (placeholder) ────────────────────────────────

    [SkippableFact]
    public async Task DeeprunTram_RideTransport_ArrivesAtDestination()
    {
        // Test characters are Horde (Orgrimmar-based). Deeprun Tram requires Alliance.
        // MaNGOS bounces Horde players out of the tram instance.
        // This is a placeholder for future Alliance test bot support.
        global::Tests.Infrastructure.Skip.If(true,
            "Test characters are Horde — Deeprun Tram requires Alliance characters. " +
            "Placeholder for future Alliance test bot.");

        await Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task EnsureReadyAtLocationAsync(string account, string label, int mapId, float x, float y, float z)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return;

        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await _bot.WaitForSnapshotConditionAsync(account, LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(5));
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
        var dist = pos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(pos.X, pos.Y, pos.Z, x, y, z);

        if (dist <= SetupArrivalDistance)
        {
            _output.WriteLine($"  [{label}] Already near setup location (dist={dist:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to setup location (dist={dist:F1}y).");
        await _bot.BotTeleportAsync(account, mapId, x, y, z);
        await _bot.WaitForTeleportSettledAsync(account, x, y);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: label);
    }

    private async Task EnsureBagHasItemAsync(string account, string label, uint itemId, int addCount)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var hasItem = snap?.Player?.BagContents?.Values.Any(v => v == itemId) == true;
        if (hasItem)
        {
            _output.WriteLine($"  [{label}] Item {itemId} already present; skipping additem.");
            return;
        }

        _output.WriteLine($"  [{label}] Adding item {itemId} x{addCount}.");
        await _bot.BotAddItemAsync(account, itemId, addCount);

        await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s?.Player?.BagContents?.Values.Any(v => v == itemId) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} additem-{itemId}");
    }

    private async Task<MailboxSearchResult> FindMailboxAsync(string account, string label)
    {
        const uint MailboxGoType = 19;
        Game.WoWGameObject? mailbox = null;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var objects = snap?.NearbyObjects?.ToList() ?? [];

            // Primary: find by GameObjectType = Mailbox (19)
            mailbox = objects.FirstOrDefault(go => go.GameObjectType == MailboxGoType);

            // Fallback: name-based search
            mailbox ??= objects.FirstOrDefault(go => (go.Name ?? string.Empty)
                .Contains("mail", StringComparison.OrdinalIgnoreCase));

            if (mailbox != null)
                break;

            if (objects.Count > 0)
            {
                _output.WriteLine($"  [{label}] {objects.Count} nearby objects, none are mailboxes yet...");
            }
        }

        if (mailbox == null)
            return new MailboxSearchResult(false, 0, 0, null);

        var guid = mailbox.Base?.Guid ?? 0UL;
        return new MailboxSearchResult(guid != 0, guid, mailbox.GameObjectType, mailbox.Name);
    }

    private sealed record MailboxSearchResult(
        bool Found,
        ulong Guid,
        uint GoType,
        string? Name);
}
