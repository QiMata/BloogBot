using BotRunner.Combat;
using Xunit;

namespace BotRunner.Tests.Combat;

public class RaidCooldownCoordinatorTests
{
    [Fact]
    public void IsSafeToUse_TrueWhenNoPriorUsage()
    {
        var coord = new RaidCooldownCoordinator();
        Assert.True(coord.IsSafeToUse("Innervate"));
    }

    [Fact]
    public void IsSafeToUse_FalseImmediatelyAfterUsage()
    {
        var coord = new RaidCooldownCoordinator();
        coord.RecordUsage("Innervate", 1, 20f, 360f);
        Assert.False(coord.IsSafeToUse("Innervate"));
    }

    [Fact]
    public void GetNextAvailableOwner_ReturnsFirstWhenNoHistory()
    {
        var coord = new RaidCooldownCoordinator();
        var owners = new ulong[] { 10, 20, 30 };
        var next = coord.GetNextAvailableOwner("Innervate", owners);
        Assert.Equal(10UL, next);
    }

    [Fact]
    public void GetNextAvailableOwner_ReturnsLeastRecentUser()
    {
        var coord = new RaidCooldownCoordinator();
        coord.RecordUsage("Innervate", 10, 20f, 360f);
        System.Threading.Thread.Sleep(10);
        coord.RecordUsage("Innervate", 20, 20f, 360f);

        var owners = new ulong[] { 10, 20, 30 };
        var next = coord.GetNextAvailableOwner("Innervate", owners);
        // 30 has never used it, so should be first (MinValue ordering)
        Assert.Equal(30UL, next);
    }

    [Fact]
    public void KnownCooldowns_ContainsExpectedEntries()
    {
        Assert.True(RaidCooldownCoordinator.KnownCooldowns.ContainsKey("Innervate"));
        Assert.True(RaidCooldownCoordinator.KnownCooldowns.ContainsKey("Power Infusion"));
        Assert.True(RaidCooldownCoordinator.KnownCooldowns.ContainsKey("Rebirth"));
        Assert.Equal(7, RaidCooldownCoordinator.KnownCooldowns.Count);
    }
}
