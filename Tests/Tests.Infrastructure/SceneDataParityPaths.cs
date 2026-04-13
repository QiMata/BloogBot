using System;
using System.Collections.Generic;
using System.IO;

namespace Tests.Infrastructure;

/// <summary>
/// Resolves the host-side data root that backs the Docker scene/pathfinding services,
/// then falls back to repo-local test outputs only when that Docker parity root is not available.
/// </summary>
public static class SceneDataParityPaths
{
    public const string DockerDefaultHostDataRoot = @"D:\MaNGOS\data";

    public static bool HasRequiredDataRoot(string? candidate, bool requireMmaps = false)
    {
        candidate = NormalizePath(candidate);
        if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
            return false;

        if (!Directory.Exists(Path.Combine(candidate, "maps")) ||
            !Directory.Exists(Path.Combine(candidate, "vmaps")) ||
            !Directory.Exists(Path.Combine(candidate, "scenes")))
        {
            return false;
        }

        return !requireMmaps || Directory.Exists(Path.Combine(candidate, "mmaps"));
    }

    public static string? ResolveDockerHostDataRoot(bool requireMmaps = false)
    {
        foreach (var candidate in new[]
        {
            Environment.GetEnvironmentVariable("WWOW_VMANGOS_DATA_DIR"),
            DockerDefaultHostDataRoot,
            Environment.GetEnvironmentVariable("WWOW_DATA_DIR"),
        })
        {
            var normalized = NormalizePath(candidate);
            if (HasRequiredDataRoot(normalized, requireMmaps))
                return normalized;
        }

        return null;
    }

    public static string? ResolvePreferredDataRoot(string? existingDataDir, string? baseDirectory, bool requireMmaps = false)
    {
        var dockerRoot = ResolveDockerHostDataRoot(requireMmaps);
        if (!string.IsNullOrWhiteSpace(dockerRoot))
            return dockerRoot;

        var normalizedExisting = NormalizePath(existingDataDir);
        if (HasRequiredDataRoot(normalizedExisting, requireMmaps))
            return normalizedExisting;

        foreach (var candidate in EnumerateRepoFallbackCandidates(baseDirectory))
        {
            if (HasRequiredDataRoot(candidate, requireMmaps))
                return candidate;
        }

        foreach (var candidate in EnumerateMachineFallbackCandidates())
        {
            if (HasRequiredDataRoot(candidate, requireMmaps))
                return candidate;
        }

        return null;
    }

    public static IEnumerable<string> EnumerateRepoFallbackCandidates(string? baseDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ancestor in EnumerateAncestors(baseDirectory))
        {
            foreach (var candidate in EnumerateOutputCandidates(ancestor))
            {
                var normalized = NormalizePath(candidate);
                if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
                    yield return normalized;
            }
        }
    }

    private static IEnumerable<string> EnumerateOutputCandidates(string root)
    {
        yield return root;
        yield return Path.Combine(root, "Data");
        yield return Path.Combine(root, "Bot", "Debug", "net8.0");
        yield return Path.Combine(root, "Bot", "Debug", "x64");
        yield return Path.Combine(root, "Bot", "Release", "net8.0");
        yield return Path.Combine(root, "Bot", "Release", "x64");
        yield return Path.Combine(root, "Tests", "Bot", "Debug", "net8.0");
    }

    private static IEnumerable<string> EnumerateMachineFallbackCandidates()
    {
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        yield return Path.Combine(systemDrive + Path.DirectorySeparatorChar, "MaNGOS", "data");
        yield return Path.Combine(systemDrive + Path.DirectorySeparatorChar, "mangos", "data");
    }

    private static IEnumerable<string> EnumerateAncestors(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(normalized);
        }
        catch
        {
            yield break;
        }

        while (current != null)
        {
            yield return NormalizePath(current.FullName);
            current = current.Parent;
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim().Trim('"');
        string normalized;
        try
        {
            normalized = Path.GetFullPath(trimmed);
        }
        catch
        {
            normalized = trimmed;
        }

        var root = Path.GetPathRoot(normalized);
        if (!string.IsNullOrWhiteSpace(root))
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedValue = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedValue, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return root;
        }

        return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
