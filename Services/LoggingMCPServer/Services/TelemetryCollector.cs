using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LoggingMCPServer.Services;

public class TelemetryCollector
{
    private readonly ILogger<TelemetryCollector> _logger;
    private readonly ConcurrentDictionary<string, object> _metrics = new();
    private readonly ConcurrentQueue<TelemetryEvent> _events = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    public TelemetryCollector(ILogger<TelemetryCollector> logger)
    {
        _logger = logger;
    }

    public async Task<object> GetCurrentTelemetryAsync()
    {
        var memoryUsage = GC.GetTotalMemory(false);
        var uptime = DateTime.UtcNow - _startTime;

        return await Task.FromResult(new
        {
            systemInfo = new
            {
                memoryUsage = memoryUsage,
                uptimeSeconds = uptime.TotalSeconds,
                timestamp = DateTime.UtcNow,
                processId = Environment.ProcessId,
                machineName = Environment.MachineName
            },
            metrics = _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            recentEvents = GetRecentEvents(10)
        });
    }

    public void RecordMetric(string name, object value)
    {
        _metrics.AddOrUpdate(name, value, (key, oldValue) => value);
        _logger.LogDebug("Recorded metric: {MetricName} = {Value}", name, value);
    }

    public void RecordEvent(string eventType, string description, object? data = null)
    {
        var telemetryEvent = new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Description = description,
            Data = data
        };

        _events.Enqueue(telemetryEvent);
        
        // Keep only the last 1000 events
        while (_events.Count > 1000)
        {
            _events.TryDequeue(out _);
        }

        _logger.LogDebug("Recorded telemetry event: {EventType} - {Description}", eventType, description);
    }

    public List<TelemetryEvent> GetRecentEvents(int count = 50)
    {
        return _events.ToArray()
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    public Dictionary<string, object> GetMetrics()
    {
        return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public Dictionary<string, double> GetSystemMetrics()
    {
        var metrics = new Dictionary<string, double>();
        foreach (var kvp in _metrics)
        {
            if (kvp.Value is double d)
                metrics[kvp.Key] = d;
            else if (kvp.Value is int i)
                metrics[kvp.Key] = i;
            else if (kvp.Value is long l)
                metrics[kvp.Key] = l;
            else if (kvp.Value is float f)
                metrics[kvp.Key] = f;
        }

        metrics["uptime_seconds"] = (DateTime.UtcNow - _startTime).TotalSeconds;
        metrics["memory_mb"] = Environment.WorkingSet / 1024.0 / 1024.0;
        metrics["total_events"] = _events.Count;

        return metrics;
    }
}

public class TelemetryEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Data { get; set; }
}