using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace BotRunner.Tests.LiveValidation;

internal static class RecordingArtifactHelper
{
    internal static string GetRecordingDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WWoW",
            "PhysicsRecordings");

    internal static string? FindLatestRecordingFile(string recordingDir, string prefix, string account, string extension)
    {
        if (!Directory.Exists(recordingDir))
            return null;

        var stablePath = Path.Combine(recordingDir, $"{prefix}_{account}.{extension}");
        if (File.Exists(stablePath))
            return stablePath;

        return Directory.GetFiles(recordingDir, $"{prefix}_{account}_*.{extension}")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    internal static string? WaitForRecordingFile(
        string recordingDir,
        string prefix,
        string account,
        string extension,
        TimeSpan timeout)
    {
        var deadlineUtc = DateTime.UtcNow + timeout;
        do
        {
            var path = FindLatestRecordingFile(recordingDir, prefix, account, extension);
            if (path != null)
                return path;

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadlineUtc);

        return null;
    }

    internal static void DeleteRecordingArtifacts(string recordingDir, string account, params string[] prefixes)
    {
        if (!Directory.Exists(recordingDir))
            return;

        foreach (var prefix in prefixes)
        {
            var stableStem = $"{prefix}_{account}";
            var stableExtension = prefix == "navtrace" ? "json" : "csv";
            var stablePath = Path.Combine(recordingDir, $"{stableStem}.{stableExtension}");
            TryDeleteFile(stablePath);

            foreach (var legacyPath in Directory.GetFiles(recordingDir, $"{stableStem}_*.*"))
                TryDeleteFile(legacyPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
