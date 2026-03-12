using System;
using System.IO;
using System.Linq;

namespace BotRunner.Tests.LiveValidation;

public class LiveBotFixtureDiagnosticsTests
{
    [Fact]
    public void ReadRecentBotRunnerDiagnosticLines_ReturnsChronologicalFilteredTailAcrossRecentFiles()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"wwow-diag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var olderFile = Path.Combine(tempDirectory, "botrunner_diag_bg_100.log");
            var newerFile = Path.Combine(tempDirectory, "botrunner_diag_fg_200.log");

            File.WriteAllLines(olderFile,
            [
                "[08:00:00.000] ignored",
                "[08:00:01.000] [RETRIEVE_CORPSE] older-no-path",
                "[08:00:02.000] [NavigationPath] older-trace"
            ]);
            File.WriteAllLines(newerFile,
            [
                "[08:01:00.000] ignored",
                "[08:01:01.000] [RETRIEVE_CORPSE] newer-no-path",
                "[08:01:02.000] [NavigationPath] newer-trace"
            ]);

            File.SetLastWriteTimeUtc(olderFile, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newerFile, DateTime.UtcNow.AddMinutes(-5));

            var lines = LiveBotFixture.ReadRecentBotRunnerDiagnosticLines(
                ["RETRIEVE_CORPSE", "NavigationPath"],
                tempDirectory,
                DateTime.UtcNow.AddHours(-1),
                maxLines: 3);

            Assert.Equal(
            [
                $"{Path.GetFileName(olderFile)}: [08:00:02.000] [NavigationPath] older-trace",
                $"{Path.GetFileName(newerFile)}: [08:01:01.000] [RETRIEVE_CORPSE] newer-no-path",
                $"{Path.GetFileName(newerFile)}: [08:01:02.000] [NavigationPath] newer-trace"
            ], lines);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ReadRecentBotRunnerDiagnosticLines_IgnoresOldFilesAndMissingFilters()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"wwow-diag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var staleFile = Path.Combine(tempDirectory, "botrunner_diag_bg_100.log");
            File.WriteAllLines(staleFile,
            [
                "[08:00:01.000] [RETRIEVE_CORPSE] stale-line"
            ]);
            File.SetLastWriteTimeUtc(staleFile, DateTime.UtcNow.AddHours(-8));

            var staleLines = LiveBotFixture.ReadRecentBotRunnerDiagnosticLines(
                ["RETRIEVE_CORPSE"],
                tempDirectory,
                DateTime.UtcNow.AddHours(-1),
                maxLines: 5);
            var emptyFilterLines = LiveBotFixture.ReadRecentBotRunnerDiagnosticLines(
                Array.Empty<string>(),
                tempDirectory,
                DateTime.UtcNow.AddHours(-24),
                maxLines: 5);
            var missingDirectoryLines = LiveBotFixture.ReadRecentBotRunnerDiagnosticLines(
                ["RETRIEVE_CORPSE"],
                Path.Combine(tempDirectory, "missing"),
                DateTime.UtcNow.AddHours(-24),
                maxLines: 5);

            Assert.Empty(staleLines);
            Assert.Empty(emptyFilterLines);
            Assert.Empty(missingDirectoryLines);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
