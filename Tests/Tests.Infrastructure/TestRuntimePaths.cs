using System;
using System.IO;

namespace Tests.Infrastructure;

/// <summary>
/// Resolves deterministic test runtime directories so large test artifacts stay on the repo drive.
/// </summary>
public static class TestRuntimePaths
{
    private const string RuntimeRootEnvVar = "WWOW_TEST_RUNTIME_ROOT";
    private const string RepoRootEnvVar = "WWOW_REPO_ROOT";
    private static string? _cachedRuntimeRoot;

    public static string GetRuntimeRoot()
    {
        var runtimeRoot = Environment.GetEnvironmentVariable(RuntimeRootEnvVar);
        if (!string.IsNullOrWhiteSpace(runtimeRoot))
            return CacheAndEnsure(runtimeRoot);

        if (!string.IsNullOrWhiteSpace(_cachedRuntimeRoot))
            return _cachedRuntimeRoot;

        var repoRoot = ResolveRepoRoot();
        if (!string.IsNullOrWhiteSpace(repoRoot))
            return CacheAndEnsure(Path.Combine(repoRoot, "tmp", "test-runtime"));

        return CacheAndEnsure(Path.Combine(Path.GetTempPath(), "WWoW", "test-runtime"));
    }

    public static string GetOrCreateSubdirectory(params string[] segments)
    {
        var path = GetRuntimeRoot();
        foreach (var segment in segments)
            path = Path.Combine(path, segment);

        Directory.CreateDirectory(path);
        return path;
    }

    private static string CacheAndEnsure(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        _cachedRuntimeRoot = fullPath;
        return fullPath;
    }

    private static string? ResolveRepoRoot()
    {
        var repoRootFromEnv = Environment.GetEnvironmentVariable(RepoRootEnvVar);
        if (!string.IsNullOrWhiteSpace(repoRootFromEnv) && Directory.Exists(repoRootFromEnv))
            return repoRootFromEnv;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WestworldOfWarcraft.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }
}
