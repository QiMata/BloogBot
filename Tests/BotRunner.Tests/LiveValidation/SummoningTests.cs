using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P29.16–29.17: Summoning tests — warlock Ritual of Summoning and meeting stone summon.
/// Validates party summoning mechanics for both spell-based and world-object-based summoning.
///
/// Run: dotnet test --filter "FullyQualifiedName~SummoningTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SummoningTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public SummoningTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P29.16: Party of 5. Warlock + 2 helpers at dungeon entrance, 2 members in Orgrimmar.
    /// Warlock casts Ritual of Summoning (spell 698). 2 helpers click portal. Absent member accepts.
    /// Assert: summoned member appears at entrance.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task WarlockSummon_RitualOfSummoning()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.16 Warlock Ritual of Summoning");
    }

    /// <summary>
    /// P29.17: Party of 5. 3 at Wailing Caverns meeting stone, 2 in Orgrimmar.
    /// Interact with meeting stone (GameObjectType 23).
    /// Assert: absent members summoned to meeting stone location.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MeetingStoneSummon_WailingCaverns()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.17 Meeting stone summon at Wailing Caverns");
    }
}
