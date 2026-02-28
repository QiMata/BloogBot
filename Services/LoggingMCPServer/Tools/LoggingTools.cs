using LoggingMCPServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace LoggingMCPServer.Tools;

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
