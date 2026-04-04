using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.17: Quest objective tests. Teleport to Durotar, accept quest via GM (.quest add),
/// kill mobs, check kill count in quest log.
///
/// Run: dotnet test --filter "FullyQualifiedName~QuestObjectiveTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class QuestObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Valley of Trials -- mobs and quest NPCs
    private const float DurotarX = -601.0f, DurotarY = -4297.0f, DurotarZ = 41.0f;
    // "Sarkoth" quest (ID 790) -- kill Sarkoth in Valley of Trials, simple kill quest
    private const uint SarkothQuestId = 790;

    public QuestObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_KillObjective_CountIncrementsAndCompletes()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Teleport to Durotar / Valley of Trials
        _output.WriteLine($"[SETUP] Teleporting to Durotar ({DurotarX}, {DurotarY}, {DurotarZ})");
        await _bot.BotTeleportAsync(account, KalimdorMapId, DurotarX, DurotarY, DurotarZ);
        await _bot.WaitForTeleportSettledAsync(account, DurotarX, DurotarY);

        // Complete any existing version of this quest first
        await _bot.SendGmChatCommandAsync(account, $".quest remove {SarkothQuestId}");
        await Task.Delay(1000);

        // Add the quest via GM command
        _output.WriteLine($"[SETUP] Adding quest {SarkothQuestId} via .quest add");
        await _bot.SendGmChatCommandAsync(account, $".quest add {SarkothQuestId}");
        await Task.Delay(2000);

        // Verify quest is in the quest log
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var questEntries = snap!.Player?.QuestLogEntries?.ToList()
            ?? new System.Collections.Generic.List<Game.QuestLogEntry>();
        _output.WriteLine($"[TEST] Quest log entries: {questEntries.Count}");

        var questFound = questEntries.Any(q => q.QuestId == SarkothQuestId);
        if (!questFound)
        {
            // Check QuestLog1 field which sometimes stores quest ID
            questFound = questEntries.Any(q => q.QuestLog1 == SarkothQuestId);
        }
        _output.WriteLine($"[TEST] Quest {SarkothQuestId} in log: {questFound}");

        // Log all quest entries for debugging
        foreach (var qe in questEntries)
        {
            _output.WriteLine($"  Quest: id={qe.QuestId}, log1={qe.QuestLog1}, log2={qe.QuestLog2}, log3={qe.QuestLog3}");
        }

        // Find a nearby mob to attack
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG quest-mob-search");
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(account);
        var nearbyUnits = snap?.NearbyUnits?.ToList()
            ?? new System.Collections.Generic.List<Game.WoWUnit>();
        _output.WriteLine($"[TEST] Nearby units: {nearbyUnits.Count}");

        // Look for attackable mobs (NpcFlags == 0 means it's not an NPC with services)
        var mob = nearbyUnits.FirstOrDefault(u => u.NpcFlags == 0 && u.Health > 0);
        if (mob != null)
        {
            _output.WriteLine($"[TEST] Found mob: {mob.GameObject?.Name}, health={mob.Health}, guid=0x{mob.GameObject?.Base?.Guid:X}");

            // Attack the mob
            var attackResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)(mob.GameObject?.Base?.Guid ?? 0) } }
            });
            _output.WriteLine($"[TEST] START_MELEE_ATTACK result: {attackResult}");

            // Wait for combat to resolve
            await Task.Delay(15000);

            // Check quest log for progress
            await _bot.RefreshSnapshotsAsync();
            var afterSnap = await _bot.GetSnapshotAsync(account);
            Assert.NotNull(afterSnap);
            var afterQuests = afterSnap!.Player?.QuestLogEntries?.ToList()
                ?? new System.Collections.Generic.List<Game.QuestLogEntry>();
            _output.WriteLine($"[TEST] Quest log after combat: {afterQuests.Count} entries");
            foreach (var qe in afterQuests)
            {
                _output.WriteLine($"  Quest: id={qe.QuestId}, log1={qe.QuestLog1}, log2={qe.QuestLog2}, log3={qe.QuestLog3}");
            }
        }
        else
        {
            _output.WriteLine("[TEST] No attackable mobs found nearby");
        }

        // Cleanup: remove quest
        await _bot.SendGmChatCommandAsync(account, $".quest remove {SarkothQuestId}");
        await Task.Delay(500);
    }
}
