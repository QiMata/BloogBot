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
/// Starter area quest integration tests — tests actual quest accept/complete via NPC interaction.
///
/// Uses Valley of Trials (Durotar starter area):
///   Quest 4641 "Your Place In The World" — talk to Kaltunk, walk ~65y, turn in at Gornek.
///   No combat, no items, no prerequisites. Any Horde race.
///
/// Flow:
///   1) Teleport to Valley of Trials near Kaltunk (quest giver)
///   2) Find Kaltunk in snapshot (NPC with UNIT_NPC_FLAG_QUESTGIVER)
///   3) AcceptQuest(npcGuid, 4641) via ActionType dispatch
///   4) Verify quest appears in snapshot
///   5) Teleport near Gornek (turn-in NPC)
///   6) CompleteQuest(npcGuid, 4641) via ActionType dispatch
///   7) Verify quest removed from snapshot
///
/// This suite is BG-only under the overhaul plan. FG remains useful as a packet
/// reference, but the asserted quest behavior belongs to the headless client.
///
/// Run: dotnet test --filter "FullyQualifiedName~StarterQuestTests" --configuration Release
/// </summary>
[Collection(BgOnlyValidationCollection.Name)]
public class StarterQuestTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Quest 4641 "Your Place In The World" — talk-to-NPC, no combat, no prerequisites
    private const uint TestQuestId = 4641;

    // Kaltunk (quest giver) — entry 10176 at (-607.43, -4251.33, 39.04) map 1
    private const int KaltunkEntry = 10176;
    private const float KaltunkX = -607.43f, KaltunkY = -4251.33f, KaltunkZ = 39.04f;

    // Gornek (turn-in NPC) — entry 3143 at (-600.13, -4186.19, 41.27) map 1
    private const int GornekEntry = 3143;
    private const float GornekX = -600.13f, GornekY = -4186.19f, GornekZ = 41.27f;

    private const int MapId = 1; // Kalimdor

    public StarterQuestTests(BgOnlyBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Quest_AcceptAndTurnIn_StarterQuest()
    {
        _output.WriteLine("[BG-ONLY] Running starter quest validation on the headless bot.");
        await RunStarterQuestScenario(_bot.BgAccountName!, "BG");
    }

    private async Task RunStarterQuestScenario(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        // Pre-flight: teleport to Orgrimmar first to stabilize zone state.
        // In full suite runs, prior tests may leave the bot far away causing
        // long cross-zone teleport + FG client area loading delays.
        _output.WriteLine($"  [{label}] Pre-flight: teleporting to Orgrimmar safe zone.");
        await _bot.BotTeleportAsync(account, MapId, 1629f, -4373f, 12f);
        await _bot.WaitForTeleportSettledAsync(account, 1629f, -4373f);

        await EnsureQuestAbsentAsync(account, label, TestQuestId);

        try
        {
            // === Step 1: Teleport near Kaltunk (quest giver) ===
            _output.WriteLine($"  [{label}] Step 1: Teleporting to Valley of Trials near Kaltunk");
            await _bot.BotTeleportAsync(account, MapId, KaltunkX, KaltunkY, KaltunkZ + 3);
            await _bot.WaitForTeleportSettledAsync(account, KaltunkX, KaltunkY);

            // === Step 2: Find Kaltunk in nearby units ===
            var kaltunkGuid = await FindNpcByEntryAsync(account, label, KaltunkEntry, "Kaltunk");
            Assert.True(kaltunkGuid != 0, $"[{label}] Kaltunk (entry {KaltunkEntry}) should be visible after teleporting to Valley of Trials.");

            // === Step 3: Accept quest via ActionType dispatch (with retry) ===
            // Server may briefly reject the accept if .quest remove hasn't fully propagated.
            bool questAdded = false;
            for (int acceptAttempt = 0; acceptAttempt < 2 && !questAdded; acceptAttempt++)
            {
                if (acceptAttempt > 0)
                    _output.WriteLine($"  [{label}] Retrying AcceptQuest (attempt {acceptAttempt + 1})...");

                _output.WriteLine($"  [{label}] Step 3: Accepting quest {TestQuestId} from Kaltunk (GUID=0x{kaltunkGuid:X})");
                var acceptResult = await _bot.SendActionAsync(account, new ActionMessage
                {
                    ActionType = ActionType.AcceptQuest,
                    Parameters =
                    {
                        new RequestParameter { LongParam = (long)kaltunkGuid },
                        new RequestParameter { IntParam = (int)TestQuestId }
                    }
                });
                _output.WriteLine($"  [{label}] AcceptQuest dispatch result: {acceptResult}");

                // === Step 4: Verify quest appears in snapshot ===
                questAdded = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: true, TimeSpan.FromSeconds(10));
                if (!questAdded && acceptAttempt == 0)
                {
                    _output.WriteLine($"  [{label}] Quest not in snapshot after first accept — re-removing and retrying.");
                    await _bot.BotSelectSelfAsync(account);
                    await Task.Delay(300);
                    await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {TestQuestId}", captureResponse: false, delayMs: 1500);
                }
            }

            if (!questAdded)
            {
                await _bot.RefreshSnapshotsAsync();
                var diagSnap = await _bot.GetSnapshotAsync(account);
                var questEntries = diagSnap?.Player?.QuestLogEntries?.ToList() ?? [];
                _output.WriteLine($"  [{label}] DIAG: Quest {TestQuestId} not found. Quest log has {questEntries.Count} entries:");
                foreach (var qe in questEntries.Take(5))
                    _output.WriteLine($"    QuestLog1={qe.QuestLog1} QuestLog2={qe.QuestLog2}");
            }
            Assert.True(questAdded, $"[{label}] Quest {TestQuestId} should appear in snapshot after AcceptQuest action.");
            _output.WriteLine($"  [{label}] Quest {TestQuestId} confirmed in quest log.");

            // === Step 5: Teleport near Gornek (turn-in NPC) ===
            _output.WriteLine($"  [{label}] Step 5: Teleporting near Gornek for quest turn-in");
            await _bot.BotTeleportAsync(account, MapId, GornekX, GornekY, GornekZ + 3);
            await _bot.WaitForTeleportSettledAsync(account, GornekX, GornekY);

            // === Step 6: Find Gornek in nearby units ===
            var gornekGuid = await FindNpcByEntryAsync(account, label, GornekEntry, "Gornek");
            Assert.True(gornekGuid != 0, $"[{label}] Gornek (entry {GornekEntry}) should be visible after teleporting.");

            // === Step 7: Complete quest via ActionType dispatch ===
            _output.WriteLine($"  [{label}] Step 7: Completing quest {TestQuestId} at Gornek (GUID=0x{gornekGuid:X})");
            var completeResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.CompleteQuest,
                Parameters =
                {
                    new RequestParameter { LongParam = (long)gornekGuid },
                    new RequestParameter { IntParam = (int)TestQuestId }
                }
            });
            _output.WriteLine($"  [{label}] CompleteQuest dispatch result: {completeResult}");
            Assert.Equal(ResponseResult.Success, completeResult);

            // === Step 8: Verify quest removed from snapshot ===
            var questRemoved = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: false, TimeSpan.FromSeconds(10));
            Assert.True(questRemoved, $"[{label}] Quest {TestQuestId} should be absent from snapshot after completion.");
            _output.WriteLine($"  [{label}] Quest {TestQuestId} confirmed completed and removed from quest log.");
        }
        finally
        {
            // Cleanup: remove quest if still present (e.g. if assertion failed mid-scenario)
            await CleanupQuestAsync(account, label, TestQuestId);

            // Teleport back to safe zone (Orgrimmar)
            await _bot.BotTeleportAsync(account, MapId, 1629f, -4373f, 12f);
        }
    }

    private async Task<ulong> FindNpcByEntryAsync(string account, string label, int npcEntry, string npcName)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var units = snap?.NearbyUnits?.ToList() ?? [];

            foreach (var unit in units)
            {
                var entry = unit.GameObject?.Entry ?? 0;
                if (entry == npcEntry)
                {
                    var guid = unit.GameObject?.Base?.Guid ?? 0;
                    _output.WriteLine($"  [{label}] Found {npcName}: GUID=0x{guid:X} Entry={entry} NpcFlags={unit.NpcFlags}");
                    return guid;
                }
            }

            if (attempt < 4)
            {
                _output.WriteLine($"  [{label}] {npcName} not found on attempt {attempt + 1}, retrying in 1s...");
                await Task.Delay(1000);
            }
        }

        // Log all visible units for debugging
        await _bot.RefreshSnapshotsAsync();
        var debugSnap = await _bot.GetSnapshotAsync(account);
        var allUnits = debugSnap?.NearbyUnits?.Take(15).ToList() ?? [];
        _output.WriteLine($"  [{label}] {npcName} not found. Visible units ({allUnits.Count}):");
        foreach (var u in allUnits)
        {
            var g = u.GameObject?.Base?.Guid ?? 0;
            _output.WriteLine($"    [0x{g:X8}] Entry={u.GameObject?.Entry} {u.GameObject?.Name} NpcFlags={u.NpcFlags}");
        }
        return 0;
    }

    private async Task EnsureQuestAbsentAsync(string account, string label, uint questId)
    {
        // Always force-remove: clears both quest log AND completed-quest history.
        // Without this, a quest completed in a prior run cannot be re-accepted.
        _output.WriteLine($"  [{label}] Force-removing quest {questId} (clears completed status).");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(500);
        await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {questId}", captureResponse: true, delayMs: 2000);
        // Extra delay: server must propagate removal to NPC quest offering lists
        await Task.Delay(1000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (HasQuest(snap, questId))
        {
            var removed = await WaitForQuestPresenceAsync(account, questId, shouldExist: false, TimeSpan.FromSeconds(8));
            Assert.True(removed, $"[{label}] Quest {questId} should be removed during setup.");
        }
    }

    private async Task<bool> WaitForQuestPresenceAsync(string account, uint questId, bool shouldExist, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (HasQuest(snap, questId) == shouldExist)
                return true;
            await Task.Delay(500);
        }
        return false;
    }

    private async Task CleanupQuestAsync(string account, string label, uint questId)
    {
        try
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (HasQuest(snap, questId))
            {
                _output.WriteLine($"  [{label}] Cleanup: removing quest {questId} left over from failed scenario.");
                await _bot.BotSelectSelfAsync(account);
                await Task.Delay(300);
                await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {questId}", captureResponse: false, delayMs: 500);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  [{label}] Cleanup warning: {ex.Message}");
        }
    }

    private static bool HasQuest(WoWActivitySnapshot? snap, uint questId)
        => snap?.Player?.QuestLogEntries?.Any(q => q.QuestLog1 == questId) == true;
}
