using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LoggingMCPServer.Services;

public class LogEventProcessor
{
    private readonly ILogger<LogEventProcessor> _logger;
    private readonly ConcurrentQueue<LogEvent> _logEvents = new();
    private readonly object _lock = new();

    public LogEventProcessor(ILogger<LogEventProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessLogEvent(LogEvent logEvent)
    {
        logEvent.Timestamp = DateTime.UtcNow;
        _logEvents.Enqueue(logEvent);

        // Log using the structured logger
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Source"] = logEvent.Source,
            ["Category"] = logEvent.Category,
            ["CorrelationId"] = logEvent.CorrelationId ?? "",
            ["ProcessId"] = logEvent.ProcessId,
            ["ThreadId"] = logEvent.ThreadId
        });

        var logLevel = ParseLogLevel(logEvent.Level);
        _logger.Log(logLevel, logEvent.Exception, "{Message} {Properties}", 
            logEvent.Message, logEvent.Properties);

        await Task.CompletedTask;
    }

    public async Task<IEnumerable<LogEvent>> GetRecentLogs(int count = 100)
    {
        var logs = new List<LogEvent>();
        var tempQueue = new Queue<LogEvent>();

        // Dequeue all events, keep only the last 'count' items
        while (_logEvents.TryDequeue(out var logEvent))
        {
            tempQueue.Enqueue(logEvent);
            if (tempQueue.Count > count)
            {
                tempQueue.Dequeue();
            }
        }

        // Put them back and return
        while (tempQueue.Count > 0)
        {
            var logEvent = tempQueue.Dequeue();
            logs.Add(logEvent);
            _logEvents.Enqueue(logEvent);
        }

        return await Task.FromResult(logs);
    }

    public async Task<IEnumerable<LogEvent>> QueryLogs(string query, string? level = null, DateTime? since = null)
    {
        var allLogs = await GetRecentLogs(1000); // Get more for querying
        
        var filteredLogs = allLogs.Where(log =>
        {
            var matchesQuery = string.IsNullOrEmpty(query) || 
                              log.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                              log.Source.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                              log.Category.Contains(query, StringComparison.OrdinalIgnoreCase);

            var matchesLevel = string.IsNullOrEmpty(level) || 
                              log.Level.Equals(level, StringComparison.OrdinalIgnoreCase);

            var matchesSince = since == null || log.Timestamp >= since;

            return matchesQuery && matchesLevel && matchesSince;
        });

        return await Task.FromResult(filteredLogs.OrderByDescending(log => log.Timestamp));
    }

    private LogLevel ParseLogLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" or "INFORMATION" => LogLevel.Information,
            "WARN" or "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "FATAL" or "CRITICAL" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}
