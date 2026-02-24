using System;
using System.Collections.Generic;

namespace Tests.Infrastructure;

/// <summary>
/// xUnit Traits for categorizing tests in the WWoW test suite.
/// 
/// Usage:
///   [Trait(TestCategories.Category, TestCategories.Integration)]
///   [Trait(TestCategories.RequiresService, TestCategories.WoWServer)]
///   public void MyIntegrationTest() { }
/// 
/// Running tests by category:
///   dotnet test --filter "Category=Unit"                     # Unit tests only
///   dotnet test --filter "Category=Integration"              # Integration tests only
///   dotnet test --filter "RequiresService=WoWServer"         # Tests requiring WoW server
///   dotnet test --filter "RequiresService=PathfindingService" # Tests requiring pathfinding
///   dotnet test --filter "Category!=Integration"             # Exclude integration tests
/// </summary>
public static class TestCategories
{
    // Trait names
    public const string Category = "Category";
    public const string RequiresService = "RequiresService";
    public const string Duration = "Duration";
    public const string Feature = "Feature";

    // Category values
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string EndToEnd = "EndToEnd";
    public const string Performance = "Performance";
    public const string RequiresInfrastructure = "RequiresInfrastructure";

    // Service values
    public const string WoWServer = "WoWServer";
    public const string PathfindingService = "PathfindingService";
    public const string MySQL = "MySQL";
    public const string MangosStack = "MangosStack";
    public const string AllServices = "AllServices";

    // Duration values
    public const string Fast = "Fast";      // < 100ms
    public const string Medium = "Medium";  // 100ms - 5s
    public const string Slow = "Slow";      // > 5s

    // Feature values
    public const string Movement = "Movement";
    public const string Combat = "Combat";
    public const string Inventory = "Inventory";
    public const string Pathfinding = "Pathfinding";
    public const string Protocol = "Protocol";
    public const string Connection = "Connection";
    public const string NativeDll = "NativeDll";
}

/// <summary>
/// Convenience attributes for common test categories.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class UnitTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Unit)];
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class IntegrationTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration)];
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresWoWServerAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        =>
        [
            new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration),
            new KeyValuePair<string, string>(TestCategories.RequiresService, TestCategories.WoWServer)
        ];
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresPathfindingServiceAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        =>
        [
            new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration),
            new KeyValuePair<string, string>(TestCategories.RequiresService, TestCategories.PathfindingService)
        ];
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresMangosStackAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        =>
        [
            new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration),
            new KeyValuePair<string, string>(TestCategories.RequiresService, TestCategories.MangosStack)
        ];
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresAllServicesAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        =>
        [
            new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration),
            new KeyValuePair<string, string>(TestCategories.RequiresService, TestCategories.AllServices)
        ];
}

/// <summary>
/// Marks tests that require infrastructure (WoW process, StateManager, etc.).
/// Filter: dotnet test --filter "Category!=RequiresInfrastructure"
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequiresInfrastructureAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>(TestCategories.Category, TestCategories.RequiresInfrastructure)];
}
