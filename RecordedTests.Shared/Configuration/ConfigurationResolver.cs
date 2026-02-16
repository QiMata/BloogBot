using System;

namespace RecordedTests.Shared.Configuration;

/// <summary>
/// Resolves configuration values using precedence: CLI arguments → Environment variables → Config file → Defaults.
/// </summary>
public static class ConfigurationResolver
{
    /// <summary>
    /// Resolves a string configuration value with precedence handling.
    /// </summary>
    /// <param name="cliValue">Value from CLI arguments (highest precedence).</param>
    /// <param name="envVarName">Environment variable name to check.</param>
    /// <param name="configValue">Value from configuration file.</param>
    /// <param name="defaultValue">Default value if no other source provides one.</param>
    /// <returns>The resolved configuration value.</returns>
    public static string ResolveString(
        string? cliValue,
        string? envVarName,
        string? configValue,
        string? defaultValue = null)
    {
        // CLI takes precedence
        if (!string.IsNullOrWhiteSpace(cliValue))
            return cliValue;

        // Then environment variable
        if (!string.IsNullOrWhiteSpace(envVarName))
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue;
        }

        // Then config file
        if (!string.IsNullOrWhiteSpace(configValue))
            return configValue;

        // Finally default
        return defaultValue ?? string.Empty;
    }

    /// <summary>
    /// Resolves an integer configuration value with precedence handling.
    /// </summary>
    public static int ResolveInt(
        int? cliValue,
        string? envVarName,
        int? configValue,
        int defaultValue)
    {
        if (cliValue.HasValue)
            return cliValue.Value;

        if (!string.IsNullOrWhiteSpace(envVarName))
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (int.TryParse(envValue, out var parsedEnvValue))
                return parsedEnvValue;
        }

        if (configValue.HasValue)
            return configValue.Value;

        return defaultValue;
    }

    /// <summary>
    /// Resolves a boolean configuration value with precedence handling.
    /// </summary>
    public static bool ResolveBool(
        bool? cliValue,
        string? envVarName,
        bool? configValue,
        bool defaultValue)
    {
        if (cliValue.HasValue)
            return cliValue.Value;

        if (!string.IsNullOrWhiteSpace(envVarName))
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (bool.TryParse(envValue, out var parsedEnvValue))
                return parsedEnvValue;
        }

        if (configValue.HasValue)
            return configValue.Value;

        return defaultValue;
    }

    /// <summary>
    /// Resolves a TimeSpan configuration value (in seconds) with precedence handling.
    /// </summary>
    public static TimeSpan ResolveTimeSpan(
        TimeSpan? cliValue,
        string? envVarName,
        TimeSpan? configValue,
        TimeSpan defaultValue)
    {
        if (cliValue.HasValue)
            return cliValue.Value;

        if (!string.IsNullOrWhiteSpace(envVarName))
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (int.TryParse(envValue, out var seconds))
                return TimeSpan.FromSeconds(seconds);
        }

        if (configValue.HasValue)
            return configValue.Value;

        return defaultValue;
    }
}
