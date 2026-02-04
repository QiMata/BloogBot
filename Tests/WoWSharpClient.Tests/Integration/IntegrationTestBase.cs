namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Base class for integration tests that require external services.
/// Provides automatic skip functionality when services are unavailable.
/// 
/// Usage:
/// 1. Inherit from this class
/// 2. Use [Trait("Category", "Integration")] on your test class
/// 3. Call EnsureServicesAvailable() or use Skip.When() pattern
/// 
/// Run integration tests only:
///   dotnet test --filter "Category=Integration"
/// 
/// Run unit tests only (exclude integration):
///   dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected LiveServerFixture Fixture { get; }

    protected IntegrationTestBase(LiveServerFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        await Fixture.InitializeAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await Fixture.DisposeAsync();
    }

    /// <summary>
    /// Skips the test if the WoW server is not available.
    /// </summary>
    protected void SkipIfServerUnavailable()
    {
        Skip.If(!Fixture.IsServerAvailable,
            "WoW server is not running. Start the server and re-run tests.");
    }

    /// <summary>
    /// Skips the test if the PathfindingService is not available.
    /// </summary>
    protected void SkipIfPathfindingUnavailable()
    {
        Skip.If(!Fixture.IsPathfindingServiceAvailable,
            "PathfindingService is not running. Start the service and re-run tests.");
    }

    /// <summary>
    /// Skips the test if any required services are unavailable.
    /// </summary>
    protected void SkipIfAnyServiceUnavailable()
    {
        SkipIfServerUnavailable();
        SkipIfPathfindingUnavailable();
    }
}

/// <summary>
/// xUnit Skip helper for cleaner conditional test skipping.
/// Uses SkipException which xUnit treats as a skipped test.
/// </summary>
public static class Skip
{
    /// <summary>
    /// Skips the current test if the condition is true.
    /// </summary>
    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new SkipException(reason);
        }
    }

    /// <summary>
    /// Skips the current test if the condition is false.
    /// </summary>
    public static void Unless(bool condition, string reason)
    {
        If(!condition, reason);
    }
}
