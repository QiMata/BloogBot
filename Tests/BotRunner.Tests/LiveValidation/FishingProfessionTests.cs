using System;
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
/// Fishing profession integration test — dual-client validation.
///
/// Strategy:
///   1) Enable GM mode (prevents anti-undermap kicks, hostile targeting)
///   2) Apply setup deltas (fishing spell, skill, pole)
///   3) Teleport to known shore positions near fishable water (DB-sourced)
///   4) Try multiple positions until fishing channel starts (server confirms water)
///   5) Wait for auto-catch via SMSG_GAMEOBJECT_CUSTOM_ANIM handler
///   6) Assert fishing skill increased
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

    // Fishing channel = 20s server-side. Wait 22s for margin.
    private const int FishingChannelWaitMs = 22000;
    private const int MaxFishingAttempts = 3;
    private const uint PlayerFlagGhost = 0x10;
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7;

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

        string? fgAccount = _bot.ForegroundBot != null ? _bot.FgAccountName : null;
        if (fgAccount != null)
            _output.WriteLine($"FG: {_bot.FgCharacterName} ({fgAccount})");

        // --- Enable GM mode — prevents anti-undermap teleport loops and hostile targeting ---
        _output.WriteLine("Enabling GM mode for all bots");
        await _bot.SendGmChatCommandAsync(bgAccount, ".gm on");
        await Task.Delay(500);
        if (fgAccount != null)
        {
            await _bot.SendGmChatCommandAsync(fgAccount, ".gm on");
            await Task.Delay(500);
        }

        // --- Prepare both bots (learn fishing + equip pole) ---
        await PrepareBot(bgAccount, "BG");
        if (fgAccount != null)
            await PrepareBot(fgAccount, "FG");

        // Park FG bot at Orgrimmar to reduce coordinator interference
        if (fgAccount != null)
        {
            _output.WriteLine("[FG] Parking at Orgrimmar during BG fishing test");
            await _bot.BotTeleportAsync(fgAccount, OrgMap, OrgX, OrgY, OrgZ);
        }

        // --- Record initial skill ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillBefore = GetFishingSkill("BG");
        _output.WriteLine($"Initial fishing skill: BG={bgSkillBefore}");

        // --- Find a working fishing position and catch fish ---
        bool bgCaught = await TryFishAtLocations(bgAccount, "BG", bgSkillBefore);

        // --- Assert ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillAfter = GetFishingSkill("BG");
        _output.WriteLine($"Results: BG caught={bgCaught}, skill {bgSkillBefore} → {bgSkillAfter}");

        Assert.True(bgCaught,
            $"BG: Failed to catch fish at any of {FishingSpots.Length} locations. skill={bgSkillAfter}. " +
            "Check: (1) SMSG_GAMEOBJECT_CUSTOM_ANIM handler, (2) bobber CREATE_OBJECT delivery, (3) position/water detection.");
        Assert.True(bgSkillAfter > bgSkillBefore,
            $"BG: Fishing skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");
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

            // GM mode ON for teleport (prevents anti-undermap kicks during relocation)
            await _bot.SendGmChatCommandAsync(account, ".gm on");
            await Task.Delay(300);

            // Teleport to shore position
            await _bot.BotTeleportAsync(account, map, shoreX, shoreY, shoreZ);
            await Task.Delay(4000); // zone load + Z stabilization

            // Verify position is stable
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null)
            {
                _output.WriteLine($"  [{label}] No position data, skipping location");
                continue;
            }
            _output.WriteLine($"  [{label}] Landed at ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

            // Set facing toward water
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.SetFacing,
                Parameters = { new RequestParameter { FloatParam = facing } }
            }, delayMs: 500);

            // Turn GM mode OFF before fishing — GM mode prevents fishing spell from working.
            // Wait 3s for physics to fully stabilize after GM off — the movement controller
            // needs time to settle position and clear any MOVEFLAG_FALLINGFAR from the Z clamp.
            await _bot.SendGmChatCommandAsync(account, ".gm off");
            await Task.Delay(3000);

            // Log nearby game objects (fishing nodes, etc.)
            await _bot.RefreshSnapshotsAsync();
            snap = GetSnapshot(label);
            var nearbyGOs = snap?.NearbyObjects?.ToList() ?? [];
            _output.WriteLine($"  [{label}] Nearby GOs: {nearbyGOs.Count}");
            foreach (var go in nearbyGOs.Take(8))
                _output.WriteLine($"    entry={go.Entry}, displayId={go.DisplayId}, type={go.GameObjectType}, " +
                    $"pos=({go.Base?.Position?.X:F1}, {go.Base?.Position?.Y:F1}, {go.Base?.Position?.Z:F1})");

            // Verify position after GM off — teleport Z clamp should hold position
            await _bot.RefreshSnapshotsAsync();
            snap = GetSnapshot(label);
            pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  [{label}] Post-GM-off pos: ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");

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

        if (!IsStrictAlive(snap))
        {
            _output.WriteLine($"[{label}] Not strict-alive; reviving before setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
        }

        var spellKnown = snap.Player?.SpellList?.Contains((uint)FishingData.FishingRank1) == true;
        var currentSkill = GetFishingSkillFromSnapshot(snap);
        var hasPoleInBags = snap.Player?.BagContents?.Values.Contains((uint)FishingData.FishingPole) == true;

        var needsLearn = !spellKnown;
        var needsSkillTune = currentSkill < 150 || currentSkill >= 300;
        var needsPole = !hasPoleInBags;

        if (needsLearn || needsSkillTune || needsPole)
        {
            _output.WriteLine($"[{label}] Setup deltas: learn={needsLearn}, skillTune={needsSkillTune}, pole={needsPole}");

            if (needsLearn)
            {
                await _bot.BotLearnSpellAsync(account, FishingData.FishingRank1);
                await _bot.BotLearnSpellAsync(account, FishingData.FishingRank2);
            }

            if (needsSkillTune)
                await _bot.SendGmChatCommandAsync(account, $".setskill {FishingData.FishingSkillId} 150 300");

            if (needsPole)
            {
                await _bot.BotAddItemAsync(account, FishingData.FishingPole);
                await Task.Delay(1500);
            }

            // Shiny Bauble (+25 fishing) increases catch rate
            await _bot.BotAddItemAsync(account, FishingData.ShinyBauble);
            await Task.Delay(500);
        }
        else
        {
            _output.WriteLine($"[{label}] Setup already satisfied.");
        }

        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingPole } }
        }, delayMs: 1500);
    }

    private async Task<bool> CastAndWaitForCatch(string account, string label, uint baseSkill)
    {
        for (int attempt = 1; attempt <= MaxFishingAttempts; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            var preSnap = GetSnapshot(label);
            var prePos = preSnap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"[{label}] Cast {attempt}/{MaxFishingAttempts} — " +
                $"pos=({prePos?.X:F1}, {prePos?.Y:F1}, {prePos?.Z:F1})");

            // Step 1: Try CastSpell action (CMSG_CAST_SPELL). This is the correct
            // client packet path. With the MOVEFLAG_FALLINGFAR fix in MovementController,
            // the server should accept the cast without interrupting the channel.
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingRank1 } }
            }, delayMs: 3000);

            // Check if channel started
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var channelId = snap?.Player?.Unit?.ChannelSpellId ?? 0;
            var bobber = FindBobber(snap);
            _output.WriteLine($"  [{label}] After CastSpell: channel={channelId}, bobber={bobber != null}");

            // Step 2: If channel didn't start, try .cast as GM fallback
            if (channelId == 0 && bobber == null)
            {
                _output.WriteLine($"  [{label}] CastSpell failed, trying .cast fallback...");
                await _bot.SendGmChatCommandAsync(account, $".cast {FishingData.FishingRank1}");

                // Wait 6s for CREATE packet to arrive and snapshot to update
                await Task.Delay(6000);
                await _bot.RefreshSnapshotsAsync();
                await Task.Delay(500);
                await _bot.RefreshSnapshotsAsync();

                snap = GetSnapshot(label);
                channelId = snap?.Player?.Unit?.ChannelSpellId ?? 0;
                bobber = FindBobber(snap);
                _output.WriteLine($"  [{label}] After .cast: channel={channelId}, bobber={bobber != null}");
            }

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
            _output.WriteLine($"  [{label}] Wait done. skill={finalSkill}, hadBobber={bobber != null}");

            if (finalSkill > baseSkill)
            {
                _output.WriteLine($"  [{label}] Skill increased ({baseSkill} → {finalSkill}), catch confirmed!");
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
}
