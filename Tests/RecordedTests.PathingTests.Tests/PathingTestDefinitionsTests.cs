using FluentAssertions;
using RecordedTests.PathingTests.Models;
using System;
using System.Linq;

namespace RecordedTests.PathingTests.Tests;

public class PathingTestDefinitionsTests
{
    // ================================================================
    // Static definitions validation
    // ================================================================

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

    // ================================================================
    // Category counts
    // ================================================================

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
        // "Edge" should NOT match "EdgeCase"
        PathingTestDefinitions.GetByCategory("Edge").Should().BeEmpty();
    }

    // ================================================================
    // GetByName
    // ================================================================

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
        // "Northshire" should NOT match "Northshire_ElwynnForest_ShortDistance"
        var act = () => PathingTestDefinitions.GetByName("Northshire");
        act.Should().Throw<ArgumentException>();
    }

    // ================================================================
    // Transport mode distribution
    // ================================================================

    [Fact]
    public void All_TransportTests_UseCorrectModes()
    {
        var boatTests = PathingTestDefinitions.All.Where(t => t.Transport == TransportMode.Boat).ToList();
        var zeppelinTests = PathingTestDefinitions.All.Where(t => t.Transport == TransportMode.Zeppelin).ToList();
        var noneTests = PathingTestDefinitions.All.Where(t => t.Transport == TransportMode.None).ToList();

        boatTests.Should().HaveCountGreaterThanOrEqualTo(2);
        zeppelinTests.Should().HaveCountGreaterThanOrEqualTo(2);
        noneTests.Should().HaveCountGreaterThanOrEqualTo(10);
    }

    // ================================================================
    // MapId coverage
    // ================================================================

    [Fact]
    public void All_CoversMultipleMaps()
    {
        var mapIds = PathingTestDefinitions.All.Select(t => t.MapId).Distinct().ToList();
        // Should cover at least map 0 (Azeroth), map 1 (Kalimdor), and an instance
        mapIds.Should().Contain(0u);
        mapIds.Should().Contain(1u);
        mapIds.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    // ================================================================
    // Specific test definitions
    // ================================================================

    [Fact]
    public void DeadminesTest_UsesInstanceMapId()
    {
        var test = PathingTestDefinitions.GetByName("CaveNavigation_Deadmines_Entrance_To_VanCleef");
        test.MapId.Should().Be(36u);
    }

    [Fact]
    public void WailingCavernsTest_UsesInstanceMapId()
    {
        var test = PathingTestDefinitions.GetByName("CaveNavigation_WailingCaverns_Spiral");
        test.MapId.Should().Be(43u);
    }

    [Fact]
    public void CrossContinentTest_HasIntermediateWaypoint()
    {
        var test = PathingTestDefinitions.GetByName("CrossContinent_Kalimdor_To_EasternKingdoms");
        test.IntermediateWaypoint.Should().NotBeNullOrEmpty();
        test.Transport.Should().Be(TransportMode.Boat);
    }

    [Fact]
    public void CircularTest_HasSameStartAndEnd()
    {
        var test = PathingTestDefinitions.GetByName("RapidPathRecalculation_Barrens_Oasis_Loop");
        test.StartPosition.X.Should().Be(test.EndPosition!.X);
        test.StartPosition.Y.Should().Be(test.EndPosition!.Y);
    }
}
