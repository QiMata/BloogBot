using System;
using System.Diagnostics;
using System.IO;
using BotRunner;

namespace ForegroundBotRunner.Diagnostics;

internal static class RecordingFileArtifactGate
{
    internal static bool IsEnabled()
        => RecordingArtifactsFeature.IsEnabled();

    internal static string ResolveWoWLogsPath(string fileName)
    {
        if (!IsEnabled())
        {
            return string.Empty;
        }

        string wowDir;
        try
        {
            wowDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName)
                ?? AppContext.BaseDirectory;
        }
        catch
        {
            wowDir = AppContext.BaseDirectory;
        }

        var logsDir = Path.Combine(wowDir, "WWoWLogs");
        try
        {
            Directory.CreateDirectory(logsDir);
        }
        catch
        {
        }

        return Path.Combine(logsDir, fileName);
    }

    internal static string ResolveDocumentsPath(params string[] segments)
    {
        if (!IsEnabled())
        {
            return string.Empty;
        }

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Documents");

        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    internal static string ResolveBaseDirectoryPath(params string[] segments)
    {
        if (!IsEnabled())
        {
            return string.Empty;
        }

        var path = AppContext.BaseDirectory;
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
            }
        }

        return path;
    }
}
