using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
    internal static string BotRunnerDiagDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot");

    internal static IReadOnlyList<string> ReadRecentBotRunnerDiagnosticLines(
        IReadOnlyList<string> filters,
        string? directory = null,
        DateTime? minWriteUtc = null,
        int maxLines = 12)
    {
        if (maxLines <= 0 || filters.Count == 0)
            return [];

        var normalizedFilters = filters
            .Where(filter => !string.IsNullOrWhiteSpace(filter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedFilters.Length == 0)
            return [];

        var diagDirectory = directory ?? BotRunnerDiagDirectory;
        if (!Directory.Exists(diagDirectory))
            return [];

        var threshold = minWriteUtc ?? DateTime.UtcNow.AddHours(-2);
        var matches = new List<string>(maxLines);

        foreach (var file in Directory.EnumerateFiles(diagDirectory, "botrunner_diag_*.log")
                     .Select(path => new FileInfo(path))
                     .Where(file => file.Exists && file.LastWriteTimeUtc >= threshold)
                     .OrderByDescending(file => file.LastWriteTimeUtc))
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file.FullName);
            }
            catch
            {
                continue;
            }

            for (var index = lines.Length - 1; index >= 0 && matches.Count < maxLines; index--)
            {
                var line = lines[index].Trim();
                if (line.Length == 0)
                    continue;

                if (!normalizedFilters.Any(filter => line.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    continue;

                matches.Add($"{file.Name}: {line}");
            }

            if (matches.Count >= maxLines)
                break;
        }

        matches.Reverse();
        return matches;
    }

    public string FormatRecentBotRunnerDiagnostics(params string[] filters)
    {
        var lines = ReadRecentBotRunnerDiagnosticLines(filters);
        return lines.Count == 0 ? "none" : string.Join(" || ", lines);
    }

    public void DumpRecentBotRunnerDiagnostics(string label, params string[] filters)
    {
        var lines = ReadRecentBotRunnerDiagnosticLines(filters);
        if (lines.Count == 0)
        {
            _testOutput?.WriteLine($"[{label}:DIAG] none");
            return;
        }

        foreach (var line in lines)
            _testOutput?.WriteLine($"[{label}:DIAG] {line}");
    }
}
