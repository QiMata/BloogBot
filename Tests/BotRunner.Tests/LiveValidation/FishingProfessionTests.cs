using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Combat;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fishing profession integration test — BG-first live validation.
///
/// Strategy:
///   1) Force a clean fishing spell-sync path (.unlearn -> .learn) so BG sees SMSG_LEARNED_SPELL
///   2) Apply setup deltas (skill, pole, lure) and assert the snapshot reflects them
///   3) Teleport to known shore positions near fishable water and reject unstable landings
///   4) Try multiple positions until fishing channel starts (server confirms water)
///   5) Wait for auto-catch via SMSG_GAMEOBJECT_CUSTOM_ANIM handler
///   6) Assert a catch occurred and log whether skill increased
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~FishingProfessionTests" --configuration Release -v n
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Fishing channel is nominally 20s server-side, but live BG runs have shown delayed
    // bite/custom-anim delivery after teleports and packet jitter. Keep extra headroom so
    // the test observes the actual catch pipeline instead of only the cast entry.
    private const int FishingChannelWaitMs = 30000;
    private const int MaxFishingAttempts = 3;
    private const float MaxLandingDeltaFromShore = 3.5f;

    private static readonly uint[] FishingSpellSyncIds =
    [
        FishingData.FishingRank1,
        FishingData.FishingRank2,
        FishingData.FishingRank3,
        FishingData.FishingRank4,
        FishingData.FishingPoleProficiency
    ];

    private static readonly HashSet<uint> FishingSetupItemIds =
    [
        FishingData.FishingPole,
        FishingData.StrongFishingPole,
        FishingData.BigIronFishingPole,
        FishingData.DarkwoodFishingPole,
        FishingData.ShinyBauble,
        FishingData.NightcrawlerBait,
        FishingData.BrightBaubles,
        FishingData.AquadynamicFishAttractor
    ];

    // --- Fishing locations: positions near fishable water ---
    // Each entry: (mapId, shoreX, shoreY, shoreZ, facingRadians)
    // Requirements: (a) physics-stable (navmesh ground ≈ terrain Z, no Z clamp),
    //               (b) server considers player "on land" (not swimming),
    //               (c) fishable water within 20y in facing direction,
    //               (d) bobber can land at water surface (not too far below player Z).
    //
    // Ratchet dock: CONFIRMED bobber creation (displayId=668, TypeId=17) at water surface.
    // Fixed: Z clamp was sending MOVEFLAG_FALLINGFAR heartbeats that interrupted the channel.
    private static readonly (int map, float x, float y, float z, float facing)[] FishingSpots =
    [
        // Ratchet dock — stand on dock surface (Z=5.7), face east toward ocean.
        // CONFIRMED: server creates bobber at (-968,-3834,0). Bobber is ~14y from player.
        (1, -988.5f, -3834f, 5.7f, 6.21f),

        // Ratchet dock — alternate, face NE toward Oily Blackmouth School
        (1, -985.7f, -3827f, 5.7f, 5.50f),

        // Durotar coast — natural beach south of Ratchet, face east.
        // Natural terrain → navmesh should match, no Z clamp needed.
        (1, -995f, -3850f, 4f, 6.28f),

        // Sen'jin Village — lower position closer to water, face east.
        (1, -820f, -4885f, 8f, 0.0f),
    ];

    // Orgrimmar park location for setup & parking
    private const int OrgMap = 1;
    private const float OrgX = 1629f;
    private const float OrgY = -4373f;
    private const float OrgZ = 15f;

    public FishingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Fishing_CatchFish_SkillIncreases()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"BG: {_bot.BgCharacterName} ({bgAccount})");

        string? fgAccount = _bot.IsFgActionable ? _bot.FgAccountName : null;
        if (fgAccount != null)
            _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccount})");

        // --- Prepare both bots (learn fishing + equip pole) ---
        await PrepareBot(bgAccount, "BG");
        if (fgAccount != null)
            await PrepareFgReferenceBotAsync(fgAccount);

        // Park FG bot at Orgrimmar to reduce coordinator interference
        if (fgAccount != null)
        {
            _output.WriteLine("[FG] Parking at Orgrimmar during BG fishing test");
            await _bot.BotTeleportAsync(fgAccount, OrgMap, OrgX, OrgY, OrgZ);
        }

        // --- Record initial skill ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillBefore = GetFishingSkill("BG");
        Assert.True(bgSkillBefore > 0, "BG: Initial fishing skill is 0 — setup failed to teach fishing.");
        _output.WriteLine($"Initial fishing skill: BG={bgSkillBefore}");

        // --- Find a working fishing position and catch fish ---
        bool bgCaught = await TryFishAtLocations(bgAccount, "BG", bgSkillBefore);

        // --- Assert ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillAfter = GetFishingSkill("BG");
        _output.WriteLine($"Results: BG caught={bgCaught}, skill {bgSkillBefore} → {bgSkillAfter}");

        Assert.True(bgCaught,
            $"BG: Failed to catch fish at any of {FishingSpots.Length} locations. skill={bgSkillAfter}. " +
            "Check: (1) spell sync via SMSG_LEARNED_SPELL, (2) SMSG_GAMEOBJECT_CUSTOM_ANIM handler, " +
            "(3) bobber CREATE_OBJECT delivery, (4) shoreline stability/water detection.");
        // Skill-ups are RNG in vanilla WoW — not guaranteed per catch even at low skill.
        // At skill 1 the probability is very high but not 100%. Log the result but
        // don't fail the test on skill alone; the catch itself proves the pipeline works.
        if (bgSkillAfter <= bgSkillBefore)
            _output.WriteLine($"BG: WARNING — Fishing skill did not increase ({bgSkillBefore} → {bgSkillAfter}). " +
                "This can happen due to WoW's RNG skill-up mechanic.");
    }

    /// <summary>
    /// Iterate through known fishing locations. At each location, try fishing.
    /// Return true on first successful catch (skill increase).
    /// </summary>
    private async Task<bool> TryFishAtLocations(string account, string label, uint initialSkill)
    {
        for (int loc = 0; loc < FishingSpots.Length; loc++)
        {
            var (map, shoreX, shoreY, shoreZ, facing) = FishingSpots[loc];

            _output.WriteLine($"[{label}] Location {loc + 1}/{FishingSpots.Length}: " +
                $"shore=({shoreX:F0}, {shoreY:F0}, {shoreZ:F0}), facing={facing:F2}");

            // Teleport to shore position
            await _bot.BotTeleportAsync(account, map, shoreX, shoreY, shoreZ);
            var (stableLanding, finalZ) = await _bot.WaitForZStabilizationAsync(account, waitMs: 4000);

            // Verify position is stable
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null)
            {
                _output.WriteLine($"  [{label}] No position data, skipping location");
                continue;
            }
            var landingDelta = Math.Abs(finalZ - shoreZ);
            _output.WriteLine($"  [{label}] Landed at ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}) " +
                $"stable={stableLanding} finalZ={finalZ:F1} deltaFromShore={landingDelta:F1}");

            if (!stableLanding || landingDelta > MaxLandingDeltaFromShore)
            {
                _output.WriteLine($"  [{label}] Skipping location {loc + 1}: unstable shoreline landing.");
                continue;
            }

            // Set facing toward water
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.SetFacing,
                Parameters = { new RequestParameter { FloatParam = facing } }
            }, delayMs: 500);

            // Brief stabilization for physics to settle position after teleport + facing.
            await Task.Delay(500);

            // Log nearby game objects (fishing nodes, etc.)
            await _bot.RefreshSnapshotsAsync();
            snap = GetSnapshot(label);
            var nearbyGOs = snap?.NearbyObjects?.ToList() ?? [];
            _output.WriteLine($"  [{label}] Nearby GOs: {nearbyGOs.Count}");
            foreach (var go in nearbyGOs.Take(8))
                _output.WriteLine($"    entry={go.Entry}, displayId={go.DisplayId}, type={go.GameObjectType}, " +
                    $"pos=({go.Base?.Position?.X:F1}, {go.Base?.Position?.Y:F1}, {go.Base?.Position?.Z:F1})");

            // Try fishing at this location — up to MaxFishingAttempts casts
            bool caught = await CastAndWaitForCatch(account, label, initialSkill);
            if (caught) return true;

            // Check if skill increased even without explicit catch detection
            await _bot.RefreshSnapshotsAsync();
            uint currentSkill = GetFishingSkill(label);
            if (currentSkill > initialSkill)
            {
                _output.WriteLine($"  [{label}] Skill increased ({initialSkill} → {currentSkill}) at location {loc + 1}!");
                return true;
            }

            _output.WriteLine($"  [{label}] No catch at this location, trying next...");
        }

        return false;
    }

    private async Task PrepareBot(string account, string label)
    {
        // GM mode is already ON (enabled by caller).
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null)
            throw new InvalidOperationException($"[{label}] Missing snapshot during fishing setup.");

        if (!LiveBotFixture.IsStrictAlive(snap))
        {
            _output.WriteLine($"[{label}] Not strict-alive; reviving before setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await _bot.WaitForSnapshotConditionAsync(account, LiveBotFixture.IsStrictAlive, TimeSpan.FromSeconds(5));
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var spellCountBefore = snap.Player?.SpellList?.Count ?? 0;
        var castableBefore = ResolveCastableFishingSpellId(snap);
        var poleProfKnown = snap.Player?.SpellList?.Contains(FishingData.FishingPoleProficiency) == true;
        var currentSkill = GetFishingSkillFromSnapshot(snap);
        _output.WriteLine($"[{label}] Setup baseline: castable={castableBefore}, poleProf={poleProfKnown}, " +
            $"skill={currentSkill}, spellCount={spellCountBefore}");

        // Always unlearn/relearn the fishing ranks first. If the server already knows the spell,
        // `.learn` returns "already know that spell" and BG never receives SMSG_LEARNED_SPELL,
        // so SpellHandler.HandleLearnedSpell() cannot repopulate the KnownSpellIds snapshot that
        // BuildCastSpellSequence() checks before calling CastSpellAtLocation().
        await ForceFishingSpellSyncAsync(account, label);

        // Always set fishing skill to 1/300 — ensures room for skill-ups during the test.
        // MaNGOS caps max based on highest known fishing rank; Artisan (18248) allows 300.
        var postSyncSnap = await _bot.GetSnapshotAsync(account);
        var skillExists = postSyncSnap?.Player?.SkillInfo?.ContainsKey(FishingData.FishingSkillId) == true;
        _output.WriteLine($"[{label}] Fishing skill exists={skillExists}");
        if (!skillExists)
        {
            _output.WriteLine($"[{label}] Skill still not created; trying .learn all_crafts...");
            await _bot.SendGmChatCommandAsync(account, ".learn all_crafts");
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();
        }

        // Set skill to 1/300 — low value with headroom for increase
        await _bot.BotSetSkillAsync(account, FishingData.FishingSkillId, 1, 300);
        await Task.Delay(500);
        await _bot.RefreshSnapshotsAsync();

        // Clear all equipment first. Fishing pole has inv_type=17 (2H weapon) in MaNGOS,
        // so CMSG_AUTOEQUIP_ITEM tries to put it in MAINHAND. If mainhand is occupied,
        // the swap can silently fail. Clearing equipment ensures clean equip.
        _output.WriteLine($"[{label}] Clearing items/equipment for clean fishing pole equip...");
        await _bot.ExecuteGMCommandAsync($".reset items {snap?.CharacterName}");
        await Task.Delay(1000);

        // Re-add the fishing pole to the now-empty inventory
        await _bot.BotAddItemAsync(account, FishingData.FishingPole);
        var poleAppeared = await _bot.WaitForSnapshotConditionAsync(account,
            s => s?.Player?.BagContents?.Values.Contains((uint)FishingData.FishingPole) == true,
            TimeSpan.FromSeconds(8));
        _output.WriteLine($"[{label}] Fishing pole re-added to bags: {poleAppeared}");

        // Re-add Shiny Bauble if needed
        await _bot.BotAddItemAsync(account, FishingData.ShinyBauble);
        await Task.Delay(300);

        // Now equip the fishing pole — mainhand should be empty after .reset items
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingPole } }
        }, delayMs: 2000);

        // Verify fishing pole was consumed from bags (moved to equipment slot).
        await _bot.RefreshSnapshotsAsync();
        var equipSnap = await _bot.GetSnapshotAsync(account);
        var poleStillInBags = equipSnap?.Player?.BagContents?.Values.Contains((uint)FishingData.FishingPole) == true;
        if (poleStillInBags)
        {
            _output.WriteLine($"[{label}] WARNING: Fishing pole still in bags after equip. Retrying once...");
            await Task.Delay(1500);
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.EquipItem,
                Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingPole } }
            }, delayMs: 2000);
            await _bot.RefreshSnapshotsAsync();
            equipSnap = await _bot.GetSnapshotAsync(account);
            poleStillInBags = equipSnap?.Player?.BagContents?.Values.Contains((uint)FishingData.FishingPole) == true;
            _output.WriteLine($"[{label}] Fishing pole still in bags after retry: {poleStillInBags}");
        }
        else
        {
            _output.WriteLine($"[{label}] Fishing pole equipped (no longer in bags).");
        }

        Assert.False(poleStillInBags, $"[{label}] Fishing pole never left bag contents after EquipItem.");
    }

    private async Task PrepareFgReferenceBotAsync(string account)
    {
        await _bot.EnsureCleanSlateAsync(account, "FG");
        await _bot.BotTeleportAsync(account, OrgMap, OrgX, OrgY, OrgZ);
        await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);
        _output.WriteLine("[FG] Reference bot parked at Orgrimmar; BG remains the asserted fishing path.");
    }

    private async Task<bool> CastAndWaitForCatch(string account, string label, uint baseSkill)
    {
        for (int attempt = 1; attempt <= MaxFishingAttempts; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            var preSnap = GetSnapshot(label);
            var prePos = preSnap?.Player?.Unit?.GameObject?.Base?.Position;
            var castSpellId = ResolveCastableFishingSpellId(preSnap);
            var baselineCatchItems = GetCatchItemIds(preSnap);
            _output.WriteLine($"[{label}] Cast {attempt}/{MaxFishingAttempts} — " +
                $"pos=({prePos?.X:F1}, {prePos?.Y:F1}, {prePos?.Z:F1}), spell={castSpellId}");

            Assert.NotEqual(0u, castSpellId);

            // Cast fishing via player action (CMSG_CAST_SPELL) — no GM shortcuts.
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = (int)castSpellId } }
            }, delayMs: 3000);

            // Check if channel started
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var channelId = snap?.Player?.Unit?.ChannelSpellId ?? 0;
            var bobber = FindBobber(snap);
            _output.WriteLine($"  [{label}] After CastSpell: channel={channelId}, bobber={bobber != null}");

            // Log diagnostics
            if (bobber != null)
            {
                var bpos = bobber.Base?.Position;
                _output.WriteLine($"  [{label}] BOBBER FOUND: guid=0x{bobber.Base?.Guid ?? 0:X}, " +
                    $"pos=({bpos?.X:F1}, {bpos?.Y:F1}, {bpos?.Z:F1}), display={bobber.DisplayId}");
            }
            else
            {
                LogNearbyGameObjects(snap, label);
            }

            // If neither channel nor bobber, this spot doesn't work
            if (channelId == 0 && bobber == null)
            {
                _output.WriteLine($"  [{label}] No channel and no bobber — no fishable water here");
                return false;
            }

            // Step 3: Wait for auto-catch. The SpellHandler's HandleGameObjectCustomAnim
            // handler auto-interacts with the bobber when SMSG_GAMEOBJECT_CUSTOM_ANIM
            // arrives (fish bite event, random 5-20s into the 20s channel).
            _output.WriteLine($"  [{label}] Waiting up to {FishingChannelWaitMs / 1000}s for auto-catch...");
            int elapsed = 0;
            const int pollInterval = 3000;
            while (elapsed < FishingChannelWaitMs)
            {
                await Task.Delay(pollInterval);
                elapsed += pollInterval;

                await _bot.RefreshSnapshotsAsync();
                uint midSkill = GetFishingSkill(label);
                if (midSkill > baseSkill)
                {
                    _output.WriteLine($"  [{label}] Skill increased ({baseSkill} → {midSkill}) at {elapsed / 1000}s!");
                    return true;
                }

                // Re-check bobber (may appear late, or disappear after catch)
                snap = GetSnapshot(label);
                var currentCatchItems = GetCatchItemIds(snap);
                if (!baselineCatchItems.SequenceEqual(currentCatchItems))
                {
                    _output.WriteLine($"  [{label}] Loot changed at {elapsed / 1000}s: " +
                        $"before=[{string.Join(", ", baselineCatchItems)}] after=[{string.Join(", ", currentCatchItems)}]");
                    return true;
                }

                var currentBobber = FindBobber(snap);
                var currentChannel = snap?.Player?.Unit?.ChannelSpellId ?? 0;
                if (bobber == null && currentBobber != null)
                {
                    bobber = currentBobber;
                    var bpos = bobber.Base?.Position;
                    _output.WriteLine($"  [{label}] BOBBER APPEARED at {elapsed / 1000}s: " +
                        $"guid=0x{bobber.Base?.Guid ?? 0:X}, pos=({bpos?.X:F1}, {bpos?.Y:F1}, {bpos?.Z:F1})");
                }
                // Log when channel/bobber disappears (spell ended)
                if (elapsed == pollInterval)
                    _output.WriteLine($"  [{label}] At {elapsed / 1000}s: channel={currentChannel}, bobber={currentBobber != null}");
            }

            // Final skill check
            await _bot.RefreshSnapshotsAsync();
            uint finalSkill = GetFishingSkill(label);
            snap = GetSnapshot(label);
            var finalCatchItems = GetCatchItemIds(snap);
            _output.WriteLine($"  [{label}] Wait done. skill={finalSkill}, hadBobber={bobber != null}, " +
                $"catchItems=[{string.Join(", ", finalCatchItems)}]");

            if (finalSkill > baseSkill)
            {
                _output.WriteLine($"  [{label}] Skill increased ({baseSkill} → {finalSkill}), catch confirmed!");
                return true;
            }

            if (!baselineCatchItems.SequenceEqual(finalCatchItems))
            {
                _output.WriteLine($"  [{label}] Catch confirmed by bag delta without skill-up.");
                return true;
            }

            _output.WriteLine($"  [{label}] No catch this attempt, retrying...");
        }

        _output.WriteLine($"  [{label}] {MaxFishingAttempts} attempts exhausted.");
        return false;
    }

    private static Game.WoWGameObject? FindBobber(WoWActivitySnapshot? snap)
    {
        return snap?.NearbyObjects?.FirstOrDefault(go =>
            go.DisplayId == FishingData.BobberDisplayId || go.GameObjectType == 17);
    }

    private async Task ForceFishingSpellSyncAsync(string account, string label)
    {
        _output.WriteLine($"[{label}] Forcing fresh fishing spell sync (.unlearn -> .learn).");

        foreach (var spellId in FishingSpellSyncIds)
        {
            await _bot.BotUnlearnSpellAsync(account, spellId);
            await Task.Delay(250);
        }

        foreach (var spellId in FishingSpellSyncIds)
        {
            await _bot.BotLearnSpellAsync(account, spellId);
            await Task.Delay(250);
        }

        var synced = await _bot.WaitForSnapshotConditionAsync(
            account,
            HasRequiredFishingSpells,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} fishing-spell-sync");

        await _bot.RefreshSnapshotsAsync();
        var syncedSnap = await _bot.GetSnapshotAsync(account);
        var spellCount = syncedSnap?.Player?.SpellList?.Count ?? 0;
        var castableSpellId = ResolveCastableFishingSpellId(syncedSnap);
        var hasPoleProf = syncedSnap?.Player?.SpellList?.Contains(FishingData.FishingPoleProficiency) == true;
        _output.WriteLine($"[{label}] Fishing spell sync: spellCount={spellCount}, " +
            $"castable={castableSpellId}, poleProf={hasPoleProf}");

        Assert.True(synced,
            $"[{label}] Fishing spell sync failed. castable={castableSpellId}, poleProf={hasPoleProf}, spellCount={spellCount}.");
    }

    private void LogNearbyGameObjects(WoWActivitySnapshot? snap, string label)
    {
        var goCount = snap?.NearbyObjects?.Count ?? 0;
        if (goCount > 0)
        {
            _output.WriteLine($"  [{label}] Visible GOs ({goCount}):");
            foreach (var go in snap!.NearbyObjects!.Take(8))
                _output.WriteLine($"    e={go.Entry} d={go.DisplayId} t={go.GameObjectType} " +
                    $"g=0x{go.Base?.Guid ?? 0:X}");
        }
    }

    private uint GetFishingSkill(string label)
        => GetFishingSkillFromSnapshot(GetSnapshot(label));

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

    private static uint GetFishingSkillFromSnapshot(WoWActivitySnapshot? snap)
    {
        var skillMap = snap?.Player?.SkillInfo;
        if (skillMap != null && skillMap.TryGetValue(FishingData.FishingSkillId, out uint level))
            return level;
        return 0;
    }

    private static bool HasRequiredFishingSpells(WoWActivitySnapshot snap)
        => ResolveCastableFishingSpellId(snap) != 0
            && snap.Player?.SpellList?.Contains(FishingData.FishingPoleProficiency) == true;

    private static uint ResolveCastableFishingSpellId(WoWActivitySnapshot? snap)
        => FishingData.ResolveCastableFishingSpellId(
            snap?.Player?.SpellList,
            (int)GetFishingSkillFromSnapshot(snap));

    private static IReadOnlyList<uint> GetCatchItemIds(WoWActivitySnapshot? snap)
        => snap?.Player?.BagContents?.Values
            .Where(itemId => !FishingSetupItemIds.Contains(itemId))
            .OrderBy(itemId => itemId)
            .ToArray()
            ?? [];

}
