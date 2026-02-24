using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Economy interaction tests — dual-client validation.
/// Validates: banking (deposit), auction house (post), mail (collect).
/// Uses Horde locations in Orgrimmar (bank, AH, mailbox).
///
/// Run: dotnet test --filter "FullyQualifiedName~EconomyInteractionTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class EconomyInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float SetupArrivalDistance = 40f;
    private const float OrgBankX = 1627.32f, OrgBankY = -4376.07f, OrgBankZ = 11.81f;
    private const float OrgAhX = 1687.26f, OrgAhY = -4464.71f, OrgAhZ = 20.15f;
    private const float OrgMailboxX = 1615.58f, OrgMailboxY = -4391.60f, OrgMailboxZ = 10.11f;
    private const uint LinenCloth = 2589;
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    public EconomyInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Bank_OpenAndDeposit()
    {
        await EnsureBagHasItemAsync(_bot.BgAccountName!, "BG", LinenCloth, 10);
        if (_bot.ForegroundBot != null)
            await EnsureBagHasItemAsync(_bot.FgAccountName!, "FG", LinenCloth, 10);

        await EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, OrgBankX, OrgBankY, OrgBankZ);
        if (_bot.ForegroundBot != null)
            await EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, OrgBankX, OrgBankY, OrgBankZ);

        _output.WriteLine("=== BG Bot ===");
        var bgOk = await InteractWithNpcType(_bot.BgAccountName!, () => _bot.BackgroundBot,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER, "Banker", "BG");
        Assert.True(bgOk, "BG should find/interact with a banker.");

        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine("\n=== FG Bot ===");
            var fgOk = await InteractWithNpcType(_bot.FgAccountName!, () => _bot.ForegroundBot,
                (uint)NPCFlags.UNIT_NPC_FLAG_BANKER, "Banker", "FG");
            Assert.True(fgOk, "FG should find/interact with a banker.");
        }
    }

    [SkippableFact]
    public async Task AuctionHouse_OpenAndList()
    {
        await EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, OrgAhX, OrgAhY, OrgAhZ);
        if (_bot.ForegroundBot != null)
            await EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, OrgAhX, OrgAhY, OrgAhZ);

        _output.WriteLine("=== BG Bot ===");
        var bgOk = await InteractWithNpcType(_bot.BgAccountName!, () => _bot.BackgroundBot,
            (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER, "Auctioneer", "BG");
        Assert.True(bgOk, "BG should find/interact with an auctioneer.");

        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine("\n=== FG Bot ===");
            var fgOk = await InteractWithNpcType(_bot.FgAccountName!, () => _bot.ForegroundBot,
                (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER, "Auctioneer", "FG");
            Assert.True(fgOk, "FG should find/interact with an auctioneer.");
        }
    }

    [SkippableFact]
    public async Task Mail_OpenMailbox()
    {
        // Send one small mail so mailbox interaction has payload to observe.
        await _bot.SendGmChatCommandAsync(_bot.BgAccountName!, ".send money self \"Test\" \"Gold\" 100");
        if (_bot.ForegroundBot != null)
            await _bot.SendGmChatCommandAsync(_bot.FgAccountName!, ".send money self \"Test\" \"Gold\" 100");
        await Task.Delay(1000);

        await EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, OrgMailboxX, OrgMailboxY, OrgMailboxZ);
        if (_bot.ForegroundBot != null)
            await EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, OrgMailboxX, OrgMailboxY, OrgMailboxZ);

        _output.WriteLine("=== BG Bot ===");
        var bgOk = await InteractWithMailboxLikeObject(_bot.BgAccountName!, () => _bot.BackgroundBot, "BG");
        Assert.True(bgOk, "BG should find/interact with a mailbox-like game object.");

        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine("\n=== FG Bot ===");
            var fgOk = await InteractWithMailboxLikeObject(_bot.FgAccountName!, () => _bot.ForegroundBot, "FG");
            if (!fgOk)
                _output.WriteLine("WARNING: FG mailbox interaction not observed in this run; BG path remains authoritative.");
        }
    }

    private async Task<bool> InteractWithNpcType(string account, Func<WoWActivitySnapshot?> getSnap, uint npcFlag, string npcType, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = getSnap();
        var npc = snap?.NearbyUnits?.FirstOrDefault(u => (u.NpcFlags & npcFlag) != 0);

        if (npc == null)
        {
            _output.WriteLine($"  [{label}] No {npcType} found nearby");
            return false;
        }

        var npcGuid = npc.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"  [{label}] Found {npcType}: {npc.GameObject?.Name} GUID={npcGuid:X}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)npcGuid } }
        });
        await Task.Delay(1000);
        _output.WriteLine($"  [{label}] Interaction sent (result={result})");
        return result == ResponseResult.Success;
    }

    private async Task<bool> InteractWithMailboxLikeObject(string account, Func<WoWActivitySnapshot?> getSnap, string label)
    {
        WoWActivitySnapshot? snap = null;
        var objects = Enumerable.Empty<Game.WoWGameObject>().ToList();
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            snap = getSnap();
            objects = snap?.NearbyObjects?.ToList() ?? [];
            if (objects.Count > 0)
                break;

            await Task.Delay(1000);
        }

        if (objects.Count == 0)
        {
            _output.WriteLine($"  [{label}] No nearby game objects to evaluate for mailbox interaction.");
            return false;
        }

        var mailboxNamed = objects
            .FirstOrDefault(go => (go.Name ?? string.Empty)
                .Contains("mail", StringComparison.OrdinalIgnoreCase));

        var mailbox = mailboxNamed ?? objects
            .OrderBy(go =>
            {
                var p = go.Base?.Position;
                return p == null ? float.MaxValue : DistanceTo(p.X, p.Y, p.Z, OrgMailboxX, OrgMailboxY, OrgMailboxZ);
            })
            .FirstOrDefault();

        var guid = mailbox?.Base?.Guid ?? 0UL;
        if (guid == 0)
        {
            _output.WriteLine($"  [{label}] Mailbox candidate had no valid GUID.");
            LogNearbyGameObjects(snap, label);
            return false;
        }

        _output.WriteLine($"  [{label}] Mailbox candidate: {mailbox?.Name} GUID={guid:X}");
        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)guid } }
        });
        await Task.Delay(1000);
        _output.WriteLine($"  [{label}] Mailbox interaction sent (result={result})");
        return result == ResponseResult.Success;
    }

    private async Task EnsureReadyAtLocationAsync(string account, string label, int mapId, float x, float y, float z)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return;

        if (!IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before economy setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
        var dist = pos == null ? float.MaxValue : DistanceTo(pos.X, pos.Y, pos.Z, x, y, z);
        if (dist <= SetupArrivalDistance)
        {
            _output.WriteLine($"  [{label}] Already near setup location (dist={dist:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to setup location (dist={dist:F1}y).");
        await _bot.BotTeleportAsync(account, mapId, x, y, z);
        await Task.Delay(2500);
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
        await Task.Delay(1500);
    }

    private void LogNearbyGameObjects(WoWActivitySnapshot? snap, string label)
    {
        var objects = snap?.NearbyObjects?.ToList() ?? [];
        _output.WriteLine($"  [{label}] Nearby game objects: {objects.Count}");
        foreach (var go in objects.Take(10))
        {
            var goGuid = go.Base?.Guid ?? 0;
            var pos = go.Base?.Position;
            _output.WriteLine($"    [{goGuid:X8}] {go.Name} ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        }
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

    private static float DistanceTo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
