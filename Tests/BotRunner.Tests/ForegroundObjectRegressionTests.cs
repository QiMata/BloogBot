using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BotRunner.Tests;

/// <summary>
/// FG-MISS-004: Regression gate for FG materialization throws.
/// Scans ForegroundBotRunner object-model source files to ensure
/// NotImplementedException was not reintroduced.
/// </summary>
public class ForegroundObjectRegressionTests
{
    private static readonly string[] ObjectModelFiles =
    [
        "WoWObject.cs",
        "WoWUnit.cs",
        "WoWPlayer.cs"
    ];

    private static readonly Regex ThrowPattern = new(
        @"throw\s+new\s+NotImplementedException",
        RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        // Walk up from test assembly location to find repo root
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "WestworldOfWarcraft.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: try common development paths
        var candidates = new[]
        {
            @"E:\repos\Westworld of Warcraft",
            @"C:\repos\Westworld of Warcraft"
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "WestworldOfWarcraft.sln")))
                return candidate;
        }

        throw new InvalidOperationException("Cannot locate repo root from test assembly path");
    }

    private static string GetObjectsDir()
    {
        var root = FindRepoRoot();
        return Path.Combine(root, "Services", "ForegroundBotRunner", "Objects");
    }

    [Fact]
    public void WoWObject_NoNotImplementedException()
    {
        AssertNoThrowsInFile("WoWObject.cs");
    }

    [Fact]
    public void WoWUnit_NoNotImplementedException()
    {
        AssertNoThrowsInFile("WoWUnit.cs");
    }

    [Fact]
    public void WoWPlayer_NoNotImplementedException()
    {
        AssertNoThrowsInFile("WoWPlayer.cs");
    }

    [Fact]
    public void AllObjectModelFiles_NoNotImplementedException()
    {
        var objectsDir = GetObjectsDir();
        if (!Directory.Exists(objectsDir))
        {
            // Skip if source not available in CI/build
            return;
        }

        var violations = ObjectModelFiles
            .Select(f => Path.Combine(objectsDir, f))
            .Where(File.Exists)
            .SelectMany(path =>
            {
                var lines = File.ReadAllLines(path);
                return lines.Select((line, idx) => new { File = Path.GetFileName(path), Line = idx + 1, Text = line })
                            .Where(x => ThrowPattern.IsMatch(x.Text));
            })
            .ToList();

        Assert.True(violations.Count == 0,
            $"NotImplementedException found in FG object model files:\n" +
            string.Join("\n", violations.Select(v => $"  {v.File}:{v.Line} → {v.Text.Trim()}")));
    }

    private static void AssertNoThrowsInFile(string fileName)
    {
        var objectsDir = GetObjectsDir();
        var filePath = Path.Combine(objectsDir, fileName);

        if (!File.Exists(filePath))
        {
            // Skip if source not available in CI/build environment
            return;
        }

        var lines = File.ReadAllLines(filePath);
        var throwLines = lines
            .Select((line, idx) => new { Line = idx + 1, Text = line })
            .Where(x => ThrowPattern.IsMatch(x.Text))
            .ToList();

        Assert.True(throwLines.Count == 0,
            $"NotImplementedException reintroduced in {fileName}:\n" +
            string.Join("\n", throwLines.Select(t => $"  Line {t.Line}: {t.Text.Trim()}")));
    }
}
