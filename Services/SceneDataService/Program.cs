using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SceneDataService;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var previousDataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            var resolvedDataDir = ResolveSceneDataDirectory(previousDataDir);

            if (string.IsNullOrEmpty(resolvedDataDir))
            {
                WriteStartupError("[SceneDataService] FATAL: Could not find scene data root containing scenes/ or vmaps/.");
                if (!string.IsNullOrWhiteSpace(previousDataDir))
                    WriteStartupError($"[SceneDataService] Existing WWOW_DATA_DIR was invalid: {previousDataDir}");
                return 1;
            }

            if (!resolvedDataDir.EndsWith(Path.DirectorySeparatorChar))
                resolvedDataDir += Path.DirectorySeparatorChar;

            Environment.SetEnvironmentVariable("WWOW_DATA_DIR", resolvedDataDir);
            WriteStartupInfo($"[SceneDataService] WWOW_DATA_DIR set to: {resolvedDataDir}");

            var ipAddress = Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_IP") ?? "127.0.0.1";
            var port = int.TryParse(Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_PORT"), out var parsedPort)
                ? parsedPort
                : 5003;

            WriteStartupInfo($"[SceneDataService] Starting on {ipAddress}:{port}");

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger("SceneDataService");

            // Check for tile-based scene data (preferred)
            var tilesDir = Path.Combine(resolvedDataDir, "scenes", "tiles");
            var hasTiles = Directory.Exists(tilesDir) && Directory.GetFiles(tilesDir, "*.scenetile").Length > 0;

            if (hasTiles)
            {
                WriteStartupInfo($"[SceneDataService] Tile mode: loading .scenetile files from {tilesDir}");
                using var tileServer = new SceneTileSocketServer(ipAddress, port, logger);
                WriteStartupInfo($"[SceneDataService] Socket listener bound on {ipAddress}:{port}");

                tileServer.LoadTiles(tilesDir);
                WriteStartupInfo($"[SceneDataService] Ready and listening on {ipAddress}:{port} (tile mode)");

                using var tileShutdown = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; tileShutdown.Set(); };
                AppDomain.CurrentDomain.ProcessExit += (_, _) => tileShutdown.Set();
                tileShutdown.Wait();
                return 0;
            }

            // Fallback: legacy AABB-based server (loads full .scene files)
            WriteStartupInfo($"[SceneDataService] Legacy mode: no .scenetile files found, using AABB extraction");
            using var server = new SceneDataSocketServer(ipAddress, port, logger);
            WriteStartupInfo($"[SceneDataService] Socket listener bound on {ipAddress}:{port}");

            server.InitializeNavigation();
            WriteStartupInfo($"[SceneDataService] Ready and listening on {ipAddress}:{port} (legacy mode)");

            using var shutdown = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                shutdown.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Set();
            shutdown.Wait();
            return 0;
        }
        catch (Exception ex)
        {
            WriteStartupError($"[SceneDataService] FATAL: {ex}");
            return 1;
        }
    }

    private static string? ResolveSceneDataDirectory(string? existingDataDir)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, existingDataDir);

        var baseDir = NormalizePath(AppContext.BaseDirectory);
        var currentDir = NormalizePath(Directory.GetCurrentDirectory());

        AddCommonOutputCandidates(candidates, baseDir);
        AddCommonOutputCandidates(candidates, currentDir);

        foreach (var ancestor in EnumerateAncestors(baseDir))
            AddCommonOutputCandidates(candidates, ancestor);

        if (!string.Equals(baseDir, currentDir, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var ancestor in EnumerateAncestors(currentDir))
                AddCommonOutputCandidates(candidates, ancestor);
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (HasSceneData(candidate))
                return candidate;
        }

        return null;
    }

    private static void AddCommonOutputCandidates(List<string> candidates, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return;

        AddCandidate(candidates, root);
        AddCandidate(candidates, Path.Combine(root, "Data"));
        AddCandidate(candidates, Path.Combine(root, "Bot", "Debug", "net8.0"));
        AddCandidate(candidates, Path.Combine(root, "Bot", "Debug", "x64"));
        AddCandidate(candidates, Path.Combine(root, "Bot", "Release", "net8.0"));
        AddCandidate(candidates, Path.Combine(root, "Bot", "Release", "x64"));
        AddCandidate(candidates, Path.Combine(root, "Tests", "Bot", "Debug", "net8.0"));
    }

    private static IEnumerable<string> EnumerateAncestors(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(path);
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

    private static void AddCandidate(List<string> candidates, string? candidate)
    {
        var normalized = NormalizePath(candidate);
        if (!string.IsNullOrWhiteSpace(normalized))
            candidates.Add(normalized);
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

    private static bool HasSceneData(string candidate)
        => Directory.Exists(candidate)
           && (Directory.Exists(Path.Combine(candidate, "scenes"))
               || Directory.Exists(Path.Combine(candidate, "vmaps")));

    private static void WriteStartupInfo(string message)
    {
        Console.WriteLine(message);
        FlushTrace(message);
    }

    private static void WriteStartupError(string message)
    {
        Console.Error.WriteLine(message);
        FlushTrace(message);
    }

    private static void FlushTrace(string message)
    {
        var tracePath = Environment.GetEnvironmentVariable("WWOW_SCENE_DATA_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tracePath)!);
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
