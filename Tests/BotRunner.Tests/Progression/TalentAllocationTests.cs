using BotRunner.Combat;
using Xunit;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Tests that TalentBuildDefinitions resolves builds by name correctly,
/// verifying the configurable talent build system.
/// </summary>
public class TalentAllocationTests
{
    [Fact]
    public void GetBuild_WarriorArms_ReturnsBuildOrder()
    {
        var build = TalentBuildDefinitions.GetBuild("WarriorArms");
        Assert.NotNull(build);
        Assert.NotEmpty(build);
    }

    [Fact]
    public void GetBuild_WarriorFury_ReturnsBuildOrder()
    {
        var build = TalentBuildDefinitions.GetBuild("WarriorFury");
        Assert.NotNull(build);
        Assert.NotEmpty(build);
    }

    [Fact]
    public void GetBuild_WarriorProtection_ReturnsBuildOrder()
    {
        var build = TalentBuildDefinitions.GetBuild("WarriorProtection");
        Assert.NotNull(build);
        Assert.NotEmpty(build);
    }

    [Fact]
    public void GetBuild_MageFrost_ReturnsBuildOrder()
    {
        var build = TalentBuildDefinitions.GetBuild("MageFrost");
        Assert.NotNull(build);
        Assert.NotEmpty(build);
    }

    [Fact]
    public void GetBuild_PriestShadow_ReturnsBuildOrder()
    {
        var build = TalentBuildDefinitions.GetBuild("PriestShadow");
        Assert.NotNull(build);
        Assert.NotEmpty(build);
    }

    [Fact]
    public void GetBuild_NonExistent_ReturnsNull()
    {
        var build = TalentBuildDefinitions.GetBuild("NonExistentBuild");
        Assert.Null(build);
    }

    [Fact]
    public void GetBuild_BuildsHaveValidTabAndPos()
    {
        var build = TalentBuildDefinitions.GetBuild("WarriorArms");
        Assert.NotNull(build);
        foreach (var (tab, pos) in build)
        {
            Assert.InRange(tab, 0u, 2u); // 3 talent tabs (0-2)
            Assert.True(pos < 30, $"Talent position {pos} seems too high");
        }
    }

    [Fact]
    public void GetBuild_DifferentSpecsSameClass_DifferentBuilds()
    {
        var arms = TalentBuildDefinitions.GetBuild("WarriorArms");
        var fury = TalentBuildDefinitions.GetBuild("WarriorFury");
        Assert.NotNull(arms);
        Assert.NotNull(fury);
        // Different specs should allocate to different primary tabs
        var armsMainTab = arms[0].tab;
        var furyMainTab = fury[0].tab;
        Assert.NotEqual(armsMainTab, furyMainTab);
    }
}
