using BotRunner.Combat;
using Xunit;

namespace BotRunner.Tests.Combat;

public class RaidRoleAssignmentTests
{
    [Fact]
    public void SetMainTank_SetsGuidAndRole()
    {
        var roles = new RaidRoleAssignment();
        roles.SetMainTank(42);
        Assert.Equal(42UL, roles.MainTankGuid);
        Assert.Equal(RaidRoleAssignment.RaidRole.Tank, roles.GetRole(42));
    }

    [Fact]
    public void SetMainAssist_SetsGuid()
    {
        var roles = new RaidRoleAssignment();
        roles.SetMainAssist(99);
        Assert.Equal(99UL, roles.MainAssistGuid);
    }

    [Fact]
    public void GetRole_DefaultsToDps()
    {
        var roles = new RaidRoleAssignment();
        Assert.Equal(RaidRoleAssignment.RaidRole.DPS, roles.GetRole(999));
    }

    [Fact]
    public void AutoAssignMainTank_PicksFirstTank()
    {
        var roles = new RaidRoleAssignment();
        roles.AssignRole(10, RaidRoleAssignment.RaidRole.Healer);
        roles.AssignRole(20, RaidRoleAssignment.RaidRole.Tank);
        roles.AssignRole(30, RaidRoleAssignment.RaidRole.DPS);
        roles.AutoAssignMainTank();
        Assert.Equal(20UL, roles.MainTankGuid);
    }

    [Fact]
    public void GetPlayersWithRole_FiltersCorrectly()
    {
        var roles = new RaidRoleAssignment();
        roles.AssignRole(1, RaidRoleAssignment.RaidRole.Healer);
        roles.AssignRole(2, RaidRoleAssignment.RaidRole.Healer);
        roles.AssignRole(3, RaidRoleAssignment.RaidRole.DPS);
        var healers = roles.GetPlayersWithRole(RaidRoleAssignment.RaidRole.Healer);
        Assert.Equal(2, healers.Count);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var roles = new RaidRoleAssignment();
        roles.SetMainTank(1);
        roles.SetMainAssist(2);
        roles.AssignRole(3, RaidRoleAssignment.RaidRole.Healer);
        roles.Reset();
        Assert.Equal(0UL, roles.MainTankGuid);
        Assert.Equal(0UL, roles.MainAssistGuid);
        Assert.Equal(RaidRoleAssignment.RaidRole.DPS, roles.GetRole(3));
    }
}
