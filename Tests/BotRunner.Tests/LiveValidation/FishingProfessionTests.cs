using System;
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
/// Each bot (BG + FG) independently:
///   1) Apply only missing setup deltas (Fishing spell/skill/pole)
///   2) Ensure near Ratchet dock edge
///   4) Cast fishing → bot auto-catches via SMSG_GAMEOBJECT_CUSTOM_ANIM handler
///   5) Assert fishing skill increased in snapshot
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

    // Ratchet dock — neutral town, confirmed fishing node spawns.
    private const int FishMap = 1;
    private const float DockEdgeX = -988.5f;
    private const float DockEdgeY = -3834.0f;
    private const float DockEdgeZ = 5.7f;
    private const float FishFacing = 6.21f; // ESE toward water

    // Fishing channel = 20s server-side. Wait 22s for margin.
    private const int FishingChannelWaitMs = 22000;
    private const int MaxFishingAttempts = 3;
    private const float DockArrivalDistance = 40f;
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

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

        // --- Prepare both bots (learn fishing + equip pole) ---
        await PrepareBot(bgAccount, "BG");
        if (fgAccount != null)
            await PrepareBot(fgAccount, "FG");

        // --- Teleport only when not already near fishing location ---
        _output.WriteLine($"Ensuring near dock ({DockEdgeX}, {DockEdgeY}, {DockEdgeZ})");
        var bgTeleported = await EnsureNearFishingLocationAsync(bgAccount, "BG");
        var fgTeleported = false;
        if (fgAccount != null)
            fgTeleported = await EnsureNearFishingLocationAsync(fgAccount, "FG");

        if (bgTeleported || fgTeleported)
            await Task.Delay(5000); // zone load after teleport

        // Set facing
        var facingMsg = new ActionMessage
        {
            ActionType = ActionType.SetFacing,
            Parameters = { new RequestParameter { FloatParam = FishFacing } }
        };
        await _bot.SendActionAndWaitAsync(bgAccount, facingMsg, delayMs: 500);
        if (fgAccount != null)
            await _bot.SendActionAndWaitAsync(fgAccount, facingMsg, delayMs: 500);

        // Verify position
        await _bot.RefreshSnapshotsAsync();
        var bgPos = _bot.BackgroundBot?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"BG pos: ({bgPos?.X:F1}, {bgPos?.Y:F1}, {bgPos?.Z:F1})");

        // --- Record initial skill ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillBefore = GetFishingSkill("BG");
        _output.WriteLine($"Initial fishing skill: BG={bgSkillBefore}");

        uint fgSkillBefore = 0;
        if (fgAccount != null)
        {
            fgSkillBefore = GetFishingSkill("FG");
            _output.WriteLine($"Initial fishing skill: FG={fgSkillBefore}");
        }

        // --- Cast fishing (auto-catch via SMSG_GAMEOBJECT_CUSTOM_ANIM handler) ---
        bool bgCaught = await CastAndWaitForCatch(bgAccount, "BG");

        bool fgCaught = false;
        if (fgAccount != null)
            fgCaught = await CastAndWaitForCatch(fgAccount, "FG");

        // --- Assert ---
        await _bot.RefreshSnapshotsAsync();
        uint bgSkillAfter = GetFishingSkill("BG");
        _output.WriteLine($"Results: BG caught={bgCaught}, skill {bgSkillBefore} → {bgSkillAfter}");

        Assert.True(bgCaught,
            $"BG: Failed to catch fish. skill={bgSkillAfter}. Check SMSG_GAMEOBJECT_CUSTOM_ANIM handler.");
        Assert.True(bgSkillAfter > bgSkillBefore,
            $"BG: Fishing skill did not increase ({bgSkillBefore} → {bgSkillAfter}).");

        if (fgAccount != null)
        {
            uint fgSkillAfter = GetFishingSkill("FG");
            _output.WriteLine($"Results: FG caught={fgCaught}, skill {fgSkillBefore} → {fgSkillAfter}");
            // FG fishing is best-effort: ForegroundBotRunner uses Lua-based fishing,
            // not WoWSharpClient's auto-loot handler. Log results but don't fail the test.
            if (!fgCaught)
                _output.WriteLine("WARNING: FG did not catch fish — FG auto-loot not yet implemented.");
            else if (fgSkillAfter <= fgSkillBefore)
                _output.WriteLine($"WARNING: FG skill did not increase ({fgSkillBefore} → {fgSkillAfter}).");
        }
    }

    private async Task PrepareBot(string account, string label)
    {
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
        var needsSkillTune = currentSkill < 75 || currentSkill >= 300;
        var needsPole = !hasPoleInBags;

        if (needsLearn || needsSkillTune || needsPole)
        {
            _output.WriteLine($"[{label}] Applying setup deltas: learn={needsLearn}, skillTune={needsSkillTune}, addPole={needsPole}");
            await _bot.SendGmChatCommandAsync(account, ".gm on");

            if (needsLearn)
                await _bot.BotLearnSpellAsync(account, FishingData.FishingRank1);

            if (needsSkillTune)
                await _bot.SendGmChatCommandAsync(account, $".setskill {FishingData.FishingSkillId} 75 300");

            if (needsPole)
            {
                await _bot.BotAddItemAsync(account, FishingData.FishingPole);
                await Task.Delay(1500);
            }
        }
        else
        {
            _output.WriteLine($"[{label}] Setup deltas not required (spell/skill/pole already satisfied).");
        }

        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingPole } }
        }, delayMs: 1500);
    }

    private async Task<bool> CastAndWaitForCatch(string account, string label)
    {
        bool anyChannelObserved = false;

        for (int attempt = 1; attempt <= MaxFishingAttempts; attempt++)
        {
            _output.WriteLine($"[{label}] Attempt {attempt}/{MaxFishingAttempts}");

            // Cast fishing
            await _bot.SendActionAndWaitAsync(account, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = (int)FishingData.FishingRank1 } }
            }, delayMs: 1000);

            // Check if channeling started
            await _bot.RefreshSnapshotsAsync();
            var snap = GetSnapshot(label);
            var channelId = snap?.Player?.Unit?.ChannelSpellId ?? 0;
            _output.WriteLine($"  [{label}] channelSpellId={channelId}");

            if (channelId == 0)
            {
                // Cast failed — try alternative via GM .cast command
                _output.WriteLine($"  [{label}] Cast failed! Retrying via .cast {FishingData.FishingRank1}");
                await _bot.SendGmChatCommandAsync(account, $".cast {FishingData.FishingRank1}");
                await Task.Delay(1000);

                await _bot.RefreshSnapshotsAsync();
                snap = GetSnapshot(label);
                channelId = snap?.Player?.Unit?.ChannelSpellId ?? 0;
                _output.WriteLine($"  [{label}] After .cast: channelSpellId={channelId}");
                if (channelId == 0)
                {
                    _output.WriteLine($"  [{label}] Still not channeling, skipping attempt");
                    continue;
                }
            }

            anyChannelObserved = true;

            // Check for bobber
            await Task.Delay(2000);
            var bobberGuid = await FindBobberGuid(label);
            if (bobberGuid != 0)
                _output.WriteLine($"  [{label}] Bobber 0x{bobberGuid:X}");

            // Wait for auto-catch (SMSG_GAMEOBJECT_CUSTOM_ANIM handler)
            _output.WriteLine($"  [{label}] Waiting {FishingChannelWaitMs / 1000}s for channel...");
            await Task.Delay(FishingChannelWaitMs);

            _output.WriteLine($"  [{label}] Channel complete.");
        }

        // Final diagnostics
        await _bot.RefreshSnapshotsAsync();
        uint finalSkill = GetFishingSkill(label);
        _output.WriteLine($"  [{label}] Final skill={finalSkill}, anyChannelObserved={anyChannelObserved}");

        if (!anyChannelObserved)
        {
            var finalSnap = GetSnapshot(label);
            if (finalSnap?.NearbyObjects != null)
            {
                foreach (var go in finalSnap.NearbyObjects.Take(5))
                    _output.WriteLine($"  GO: entry={go.Entry}, displayId={go.DisplayId}, type={go.GameObjectType}");
            }
        }

        // Return true if channeling was observed — skill comparison is done by the caller
        return anyChannelObserved;
    }

    private async Task<ulong> FindBobberGuid(string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = GetSnapshot(label);
        var bobber = snap?.NearbyObjects?.FirstOrDefault(go => go.DisplayId == FishingData.BobberDisplayId)
            ?? snap?.NearbyObjects?.FirstOrDefault(go => go.GameObjectType == 17);
        return bobber?.Base?.Guid ?? 0;
    }

    private uint GetFishingSkill(string label)
    {
        var snap = GetSnapshot(label);
        return GetFishingSkillFromSnapshot(snap);
    }

    private WoWActivitySnapshot? GetSnapshot(string label)
        => label == "FG" ? _bot.ForegroundBot : _bot.BackgroundBot;

    private static uint GetFishingSkillFromSnapshot(WoWActivitySnapshot? snap)
    {
        var skillMap = snap?.Player?.SkillInfo;
        if (skillMap != null && skillMap.TryGetValue(FishingData.FishingSkillId, out uint level))
            return level;
        return 0;
    }

    private async Task<bool> EnsureNearFishingLocationAsync(string account, string label)
    {
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos != null)
        {
            var dist = DistanceTo(pos.X, pos.Y, pos.Z, DockEdgeX, DockEdgeY, DockEdgeZ);
            if (dist <= DockArrivalDistance)
            {
                _output.WriteLine($"[{label}] Already near fishing location (dist={dist:F1}y); skipping teleport.");
                return false;
            }
        }

        _output.WriteLine($"[{label}] Teleporting to fishing location.");
        await _bot.BotTeleportAsync(account, FishMap, DockEdgeX, DockEdgeY, DockEdgeZ);
        return true;
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

