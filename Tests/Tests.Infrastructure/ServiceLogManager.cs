using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Tests.Infrastructure;

/// <summary>
/// Manages per-service log files for test runs. Each service (StateManager,
/// PathfindingService, BotRunners) gets its own log file that is cleared at
/// the start of each test run.
///
/// Log directory: TestResults/ServiceLogs/{TestName}/
///   StateManager.log
///   PathfindingService.log
///   BotRunner_{AccountName}.log
///   Snapshots/{PollIndex}_{AccountName}.json
///
/// Usage:
///   var logs = new ServiceLogManager("RFC_FullDungeonRun");
///   logs.WriteStateManager("some log line");
///   logs.ClassifyAndWrite("[BotRunner RFCBOT2] connected");
///   logs.DumpSnapshot(pollIndex, accountName, snapshotJson);
///   logs.Dispose();
/// </summary>
public sealed class ServiceLogManager : IDisposable
{
    private readonly string _logDir;
    private readonly string _snapshotDir;
    private readonly Dictionary<string, StreamWriter> _writers = new();
    private readonly object _lock = new();
    private bool _disposed;

    public string LogDirectory => _logDir;

    public ServiceLogManager(string testName)
    {
        // Use the test output directory as base
        var baseDir = Path.Combine(
            AppContext.BaseDirectory, "TestResults", "ServiceLogs", SanitizeName(testName));
        _logDir = baseDir;
        _snapshotDir = Path.Combine(baseDir, "Snapshots");

        // Clear and recreate
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(_snapshotDir);
    }

    /// <summary>
    /// Write a line to the StateManager log.
    /// </summary>
    public void WriteStateManager(string line)
        => WriteTo("StateManager", line);

    /// <summary>
    /// Write a line to the StateManager error log.
    /// </summary>
    public void WriteStateManagerError(string line)
        => WriteTo("StateManager_errors", line);

    /// <summary>
    /// Classify a StateManager output line and route it to the appropriate service log.
    /// Lines prefixed with [BotRunner ACCOUNT] go to BotRunner_{account}.log.
    /// Lines containing PathfindingService markers go to PathfindingService.log.
    /// Everything else goes to StateManager.log.
    /// </summary>
    public void ClassifyAndWrite(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        // PathfindingService lines
        if (line.Contains("PathfindingService") || line.Contains("[Physics]") ||
            line.Contains("[Pathfinding]") || line.Contains("Navigation data"))
        {
            WriteTo("PathfindingService", line);
            WriteTo("StateManager", line); // Also keep in master log
            return;
        }

        // BotRunner lines: [BotRunner RFCBOT2], [NewWorldClient], [LoginHandler], [MovementController]
        // These come from individual bot instances — try to extract account name
        if (line.Contains("[NewWorldClient]") || line.Contains("[LoginHandler]") ||
            line.Contains("[MovementController]") || line.Contains("[BotRunner]") ||
            line.Contains("BotRunner.Clients"))
        {
            // Route to master log — we can't always determine account from these lines
            WriteTo("StateManager", line);
            return;
        }

        // Coordinator lines
        if (line.Contains("DUNGEON_COORD") || line.Contains("COMBAT_COORD"))
        {
            WriteTo("Coordinator", line);
            WriteTo("StateManager", line);
            return;
        }

        // Default: StateManager
        WriteTo("StateManager", line);
    }

    /// <summary>
    /// Dump a snapshot to a JSON file for offline analysis and unit test creation.
    /// </summary>
    public void DumpSnapshot(int pollIndex, string accountName, object snapshot)
    {
        try
        {
            var fileName = $"{pollIndex:D4}_{SanitizeName(accountName)}.json";
            var path = Path.Combine(_snapshotDir, fileName);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(path, json);
        }
        catch { /* swallow I/O errors */ }
    }

    /// <summary>
    /// Dump all snapshots for a single poll cycle.
    /// </summary>
    public void DumpPollCycle(int pollIndex, IEnumerable<(string account, object snapshot)> snapshots)
    {
        foreach (var (account, snapshot) in snapshots)
            DumpSnapshot(pollIndex, account, snapshot);
    }

    /// <summary>
    /// Write a summary line to a dedicated test-result summary file.
    /// </summary>
    public void WriteSummary(string line)
        => WriteTo("_summary", line);

    private void WriteTo(string service, string line)
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (!_writers.TryGetValue(service, out var writer))
            {
                var path = Path.Combine(_logDir, $"{service}.log");
                writer = new StreamWriter(path, append: false) { AutoFlush = true };
                writer.WriteLine($"=== {service} log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _writers[service] = writer;
            }
            writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {line}");
        }
    }

    private static string SanitizeName(string name)
        => name.Replace('/', '_').Replace('\\', '_').Replace(':', '_')
               .Replace('*', '_').Replace('?', '_').Replace('"', '_')
               .Replace('<', '_').Replace('>', '_').Replace('|', '_');

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var writer in _writers.Values)
            {
                try { writer.Dispose(); } catch { }
            }
            _writers.Clear();
        }
    }
}
