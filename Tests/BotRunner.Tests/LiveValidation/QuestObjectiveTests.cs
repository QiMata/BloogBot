using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed quest objective coverage. SHODAN stages quest state and
/// position; the BG BotRunner target receives only combat action dispatch.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class QuestObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint SarkothQuestId = 790;

    public QuestObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_KillObjective_CountIncrementsAndCompletes()
    {
        await QuestTestSupport.EnsureQuestSettingsAsync(_bot, _output);
        var target = QuestTestSupport.ResolveBgActionTarget(_bot, _output);

        var staged = await _bot.StageBotRunnerAtDurotarQuestObjectiveAreaAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected to stage near Durotar quest objective mobs.");

        var absent = await _bot.StageBotRunnerQuestAbsentAsync(
            target.AccountName,
            target.RoleLabel,
            SarkothQuestId);
        Assert.True(absent, $"{target.RoleLabel}: quest {SarkothQuestId} should be absent before staging.");

        try
        {
            var added = await _bot.StageBotRunnerQuestAddedAsync(
                target.AccountName,
                target.RoleLabel,
                SarkothQuestId);
            Assert.True(added, $"{target.RoleLabel}: quest {SarkothQuestId} should appear in the quest log.");

            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);

            var questEntries = snap!.Player?.QuestLogEntries?.ToList() ?? [];
            Assert.True(
                questEntries.Any(q => q.QuestLog1 == SarkothQuestId || q.QuestId == SarkothQuestId),
                $"{target.RoleLabel}: quest {SarkothQuestId} should be visible before combat.");

            await _bot.WaitForNearbyUnitsPopulatedAsync(
                target.AccountName,
                timeoutMs: 5000,
                progressLabel: $"{target.RoleLabel} quest-mob-search");
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(target.AccountName);
            var mob = snap?.NearbyUnits.FirstOrDefault(unit => unit.NpcFlags == 0 && unit.Health > 0);

            global::Tests.Infrastructure.Skip.If(
                mob == null,
                $"{target.RoleLabel}: no attackable quest-area mob found after Shodan staging.");

            var mobGuid = mob!.GameObject?.Base?.Guid ?? 0UL;
            _output.WriteLine(
                $"[QUEST] Found mob: {mob.GameObject?.Name}, health={mob.Health}, guid=0x{mobGuid:X}");
            Assert.True(mobGuid != 0, $"{target.RoleLabel}: mob should have a valid GUID.");

            var attackResult = await QuestTestSupport.SendQuestActionAsync(
                _bot,
                _output,
                target,
                QuestTestSupport.MakeStartMeleeAttack(mobGuid),
                "StartMeleeAttack",
                timeoutSeconds: 12);
            Assert.Equal(ResponseResult.Success, attackResult);

            await Task.Delay(12000);
            await _bot.RefreshSnapshotsAsync();
            var afterSnap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(afterSnap);
            var afterQuests = afterSnap!.Player?.QuestLogEntries?.ToList() ?? [];
            _output.WriteLine($"[QUEST] Quest log after combat: {afterQuests.Count} entries");
            foreach (var entry in afterQuests.Take(5))
            {
                _output.WriteLine(
                    $"  Quest: id={entry.QuestId}, log1={entry.QuestLog1}, log2={entry.QuestLog2}, log3={entry.QuestLog3}");
            }
        }
        finally
        {
            await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, SarkothQuestId);
        }
    }
}
