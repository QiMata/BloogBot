namespace Tests.Infrastructure;

/// <summary>
/// Helper for conditionally skipping tests at runtime.
/// Delegates to <see cref="Xunit.Skip"/> from Xunit.SkippableFact so that
/// the xUnit v2 runner properly reports the test as skipped instead of failed.
/// Tests using these helpers must be decorated with <c>[SkippableFact]</c> or
/// <c>[SkippableTheory]</c> instead of <c>[Fact]</c>/<c>[Theory]</c>.
/// </summary>
public static class Skip
{
    /// <summary>
    /// Skips the current test if the condition is true.
    /// </summary>
    public static void If(bool condition, string reason)
    {
        Xunit.Skip.If(condition, reason);
    }

    /// <summary>
    /// Skips the current test if the condition is false.
    /// </summary>
    public static void IfNot(bool condition, string reason)
    {
        Xunit.Skip.IfNot(condition, reason);
    }

    /// <summary>
    /// Skips the current test if the condition is false.
    /// Alias for IfNot.
    /// </summary>
    public static void Unless(bool condition, string reason)
    {
        Xunit.Skip.IfNot(condition, reason);
    }
}
