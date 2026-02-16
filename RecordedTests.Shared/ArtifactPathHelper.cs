namespace RecordedTests.Shared;

using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class ArtifactPathHelper
{
    public static ArtifactPathInfo PrepareArtifactDirectories(string artifactsRootDirectory, string testName, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(artifactsRootDirectory))
        {
            throw new ArgumentException("Artifacts root directory is required.", nameof(artifactsRootDirectory));
        }

        var sanitizedTestName = SanitizeName(testName);
        var rootDirectory = Path.Combine(artifactsRootDirectory, sanitizedTestName);
        var runDirectoryName = startedAt.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var runDirectory = Path.Combine(rootDirectory, runDirectoryName);

        Directory.CreateDirectory(runDirectory);

        return new ArtifactPathInfo(artifactsRootDirectory, sanitizedTestName, rootDirectory, runDirectory);
    }

    public static string SanitizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "RecordedTest";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            builder.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "RecordedTest" : sanitized;
    }

    public readonly record struct ArtifactPathInfo(
        string ArtifactsRootDirectory,
        string SanitizedTestName,
        string TestRootDirectory,
        string TestRunDirectory);
}
