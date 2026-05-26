using System;
using System.IO;
using System.Text.Json;

namespace BotRunner.Tests.LiveValidation;

internal static class LongPathingSettings
{
    internal const string OverrideSettingsPathEnvVar = "WWOW_LONG_PATHING_SETTINGS_PATH";
    private const string DefaultConfigFileName = "LongPathing.config.json";

    internal readonly record struct ForegroundTargetProfile(
        string AccountName,
        string Race,
        string Gender);

    internal static string ResolveSettingsPath()
    {
        var overrideValue = Environment.GetEnvironmentVariable(OverrideSettingsPathEnvVar);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var overridePath = overrideValue.Trim();
            if (!Path.IsPathRooted(overridePath))
                overridePath = ResolveRepoConfigPath(overridePath);

            if (!File.Exists(overridePath))
            {
                throw new FileNotFoundException(
                    $"Long-pathing settings override '{overridePath}' from {OverrideSettingsPathEnvVar} does not exist.",
                    overridePath);
            }

            return Path.GetFullPath(overridePath);
        }

        return ResolveRepoConfigPath(DefaultConfigFileName);
    }

    internal static ForegroundTargetProfile LoadForegroundTargetProfile(string settingsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

        string? accountName = null;
        string? race = null;
        string? gender = null;
        var foregroundCount = 0;

        foreach (var element in EnumerateCharacterSettings(document.RootElement))
        {
            if (element.TryGetProperty("ShouldRun", out var shouldRunProperty)
                && shouldRunProperty.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            if (!TryGetStringProperty(element, "RunnerType", out var runnerType)
                || !string.Equals(runnerType, "Foreground", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foregroundCount++;
            accountName = RequireStringProperty(element, "AccountName", settingsPath);
            race = RequireStringProperty(element, "CharacterRace", settingsPath);
            gender = RequireStringProperty(element, "CharacterGender", settingsPath);
        }

        if (foregroundCount != 1
            || string.IsNullOrWhiteSpace(accountName)
            || string.IsNullOrWhiteSpace(race)
            || string.IsNullOrWhiteSpace(gender))
        {
            throw new InvalidOperationException(
                $"Expected exactly one runnable Foreground account in {settingsPath}, found {foregroundCount}.");
        }

        return new ForegroundTargetProfile(accountName, race, gender);
    }

    private static string ResolveRepoConfigPath(string configFileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Services",
                "WoWStateManager",
                "Settings",
                "Configs",
                configFileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find {configFileName}.");
    }

    private static string RequireStringProperty(JsonElement element, string propertyName, string settingsPath)
    {
        if (!TryGetStringProperty(element, propertyName, out var value))
        {
            throw new InvalidOperationException(
                $"Foreground target in {settingsPath} is missing required string property '{propertyName}'.");
        }

        return value;
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = property.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        value = raw.Trim();
        return true;
    }

    private static JsonElement.ArrayEnumerator EnumerateCharacterSettings(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("Characters", out var wrappedCharacters)
            && wrappedCharacters.ValueKind == JsonValueKind.Array)
        {
            return wrappedCharacters.EnumerateArray();
        }

        throw new InvalidOperationException(
            $"Unexpected long-pathing settings shape ({root.ValueKind}); expected JSON array or {{ Mode, Characters }}.");
    }
}
