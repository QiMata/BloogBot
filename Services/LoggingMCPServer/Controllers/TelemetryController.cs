using Microsoft.AspNetCore.Mvc;
using BloogBot.LoggingMCPServer.Services;

namespace BloogBot.LoggingMCPServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly TelemetryCollector _telemetryCollector;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(TelemetryCollector telemetryCollector, ILogger<TelemetryController> logger)
    {
        _telemetryCollector = telemetryCollector;
        _logger = logger;
    }

    /// <summary>
    /// Get all system metrics
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        try
        {
            var metrics = _telemetryCollector.GetMetrics();
            
            _telemetryCollector.RecordEvent("metrics_retrieved", "System metrics requested");

            return Ok(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                metrics = metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get recent system events
    /// </summary>
    [HttpGet("events")]
    public IActionResult GetEvents([FromQuery] int limit = 100)
    {
        try
        {
            var events = _telemetryCollector.GetRecentEvents(limit);
            
            _telemetryCollector.RecordEvent("events_retrieved", $"Retrieved {events.Count} events");

            return Ok(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                count = events.Count,
                events = events
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get comprehensive system status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetSystemStatus()
    {
        try
        {
            var metrics = _telemetryCollector.GetMetrics();
            var recentEvents = _telemetryCollector.GetRecentEvents(10);
            
            var status = new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                systemInfo = new
                {
                    processId = Environment.ProcessId,
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    clrVersion = Environment.Version.ToString(),
                    workingSet = Environment.WorkingSet,
                    tickCount = Environment.TickCount64
                },
                metrics = metrics,
                recentEvents = recentEvents
            };

            _telemetryCollector.RecordEvent("system_status_retrieved", "Full system status requested");

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system status");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Record a custom metric
    /// </summary>
    [HttpPost("metrics/{name}")]
    public IActionResult RecordMetric(string name, [FromBody] MetricRequest request)
    {
        try
        {
            _telemetryCollector.RecordMetric(name, request.Value);
            
            return Ok(new
            {
                success = true,
                message = $"Metric '{name}' recorded with value {request.Value}",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording metric {MetricName}", name);
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Record a custom event
    /// </summary>
    [HttpPost("events")]
    public IActionResult RecordEvent([FromBody] EventRequest request)
    {
        try
        {
            _telemetryCollector.RecordEvent(request.Type, request.Description, request.Data);
            
            return Ok(new
            {
                success = true,
                message = "Event recorded successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording event");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get specific metric value
    /// </summary>
    [HttpGet("metrics/{name}")]
    public IActionResult GetMetric(string name)
    {
        try
        {
            var metrics = _telemetryCollector.GetMetrics();
            
            if (metrics.TryGetValue(name, out var value))
            {
                return Ok(new
                {
                    success = true,
                    name = name,
                    value = value,
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Metric '{name}' not found"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metric {MetricName}", name);
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public class MetricRequest
{
    public double Value { get; set; }
}

public class EventRequest
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public object? Data { get; set; }
}
