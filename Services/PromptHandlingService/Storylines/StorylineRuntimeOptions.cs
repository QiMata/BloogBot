using Microsoft.Extensions.Configuration;

namespace PromptHandlingService.Storylines;

public sealed class StorylineRuntimeOptions
{
    public const string SectionName = "Foundry:StorylineRuntime";

    public string DatabasePath { get; init; } = string.Empty;
    public string SeedPath { get; init; } = string.Empty;
    public bool ImportSeedOnEmpty { get; init; } = true;
    public int MaxMemorySummaryCharacters { get; init; } = 1200;

    public static StorylineRuntimeOptions FromConfiguration(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var legacySection = configuration.GetSection("StorylineRuntime");

        string? ReadString(string key) =>
            section[key] ??
            legacySection[key] ??
            configuration[$"StorylineRuntime:{key}"] ??
            configuration[key];

        bool ReadBool(string key, bool defaultValue) =>
            bool.TryParse(ReadString(key), out var value) ? value : defaultValue;

        int ReadInt(string key, int defaultValue) =>
            int.TryParse(ReadString(key), out var value) ? value : defaultValue;

        var databasePath = ReadString("databasePath") ??
            ReadString("DatabasePath") ??
            "storyline_runtime.sqlite";
        var seedPath = ReadString("seedPath") ??
            ReadString("SeedPath") ??
            Path.Combine("Config", "foundry", "storyline-seed.json");

        return new StorylineRuntimeOptions
        {
            DatabasePath = ResolvePath(contentRootPath, databasePath),
            SeedPath = ResolvePath(contentRootPath, seedPath),
            ImportSeedOnEmpty = ReadBool("importSeedOnEmpty", true),
            MaxMemorySummaryCharacters = ReadInt("maxMemorySummaryCharacters", 1200)
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException("Storyline runtime databasePath is required.");
        }

        if (MaxMemorySummaryCharacters <= 0)
        {
            throw new InvalidOperationException("Storyline runtime maxMemorySummaryCharacters must be greater than zero.");
        }
    }

    private static string ResolvePath(string contentRootPath, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var root = string.IsNullOrWhiteSpace(contentRootPath)
            ? AppContext.BaseDirectory
            : contentRootPath;

        return Path.GetFullPath(Path.Combine(root, path));
    }
}
