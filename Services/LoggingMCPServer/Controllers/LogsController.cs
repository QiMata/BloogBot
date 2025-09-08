using Microsoft.AspNetCore.Mvc;
using BloogBot.LoggingMCPServer.Services;
using System.ComponentModel.DataAnnotations;

namespace BloogBot.LoggingMCPServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogEventProcessor _logProcessor;
    private readonly TelemetryCollector _telemetryCollector;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        LogEventProcessor logProcessor,
        TelemetryCollector telemetryCollector,
        ILogger<LogsController> logger)
    {
        _logProcessor = logProcessor;
        _telemetryCollector = telemetryCollector;
        _logger = logger;
    }

    /// <summary>
    /// Create a new log event
    /// </summary>
    [HttpPost("event")]
    public async Task<IActionResult> LogEvent([FromBody] LogEventRequest request)
    {
        try
        {
            var logEvent = new LogEvent
            {
                Timestamp = DateTime.UtcNow,
                Level = request.Level ?? "info",
                Message = request.Message ?? "",
                Source = request.Source ?? "HTTP",
                Category = request.Category ?? "",
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                Properties = request.Properties?.ToDictionary(k => k.Key, k => (object)(k.Value ?? "")) ?? new Dictionary<string, object>()
            };

            await _logProcessor.ProcessLogEvent(logEvent);

            // Record telemetry
            _telemetryCollector.RecordEvent("log_event_created", $"Log level: {logEvent.Level}");
            _telemetryCollector.RecordMetric("total_log_events", DateTime.UtcNow.Ticks);

            return Ok(new
            {
                success = true,
                message = "Log event processed successfully",
                timestamp = logEvent.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing log event");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Retrieve recent log events
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int count = 100,
        [FromQuery] string? level = null,
        [FromQuery] string? source = null,
        [FromQuery] string? q = null)
    {
        try
        {
            var logs = await _logProcessor.GetRecentLogs(count);

            // Apply filters manually
            var filteredLogs = logs.AsEnumerable();
            
            if (!string.IsNullOrEmpty(level))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(source))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Source?.Contains(source, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Apply text search if provided
            if (!string.IsNullOrEmpty(q))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Message.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (log.Source?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (log.Category?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }
            
            var finalLogs = filteredLogs.ToList();

            _telemetryCollector.RecordEvent("logs_retrieved", $"Retrieved {finalLogs.Count} logs");

            return Ok(new
            {
                success = true,
                count = finalLogs.Count,
                logs = finalLogs.Select(log => new
                {
                    timestamp = log.Timestamp,
                    level = log.Level,
                    message = log.Message,
                    source = log.Source,
                    category = log.Category,
                    processId = log.ProcessId,
                    threadId = log.ThreadId,
                    properties = log.Properties
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Query logs with advanced filtering
    /// </summary>
    [HttpGet("query")]
    public async Task<IActionResult> QueryLogs(
        [FromQuery] string? q = null,
        [FromQuery] string? level = null,
        [FromQuery] string? source = null,
        [FromQuery] string? category = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            var logs = await _logProcessor.QueryLogs(q ?? "", level, from);
            
            // Apply additional filters manually
            var filteredLogs = logs.AsEnumerable();
            
            if (!string.IsNullOrEmpty(source))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Source?.Contains(source, StringComparison.OrdinalIgnoreCase) == true);
            }
            
            if (!string.IsNullOrEmpty(category))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Category?.Contains(category, StringComparison.OrdinalIgnoreCase) == true);
            }
            
            if (to.HasValue)
            {
                filteredLogs = filteredLogs.Where(log => log.Timestamp <= to.Value);
            }
            
            var finalLogs = filteredLogs.Take(limit).ToList();

            _telemetryCollector.RecordEvent("logs_queried", $"Query returned {finalLogs.Count} results");

            return Ok(new
            {
                success = true,
                count = finalLogs.Count,
                query = new { q, level, source, category, from, to, limit },
                logs = finalLogs.Select(log => new
                {
                    timestamp = log.Timestamp,
                    level = log.Level,
                    message = log.Message,
                    source = log.Source,
                    category = log.Category,
                    processId = log.ProcessId,
                    threadId = log.ThreadId,
                    properties = log.Properties
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying logs");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetLogStats()
    {
        try
        {
            var allLogs = await _logProcessor.GetRecentLogs(1000);
            
            var stats = new
            {
                totalEvents = allLogs.Count(),
                levelDistribution = allLogs.GroupBy(l => l.Level)
                    .ToDictionary(g => g.Key, g => g.Count()),
                sourceDistribution = allLogs.GroupBy(l => l.Source)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
                categoryDistribution = allLogs.GroupBy(l => l.Category)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
                timeRange = allLogs.Any() ? new
                {
                    earliest = allLogs.Min(l => l.Timestamp),
                    latest = allLogs.Max(l => l.Timestamp)
                } : null
            };
            
            _telemetryCollector.RecordEvent("log_stats_retrieved", "Log statistics requested");

            return Ok(new
            {
                success = true,
                statistics = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log statistics");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public class LogEventRequest
{
    [Required]
    public string? Message { get; set; }
    
    public string? Level { get; set; } = "info";
    public string? Source { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, string?>? Properties { get; set; }
}
