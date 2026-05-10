using System;
using System.IO;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="ScreenshotRunIsolation"/>. Verifies the
/// hard-wipe semantics on a temp directory so the assertion runs without
/// touching the repo's tmp/test-runtime tree.
/// </summary>
public class ScreenshotRunIsolationTests
{
    [Fact]
    public void Reset_NonExistentPath_CreatesEmptyDir()
    {
        var dir = NewTempDir();
        Directory.Delete(dir, recursive: true); // ensure missing

        try
        {
            ScreenshotRunIsolation.Reset(dir);
            Assert.True(Directory.Exists(dir));
            Assert.Empty(Directory.EnumerateFileSystemEntries(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Reset_PopulatedPath_WipesContents()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "stale.png"), "old");
            Directory.CreateDirectory(Path.Combine(dir, "nested"));
            File.WriteAllText(Path.Combine(dir, "nested", "stale.json"), "{}");

            ScreenshotRunIsolation.Reset(dir);

            Assert.True(Directory.Exists(dir));
            Assert.Empty(Directory.EnumerateFileSystemEntries(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResetLongPathingDir_BuildsExpectedPath()
    {
        var repoRoot = NewTempDir();
        try
        {
            var dir = ScreenshotRunIsolation.ResetLongPathingDir(repoRoot);
            Assert.Equal(
                Path.Combine(repoRoot, "tmp", "test-runtime", "screenshots", "long-pathing"),
                dir);
            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(repoRoot)) Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void Reset_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScreenshotRunIsolation.Reset(""));
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "screenshot-iso-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
