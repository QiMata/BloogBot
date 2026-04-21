using System;
using System.Linq;
using System.Threading.Tasks;
using GameData.Core.Constants;
using GameData.Core.Enums;
using Xunit;

namespace BotRunner.Tests;

public sealed class RaceDimensionsConcurrencyTests
{
    [Fact]
    public void GetCapsuleForRace_ConcurrentFirstCalls_DoNotCorruptCaches()
    {
        // Regression guard: 20-bot battleground fixtures have each bot worker's
        // MovementController.RunPhysics hit RaceDimensions.GetCapsuleForRace on
        // its first physics tick. The old double-check used a non-volatile
        // isInitialized flag with no lock, so multiple threads would race into
        // the dictionary allocations, corrupt the shared cache, and throw
        // "Operations that change non-concurrent collections must have
        // exclusive access" from Dictionary.TryInsert on WSG/AV/AB runs.

        var races = new[]
        {
            Race.Human, Race.Dwarf, Race.NightElf, Race.Gnome,
            Race.Orc, Race.Undead, Race.Tauren, Race.Troll,
        };
        var genders = new[] { Gender.Male, Gender.Female };
        var pairs = races.SelectMany(r => genders.Select(g => (r, g))).ToArray();

        var exceptions = Parallel.ForEach(
            Enumerable.Range(0, 512),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            i =>
            {
                var (race, gender) = pairs[i % pairs.Length];
                var capsule = RaceDimensions.GetCapsuleForRace(race, gender);
                Assert.True(capsule.radius > 0f);
                Assert.True(capsule.height > 0f);
            });

        Assert.True(exceptions.IsCompleted);
    }
}
