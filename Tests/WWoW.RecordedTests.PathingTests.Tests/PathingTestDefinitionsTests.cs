using FluentAssertions;
using WWoW.RecordedTests.PathingTests.Models;

namespace WWoW.RecordedTests.PathingTests.Tests;

public class PathingTestDefinitionsTests
{
    [Fact]
    public void All_Returns20Tests()
    {
        PathingTestDefinitions.All.Should().HaveCount(20);
    }

    [Fact]
    public void All_TestNamesAreUnique()
    {
        var names = PathingTestDefinitions.All.Select(t => t.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_EveryTestHasNonEmptyName()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.Name.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_EveryTestHasCategory()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.Category.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_EveryTestHasDescription()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.Description.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_EveryTestHasSetupCommands()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.SetupCommands.Should().NotBeNull().And.NotBeEmpty());
    }

    [Fact]
    public void All_EveryTestHasTeardownCommands()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.TeardownCommands.Should().NotBeNull().And.NotBeEmpty());
    }

    [Fact]
    public void All_EveryTestHasPositiveExpectedDuration()
    {
        PathingTestDefinitions.All.Should().AllSatisfy(t =>
            t.ExpectedDuration.Should().BeGreaterThan(TimeSpan.Zero));
    }

    [Fact]
    public void GetCategories_Returns6Categories()
    {
        PathingTestDefinitions.GetCategories().Should().HaveCount(6);
    }

    [Theory]
    [InlineData("Basic", 3)]
    [InlineData("Transport", 4)]
    [InlineData("Cave", 3)]
    [InlineData("Terrain", 3)]
    [InlineData("Advanced", 3)]
    [InlineData("EdgeCase", 4)]
    public void GetByCategory_ReturnsExpectedCount(string category, int expectedCount)
    {
        PathingTestDefinitions.GetByCategory(category).Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData("basic", 3)]
    [InlineData("TRANSPORT", 4)]
    [InlineData("edgecase", 4)]
    public void GetByCategory_IsCaseInsensitive(string category, int expectedCount)
    {
        PathingTestDefinitions.GetByCategory(category).Should().HaveCount(expectedCount);
    }

    [Fact]
    public void GetByCategory_NonExistent_ReturnsEmpty()
    {
        PathingTestDefinitions.GetByCategory("NonExistent").Should().BeEmpty();
    }

    [Fact]
    public void GetByCategory_PartialName_ReturnsEmpty()
    {
        PathingTestDefinitions.GetByCategory("Edge").Should().BeEmpty();
    }

    [Fact]
    public void GetByName_ExactMatch_ReturnsTest()
    {
        var test = PathingTestDefinitions.GetByName("Northshire_ElwynnForest_ShortDistance");
        test.Name.Should().Be("Northshire_ElwynnForest_ShortDistance");
        test.Category.Should().Be("Basic");
    }

    [Fact]
    public void GetByName_CaseInsensitive_ReturnsTest()
    {
        var test = PathingTestDefinitions.GetByName("northshire_elwynnforest_shortdistance");
        test.Name.Should().Be("Northshire_ElwynnForest_ShortDistance");
    }

    [Fact]
    public void GetByName_NonExistent_ThrowsArgumentException()
    {
        var act = () => PathingTestDefinitions.GetByName("NonExistentTest");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void GetByName_PartialMatch_ThrowsArgumentException()
    {
        var act = () => PathingTestDefinitions.GetByName("Northshire");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void All_CoversMultipleMaps()
    {
        var mapIds = PathingTestDefinitions.All.Select(t => t.MapId).Distinct().ToList();
        mapIds.Should().Contain(0u);
        mapIds.Should().Contain(1u);
        mapIds.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void DeadminesTest_UsesInstanceMapId()
    {
        var test = PathingTestDefinitions.GetByName("CaveNavigation_Deadmines_Entrance_To_VanCleef");
        test.MapId.Should().Be(36u);
    }

    [Fact]
    public void CircularTest_HasSameStartAndEnd()
    {
        var test = PathingTestDefinitions.GetByName("RapidPathRecalculation_Barrens_Oasis_Loop");
        test.StartPosition.X.Should().Be(test.EndPosition!.X);
        test.StartPosition.Y.Should().Be(test.EndPosition!.Y);
    }
}
