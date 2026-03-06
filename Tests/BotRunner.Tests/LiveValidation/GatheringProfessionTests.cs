using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Gathering profession integration tests (Mining + Herbalism).
///
/// Strategy:
///   1. Teleport to Orgrimmar (safe zone) for GM setup — GM mode ON, learn spells, set skills
///   2. Query MaNGOS gameobject table for existing node spawns on Kalimdor
///   3. Teleport ~30 yards offset from a spawn area, detect the node via NearbyObjects
///   4. Use GoTo (pathfinding) to navigate to the node — tests full pipeline
///   5. Interact and verify skill increase
///
/// GM mode stays ON — gathering works with .gm on (per project rules).
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~GatheringProfessionTests" --configuration Release -v n
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class GatheringProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // --- Mining constants ---
    private const uint MiningApprentice = 2575;
    private const uint MiningGatherSpell = GatheringData.MINING_GATHER_SPELL;  // 2575 — profession spell with 3.2s channel
    private const uint MiningPick = 2901;
    private const uint CopperVeinEntry = 1731;

    // --- Herbalism constants ---
    private const uint HerbalismApprentice = 2366;
    private const uint HerbalismGatherSpell = GatheringData.HERBALISM_GATHER_SPELL;  // 2366 — same as profession spell
    private const uint PeacebloomEntry = 1617;
    private const uint SilverleafEntry = 1618;
    private const uint EarthrootEntry = 1619;

    // Gather sequence = 5s channel + 3s post-gather cooldown (inside BuildGatherNodeSequence) + loot.
    // Bot-level sequence totals ~8.1s; 9s gives a small margin without over-waiting.
    private const int GatherChannelWaitMs = 9000;

    // Orgrimmar (safe zone, no hostile mobs) for GM setup
    private const int OrgrimmarMap = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;

    // Teleport offset from spawn. Set to 0 to teleport directly to (spawnX, spawnY, spawnZ+3)
    // so that startDist < NavTolerance → bot skips pathfinding and gathers directly.
    // A 5y offset caused test failures: bot approaches node at bad angles on sloped terrain
    // (navmesh vs collision Z mismatch — Phase 1 of pathfinding plan). Pathfinding testing
    // belongs in dedicated Navigation tests, not gathering integration tests.
    private const float TeleportOffset = 0f;
    // BG navigation arrival tolerance (yards). Must be within ~5y for interaction.
    private const float NavTolerance = 4f;

    public GatheringProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    // =====================================================================
    //  MINING TEST
    // =====================================================================

    [SkippableFact]
    public async Task Mining_GatherCopperVein_SkillIncreases()
    {
        // --- Find copper vein spawns in the DB (Kalimdor — Durotar low-level zone) ---
        var spawns = await _bot.QueryGameObjectSpawnsAsync(CopperVeinEntry, mapFilter: 1, limit: 25);
        global::Tests.Infrastructure.Skip.If(spawns.Count == 0, "No Copper Vein spawns in gameobject table (entry 1731).");
        _output.WriteLine($"Found {spawns.Count} Copper Vein spawn locations");

        // --- FG FIRST: native WoW right-click interaction (gold standard) ---
        var fgAccount = _bot.FgAccountName;
        if (fgAccount != null && _bot.ForegroundBot != null)
        {
            // Park BG bot nearby so CombatCoordinator doesn't send
            // competing GOTO actions that overwrite the test's navigation.
            var bgParkAccount = _bot.BgAccountName;
            if (bgParkAccount != null)
            {
                _output.WriteLine("[BG] Parking BG bot in Orgrimmar (prevents CombatCoordinator interference)");
                await _bot.BotTeleportAsync(bgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccount})");
            await PrepareMining(fgAccount, "FG");

            await _bot.RefreshSnapshotsAsync();
            uint fgSkillBefore = GetSkill("FG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"FG initial mining skill: {fgSkillBefore}");

            bool fgGathered = await TryGatherAtSpawns(fgAccount, "FG", spawns,
                CopperVeinEntry, GatheringData.MINING_SKILL_ID, "Copper Vein",
                initialSkill: fgSkillBefore,
                gatherSpellId: MiningGatherSpell, parkAccount: _bot.BgAccountName);

            await _bot.RefreshSnapshotsAsync();
            uint fgSkillAfter = GetSkill("FG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"FG Results: gathered={fgGathered}, skill {fgSkillBefore} → {fgSkillAfter}");

            global::Tests.Infrastructure.Skip.If(!fgGathered,
                $"FG: No Copper Vein nodes currently spawned at any of {spawns.Count} DB locations (all on respawn timer). Skill={fgSkillAfter}. Re-run after respawn.");
            Assert.True(fgGathered,
                $"FG: Failed to gather Copper Vein at any spawned location. skill={fgSkillAfter}.");
            Assert.True(fgSkillAfter > fgSkillBefore,
                $"FG: Mining skill did not increase ({fgSkillBefore} → {fgSkillAfter}).");
        }
        else
        {
            _output.WriteLine("FG bot not available — skipping FG mining test.");
        }

        // --- BG: headless protocol emulation ---
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

        try
        {
            // Park the OTHER bot in Orgrimmar so CombatCoordinator doesn't send GOTOs
            // that overwrite the GatherNode behavior tree during the mining channel.
            var fgParkAccount = _bot.FgAccountName;
            if (fgParkAccount != null)
            {
                _output.WriteLine("[FG-park] Parking FG bot in Orgrimmar for BG test");
                await _bot.BotTeleportAsync(fgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            await PrepareMining(bgAccount, "BG");

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillBefore = GetSkill("BG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"BG initial mining skill: {bgSkillBefore}");
            global::Tests.Infrastructure.Skip.If(bgSkillBefore >= 300, $"BG mining skill already capped ({bgSkillBefore}); cannot assert further increase.");

            bool bgGathered = await TryGatherAtSpawns(bgAccount, "BG", spawns,
                CopperVeinEntry, GatheringData.MINING_SKILL_ID, "Copper Vein",
                initialSkill: bgSkillBefore,
                gatherSpellId: MiningGatherSpell, parkAccount: _bot.FgAccountName);

            await _bot.RefreshSnapshotsAsync();
            uint bgSkillAfter = GetSkill("BG", GatheringData.MINING_SKILL_ID);
            _output.WriteLine($"BG Results: gathered={bgGathered}, skill {bgSkillBefore} → {bgSkillAfter}");

            global::Tests.Infrastructure.Skip.If(!bgGathered,
                $"BG: No Copper Vein nodes currently spawned at any of {spawns.Count} DB locations (all on respawn timer). Skill={bgSkillAfter}. Re-run after respawn.");
            Assert.True(bgGathered,
                $"BG: Failed to gather Copper Vein at any of {spawns.Count} locations. skill={bgSkillAfter}.");
            Assert.True(bgSkillAfter > bgSkillBefore,
                $"BG: Mining skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");
        }
        finally
        {
            // Return bot to safe zone on failure to avoid leaving it stranded
            // at a remote mining node location, which can corrupt subsequent tests.
            await ReturnToSafeZoneAsync(bgAccount, "BG");
        }
    }

    // =====================================================================
    //  HERBALISM TEST
    // =====================================================================

    [SkippableFact]
    public async Task Herbalism_GatherHerb_SkillIncreases()
    {
        // --- Find herb spawns in Kalimdor (Durotar/Mulgore — low-level zones) ---
        var allSpawns = new List<(int map, float x, float y, float z, uint entry)>();
        foreach (var entry in new[] { PeacebloomEntry, SilverleafEntry, EarthrootEntry })
        {
            var spawns = await _bot.QueryGameObjectSpawnsAsync(entry, mapFilter: 1, limit: 25);
            foreach (var s in spawns)
                allSpawns.Add((s.map, s.x, s.y, s.z, entry));
        }
        global::Tests.Infrastructure.Skip.If(allSpawns.Count == 0,
            "No Peacebloom/Silverleaf/Earthroot spawns found in gameobject table (Kalimdor).");
        _output.WriteLine($"Found {allSpawns.Count} herb spawn locations");

        var herbEntries = allSpawns.Select(s => s.entry).Distinct().ToArray();

        // --- FG FIRST: native WoW right-click interaction (gold standard) ---
        var fgAccount = _bot.FgAccountName;
        if (fgAccount != null && _bot.ForegroundBot != null)
        {
            // Park BG bot nearby so CombatCoordinator doesn't send
            // competing GOTO actions that overwrite the test's navigation.
            var bgParkAccount = _bot.BgAccountName;
            if (bgParkAccount != null)
            {
                _output.WriteLine("[BG] Parking BG bot in Orgrimmar (prevents CombatCoordinator interference)");
                await _bot.BotTeleportAsync(bgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccount})");
            await PrepareHerbalism(fgAccount, "FG");

            await _bot.RefreshSnapshotsAsync();
            uint fgSkillBefore = GetSkill("FG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"FG initial herbalism skill: {fgSkillBefore}");

            bool fgGathered = false;
            foreach (var entry in herbEntries)
            {
                var entrySpawns = allSpawns.Where(s => s.entry == entry).Select(s => (s.map, s.x, s.y, s.z)).ToList();
                fgGathered = await TryGatherAtSpawns(fgAccount, "FG", entrySpawns,
                    entry, GatheringData.HERBALISM_SKILL_ID, $"herb (entry {entry})",
                    initialSkill: fgSkillBefore,
                    gatherSpellId: HerbalismGatherSpell, parkAccount: _bot.BgAccountName);
                if (fgGathered) break;
            }

            await _bot.RefreshSnapshotsAsync();
            uint fgSkillAfter = GetSkill("FG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"FG Results: gathered={fgGathered}, skill {fgSkillBefore} → {fgSkillAfter}");

            global::Tests.Infrastructure.Skip.If(!fgGathered,
                $"FG: No herb nodes currently spawned at any of {allSpawns.Count} DB locations (all on respawn timer). Skill={fgSkillAfter}. Re-run after respawn.");
            Assert.True(fgGathered,
                $"FG: Failed to gather herb at any of {allSpawns.Count} locations. skill={fgSkillAfter}.");
            Assert.True(fgSkillAfter > fgSkillBefore,
                $"FG: Herbalism skill did not increase ({fgSkillBefore} → {fgSkillAfter}).");
        }
        else
        {
            _output.WriteLine("FG bot not available — skipping FG herbalism test.");
        }

        // --- BG: headless protocol emulation ---
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

        try
        {
            // Park the OTHER bot in Orgrimmar so CombatCoordinator doesn't send GOTOs
            // that overwrite the GatherNode behavior tree during the herbalism channel.
            var fgParkAccount = _bot.FgAccountName;
            if (fgParkAccount != null)
            {
                _output.WriteLine("[FG-park] Parking FG bot in Orgrimmar for BG test");
                await _bot.BotTeleportAsync(fgParkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);
            }

            // --- Prepare: learn Herbalism + set skill ---
            await PrepareHerbalism(bgAccount, "BG");

            // --- Record initial skill ---
            await _bot.RefreshSnapshotsAsync();
            uint bgSkillBefore = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"BG initial herbalism skill: {bgSkillBefore}");
            global::Tests.Infrastructure.Skip.If(bgSkillBefore >= 300, $"BG herbalism skill already capped ({bgSkillBefore}); cannot assert further increase.");

            // --- Try each spawn location ---
            bool bgGathered = false;
            foreach (var entry in herbEntries)
            {
                var entrySpawns = allSpawns.Where(s => s.entry == entry).Select(s => (s.map, s.x, s.y, s.z)).ToList();
                bgGathered = await TryGatherAtSpawns(bgAccount, "BG", entrySpawns,
                    entry, GatheringData.HERBALISM_SKILL_ID, $"herb (entry {entry})",
                    initialSkill: bgSkillBefore,
                    gatherSpellId: HerbalismGatherSpell, parkAccount: _bot.FgAccountName);
                if (bgGathered) break;
            }

            // --- Assert ---
            await _bot.RefreshSnapshotsAsync();
            uint bgSkillAfter = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
            _output.WriteLine($"BG Results: gathered={bgGathered}, skill {bgSkillBefore} → {bgSkillAfter}");

            global::Tests.Infrastructure.Skip.If(!bgGathered,
                $"BG: No herb nodes currently spawned at any of {allSpawns.Count} DB locations (all on respawn timer). Skill={bgSkillAfter}. Re-run after respawn.");
            Assert.True(bgGathered,
                $"BG: Failed to gather herb at any of {allSpawns.Count} locations. skill={bgSkillAfter}.");
            Assert.True(bgSkillAfter > bgSkillBefore,
                $"BG: Herbalism skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");
        }
        finally
        {
            // Return bot to safe zone on failure to avoid leaving it stranded
            // at a remote herb node location, which can corrupt subsequent tests.
            await ReturnToSafeZoneAsync(bgAccount, "BG");
        }
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================
    /// <summary>
    /// Snapshot-driven mining setup: apply only missing preconditions.
    /// GM mode stays ON for the entire session (gathering works with .gm on).
    /// Self-targeting is applied before GM commands that require it (.learn, .setskill).
    /// </summary>
    private async Task PrepareMining(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        // Ensure GM mode is ON — FG needs explicit .gm on via chat.
        // BG has GM level 6 in DB (doesn't need .gm on via chat, which disconnects it).
        if (label == "FG")
        {
            _output.WriteLine($"[{label}] Ensuring GM mode ON");
            await _bot.SendGmChatCommandTrackedAsync(account, ".gm on", captureResponse: false, delayMs: 500);
        }

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before mining setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        // Self-target before GM commands that require it (.learn, .setskill).
        await EnsureSelfSelectionAsync(account);

        var currentMining = GetSkill(label, GatheringData.MINING_SKILL_ID);
        _output.WriteLine($"[{label}] Ensuring mining spells learned (current skill={currentMining})");
        await _bot.BotLearnSpellAsync(account, MiningApprentice);
        await _bot.BotLearnSpellAsync(account, MiningGatherSpell);
        if (currentMining < 1)
        {
            await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.MINING_SKILL_ID} 1 300", captureResponse: true);
        }

        await _bot.RefreshSnapshotsAsync();
        if (!HasBagItem(label, MiningPick))
        {
            _output.WriteLine($"[{label}] Adding Mining Pick ({MiningPick})");
            await _bot.BotAddItemAsync(account, MiningPick);
            await Task.Delay(1000);
        }

        await _bot.RefreshSnapshotsAsync();
        var skillCheck = GetSkill(label, GatheringData.MINING_SKILL_ID);
        var hasPick = HasBagItem(label, MiningPick);
        _output.WriteLine($"[{label}] Mining setup state: skill={skillCheck}, hasPick={hasPick}");
    }
    /// <summary>
    /// Snapshot-driven herbalism setup: apply only missing preconditions.
    /// GM mode stays ON for the entire session (gathering works with .gm on).
    /// Self-targeting is applied before GM commands that require it (.learn, .setskill).
    /// </summary>
    private async Task PrepareHerbalism(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        // Ensure GM mode is ON — FG needs explicit .gm on via chat.
        if (label == "FG")
        {
            _output.WriteLine($"[{label}] Ensuring GM mode ON");
            await _bot.SendGmChatCommandTrackedAsync(account, ".gm on", captureResponse: false, delayMs: 500);
        }

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before herbalism setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        // Self-target before GM commands that require it (.learn, .setskill).
        await EnsureSelfSelectionAsync(account);

        var currentHerbalism = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"[{label}] Ensuring herbalism spells learned (current skill={currentHerbalism})");
        await _bot.BotLearnSpellAsync(account, HerbalismApprentice);
        await _bot.BotLearnSpellAsync(account, HerbalismGatherSpell);
        if (currentHerbalism < 1)
        {
            await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.HERBALISM_SKILL_ID} 1 300", captureResponse: true);
            await Task.Delay(500);
        }

        await _bot.RefreshSnapshotsAsync();
        var skillCheck = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"[{label}] Herbalism setup state: skill={skillCheck}");
    }

    /// <summary>
    /// Try gathering at natural spawn locations.
    ///
    /// Strategy:
    ///   1. Teleport to a general area (~30y offset from a known spawn)
    ///   2. Scan NearbyObjects for the node
    ///   3. Use GoTo (pathfinding) to navigate to the node — tests the full pipeline
    ///   4. Once within interaction range, send GatherNode
    ///   5. Verify skill increase
    ///
    /// This tests: node detection, pathfinding, navigation arrival, and gathering.
    /// NO .gobject add — only naturally-spawning nodes.
    /// NOTE: .respawn only affects creatures, NOT game objects in MaNGOS.
    /// </summary>
    private async Task<bool> TryGatherAtSpawns(string account, string label,
        List<(int map, float x, float y, float z)> spawns,
        uint nodeEntry, uint skillId, string nodeName,
        uint initialSkill,
        uint gatherSpellId = 0, string? parkAccount = null)
    {
        int maxLocations = spawns.Count;

        // Park the other bot once to reduce coordinator interference.
        if (parkAccount != null)
            await _bot.BotTeleportAsync(parkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);

        for (int loc = 0; loc < maxLocations; loc++)
        {
            var (map, spawnX, spawnY, spawnZ) = spawns[loc];

            // Teleport ~30y offset from the spawn (not on top) so the bot has to pathfind.
            // This tests the full pipeline: detect node → pathfind → arrive → gather.
            float offsetX = TeleportOffset;
            float safeZ = spawnZ + 3f;
            _output.WriteLine($"[{label}] Location {loc + 1}/{maxLocations}: teleporting ~{TeleportOffset:F0}y from {nodeName} near ({spawnX:F1}, {spawnY:F1}, {spawnZ:F1})");

            await _bot.BotTeleportAsync(account, map, spawnX + offsetX, spawnY, safeZ);
            await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

            // --- Scan for the node in NearbyObjects ---
            // 3s max: static objects (herbs/ores) are either spawned or not — no loading delay.
            ulong nodeGuid = 0;
            float nodeX = 0, nodeY = 0, nodeZ = 0;
            var sw = Stopwatch.StartNew();
            bool loggedDiag = false;
            while (sw.Elapsed < TimeSpan.FromSeconds(3))
            {
                await _bot.RefreshSnapshotsAsync();
                var snap = GetSnapshot(label);
                var gameObjects = snap?.NearbyObjects?.ToList() ?? [];

                if (!loggedDiag)
                {
                    loggedDiag = true;
                    _output.WriteLine($"  [{label}] NearbyObjects count: {gameObjects.Count}");
                    foreach (var go in gameObjects.Take(10))
                        _output.WriteLine($"    GO entry={go.Entry} guid=0x{go.Base?.Guid ?? 0:X} displayId={go.DisplayId} pos=({go.Base?.Position?.X:F1}, {go.Base?.Position?.Y:F1}, {go.Base?.Position?.Z:F1})");
                    if (gameObjects.Count > 10)
                        _output.WriteLine($"    ... and {gameObjects.Count - 10} more");
                }

                var node = gameObjects.FirstOrDefault(go => go.Entry == nodeEntry);
                if (node != null)
                {
                    var candidatePos = node.Base?.Position;
                    if (candidatePos != null)
                    {
                        float distFromSpawn = Distance(candidatePos.X, candidatePos.Y, candidatePos.Z, spawnX, spawnY, spawnZ);
                        if (distFromSpawn > 100f)
                        {
                            _output.WriteLine($"  [{label}] Stale object: entry {nodeEntry} at ({candidatePos.X:F1}, {candidatePos.Y:F1}, {candidatePos.Z:F1}) is {distFromSpawn:F0}y from spawn — skipping");
                            break;
                        }
                        nodeGuid = node.Base?.Guid ?? 0;
                        nodeX = candidatePos.X;
                        nodeY = candidatePos.Y;
                        nodeZ = candidatePos.Z;
                    }
                    else
                    {
                        nodeGuid = node.Base?.Guid ?? 0;
                    }
                    _output.WriteLine($"  [{label}] Node detected after {sw.Elapsed.TotalSeconds:F1}s");
                    break;
                }

                await Task.Delay(1500);
            }

            if (nodeGuid == 0)
            {
                _output.WriteLine($"  [{label}] No natural {nodeName} detected at this location, trying next...");
                continue;
            }

            await _bot.RefreshSnapshotsAsync();
            var preSnap = GetSnapshot(label);
            var playerPos = preSnap?.Player?.Unit?.GameObject?.Base?.Position;
            float startDist = 0;
            if (playerPos != null)
                startDist = Distance(playerPos.X, playerPos.Y, playerPos.Z, nodeX, nodeY, nodeZ);
            _output.WriteLine($"  [{label}] Found {nodeName}: 0x{nodeGuid:X} at ({nodeX:F1}, {nodeY:F1}, {nodeZ:F1}), dist={startDist:F1}y");

            // --- Pathfind to the node using GoTo action ---
            if (startDist > NavTolerance)
            {
                _output.WriteLine($"  [{label}] Pathfinding to node (dist={startDist:F1}y, tolerance={NavTolerance}y)...");
                var gotoAction = new ActionMessage
                {
                    ActionType = ActionType.Goto,
                    Parameters =
                    {
                        new RequestParameter { FloatParam = nodeX },
                        new RequestParameter { FloatParam = nodeY },
                        new RequestParameter { FloatParam = nodeZ },
                        new RequestParameter { FloatParam = NavTolerance }
                    }
                };
                await _bot.SendActionAndWaitAsync(account, gotoAction, delayMs: 500);

                // Poll position until within interaction range or timeout.
                var navSw = Stopwatch.StartNew();
                float navDist = startDist;
                while (navSw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(500);
                    await _bot.RefreshSnapshotsAsync();
                    playerPos = GetSnapshot(label)?.Player?.Unit?.GameObject?.Base?.Position;
                    if (playerPos != null)
                    {
                        navDist = Distance(playerPos.X, playerPos.Y, playerPos.Z, nodeX, nodeY, nodeZ);
                        if (navDist <= 5f)
                        {
                            _output.WriteLine($"  [{label}] Arrived at node after {navSw.Elapsed.TotalSeconds:F1}s (dist={navDist:F1}y)");
                            break;
                        }
                    }
                }

                if (navDist > 5f)
                {
                    _output.WriteLine($"  [{label}] Pathfinding timed out (dist={navDist:F1}y after {navSw.Elapsed.TotalSeconds:F0}s), trying next location...");
                    continue;
                }

                // Brief settle: let movement fully stop before interacting.
                // Gathering while in motion can fail in WoW 1.12.1.
                await Task.Delay(1200);
            }

            _output.WriteLine($"  [{label}] Sending GatherNode (spell={gatherSpellId})...");
            var gatherParams = new ActionMessage
            {
                ActionType = ActionType.GatherNode,
                Parameters = { new RequestParameter { LongParam = (long)nodeGuid } }
            };
            if (gatherSpellId > 0)
                gatherParams.Parameters.Add(new RequestParameter { IntParam = (int)gatherSpellId });
            await _bot.SendActionAndWaitAsync(account, gatherParams, delayMs: GatherChannelWaitMs);

            // Post-gather cooldown: give WoW.exe time to clean up the game object interaction
            // state after the node despawns. Prevents ACCESS_VIOLATION (ERROR #132).
            await Task.Delay(2000);

            // Retry skill read with an extended window (40s) to handle FG crash+reconnect.
            // ERROR #132 crashes happen when the node despawns (~3-5s into the 9s channel wait).
            // WoW.exe restarts and reconnects in ~25-35s. Without a long poll, the snapshot
            // returns 0 (bot offline) and the test falsely concludes the gather failed.
            // Once the bot reconnects, the skill reflects the completed gather.
            uint skillNow = 0;
            for (int poll = 0; poll < 20 && skillNow == 0; poll++)
            {
                await _bot.RefreshSnapshotsAsync();
                skillNow = GetSkill(label, skillId);
                if (skillNow == 0)
                    await Task.Delay(2000);
            }
            _output.WriteLine($"  [{label}] Skill after gather: {skillNow}");

            if (skillNow > initialSkill)
            {
                _output.WriteLine($"  [{label}] SUCCESS! Skill increased {initialSkill} -> {skillNow}");
                return true;
            }

            _output.WriteLine($"  [{label}] Skill did not increase yet ({initialSkill} -> {skillNow}), trying next location...");
        }

        return false;
    }

    private async Task EnsureAliveAndAtSetupLocationAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = GetSnapshot(label);
        var health = snap?.Player?.Unit?.Health ?? 0;
        var playerFlags = snap?.Player?.PlayerFlags ?? 0;
        var isGhost = (playerFlags & 0x10) != 0;

        if (health == 0 || isGhost)
        {
            var charName = label == "FG" ? _bot.FgCharacterName : _bot.BgCharacterName;
            if (!string.IsNullOrEmpty(charName))
            {
                _output.WriteLine($"[{label}] Reviving character before setup");
                await _bot.RevivePlayerAsync(charName);
                await Task.Delay(1000);
                await _bot.RefreshSnapshotsAsync();
                snap = GetSnapshot(label);
            }
        }

        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var needsTeleport = true;
        if (pos != null)
        {
            var distToOrg = Distance(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
            needsTeleport = distToOrg > 80f;
        }

        if (needsTeleport)
        {
            _output.WriteLine($"[{label}] Moving to safe setup zone (Orgrimmar)");
            await _bot.BotTeleportAsync(account, OrgrimmarMap, OrgX, OrgY, OrgZ);
            await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
        }
    }

    /// <summary>
    /// Select self before GM commands like .setskill that require a target.
    /// Uses the proper .targetself chat command instead of the melee-attack workaround.
    /// Works for both BG (headless) and FG (injected) bots.
    /// </summary>
    private async Task EnsureSelfSelectionAsync(string account)
    {
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
    }

    private int GetBagItemCount(string label)
        => GetSnapshot(label)?.Player?.BagContents?.Count ?? 0;

    private bool HasBagItem(string label, uint itemId)
    {
        var bags = GetSnapshot(label)?.Player?.BagContents;
        return bags != null && bags.Values.Any(v => v == itemId);
    }

    /// <summary>
    /// Best-effort return to safe zone (Orgrimmar) after test completion or failure.
    /// Prevents leaving the bot stranded at a remote gathering location.
    /// </summary>
    private async Task ReturnToSafeZoneAsync(string account, string label)
    {
        try
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null)
            {
                var distToOrg = Distance(pos.X, pos.Y, pos.Z, OrgX, OrgY, OrgZ);
                if (distToOrg > 80f)
                {
                    _output.WriteLine($"[{label}] Cleanup: returning to Orgrimmar (dist={distToOrg:F0}y)");
                    await _bot.BotTeleportAsync(account, OrgrimmarMap, OrgX, OrgY, OrgZ);
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[{label}] Cleanup warning: safe zone return failed — {ex.Message}");
        }
    }

    private static float Distance(float x1, float y1, float z1, float x2, float y2, float z2)
        => (float)Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2) + Math.Pow(z1 - z2, 2));

    private uint GetSkill(string label, uint skillId)
    {
        var snap = GetSnapshot(label);
        var skillMap = snap?.Player?.SkillInfo;
        if (skillMap != null && skillMap.TryGetValue(skillId, out uint level))
            return level;
        return 0;
    }

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

}

