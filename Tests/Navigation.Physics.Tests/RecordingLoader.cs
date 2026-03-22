using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Navigation.Physics.Tests;

/// <summary>
/// Loads movement recording JSON files produced by MovementRecorder.
/// Model classes are defined in Helpers/RecordingModels.cs.
/// </summary>
public static class RecordingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static MovementRecording LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MovementRecording>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize recording from {path}");
    }

    /// <summary>
    /// Finds the recordings directory. Checks common locations.
    /// </summary>
    public static string GetRecordingsDirectory()
    {
        // Priority 1: Walk up from test assembly to find repo Recordings dir
        var asmDir = Path.GetDirectoryName(typeof(RecordingLoader).Assembly.Location);
        if (!string.IsNullOrEmpty(asmDir))
        {
            var dir = new DirectoryInfo(asmDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Tests", "Navigation.Physics.Tests", "Recordings");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        // Priority 2: Build output copy
        var buildCandidate = Path.Combine(AppContext.BaseDirectory, "Recordings");
        if (Directory.Exists(buildCandidate))
            return buildCandidate;

        // Priority 3: Env var
        var envDir = Environment.GetEnvironmentVariable("WWOW_RECORDINGS_DIR");
        if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
            return envDir;

        // Priority 4: Legacy Documents location
        var docsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot", "MovementRecordings");
        if (Directory.Exists(docsDir))
            return docsDir;

        throw new DirectoryNotFoundException(
            "Movement recordings directory not found. Place recordings in Tests/Navigation.Physics.Tests/Recordings/ or set WWOW_RECORDINGS_DIR.");
    }

    /// <summary>
    /// Finds the most recent recording matching a scenario name prefix (e.g., "01_flat_run_forward").
    /// </summary>
    public static string FindRecording(string scenarioPrefix)
    {
        var dir = GetRecordingsDirectory();
        var files = Directory.GetFiles(dir, "*.json")
            .Select(f => new { Path = f, Recording = TryLoadHeader(f) })
            .Where(f => f.Recording?.Description?.Contains(scenarioPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(f => f.Recording!.StartTimestampUtc)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found for scenario '{scenarioPrefix}' in {dir}");

        return files[0].Path;
    }

    /// <summary>
    /// Finds a recording by filename pattern (e.g., "Dralrahgra_Durotar_2026-02-07_19-29-21").
    /// </summary>
    public static string FindRecordingByFilename(string filenamePattern)
    {
        var dir = GetRecordingsDirectory();
        var files = Directory.GetFiles(dir, "*.json")
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .Contains(filenamePattern, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();

        if (files.Count == 0)
            throw new FileNotFoundException($"No recording found matching '{filenamePattern}' in {dir}");

        return files[0];
    }

    private static MovementRecording? TryLoadHeader(string path)
    {
        try
        {
            return LoadFromFile(path);
        }
        catch
        {
            return null;
        }
    }
}
