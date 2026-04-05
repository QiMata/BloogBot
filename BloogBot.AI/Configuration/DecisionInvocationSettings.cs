namespace BloogBot.AI.Configuration;

/// <summary>
/// Configuration settings for decision invocation timing and control.
/// Supports precedence: CLI > Environment Variable > appsettings.json > Defaults.
/// </summary>
public sealed class DecisionInvocationSettings
{
    /// <summary>
    /// Section name in appsettings.json.
    /// </summary>
    public const string SectionName = "DecisionInvocation";

    /// <summary>
    /// Default interval between automatic decision invocations.
    /// </summary>
    public TimeSpan DefaultInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Minimum allowed interval to prevent system overload.
    /// Values below this will be clamped.
    /// </summary>
    public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum allowed interval.
    /// Values above this will be clamped.
    /// </summary>
    public TimeSpan MaximumInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether ad-hoc invocations should reset the interval timer.
    /// When true, calling InvokeNowAsync resets the countdown to the next automatic invocation.
    /// </summary>
    public bool ResetTimerOnAdHocInvocation { get; set; } = true;

    /// <summary>
    /// Enable or disable automatic interval-based invocations.
    /// When false, decisions are only made via ad-hoc invocation.
    /// </summary>
    public bool EnableAutomaticInvocation { get; set; } = true;

    /// <summary>
    /// Validates and clamps the interval to valid range.
    /// </summary>
    public void Validate()
    {
        if (DefaultInterval < MinimumInterval)
            DefaultInterval = MinimumInterval;

        if (DefaultInterval > MaximumInterval)
            DefaultInterval = MaximumInterval;
    }

    /// <summary>
    /// Creates settings with all defaults.
    /// </summary>
    public static DecisionInvocationSettings CreateDefault() => new();
}
