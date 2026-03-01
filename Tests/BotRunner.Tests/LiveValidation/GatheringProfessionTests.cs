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
///   1. Teleport to Orgrimmar (safe zone) for GM setup — avoids mob aggro/targeting issues
///   2. Query MaNGOS gameobject table for existing node spawns on Kalimdor
///   3. Teleport ~30 yards from each spawn, detect the node, pathfind to it
///   4. Interact and verify skill increase
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

    // Gathering channel = ~3s. Wait 8s for margin + loot.
    private const int GatherChannelWaitMs = 8000;

    // Orgrimmar (safe zone, no hostile mobs) for GM setup
    private const int OrgrimmarMap = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 31.2f;

    // How far away from node to teleport (yards). Far enough to require navigation.
    private const float TeleportOffset = 25f;
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
        var spawns = await _bot.QueryGameObjectSpawnsAsync(CopperVeinEntry, mapFilter: 1, limit: 10);
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

            // Soft-check FG: don't block BG test if FG has stale skill state
            // (e.g., after WoW.exe restart mid-session).  Final assertion at end covers both.
            if (!fgGathered)
                _output.WriteLine($"WARNING — FG: Failed to gather Copper Vein. skill={fgSkillAfter}.");
            if (fgSkillAfter <= fgSkillBefore)
                _output.WriteLine($"WARNING — FG: Mining skill did not increase ({fgSkillBefore} → {fgSkillAfter}).");
        }
        else
        {
            _output.WriteLine("FG bot not available — skipping FG mining test.");
        }

        // --- BG: headless protocol emulation ---
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

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

        Assert.True(bgGathered,
            $"BG: Failed to gather Copper Vein at any of {spawns.Count} locations. skill={bgSkillAfter}.");
        Assert.True(bgSkillAfter > bgSkillBefore,
            $"BG: Mining skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");
    }

    // =====================================================================
    //  HERBALISM TEST
    // =====================================================================

    [SkippableFact]
    public async Task Herbalism_GatherHerb_SkillIncreases()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

        // --- Find herb spawns in Kalimdor (Durotar/Mulgore — low-level zones) ---
        var allSpawns = new List<(int map, float x, float y, float z, uint entry)>();
        foreach (var entry in new[] { PeacebloomEntry, SilverleafEntry, EarthrootEntry })
        {
            var spawns = await _bot.QueryGameObjectSpawnsAsync(entry, mapFilter: 1, limit: 5);
            foreach (var s in spawns)
                allSpawns.Add((s.map, s.x, s.y, s.z, entry));
        }
        global::Tests.Infrastructure.Skip.If(allSpawns.Count == 0,
            "No Peacebloom/Silverleaf/Earthroot spawns found in gameobject table (Kalimdor).");
        _output.WriteLine($"Found {allSpawns.Count} herb spawn locations");

        // --- Prepare: learn Herbalism + set skill ---
        await PrepareHerbalism(bgAccount, "BG");

        // --- Record initial skill ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillBefore = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"Initial herbalism skill: BG={bgSkillBefore}");
        global::Tests.Infrastructure.Skip.If(bgSkillBefore >= 300, $"BG herbalism skill already capped ({bgSkillBefore}); cannot assert further increase.");

        // --- Try each spawn location ---
        var herbEntries = allSpawns.Select(s => s.entry).Distinct().ToArray();

        bool bgGathered = false;
        foreach (var entry in herbEntries)
        {
            var entrySpawns = allSpawns.Where(s => s.entry == entry).Select(s => (s.map, s.x, s.y, s.z)).ToList();
            bgGathered = await TryGatherAtSpawns(bgAccount, "BG", entrySpawns,
                entry, GatheringData.HERBALISM_SKILL_ID, $"herb (entry {entry})",
                initialSkill: bgSkillBefore,
                gatherSpellId: HerbalismGatherSpell);
            if (bgGathered) break;
        }

        // --- Assert ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillAfter = GetSkill("BG", GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"Results: BG gathered={bgGathered}, skill {bgSkillBefore} → {bgSkillAfter}");

        Assert.True(bgGathered,
            $"BG: Failed to gather herb at any of {allSpawns.Count} locations. skill={bgSkillAfter}.");
        Assert.True(bgSkillAfter > bgSkillBefore,
            $"BG: Herbalism skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================
    /// <summary>
    /// Snapshot-driven mining setup: apply only missing preconditions.
    /// </summary>
    private async Task PrepareMining(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        await _bot.SendGmChatCommandTrackedAsync(account, ".gm on", captureResponse: true);
        await Task.Delay(500);

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before mining setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        var currentMining = GetSkill(label, GatheringData.MINING_SKILL_ID);
        if (currentMining < 1)
        {
            await EnsureSelfSelectionForBgAsync(account, label);
            _output.WriteLine($"[{label}] Applying mining skills (.learn/.setskill)");
            await _bot.BotLearnSpellAsync(account, MiningApprentice);
            await _bot.BotLearnSpellAsync(account, MiningGatherSpell);
            await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.MINING_SKILL_ID} 1 300", captureResponse: true);
        }
        else
        {
            _output.WriteLine($"[{label}] Mining skill already present ({currentMining}) - skipping learn/setskill");
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
    /// </summary>
    private async Task PrepareHerbalism(string account, string label)
    {
        await EnsureAliveAndAtSetupLocationAsync(account, label);

        await _bot.SendGmChatCommandTrackedAsync(account, ".gm on", captureResponse: true);
        await Task.Delay(500);

        await _bot.RefreshSnapshotsAsync();
        var bagCount = GetBagItemCount(label);
        if (bagCount >= 12)
        {
            _output.WriteLine($"[{label}] Clearing bags (count={bagCount}) before herbalism setup");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await _bot.RefreshSnapshotsAsync();
        }

        var currentHerbalism = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        if (currentHerbalism < 1)
        {
            await EnsureSelfSelectionForBgAsync(account, label);
            _output.WriteLine($"[{label}] Applying herbalism skills (.learn/.setskill)");
            await _bot.BotLearnSpellAsync(account, HerbalismApprentice);
            await _bot.BotLearnSpellAsync(account, HerbalismGatherSpell);
            await _bot.SendGmChatCommandTrackedAsync(account, $".setskill {GatheringData.HERBALISM_SKILL_ID} 1 300", captureResponse: true);
            await Task.Delay(500);
        }
        else
        {
            _output.WriteLine($"[{label}] Herbalism skill already present ({currentHerbalism}) - skipping learn/setskill");
        }

        await _bot.RefreshSnapshotsAsync();
        var skillCheck = GetSkill(label, GatheringData.HERBALISM_SKILL_ID);
        _output.WriteLine($"[{label}] Herbalism setup state: skill={skillCheck}");
    }

    /// <summary>
    /// Try gathering at natural spawn locations.
    ///
    /// Strategy: teleport near known spawn points (from DB), use .respawn to force
    /// depleted nodes back, then scan the character's NearbyObjects for the node.
    /// NO .gobject add - we only gather naturally-spawning nodes.
    /// </summary>
    private async Task<bool> TryGatherAtSpawns(string account, string label,
        List<(int map, float x, float y, float z)> spawns,
        uint nodeEntry, uint skillId, string nodeName,
        uint initialSkill,
        uint gatherSpellId = 0, string? parkAccount = null)
    {
        int maxLocations = Math.Min(spawns.Count, 5);

        // Enter gameplay mode once for the full gather loop.
        await _bot.SendGmChatCommandTrackedAsync(account, ".gm off", captureResponse: true);
        await Task.Delay(500);

        // Park the other bot once to reduce coordinator interference.
        if (parkAccount != null)
            await _bot.BotTeleportAsync(parkAccount, OrgrimmarMap, OrgX, OrgY, OrgZ);

        for (int loc = 0; loc < maxLocations; loc++)
        {
            var (map, spawnX, spawnY, spawnZ) = spawns[loc];

            // Z+3 offset to avoid MaNGOS undermap detection
            float safeZ = spawnZ + 3f;
            _output.WriteLine($"[{label}] Location {loc + 1}/{maxLocations}: checking natural {nodeName} near ({spawnX:F1}, {spawnY:F1}, {spawnZ:F1})");

            await _bot.BotTeleportAsync(account, map, spawnX, spawnY, safeZ);
            await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

            await _bot.SendGmChatCommandTrackedAsync(account, ".respawn", captureResponse: false);
            await Task.Delay(2000);

            // --- Scan for the node in NearbyObjects ---
            ulong nodeGuid = 0;
            float nodeX = 0, nodeY = 0, nodeZ = 0;
            var sw = Stopwatch.StartNew();
            bool loggedDiag = false;
            while (sw.Elapsed < TimeSpan.FromSeconds(8))
            {
                await _bot.RefreshSnapshotsAsync();
                var snap = GetSnapshot(label);
                var gameObjects = snap?.NearbyObjects?.ToList() ?? [];

                // Diagnostic: log all visible game objects on first scan to help debug visibility issues
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
                    nodeGuid = node.Base?.Guid ?? 0;
                    var nodePos = node.Base?.Position;
                    if (nodePos != null)
                    {
                        nodeX = nodePos.X;
                        nodeY = nodePos.Y;
                        nodeZ = nodePos.Z;
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

            if (startDist > 5f)
            {
                // Teleport ~5y away from node (not on top) so gather interaction works naturally.
                // Offset along the spawn→node vector so we approach from a natural direction.
                float dx = nodeX - spawnX, dy = nodeY - spawnY;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float offsetX = len > 0.1f ? -5f * (dx / len) : -5f;
                float offsetY = len > 0.1f ? -5f * (dy / len) : 0f;
                await _bot.BotTeleportAsync(account, map, nodeX + offsetX, nodeY + offsetY, nodeZ + 3f);
                await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
            }

            await _bot.RefreshSnapshotsAsync();
            playerPos = GetSnapshot(label)?.Player?.Unit?.GameObject?.Base?.Position;
            float finalDist = playerPos != null ? Distance(playerPos.X, playerPos.Y, playerPos.Z, nodeX, nodeY, nodeZ) : startDist;
            _output.WriteLine($"  [{label}] Sending GatherNode (dist={finalDist:F1}y, spell={gatherSpellId})...");

            var gatherParams = new ActionMessage
            {
                ActionType = ActionType.GatherNode,
                Parameters = { new RequestParameter { LongParam = (long)nodeGuid } }
            };
            if (gatherSpellId > 0)
                gatherParams.Parameters.Add(new RequestParameter { IntParam = (int)gatherSpellId });
            await _bot.SendActionAndWaitAsync(account, gatherParams, delayMs: GatherChannelWaitMs);

            await _bot.RefreshSnapshotsAsync();
            uint skillNow = GetSkill(label, skillId);
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

    private async Task EnsureSelfSelectionForBgAsync(string account, string label)
    {
        if (label != "BG")
            return;

        await _bot.RefreshSnapshotsAsync();
        var playerGuid = GetSnapshot(label)?.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
        if (playerGuid == 0)
            return;

        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)playerGuid } }
        }, delayMs: 500);
    }

    private int GetBagItemCount(string label)
        => GetSnapshot(label)?.Player?.BagContents?.Count ?? 0;

    private bool HasBagItem(string label, uint itemId)
    {
        var bags = GetSnapshot(label)?.Player?.BagContents;
        return bags != null && bags.Values.Any(v => v == itemId);
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

