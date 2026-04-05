using BotRunner.Tasks.Progression;

namespace BotRunner.Tests.Progression;

public class TalentAutoAllocatorTests
{
    [Fact]
    public void GetNextAllocation_ReturnsCorrectTalent()
    {
        // Warrior_Fury at level 10, 0 points spent => first talent
        var alloc = TalentAutoAllocator.GetNextAllocation("Warrior", "Fury", 10, 0);

        Assert.NotNull(alloc);
        Assert.Equal("Booming Voice", alloc!.TalentName);
        Assert.Equal(1, alloc.TabIndex);
    }

    [Fact]
    public void GetNextAllocation_NullWhenAllSpent()
    {
        // Level 10 = 1 available point. 1 spent => nothing to do.
        var alloc = TalentAutoAllocator.GetNextAllocation("Warrior", "Fury", 10, 1);

        Assert.Null(alloc);
    }

    [Fact]
    public void GetNextAllocation_NullBeforeLevel10()
    {
        // Talents start at level 10; level 9 => no talents available
        var alloc = TalentAutoAllocator.GetNextAllocation("Warrior", "Fury", 9, 0);

        Assert.Null(alloc);
    }

    [Fact]
    public void GetPendingAllocations_ReturnsBurst()
    {
        // Level 14 = 5 available points, 0 spent => burst of 5
        var pending = TalentAutoAllocator.GetPendingAllocations("Warrior", "Fury", 14, 0);

        Assert.Equal(5, pending.Count);
        // All first 5 should be Booming Voice (Fury build)
        Assert.All(pending, p => Assert.Equal("Booming Voice", p.TalentName));
    }

    [Fact]
    public void TalentBuilds_ContainsWarriorFury()
    {
        Assert.True(TalentAutoAllocator.TalentBuilds.ContainsKey("Warrior_Fury"));
        Assert.True(TalentAutoAllocator.TalentBuilds["Warrior_Fury"].Count > 0);
    }

    [Fact]
    public void GetNextAllocation_NullForUnknownSpec()
    {
        var alloc = TalentAutoAllocator.GetNextAllocation("Warrior", "Dance", 20, 0);

        Assert.Null(alloc);
    }

    [Fact]
    public void GetNextAllocation_SkipsAlreadySpent()
    {
        // Level 15 = 6 available, 5 spent => index 5 = Cruelty (first Cruelty point)
        var alloc = TalentAutoAllocator.GetNextAllocation("Warrior", "Fury", 15, 5);

        Assert.NotNull(alloc);
        Assert.Equal("Cruelty", alloc!.TalentName);
    }
}
