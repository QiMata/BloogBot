using BotRunner.Combat;
using Xunit;

namespace BotRunner.Tests.Combat;

public class ThreatTrackerTests
{
    [Fact]
    public void RecordDamage_AccumulatesThreat()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 100f);
        tracker.RecordDamage(1, 50f);
        Assert.Equal(150f, tracker.GetThreat(1));
    }

    [Fact]
    public void RecordDamage_TankMultiplier_IncreasesBy30Percent()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 100f, isTank: true);
        Assert.Equal(130f, tracker.GetThreat(1));
    }

    [Fact]
    public void RecordHealing_HalfThreat()
    {
        var tracker = new ThreatTracker();
        tracker.RecordHealing(1, 200f);
        Assert.Equal(100f, tracker.GetThreat(1));
    }

    [Fact]
    public void GetHighestThreat_ReturnsTank()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 500f, isTank: true); // 650
        tracker.RecordDamage(2, 400f);                // 400
        var highest = tracker.GetHighestThreat();
        Assert.NotNull(highest);
        Assert.Equal(1UL, highest!.Value.Guid);
    }

    [Fact]
    public void ShouldThrottle_TrueAt90PercentOfTank()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 100f, isTank: true); // 130 threat
        tracker.RecordDamage(2, 120f);                // 120 threat (92% of 130)
        Assert.True(tracker.ShouldThrottle(2, 1));
    }

    [Fact]
    public void ShouldThrottle_FalseWhenWellBelowTank()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 200f, isTank: true); // 260
        tracker.RecordDamage(2, 50f);                 // 50 (19% of 260)
        Assert.False(tracker.ShouldThrottle(2, 1));
    }

    [Fact]
    public void Reset_ClearsAllThreat()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 100f);
        tracker.Reset();
        Assert.Equal(0f, tracker.GetThreat(1));
        Assert.Null(tracker.GetHighestThreat());
    }

    [Fact]
    public void GetThreatTable_SortedDescending()
    {
        var tracker = new ThreatTracker();
        tracker.RecordDamage(1, 100f);
        tracker.RecordDamage(2, 300f);
        tracker.RecordDamage(3, 200f);
        var table = tracker.GetThreatTable();
        Assert.Equal(3, table.Count);
        Assert.Equal(2UL, table[0].Guid);
        Assert.Equal(3UL, table[1].Guid);
        Assert.Equal(1UL, table[2].Guid);
    }
}
