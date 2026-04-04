using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoadTests;

/// <summary>
/// Central metrics aggregator for load tests. Bots report metrics via UDP.
/// Collector writes to CSV file periodically.
///
/// Fields per report: accountName, frameTimeMs, snapshotLatencyMs,
/// posX, posY, posZ, movementFlags, isConnected, memoryMB.
/// </summary>
public class BotMetricsCollector : IDisposable
{
    public record MetricsReport(
        string AccountName,
        float FrameTimeMs,
        float SnapshotLatencyMs,
        float PosX, float PosY, float PosZ,
        uint MovementFlags,
        bool IsConnected,
        float MemoryMB,
        DateTime Timestamp);

    private readonly ConcurrentDictionary<string, MetricsReport> _latestMetrics = new();
    private readonly ConcurrentBag<MetricsReport> _history = new();
    private readonly UdpClient? _udpListener;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _csvOutputPath;
    private Task? _listenerTask;

    public BotMetricsCollector(int listenPort = 9100, string? csvOutputDir = null)
    {
        _csvOutputPath = Path.Combine(
            csvOutputDir ?? Path.Combine(AppContext.BaseDirectory, "LoadTestMetrics"),
            $"metrics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

        Directory.CreateDirectory(Path.GetDirectoryName(_csvOutputPath)!);

        try
        {
            _udpListener = new UdpClient(listenPort);
        }
        catch (SocketException)
        {
            // Port in use — fallback to direct reporting
        }
    }

    /// <summary>Start listening for UDP metric reports.</summary>
    public void StartListening()
    {
        if (_udpListener == null) return;
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>Report metrics directly (no UDP needed).</summary>
    public void Report(MetricsReport report)
    {
        _latestMetrics[report.AccountName] = report;
        _history.Add(report);
    }

    /// <summary>Get latest metrics for all bots.</summary>
    public IReadOnlyDictionary<string, MetricsReport> GetLatestMetrics()
        => _latestMetrics;

    /// <summary>Get summary statistics.</summary>
    public (int ConnectedCount, float AvgFrameTimeMs, float P95FrameTimeMs, float TotalMemoryMB) GetSummary()
    {
        var reports = _latestMetrics.Values.ToList();
        if (reports.Count == 0) return (0, 0, 0, 0);

        var connected = reports.Count(r => r.IsConnected);
        var frameTimes = reports.Select(r => r.FrameTimeMs).OrderBy(f => f).ToList();
        var avg = frameTimes.Average();
        var p95 = frameTimes[(int)(frameTimes.Count * 0.95)];
        var totalMem = reports.Sum(r => r.MemoryMB);

        return (connected, avg, p95, totalMem);
    }

    /// <summary>Write all collected metrics to CSV.</summary>
    public void WriteCsv()
    {
        var lines = new List<string>
        {
            "Timestamp,AccountName,FrameTimeMs,SnapshotLatencyMs,PosX,PosY,PosZ,MovementFlags,IsConnected,MemoryMB"
        };

        foreach (var r in _history)
        {
            lines.Add($"{r.Timestamp:O},{r.AccountName},{r.FrameTimeMs:F2},{r.SnapshotLatencyMs:F2}," +
                       $"{r.PosX:F1},{r.PosY:F1},{r.PosZ:F1},{r.MovementFlags},{r.IsConnected},{r.MemoryMB:F1}");
        }

        File.WriteAllLines(_csvOutputPath, lines);
    }

    public string CsvOutputPath => _csvOutputPath;

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpListener != null)
        {
            try
            {
                var result = await _udpListener.ReceiveAsync(ct);
                var data = Encoding.UTF8.GetString(result.Buffer);
                var report = ParseReport(data);
                if (report != null)
                    Report(report);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore malformed packets */ }
        }
    }

    private static MetricsReport? ParseReport(string csv)
    {
        var parts = csv.Split(',');
        if (parts.Length < 9) return null;
        try
        {
            return new MetricsReport(
                parts[0],
                float.Parse(parts[1]),
                float.Parse(parts[2]),
                float.Parse(parts[3]),
                float.Parse(parts[4]),
                float.Parse(parts[5]),
                uint.Parse(parts[6]),
                parts[7] == "1",
                float.Parse(parts[8]),
                DateTime.UtcNow);
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        _udpListener?.Dispose();
        _cts.Dispose();
    }
}
