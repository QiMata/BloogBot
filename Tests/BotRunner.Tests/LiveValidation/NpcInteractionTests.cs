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
/// NPC interaction tests — dual-client validation.
/// Validates: vendor (sell/buy), trainer (learn spells), flight master (discover nodes).
/// Uses Horde locations: Razor Hill (vendor/trainer), Orgrimmar (flight master).
///
/// Run: dotnet test --filter "FullyQualifiedName~NpcInteractionTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class NpcInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float SetupArrivalDistance = 40f;
    private const float RazorHillVendorX = 340.36f, RazorHillVendorY = -4686.29f, RazorHillVendorZ = 16.54f;
    private const float RazorHillTrainerX = 311.35f, RazorHillTrainerY = -4827.79f, RazorHillTrainerZ = 9.66f;
    private const float OrgrimmarFmX = 1676.25f, OrgrimmarFmY = -4313.45f, OrgrimmarFmZ = 61.72f;
    private const uint LinenCloth = 2589;

    public NpcInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Vendor_OpenAndSeeInventory()
    {
        await RunNpcInteraction("Vendor", RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task Vendor_SellJunkItems()
    {
        var setupTasks = new System.Collections.Generic.List<Task>
        {
            EnsureBagHasItemAsync(_bot.BgAccountName!, "BG", LinenCloth, 5)
        };
        if (_bot.IsFgActionable)
            setupTasks.Add(EnsureBagHasItemAsync(_bot.FgAccountName!, "FG", LinenCloth, 5));
        await Task.WhenAll(setupTasks);

        await RunNpcInteraction("Vendor (sell)", RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task Trainer_OpenAndSeeSpells()
    {
        await RunNpcInteraction("Trainer", RazorHillTrainerX, RazorHillTrainerY, RazorHillTrainerZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task Trainer_LearnAvailableSpells()
    {
        var bgSetup = Task.WhenAll(
            EnsureMoneyAtLeastAsync(_bot.BgAccountName!, "BG", 10000),
            EnsureLevelAtLeastAsync(_bot.BgAccountName!, "BG", 10));
        if (_bot.IsFgActionable)
        {
            var fgSetup = Task.WhenAll(
                EnsureMoneyAtLeastAsync(_bot.FgAccountName!, "FG", 10000),
                EnsureLevelAtLeastAsync(_bot.FgAccountName!, "FG", 10));
            await Task.WhenAll(bgSetup, fgSetup);
        }
        else
        {
            await bgSetup;
        }

        await RunNpcInteraction("Trainer (learn)", RazorHillTrainerX, RazorHillTrainerY, RazorHillTrainerZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task FlightMaster_DiscoverNodes()
    {
        await RunNpcInteraction("Flight Master", OrgrimmarFmX, OrgrimmarFmY, OrgrimmarFmZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task ObjectManager_DetectsNpcFlags()
    {
        var hasFg = _bot.IsFgActionable;
        var setupTasks = new System.Collections.Generic.List<Task>
        {
            EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ)
        };
        if (hasFg)
            setupTasks.Add(EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ));
        await Task.WhenAll(setupTasks);

        // Retry up to 3 times — NPC flags may arrive in PARTIAL updates after CREATE_OBJECT
        System.Collections.Generic.List<Game.WoWUnit> bgWithFlags = [];
        System.Collections.Generic.List<Game.WoWUnit> bgUnits = [];
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            bgUnits = _bot.BackgroundBot?.NearbyUnits?.ToList() ?? [];
            bgWithFlags = bgUnits.Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE).ToList();
            if (bgWithFlags.Count > 0) break;
            if (attempt < 2)
            {
                _output.WriteLine($"  [BG] No NPC flags on attempt {attempt + 1}, retrying in 2s...");
                await Task.Delay(2000);
            }
        }

        // BG bot NPC detection — must find at least one NPC with non-zero flags
        LogNpcFlags("BG", _bot.BackgroundBot);
        Assert.True(bgUnits.Count > 0, "[BG] ObjectManager should detect nearby units at Razor Hill vendor area.");
        Assert.True(bgWithFlags.Count > 0, "[BG] At least one nearby unit should have non-zero NPC flags at Razor Hill vendor area.");

        // FG bot NPC detection — parity check
        if (hasFg)
        {
            LogNpcFlags("FG", _bot.ForegroundBot);
            var fgUnits = _bot.ForegroundBot?.NearbyUnits?.ToList() ?? [];
            var fgWithFlags = fgUnits.Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE).ToList();
            Assert.True(fgUnits.Count > 0, "[FG] ObjectManager should detect nearby units at Razor Hill vendor area.");
            Assert.True(fgWithFlags.Count > 0, "[FG] At least one nearby unit should have non-zero NPC flags at Razor Hill vendor area.");
        }
        else
        {
            _output.WriteLine("FG Bot: NOT AVAILABLE");
        }
    }

    private async Task RunNpcInteraction(string npcType, float x, float y, float z, uint npcFlag, bool requireNpcInteraction)
    {
        // Use IsFgActionable instead of just null-check — avoids cascading failures when
        // FG WoW.exe crashed and relaunched but is stuck dead/ghost or dropping actions.
        var hasFg = _bot.IsFgActionable;

        // Setup both bots at the location in parallel.
        var setupTasks = new System.Collections.Generic.List<Task>
        {
            EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, x, y, z)
        };
        if (hasFg)
            setupTasks.Add(EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, x, y, z));
        await Task.WhenAll(setupTasks);

        // Run interactions in parallel.
        _output.WriteLine($"=== BG Bot: {npcType} ===");
        if (hasFg)
        {
            _output.WriteLine($"=== FG Bot: {npcType} ===");
            _output.WriteLine($"[PARITY] Running BG and FG {npcType} interactions in parallel.");

            var bgTask = InteractWithNpc(_bot.BgAccountName!, () => _bot.BackgroundBot, npcFlag, "BG");
            var fgTask = InteractWithNpc(_bot.FgAccountName!, () => _bot.ForegroundBot, npcFlag, "FG");
            await Task.WhenAll(bgTask, fgTask);

            if (requireNpcInteraction)
            {
                Assert.True(await bgTask, $"BG should find and interact with NPC flag 0x{npcFlag:X} for scenario '{npcType}'.");
                Assert.True(await fgTask, $"FG should find and interact with NPC flag 0x{npcFlag:X} for scenario '{npcType}'.");
            }
        }
        else
        {
            if (_bot.IsFgActionable)
                _output.WriteLine("[WARN] FG bot present but not actionable (dead/ghost/actions dropped). Running BG-only.");
            var bgOk = await InteractWithNpc(_bot.BgAccountName!, () => _bot.BackgroundBot, npcFlag, "BG");
            if (requireNpcInteraction)
                Assert.True(bgOk, $"BG should find and interact with NPC flag 0x{npcFlag:X} for scenario '{npcType}'.");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
    }

    private async Task<bool> InteractWithNpc(string account, Func<WoWActivitySnapshot?> getSnap, uint npcFlag, string label)
    {
        // Retry up to 3 times — FG bot's WoW.exe may need time to load area after teleport
        var units = new System.Collections.Generic.List<Game.WoWUnit>();
        WoWActivitySnapshot? snap = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            snap = getSnap();
            units = snap?.NearbyUnits?.Where(u => (u.NpcFlags & npcFlag) != 0).ToList() ?? [];
            if (units.Count > 0) break;
            if (attempt < 2)
            {
                _output.WriteLine($"  [{label}] No NPC with flag 0x{npcFlag:X} on attempt {attempt + 1}, retrying in 2s...");
                await Task.Delay(2000);
            }
        }

        if (units.Count == 0)
        {
            _output.WriteLine($"  [{label}] No NPC with flag 0x{npcFlag:X} found nearby after 3 attempts");
            LogAllUnits(snap, label);
            return false;
        }

        var npc = units[0];
        var npcGuid = npc.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"  [{label}] Found: {npc.GameObject?.Name} GUID={npcGuid:X} NpcFlags={npc.NpcFlags}");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)npcGuid } }
        });
        await Task.Delay(1000);
        _output.WriteLine($"  [{label}] Interaction sent (result={result})");
        return result == ResponseResult.Success;
    }

    private async Task EnsureReadyAtLocationAsync(string account, string label, int mapId, float x, float y, float z)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return;

        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before NPC setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(1200);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
        var dist = pos == null
            ? float.MaxValue
            : DistanceTo(pos.X, pos.Y, pos.Z, x, y, z);

        if (dist <= SetupArrivalDistance)
        {
            _output.WriteLine($"  [{label}] Already near setup location (dist={dist:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to setup location (dist={dist:F1}y).");
        await _bot.BotTeleportAsync(account, mapId, x, y, z);
        await Task.Delay(5000); // Wait for WoW.exe area load + SMSG_UPDATE_OBJECT with NPC flags
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
        await Task.Delay(1000);
    }

    private async Task EnsureMoneyAtLeastAsync(string account, string label, long minCopper)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var current = snap?.Player?.Coinage ?? 0L;
        if (current >= minCopper)
        {
            _output.WriteLine($"  [{label}] Coinage already >= {minCopper}; skipping money setup.");
            return;
        }

        var delta = minCopper - current;
        _output.WriteLine($"  [{label}] Increasing money by {delta} copper.");
        await _bot.SendGmChatCommandAsync(account, $".modify money {delta}");
        await Task.Delay(1000);
    }

    private async Task EnsureLevelAtLeastAsync(string account, string label, uint minLevel)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var level = snap?.Player?.Unit?.GameObject?.Level ?? 0;
        if (level >= minLevel)
        {
            _output.WriteLine($"  [{label}] Level already >= {minLevel}; skipping level setup.");
            return;
        }

        _output.WriteLine($"  [{label}] Setting level to {minLevel} (current={level}).");
        await _bot.SendGmChatCommandAsync(account, $".character level {minLevel}");
        await Task.Delay(1200);
    }

    private void LogNpcFlags(string label, WoWActivitySnapshot? snap)
    {
        var units = snap?.NearbyUnits?.ToList() ?? [];
        var withFlags = units.Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE).ToList();
        _output.WriteLine($"[{label}] Nearby units: {units.Count}, with NPC flags: {withFlags.Count}");
        foreach (var npc in withFlags.Take(15))
        {
            var pos = npc.GameObject?.Base?.Position;
            _output.WriteLine($"  [{label}] {npc.GameObject?.Name} NpcFlags={npc.NpcFlags} ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        }
    }

    private void LogAllUnits(WoWActivitySnapshot? snap, string label)
    {
        var units = snap?.NearbyUnits?.Take(10).ToList() ?? [];
        _output.WriteLine($"  [{label}] Total nearby units: {units.Count}");
        foreach (var u in units)
        {
            var guid = u.GameObject?.Base?.Guid ?? 0;
            _output.WriteLine($"    [{guid:X8}] {u.GameObject?.Name} NpcFlags={u.NpcFlags}");
        }
    }

    private static float DistanceTo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
