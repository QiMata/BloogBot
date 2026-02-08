namespace Tests.Infrastructure;

/// <summary>
/// Exception thrown when a test should be skipped due to missing prerequisites.
/// xUnit recognizes this exception type and marks the test as skipped rather than failed.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}

/// <summary>
/// Helper for conditionally skipping tests at runtime.
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
            throw new SkipException(reason);
    }

    /// <summary>
    /// Skips the current test if the condition is false.
    /// </summary>
    public static void IfNot(bool condition, string reason)
    {
        If(!condition, reason);
    }

    /// <summary>
    /// Skips the current test if the condition is false.
    /// Alias for IfNot.
    /// </summary>
    public static void Unless(bool condition, string reason)
    {
        If(!condition, reason);
    }
}
