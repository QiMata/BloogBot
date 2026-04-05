using Microsoft.Extensions.Configuration;

namespace BloogBot.AI.Configuration;

/// <summary>
/// Resolves DecisionInvocationSettings with precedence:
/// CLI arguments > Environment variables > appsettings.json > Defaults.
/// </summary>
public static class DecisionInvocationSettingsResolver
{
    // Environment variable names
    private const string EnvIntervalSeconds = "WWOW_DECISION_INTERVAL_SECONDS";
    private const string EnvResetTimerOnAdHoc = "WWOW_RESET_TIMER_ON_ADHOC";
    private const string EnvEnableAutoInvocation = "WWOW_ENABLE_AUTO_INVOCATION";

    // CLI argument names
    private const string CliIntervalPrefix = "--decision-interval=";
    private const string CliResetTimerOnAdHoc = "--reset-timer-on-adhoc";
    private const string CliNoResetTimerOnAdHoc = "--no-reset-timer-on-adhoc";
    private const string CliEnableAutoInvocation = "--enable-auto-invocation";
    private const string CliDisableAutoInvocation = "--disable-auto-invocation";

    /// <summary>
    /// Resolves settings from all configuration sources.
    /// </summary>
    /// <param name="configuration">The IConfiguration to read from (appsettings.json).</param>
    /// <param name="cliArgs">Command line arguments, if any.</param>
    /// <returns>Resolved and validated settings.</returns>
    public static DecisionInvocationSettings Resolve(
        IConfiguration? configuration = null,
        string[]? cliArgs = null)
    {
        var settings = new DecisionInvocationSettings();
        var section = configuration?.GetSection(DecisionInvocationSettings.SectionName);

        // Resolve DefaultInterval
        settings.DefaultInterval = ResolveInterval(
            ParseCliInterval(cliArgs),
            GetEnvInt(EnvIntervalSeconds),
            section?.GetValue<int?>("DefaultIntervalSeconds"),
            (int)settings.DefaultInterval.TotalSeconds);

        // Resolve ResetTimerOnAdHocInvocation
        settings.ResetTimerOnAdHocInvocation = ResolveBool(
            ParseCliBool(cliArgs, CliResetTimerOnAdHoc, CliNoResetTimerOnAdHoc),
            GetEnvBool(EnvResetTimerOnAdHoc),
            section?.GetValue<bool?>("ResetTimerOnAdHocInvocation"),
            settings.ResetTimerOnAdHocInvocation);

        // Resolve EnableAutomaticInvocation
        settings.EnableAutomaticInvocation = ResolveBool(
            ParseCliBool(cliArgs, CliEnableAutoInvocation, CliDisableAutoInvocation),
            GetEnvBool(EnvEnableAutoInvocation),
            section?.GetValue<bool?>("EnableAutomaticInvocation"),
            settings.EnableAutomaticInvocation);

        // Validate and clamp
        settings.Validate();

        return settings;
    }

    private static TimeSpan ResolveInterval(int? cli, int? env, int? config, int defaultValue)
    {
        var seconds = cli ?? env ?? config ?? defaultValue;
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool ResolveBool(bool? cli, bool? env, bool? config, bool defaultValue)
    {
        return cli ?? env ?? config ?? defaultValue;
    }

    private static int? ParseCliInterval(string[]? args)
    {
        if (args == null) return null;

        var arg = args.FirstOrDefault(a => a.StartsWith(CliIntervalPrefix, StringComparison.OrdinalIgnoreCase));
        if (arg == null) return null;

        var value = arg.Substring(CliIntervalPrefix.Length);
        return int.TryParse(value, out var result) ? result : null;
    }

    private static bool? ParseCliBool(string[]? args, string enableFlag, string disableFlag)
    {
        if (args == null) return null;

        if (args.Any(a => a.Equals(enableFlag, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (args.Any(a => a.Equals(disableFlag, StringComparison.OrdinalIgnoreCase)))
            return false;

        return null;
    }

    private static int? GetEnvInt(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var result) ? result : null;
    }

    private static bool? GetEnvBool(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) return null;

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => null
        };
    }
}
