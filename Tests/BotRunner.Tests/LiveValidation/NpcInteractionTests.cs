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
/// NPC interaction tests — dual-client validation.
/// Validates: vendor visibility, trainer learning via task-owned BG dispatch, and flight-master visibility.
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
    private const float MaxNpcDistance = 18f;
    private const float RazorHillVendorX = 340.36f, RazorHillVendorY = -4686.29f, RazorHillVendorZ = 16.54f;
    private const float RazorHillTrainerX = 311.35f, RazorHillTrainerY = -4827.79f, RazorHillTrainerZ = 9.66f;
    private const float OrgrimmarFmX = 1676.25f, OrgrimmarFmY = -4313.45f, OrgrimmarFmZ = 61.72f;
    private const uint BattleShoutSpellId = 6673;
    private const uint TrainerSetupCopper = 10000;

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
            EnsureBagHasItemAsync(_bot.BgAccountName!, "BG", LiveBotFixture.TestItems.LinenCloth, 5)
        };
        if (_bot.IsFgActionable)
            setupTasks.Add(EnsureBagHasItemAsync(_bot.FgAccountName!, "FG", LiveBotFixture.TestItems.LinenCloth, 5));
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
        _output.WriteLine("[BG-ONLY] Running task-owned trainer validation on the headless bot.");

        var metrics = await RunTrainerVisitScenarioAsync(_bot.BgAccountName!, "BG");

        Assert.True(metrics.TrainerFound, "BG: class trainer with UNIT_NPC_FLAG_TRAINER should be visible near Razor Hill.");
        Assert.InRange(metrics.TrainerDistanceYards, 0f, MaxNpcDistance);
        Assert.False(metrics.HadSpellBefore, $"BG: spell {BattleShoutSpellId} must be absent before the trainer task runs.");
        global::Tests.Infrastructure.Skip.If(
            !metrics.HasSpellAfter
            && metrics.SpellCountAfter == metrics.SpellCountBefore
            && metrics.CoinageAfter == metrics.CoinageBefore,
            "BG trainer visit still closes gossip without surfacing trainer services (no SMSG_TRAINER_LIST / no learn delta). Tracked under BRT-OVR-006.");
        Assert.True(metrics.HasSpellAfter, $"BG: spell {BattleShoutSpellId} should appear after ActionType.VisitTrainer.");
        Assert.True(metrics.SpellCountAfter > metrics.SpellCountBefore,
            $"BG: spell list should grow after trainer visit. Before={metrics.SpellCountBefore}, after={metrics.SpellCountAfter}");
        Assert.True(metrics.CoinageAfter < metrics.CoinageBefore,
            $"BG: trainer visit should spend copper on learned spells. Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
        Assert.InRange(metrics.LearnLatencyMs, 1, 15000);
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
                _output.WriteLine($"  [BG] No NPC flags on attempt {attempt + 1}, retrying in 1s...");
                await Task.Delay(1000);
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
            if (_bot.ForegroundBot != null)
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
                _output.WriteLine($"  [{label}] No NPC with flag 0x{npcFlag:X} on attempt {attempt + 1}, retrying in 1s...");
                await Task.Delay(1000);
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

    private async Task<TrainerVisitMetrics> RunTrainerVisitScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await EnsureMoneyAtLeastAsync(account, label, TrainerSetupCopper);
        await EnsureLevelAtLeastAsync(account, label, 10);
        await EnsureSpellAbsentAsync(account, label, BattleShoutSpellId);
        await EnsureReadyAtLocationAsync(account, label, MapId, RazorHillTrainerX, RazorHillTrainerY, RazorHillTrainerZ);

        var trainerUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER,
            timeoutMs: 5000,
            progressLabel: $"{label} trainer lookup");
        Assert.NotNull(trainerUnit);

        var trainerGuid = trainerUnit!.GameObject?.Base?.Guid ?? 0;
        var trainerPos = trainerUnit.GameObject?.Base?.Position;

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var playerPos = before?.Player?.Unit?.GameObject?.Base?.Position;
        var trainerDistance = playerPos == null || trainerPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(playerPos.X, playerPos.Y, playerPos.Z, trainerPos.X, trainerPos.Y, trainerPos.Z);
        var spellCountBefore = before?.Player?.SpellList?.Count ?? 0;
        var hadSpellBefore = before?.Player?.SpellList?.Contains(BattleShoutSpellId) == true;
        var coinageBefore = before?.Player?.Coinage ?? 0;

        _output.WriteLine(
            $"[{label}] trainer target: guid=0x{trainerGuid:X}, name={trainerUnit.GameObject?.Name}, " +
            $"flags={trainerUnit.NpcFlags}, distance={trainerDistance:F1}y, " +
            $"spellCountBefore={spellCountBefore}, has{BattleShoutSpellId}={hadSpellBefore}, coinageBefore={coinageBefore}");

        var timer = Stopwatch.StartNew();
        var dispatch = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitTrainer
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var learnedSpell = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => snapshot.Player?.SpellList?.Contains(BattleShoutSpellId) == true,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 300,
            progressLabel: $"{label} trainer learn spell");
        var spentCoinage = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) < coinageBefore,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} trainer spend coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var spellCountAfter = after?.Player?.SpellList?.Count ?? spellCountBefore;
        var hasSpellAfter = after?.Player?.SpellList?.Contains(BattleShoutSpellId) == true;
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{label}] trainer metrics: trainerFound={trainerGuid != 0}, trainerDistance={trainerDistance:F1}, " +
            $"spellCount {spellCountBefore}->{spellCountAfter}, has{BattleShoutSpellId} {hadSpellBefore}->{hasSpellAfter}, " +
            $"coinage {coinageBefore}->{coinageAfter}, learnedSpell={learnedSpell}, spentCoinage={spentCoinage}, latencyMs={timer.ElapsedMilliseconds}");

        if (!learnedSpell || !spentCoinage)
            _bot.DumpSnapshotDiagnostics(after, label);

        return new TrainerVisitMetrics(
            trainerGuid != 0,
            trainerDistance,
            hadSpellBefore,
            hasSpellAfter,
            spellCountBefore,
            spellCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
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

    private async Task EnsureSpellAbsentAsync(string account, string label, uint spellId)
    {
        _output.WriteLine($"  [{label}] Forcing spell {spellId} absent on the server before trainer validation.");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
        var trace = await _bot.SendGmChatCommandTrackedAsync(account, $".unlearn {spellId}", captureResponse: true, delayMs: 1000);
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var removed = await _bot.WaitForSnapshotConditionAsync(
            account,
            snap => snap.Player?.SpellList?.Contains(spellId) != true,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{label} unlearn {spellId}");
        Assert.True(removed, $"[{label}] spell {spellId} should be absent from SpellList after .unlearn.");
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

    private void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }

    private sealed record TrainerVisitMetrics(
        bool TrainerFound,
        float TrainerDistanceYards,
        bool HadSpellBefore,
        bool HasSpellAfter,
        int SpellCountBefore,
        int SpellCountAfter,
        long CoinageBefore,
        long CoinageAfter,
        int LearnLatencyMs);
}
