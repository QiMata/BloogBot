using BotRunner.Tests.LiveValidation.Dungeons;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;

namespace BotRunner.Tests.LiveValidation.Raids;

/// <summary>
/// Raid instance entry tests. Each test launches 10 bots (1 FG + 9 BG),
/// forms a raid group, travels to the instance entrance, and enters.
/// Uses the same DungeonEntryTestRunner as dungeon tests.
///
/// Run all raids:
///   dotnet test --filter "Namespace~Raids" --configuration Release -v n --blame-hang --blame-hang-timeout 20m
/// </summary>

[Collection(ZulGurubCollection.Name)]
public class ZulGurubTests(ZulGurubFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task ZG_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(AQ20Collection.Name)]
public class AQ20Tests(AQ20Fixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task AQ20_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(MoltenCoreCollection.Name)]
public class MoltenCoreTests(MoltenCoreFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task MC_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(OnyxiasLairCollection.Name)]
public class OnyxiasLairTests(OnyxiasLairFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task ONY_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(BlackwingLairCollection.Name)]
public class BlackwingLairTests(BlackwingLairFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task BWL_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(AQ40Collection.Name)]
public class AQ40Tests(AQ40Fixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task AQ40_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}

[Collection(NaxxramasCollection.Name)]
public class NaxxramasTests(NaxxramasFixture bot, ITestOutputHelper output)
{
    [SkippableFact] public async Task NAXX_RaidFormAndEnter() =>
        await DungeonEntryTestRunner.RunDungeonEntryTest(bot, output);
}
