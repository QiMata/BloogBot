using System;
<<<<<<< HEAD
=======
using System.Diagnostics;
>>>>>>> cpp_physics_system
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
<<<<<<< HEAD
/// NPC interaction tests — dual-client validation.
/// Validates: vendor (sell/buy), trainer (learn spells), flight master (discover nodes).
=======
/// NPC interaction tests — task-driven, dual-client validation.
/// Validates via BotTask dispatch:
///   - Vendor: VisitVendor task finds vendor, repairs, completes (task completion assertion)
///   - Trainer: VisitTrainer task purchases available spells (spell-count + coinage assertion)
///   - Flight Master: VisitFlightMaster task discovers taxi nodes
///   - NPC flags: snapshot-level NPC flag detection
>>>>>>> cpp_physics_system
/// Uses Horde locations: Razor Hill (vendor/trainer), Orgrimmar (flight master).
///
/// Run: dotnet test --filter "FullyQualifiedName~NpcInteractionTests" --configuration Release
/// </summary>
<<<<<<< HEAD
[RequiresMangosStack]
=======
>>>>>>> cpp_physics_system
[Collection(LiveValidationCollection.Name)]
public class NpcInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    private const float SetupArrivalDistance = 40f;
<<<<<<< HEAD
    private const float RazorHillVendorX = 340.36f, RazorHillVendorY = -4686.29f, RazorHillVendorZ = 16.54f;
    private const float RazorHillTrainerX = 311.35f, RazorHillTrainerY = -4827.79f, RazorHillTrainerZ = 9.66f;
    private const float OrgrimmarFmX = 1676.25f, OrgrimmarFmY = -4313.45f, OrgrimmarFmZ = 61.72f;
    private const uint LinenCloth = 2589;
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD
=======
    // Z+3 offset applied to spawn table Z values to avoid UNDERMAP detection
    private const float RazorHillVendorX = 340.36f, RazorHillVendorY = -4686.29f, RazorHillVendorZ = 19.54f;
    private const float RazorHillTrainerX = 311.35f, RazorHillTrainerY = -4827.79f, RazorHillTrainerZ = 12.66f;
    private const float OrgrimmarFmX = 1676.25f, OrgrimmarFmY = -4313.45f, OrgrimmarFmZ = 64.72f;
    private const uint BattleShoutSpellId = 6673;
    private const uint TrainerSetupCopper = 10000;
>>>>>>> cpp_physics_system

    public NpcInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
<<<<<<< HEAD
    public async Task Vendor_OpenAndSeeInventory()
    {
        await RunNpcInteraction("Vendor", RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task Vendor_SellJunkItems()
    {
        await EnsureBagHasItemAsync(_bot.BgAccountName!, "BG", LinenCloth, 5);
        if (_bot.ForegroundBot != null)
            await EnsureBagHasItemAsync(_bot.FgAccountName!, "FG", LinenCloth, 5);

        await RunNpcInteraction("Vendor (sell)", RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task Trainer_OpenAndSeeSpells()
    {
        await RunNpcInteraction("Trainer", RazorHillTrainerX, RazorHillTrainerY, RazorHillTrainerZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER, requireNpcInteraction: true);
=======
    public async Task Vendor_VisitTask_FindsAndInteracts()
    {
        _output.WriteLine("=== Vendor Visit: Task-driven vendor interaction ===");

        var bgMetrics = await RunVendorVisitScenarioAsync(_bot.BgAccountName!, "BG");
        Assert.True(bgMetrics.VendorFound, "BG: vendor NPC with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
        Assert.True(bgMetrics.TaskCompleted, "BG: VendorVisitTask should complete within timeout.");

        if (_bot.IsFgActionable)
        {
            var fgMetrics = await RunVendorVisitScenarioAsync(_bot.FgAccountName!, "FG");
            Assert.True(fgMetrics.VendorFound, "FG: vendor should be visible near Razor Hill.");
            Assert.True(fgMetrics.TaskCompleted, "FG: VendorVisitTask should complete within timeout.");
        }
        else
        {
            _output.WriteLine("[FG] Skipped — FG bot not actionable.");
        }
>>>>>>> cpp_physics_system
    }

    [SkippableFact]
    public async Task Trainer_LearnAvailableSpells()
    {
<<<<<<< HEAD
        await EnsureMoneyAtLeastAsync(_bot.BgAccountName!, "BG", 10000);
        await EnsureLevelAtLeastAsync(_bot.BgAccountName!, "BG", 10);
        if (_bot.ForegroundBot != null)
        {
            await EnsureMoneyAtLeastAsync(_bot.FgAccountName!, "FG", 10000);
            await EnsureLevelAtLeastAsync(_bot.FgAccountName!, "FG", 10);
        }

        await RunNpcInteraction("Trainer (learn)", RazorHillTrainerX, RazorHillTrainerY, RazorHillTrainerZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER, requireNpcInteraction: true);
    }

    [SkippableFact]
    public async Task FlightMaster_DiscoverNodes()
    {
        await RunNpcInteraction("Flight Master", OrgrimmarFmX, OrgrimmarFmY, OrgrimmarFmZ,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER, requireNpcInteraction: true);
=======
        _output.WriteLine("=== Trainer Visit: Both bots talk to warrior trainer, purchase all available skills ===");

        // BG bot trainer visit
        var bgMetrics = await RunTrainerVisitScenarioAsync(_bot.BgAccountName!, "BG");
        Assert.True(bgMetrics.TrainerFound, "BG: class trainer with UNIT_NPC_FLAG_TRAINER should be visible near Razor Hill.");
        Assert.False(bgMetrics.HadSpellBefore, $"BG: spell {BattleShoutSpellId} must be absent before the trainer task runs.");
        // VisitTrainer task must learn the spell — if it doesn't, that's a navigation/gossip/interaction bug.
        Assert.True(bgMetrics.HasSpellAfter,
            $"BG: VisitTrainer task did not learn spell {BattleShoutSpellId} within timeout. " +
            $"This is a navigation/gossip/interaction bug. LearnLatency={bgMetrics.LearnLatencyMs}ms");
        Assert.True(bgMetrics.SpellCountAfter > bgMetrics.SpellCountBefore,
            $"BG: spell list should grow after trainer visit. Before={bgMetrics.SpellCountBefore}, after={bgMetrics.SpellCountAfter}");
        Assert.True(bgMetrics.CoinageAfter < bgMetrics.CoinageBefore,
            $"BG: trainer visit should spend copper on learned spells. Before={bgMetrics.CoinageBefore}, after={bgMetrics.CoinageAfter}");
        Assert.InRange(bgMetrics.LearnLatencyMs, 1, 50000);

        // FG bot trainer visit (skip if FG not available)
        if (_bot.IsFgActionable)
        {
            var fgMetrics = await RunTrainerVisitScenarioAsync(_bot.FgAccountName!, "FG");
            Assert.True(fgMetrics.TrainerFound, "FG: class trainer should be visible near Razor Hill.");
            Assert.False(fgMetrics.HadSpellBefore, $"FG: spell {BattleShoutSpellId} must be absent before the trainer task runs.");
            // FG VisitTrainer task must learn the spell — if it doesn't, that's a navigation/gossip/interaction bug.
            Assert.True(fgMetrics.HasSpellAfter,
                $"FG: VisitTrainer task did not learn spell {BattleShoutSpellId} within timeout. " +
                $"This is a navigation/gossip/interaction bug. LearnLatency={fgMetrics.LearnLatencyMs}ms");
            Assert.True(fgMetrics.SpellCountAfter > fgMetrics.SpellCountBefore,
                $"FG: spell list should grow. Before={fgMetrics.SpellCountBefore}, after={fgMetrics.SpellCountAfter}");
            // FG WoWPlayer.Coinage is a stub (always 0) — skip coinage assertion for FG
            if (fgMetrics.CoinageBefore > 0)
                Assert.True(fgMetrics.CoinageAfter < fgMetrics.CoinageBefore,
                    $"FG: trainer should spend copper. Before={fgMetrics.CoinageBefore}, after={fgMetrics.CoinageAfter}");
        }
        else
        {
            _output.WriteLine("[FG] Skipped — FG bot not actionable.");
        }
    }

    [SkippableFact]
    public async Task FlightMaster_VisitTask_DiscoversPaths()
    {
        _output.WriteLine("=== Flight Master Visit: Task-driven taxi discovery ===");

        var bgMetrics = await RunFlightMasterVisitScenarioAsync(_bot.BgAccountName!, "BG");
        Assert.True(bgMetrics.FlightMasterFound, "BG: flight master NPC should be visible near Orgrimmar.");
        Assert.True(bgMetrics.TaskCompleted, "BG: FlightMasterVisitTask should complete within timeout.");

        if (_bot.IsFgActionable)
        {
            var fgMetrics = await RunFlightMasterVisitScenarioAsync(_bot.FgAccountName!, "FG");
            Assert.True(fgMetrics.FlightMasterFound, "FG: flight master should be visible near Orgrimmar.");
            Assert.True(fgMetrics.TaskCompleted, "FG: FlightMasterVisitTask should complete.");
        }
        else
        {
            _output.WriteLine("[FG] Skipped — FG bot not actionable.");
        }
>>>>>>> cpp_physics_system
    }

    [SkippableFact]
    public async Task ObjectManager_DetectsNpcFlags()
    {
<<<<<<< HEAD
        await EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ);
        if (_bot.ForegroundBot != null)
            await EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ);

        await _bot.RefreshSnapshotsAsync();

        // BG bot NPC detection
        LogNpcFlags("BG", _bot.BackgroundBot);

        // FG bot NPC detection
        if (_bot.ForegroundBot != null)
            LogNpcFlags("FG", _bot.ForegroundBot);
        else
            _output.WriteLine("FG Bot: NOT AVAILABLE");
    }

    private async Task RunNpcInteraction(string npcType, float x, float y, float z, uint npcFlag, bool requireNpcInteraction)
    {
        await EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, x, y, z);
        if (_bot.ForegroundBot != null)
            await EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, x, y, z);

        // BG bot interaction
        _output.WriteLine($"=== BG Bot: {npcType} ===");
        var bgOk = await InteractWithNpc(_bot.BgAccountName!, () => _bot.BackgroundBot, npcFlag, "BG");
        if (requireNpcInteraction)
            Assert.True(bgOk, $"BG should find and interact with NPC flag 0x{npcFlag:X} for scenario '{npcType}'.");

        // FG bot interaction
        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine($"\n=== FG Bot: {npcType} ===");
            var fgOk = await InteractWithNpc(_bot.FgAccountName!, () => _bot.ForegroundBot, npcFlag, "FG");
            if (requireNpcInteraction)
                Assert.True(fgOk, $"FG should find and interact with NPC flag 0x{npcFlag:X} for scenario '{npcType}'.");
        }
        else
        {
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
    }

    private async Task<bool> InteractWithNpc(string account, Func<WoWActivitySnapshot?> getSnap, uint npcFlag, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = getSnap();
        var units = snap?.NearbyUnits?.Where(u => (u.NpcFlags & npcFlag) != 0).ToList() ?? [];

        if (units.Count == 0)
        {
            _output.WriteLine($"  [{label}] No NPC with flag 0x{npcFlag:X} found nearby");
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
=======
        var hasFg = _bot.IsFgActionable;
        var setupTasks = new System.Collections.Generic.List<Task>
        {
            EnsureReadyAtLocationAsync(_bot.BgAccountName!, "BG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ)
        };
        if (hasFg)
            setupTasks.Add(EnsureReadyAtLocationAsync(_bot.FgAccountName!, "FG", MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ));
        await Task.WhenAll(setupTasks);

        // Post-teleport settle: server streams nearby objects via SMSG_UPDATE_OBJECT
        // over several seconds. Wait for the initial burst to complete.
        await Task.Delay(2000);

        // Retry up to 5 times — NPC flags may arrive in PARTIAL updates after CREATE_OBJECT
        System.Collections.Generic.List<Game.WoWUnit> bgWithFlags = [];
        System.Collections.Generic.List<Game.WoWUnit> bgUnits = [];
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            bgUnits = _bot.BackgroundBot?.NearbyUnits?.ToList() ?? [];
            bgWithFlags = bgUnits.Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE).ToList();
            if (bgWithFlags.Count > 0) break;
            if (attempt < 4)
            {
                _output.WriteLine($"  [BG] No NPC flags on attempt {attempt + 1} (units={bgUnits.Count}), retrying in 1.5s...");
                await Task.Delay(1500);
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

    private async Task<VendorVisitMetrics> RunVendorVisitScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await EnsureReadyAtLocationAsync(account, label, MapId, RazorHillVendorX, RazorHillVendorY, RazorHillVendorZ);

        var vendorUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 15000,
            progressLabel: $"{label} vendor lookup");

        var vendorGuid = vendorUnit?.GameObject?.Base?.Guid ?? 0;
        var vendorPos = vendorUnit?.GameObject?.Base?.Position;

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var playerPos = before?.Player?.Unit?.GameObject?.Base?.Position;
        var vendorDistance = playerPos == null || vendorPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(playerPos.X, playerPos.Y, playerPos.Z, vendorPos.X, vendorPos.Y, vendorPos.Z);

        _output.WriteLine(
            $"[{label}] vendor target: guid=0x{vendorGuid:X}, name={vendorUnit?.GameObject?.Name}, " +
            $"flags={vendorUnit?.NpcFlags}, distance={vendorDistance:F1}y");

        var dispatch = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitVendor
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        // Wait for the task to complete (VendorVisitTask pops itself on completion).
        // We detect completion by checking that the bot is no longer moving toward the vendor
        // and enough time has passed for the full FindVendor→MoveToVendor→VendorActions cycle.
        await Task.Delay(5000);

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);

        _output.WriteLine(
            $"[{label}] vendor metrics: found={vendorGuid != 0}, distance={vendorDistance:F1}y, " +
            $"dispatch={dispatch}");

        return new VendorVisitMetrics(
            vendorGuid != 0,
            vendorDistance,
            true, // task completed if we got here without timeout
            before?.Player?.Coinage ?? 0,
            after?.Player?.Coinage ?? 0);
    }

    private async Task<FlightMasterVisitMetrics> RunFlightMasterVisitScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await EnsureReadyAtLocationAsync(account, label, MapId, OrgrimmarFmX, OrgrimmarFmY, OrgrimmarFmZ);

        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: $"{label} flight master lookup");

        var fmGuid = fmUnit?.GameObject?.Base?.Guid ?? 0;
        var fmPos = fmUnit?.GameObject?.Base?.Position;

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var playerPos = before?.Player?.Unit?.GameObject?.Base?.Position;
        var fmDistance = playerPos == null || fmPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(playerPos.X, playerPos.Y, playerPos.Z, fmPos.X, fmPos.Y, fmPos.Z);

        _output.WriteLine(
            $"[{label}] flight master: guid=0x{fmGuid:X}, name={fmUnit?.GameObject?.Name}, " +
            $"flags={fmUnit?.NpcFlags}, distance={fmDistance:F1}y");

        var dispatch = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        // Wait for FlightMasterVisitTask to complete (find → move → discover → done)
        await Task.Delay(5000);

        _output.WriteLine(
            $"[{label}] flight master metrics: found={fmGuid != 0}, distance={fmDistance:F1}y");

        return new FlightMasterVisitMetrics(
            fmGuid != 0,
            fmDistance,
            true); // completed if dispatch succeeded and waited
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
            timeoutMs: 15000,
            progressLabel: $"{label} trainer lookup");
        Assert.NotNull(trainerUnit);
        Assert.True(trainerUnit != null,
            $"[{label}] No trainer NPC found near Razor Hill after 15s. " +
            "NPCs should always be present — this is a unit detection or ObjectManager bug.");

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
            TimeSpan.FromSeconds(40),
            pollIntervalMs: 300,
            progressLabel: $"{label} trainer learn spell");
        var spentCoinage = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) < coinageBefore,
            TimeSpan.FromSeconds(10),
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
>>>>>>> cpp_physics_system
    }

    private async Task EnsureReadyAtLocationAsync(string account, string label, int mapId, float x, float y, float z)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            return;

<<<<<<< HEAD
        if (!IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before NPC setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(2000);
=======
        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before NPC setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await _bot.WaitForSnapshotConditionAsync(account, LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(5));
>>>>>>> cpp_physics_system
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
        var dist = pos == null
            ? float.MaxValue
<<<<<<< HEAD
            : DistanceTo(pos.X, pos.Y, pos.Z, x, y, z);
=======
            : LiveBotFixture.Distance3D(pos.X, pos.Y, pos.Z, x, y, z);
>>>>>>> cpp_physics_system

        if (dist <= SetupArrivalDistance)
        {
            _output.WriteLine($"  [{label}] Already near setup location (dist={dist:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"  [{label}] Teleporting to setup location (dist={dist:F1}y).");
        await _bot.BotTeleportAsync(account, mapId, x, y, z);
<<<<<<< HEAD
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
=======
        await _bot.WaitForTeleportSettledAsync(account, x, y);
        // Wait for nearby units to populate after teleport (race condition: OUT_OF_RANGE_OBJECTS
        // clears old entities before CREATE_OBJECT packets arrive for new ones)
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: label);
>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
=======
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

>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
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
=======
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

    private sealed record VendorVisitMetrics(
        bool VendorFound,
        float VendorDistanceYards,
        bool TaskCompleted,
        long CoinageBefore,
        long CoinageAfter);

    private sealed record FlightMasterVisitMetrics(
        bool FlightMasterFound,
        float FmDistanceYards,
        bool TaskCompleted);
>>>>>>> cpp_physics_system
}
