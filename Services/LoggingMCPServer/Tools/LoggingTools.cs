using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Collections.Concurrent;

namespace BloogBot.LoggingMCPServer.Tools;

[McpServerToolType]
public class LoggingTools
{
    private readonly LogEventProcessor _logProcessor;
    private readonly TelemetryCollector _telemetryCollector;
    private readonly ILogger<LoggingTools> _logger;

    public LoggingTools(
        LogEventProcessor logProcessor, 
        TelemetryCollector telemetryCollector,
        ILogger<LoggingTools> logger)
    {
        _logProcessor = logProcessor;
        _telemetryCollector = telemetryCollector;
        _logger = logger;
    }

    [McpServerTool, Description("Create a new log event with structured data")]
    public async Task<string> LogEvent(
        [Description("The log message")] string message,
        [Description("Log level (info, warn, error, debug)")] string level = "info",
        [Description("Source component or system")] string? source = null,
        [Description("Log category or classification")] string? category = null)
    {
        try
        {
            var logEvent = new LogEvent
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Source = source ?? "MCP",
                Category = category ?? "",
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                Properties = new Dictionary<string, object>()
            };

            await _logProcessor.ProcessLogEvent(logEvent);

            _telemetryCollector.RecordEvent("mcp_log_event_created", $"Log event created via MCP: {level}");

            _logger.LogInformation("MCP log event created: {Level} - {Message}", level, message);

            return $"Log event created successfully. Level: {level}, Timestamp: {logEvent.Timestamp:yyyy-MM-dd HH:mm:ss} UTC";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating log event via MCP");
            return $"Error creating log event: {ex.Message}";
        }
    }

    [McpServerTool, Description("Retrieve recent log events with optional filtering")]
    public async Task<string> GetLogs(
        [Description("Number of logs to retrieve")] int count = 100,
        [Description("Filter by log level")] string? level = null,
        [Description("Filter by source")] string? source = null,
        [Description("Text search query")] string? query = null)
    {
        try
        {
            var logs = (await _logProcessor.GetRecentLogs(count)).ToList();

            // Apply filtering
            if (!string.IsNullOrEmpty(level))
            {
                logs = logs.Where(log => log.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(source))
            {
                logs = logs.Where(log => log.Source?.Contains(source, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            }

            if (!string.IsNullOrEmpty(query))
            {
                logs = logs.Where(log => 
                    log.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (log.Source?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (log.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            _telemetryCollector.RecordEvent("mcp_logs_retrieved", $"Retrieved {logs.Count} logs via MCP", new { count, level, source, query });

            var result = $"Retrieved {logs.Count} log events:\n\n";
            foreach (var log in logs.Take(50)) // Limit output for readability
            {
                result += $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.Level.ToUpper()}: {log.Message}";
                if (!string.IsNullOrEmpty(log.Source))
                    result += $" (Source: {log.Source})";
                if (!string.IsNullOrEmpty(log.Category))
                    result += $" (Category: {log.Category})";
                result += "\n";
            }

            if (logs.Count > 50)
            {
                result += $"\n... and {logs.Count - 50} more entries.";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs via MCP");
            return $"Error retrieving logs: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get system telemetry and metrics")]
    public string GetTelemetry()
    {
        try
        {
            var metrics = _telemetryCollector.GetSystemMetrics();
            var events = _telemetryCollector.GetRecentEvents(10);

            var result = "System Telemetry:\n\n";
            result += "Metrics:\n";
            foreach (var metric in metrics)
            {
                result += $"  {metric.Key}: {metric.Value}\n";
            }

            result += "\nRecent Events:\n";
            foreach (var evt in events)
            {
                result += $"  [{evt.Timestamp:HH:mm:ss}] {evt.EventType}: {evt.Description}\n";
            }

            _telemetryCollector.RecordEvent("mcp_telemetry_retrieved", "Telemetry data requested via MCP");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry via MCP");
            return $"Error retrieving telemetry: {ex.Message}";
        }
    }
}

// Data models (moved from Services namespace)
public class LogEvent
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Source { get; set; }
    public string? Category { get; set; }
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class LogEventProcessor
{
    private readonly ConcurrentBag<LogEvent> _logEvents = new();
    private readonly ILogger<LogEventProcessor> _logger;

    public LogEventProcessor(ILogger<LogEventProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessLogEvent(LogEvent logEvent)
    {
        _logEvents.Add(logEvent);
        
        // Also log to Serilog
        var logLevel = logEvent.Level.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Information,
            "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "[{Source}] {Message}", logEvent.Source, logEvent.Message);
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<LogEvent>> GetRecentLogs(int count)
    {
        var logs = _logEvents
            .OrderByDescending(log => log.Timestamp)
            .Take(count);
        
        return Task.FromResult(logs);
    }

    public Task<IEnumerable<LogEvent>> QueryLogs(string query, string? level = null, DateTime? from = null)
    {
        var logs = _logEvents.AsEnumerable();

        // Apply time filter
        if (from.HasValue)
        {
            logs = logs.Where(log => log.Timestamp >= from.Value);
        }

        // Apply level filter
        if (!string.IsNullOrEmpty(level))
        {
            logs = logs.Where(log => log.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        // Apply text search
        if (!string.IsNullOrEmpty(query))
        {
            logs = logs.Where(log => 
                log.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (log.Source?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (log.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }

        var result = logs.OrderByDescending(log => log.Timestamp).AsEnumerable();
        
        return Task.FromResult(result);
    }
}

public class TelemetryCollector
{
    private readonly ConcurrentDictionary<string, double> _metrics = new();
    private readonly ConcurrentQueue<TelemetryEvent> _events = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

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
    }

    public void RecordMetric(string name, double value)
    {
        _metrics.AddOrUpdate(name, value, (key, oldValue) => value);
    }

    public Dictionary<string, double> GetSystemMetrics()
    {
        var metrics = new Dictionary<string, double>(_metrics);
        
        // Add automatic system metrics
        metrics["uptime_seconds"] = (DateTime.UtcNow - _startTime).TotalSeconds;
        metrics["memory_mb"] = Environment.WorkingSet / 1024.0 / 1024.0;
        metrics["total_events"] = _events.Count;
        
        return metrics;
    }

    public List<TelemetryEvent> GetRecentEvents(int count)
    {
        return _events.TakeLast(count).ToList();
    }
}

public class TelemetryEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public object? Data { get; set; }
}
