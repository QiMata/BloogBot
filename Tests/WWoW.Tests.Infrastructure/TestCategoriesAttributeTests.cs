namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="TestCategories"/> constants.
/// Verifies that all expected category/trait strings are present and non-empty.
/// </summary>
[UnitTest]
public class TestCategoriesConstantsTests
{
    // ======== Trait Name Constants ========

    [Fact]
    public void Category_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(TestCategories.Category));
    }

    [Fact]
    public void RequiresService_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(TestCategories.RequiresService));
    }

    [Fact]
    public void Duration_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(TestCategories.Duration));
    }

    [Fact]
    public void Feature_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(TestCategories.Feature));
    }

    // ======== Category Value Constants ========

    [Fact]
    public void Unit_Equals_Unit()
    {
        Assert.Equal("Unit", TestCategories.Unit);
    }

    [Fact]
    public void Integration_Equals_Integration()
    {
        Assert.Equal("Integration", TestCategories.Integration);
    }

    [Fact]
    public void EndToEnd_Equals_EndToEnd()
    {
        Assert.Equal("EndToEnd", TestCategories.EndToEnd);
    }

    [Fact]
    public void Performance_Equals_Performance()
    {
        Assert.Equal("Performance", TestCategories.Performance);
    }

    // ======== Service Value Constants ========

    [Fact]
    public void WoWServer_Equals_WoWServer()
    {
        Assert.Equal("WoWServer", TestCategories.WoWServer);
    }

    [Fact]
    public void PathfindingService_Equals_PathfindingService()
    {
        Assert.Equal("PathfindingService", TestCategories.PathfindingService);
    }

    [Fact]
    public void AllServices_Equals_AllServices()
    {
        Assert.Equal("AllServices", TestCategories.AllServices);
    }

    // ======== Duration Value Constants ========

    [Fact]
    public void Fast_Equals_Fast()
    {
        Assert.Equal("Fast", TestCategories.Fast);
    }

    [Fact]
    public void Medium_Equals_Medium()
    {
        Assert.Equal("Medium", TestCategories.Medium);
    }

    [Fact]
    public void Slow_Equals_Slow()
    {
        Assert.Equal("Slow", TestCategories.Slow);
    }

    // ======== Feature Value Constants ========

    [Fact]
    public void Movement_Equals_Movement()
    {
        Assert.Equal("Movement", TestCategories.Movement);
    }

    [Fact]
    public void Combat_Equals_Combat()
    {
        Assert.Equal("Combat", TestCategories.Combat);
    }

    [Fact]
    public void Inventory_Equals_Inventory()
    {
        Assert.Equal("Inventory", TestCategories.Inventory);
    }

    [Fact]
    public void Pathfinding_Equals_Pathfinding()
    {
        Assert.Equal("Pathfinding", TestCategories.Pathfinding);
    }

    [Fact]
    public void Protocol_Equals_Protocol()
    {
        Assert.Equal("Protocol", TestCategories.Protocol);
    }

    [Fact]
    public void Connection_Equals_Connection()
    {
        Assert.Equal("Connection", TestCategories.Connection);
    }
}

/// <summary>
/// Tests for custom xUnit trait attributes: <see cref="UnitTestAttribute"/>,
/// <see cref="IntegrationTestAttribute"/>, <see cref="RequiresWoWServerAttribute"/>,
/// <see cref="RequiresPathfindingServiceAttribute"/>, and <see cref="RequiresAllServicesAttribute"/>.
/// </summary>
[UnitTest]
public class TestCategoriesAttributeTests
{
    // ======== UnitTestAttribute ========

    [Fact]
    public void UnitTestAttribute_ReturnsUnitCategoryTrait()
    {
        var attr = new UnitTestAttribute();
        var traits = attr.GetTraits();

        Assert.Single(traits);
        var trait = traits.First();
        Assert.Equal(TestCategories.Category, trait.Key);
        Assert.Equal(TestCategories.Unit, trait.Value);
    }

    [Fact]
    public void UnitTestAttribute_IsApplicableToMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(UnitTestAttribute), typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    // ======== IntegrationTestAttribute ========

    [Fact]
    public void IntegrationTestAttribute_ReturnsIntegrationCategoryTrait()
    {
        var attr = new IntegrationTestAttribute();
        var traits = attr.GetTraits();

        Assert.Single(traits);
        var trait = traits.First();
        Assert.Equal(TestCategories.Category, trait.Key);
        Assert.Equal(TestCategories.Integration, trait.Value);
    }

    [Fact]
    public void IntegrationTestAttribute_IsApplicableToMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(IntegrationTestAttribute), typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
    }

    // ======== RequiresWoWServerAttribute ========

    [Fact]
    public void RequiresWoWServerAttribute_ReturnsTwoTraits()
    {
        var attr = new RequiresWoWServerAttribute();
        var traits = attr.GetTraits();

        Assert.Equal(2, traits.Count);
    }

    [Fact]
    public void RequiresWoWServerAttribute_IncludesIntegrationCategory()
    {
        var attr = new RequiresWoWServerAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.Category && t.Value == TestCategories.Integration);
    }

    [Fact]
    public void RequiresWoWServerAttribute_IncludesWoWServerService()
    {
        var attr = new RequiresWoWServerAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.RequiresService && t.Value == TestCategories.WoWServer);
    }

    // ======== RequiresPathfindingServiceAttribute ========

    [Fact]
    public void RequiresPathfindingServiceAttribute_ReturnsTwoTraits()
    {
        var attr = new RequiresPathfindingServiceAttribute();
        var traits = attr.GetTraits();

        Assert.Equal(2, traits.Count);
    }

    [Fact]
    public void RequiresPathfindingServiceAttribute_IncludesIntegrationCategory()
    {
        var attr = new RequiresPathfindingServiceAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.Category && t.Value == TestCategories.Integration);
    }

    [Fact]
    public void RequiresPathfindingServiceAttribute_IncludesPathfindingService()
    {
        var attr = new RequiresPathfindingServiceAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.RequiresService && t.Value == TestCategories.PathfindingService);
    }

    // ======== RequiresAllServicesAttribute ========

    [Fact]
    public void RequiresAllServicesAttribute_ReturnsTwoTraits()
    {
        var attr = new RequiresAllServicesAttribute();
        var traits = attr.GetTraits();

        Assert.Equal(2, traits.Count);
    }

    [Fact]
    public void RequiresAllServicesAttribute_IncludesIntegrationCategory()
    {
        var attr = new RequiresAllServicesAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.Category && t.Value == TestCategories.Integration);
    }

    [Fact]
    public void RequiresAllServicesAttribute_IncludesAllServices()
    {
        var attr = new RequiresAllServicesAttribute();
        var traits = attr.GetTraits();

        Assert.Contains(traits,
            t => t.Key == TestCategories.RequiresService && t.Value == TestCategories.AllServices);
    }
}
