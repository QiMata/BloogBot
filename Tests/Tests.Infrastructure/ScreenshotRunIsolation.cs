using System;
using System.IO;

namespace Tests.Infrastructure;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — hard-wipe helper for the long-pathing
/// screenshot directory. The bake-validation harness writes per-checkpoint
/// PNGs and the structured JSON report into a single per-run directory;
/// without isolation, archival debris from previous runs masks freshly-
/// captured failures. Call <see cref="Reset"/> at fixture construction (or
/// at the top of each test that produces screenshots) to start clean.
///
/// Per user mandate (2026-05-10 validation-harness brief): hard-wipe is
/// preferred over archival rotation — agents should never have to wade
/// through a mix of new and stale captures to diagnose a failure.
/// </summary>
public static class ScreenshotRunIsolation
{
    /// <summary>
    /// Default subdirectory under the repo's <c>tmp/test-runtime/screenshots/</c>
    /// where the long-pathing bake-validation harness writes captures.
    /// </summary>
    public const string LongPathingSubDir = "long-pathing";

    /// <summary>
    /// Recursively delete the directory at <paramref name="absolutePath"/>
    /// if it exists, then recreate it empty. No-ops cleanly if the path
    /// does not exist yet (first-run case). Does not throw on missing
    /// parent directories.
    /// </summary>
    public static void Reset(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            throw new ArgumentException("absolutePath must be non-empty", nameof(absolutePath));

        if (Directory.Exists(absolutePath))
        {
            // Two-phase delete avoids "directory not empty" races on Windows
            // when handles are still being closed by the prior test run.
            ClearDirectoryContents(absolutePath);
            Directory.Delete(absolutePath, recursive: true);
        }
        Directory.CreateDirectory(absolutePath);
    }

    /// <summary>
    /// Resolve and reset the standard long-pathing screenshot directory
    /// underneath the repo's <c>tmp/test-runtime/screenshots/</c>.
    /// Returns the absolute path of the freshly-created directory.
    /// </summary>
    public static string ResetLongPathingDir(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
            throw new ArgumentException("repoRoot must be non-empty", nameof(repoRoot));

        var dir = Path.Combine(repoRoot, "tmp", "test-runtime", "screenshots", LongPathingSubDir);
        Reset(dir);
        return dir;
    }

    private static void ClearDirectoryContents(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (IOException)
            {
                // File may have been deleted concurrently; skip and let the
                // outer Directory.Delete try again.
            }
            catch (UnauthorizedAccessException)
            {
                // Read-only or in-use; same handling as IOException.
            }
        }
    }
}
