using Microsoft.AspNetCore.Mvc;
using LoggingMCPServer.Services;
using System.Text.Json;

namespace LoggingMCPServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly LogEventProcessor _logProcessor;
    private readonly TelemetryCollector _telemetryCollector;
    private readonly ILogger<McpController> _logger;

    public McpController(
        LogEventProcessor logProcessor,
        TelemetryCollector telemetryCollector,
        ILogger<McpController> logger)
    {
        _logProcessor = logProcessor;
        _telemetryCollector = telemetryCollector;
        _logger = logger;
    }

    /// <summary>
    /// MCP Initialize endpoint
    /// </summary>
    [HttpPost("initialize")]
    public IActionResult Initialize([FromBody] McpInitializeRequest request)
    {
        try
        {
            var response = new McpInitializeResponse
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new McpCapabilities
                {
                    Tools = new Dictionary<string, object>()
                },
                ServerInfo = new McpServerInfo
                {
                    Name = "BloogBot Logging MCP Server",
                    Version = "1.0.0"
                }
            };

            _telemetryCollector.RecordEvent("mcp_initialized", "MCP protocol initialized", new { request.ProtocolVersion, ClientInfo = request.ClientInfo });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MCP");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// MCP Tools List endpoint
    /// </summary>
    [HttpGet("tools")]
    public IActionResult ListTools()
    {
        try
        {
            var tools = new List<McpTool>
            {
                new McpTool
                {
                    Name = "log_event",
                    Description = "Create a new log event with structured data",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            message = new { type = "string", description = "The log message" },
                            level = new { type = "string", description = "Log level (info, warn, error, debug)", @default = "info" },
                            source = new { type = "string", description = "Source component or system" },
                            category = new { type = "string", description = "Log category or classification" },
                            properties = new { type = "object", description = "Additional structured properties" }
                        },
                        required = new[] { "message" }
                    }
                },
                new McpTool
                {
                    Name = "get_logs",
                    Description = "Retrieve recent log events with optional filtering",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            count = new { type = "integer", description = "Number of logs to retrieve", @default = 100 },
                            level = new { type = "string", description = "Filter by log level" },
                            source = new { type = "string", description = "Filter by source" },
                            query = new { type = "string", description = "Text search query" }
                        }
                    }
                }
            };

            _telemetryCollector.RecordEvent("mcp_tools_listed", $"Listed {tools.Count} tools");

            return Ok(new McpToolsResponse { Tools = tools });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MCP tools");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// MCP Tool Call endpoint
    /// </summary>
    [HttpPost("tools/call")]
    public async Task<IActionResult> CallTool([FromBody] McpToolCallRequest request)
    {
        try
        {
            switch (request.Name)
            {
                case "log_event":
                    return await HandleLogEvent(request.Arguments);
                
                case "get_logs":
                    return await HandleGetLogs(request.Arguments);
                
                default:
                    return BadRequest(new { error = $"Unknown tool: {request.Name}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool {ToolName}", request.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IActionResult> HandleLogEvent(JsonElement? arguments)
    {
        try
        {
            var message = arguments?.GetProperty("message").GetString() ?? "";
            var level = arguments?.TryGetProperty("level", out var levelProp) == true ? levelProp.GetString() : "info";
            var source = arguments?.TryGetProperty("source", out var sourceProp) == true ? sourceProp.GetString() : "MCP";
            var category = arguments?.TryGetProperty("category", out var categoryProp) == true ? categoryProp.GetString() : null;

            Dictionary<string, string?>? properties = null;
            if (arguments?.TryGetProperty("properties", out var propertiesProp) == true)
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, string?>>(propertiesProp.GetRawText());
            }

            var logEvent = new LogEvent
            {
                Timestamp = DateTime.UtcNow,
                Level = level ?? "info",
                Message = message,
                Source = source ?? "MCP",
                Category = category ?? "",
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                Properties = properties?.ToDictionary(k => k.Key, k => (object)(k.Value ?? "")) ?? new Dictionary<string, object>()
            };

            await _logProcessor.ProcessLogEvent(logEvent);

            _telemetryCollector.RecordEvent("mcp_log_event_created", $"Log event created via MCP: {level}");

            return Ok(new McpToolResponse
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Log event created successfully. Level: {logEvent.Level}, Timestamp: {logEvent.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error processing log event: {ex.Message}" });
        }
    }

    private async Task<IActionResult> HandleGetLogs(JsonElement? arguments)
    {
        try
        {
            var count = arguments?.TryGetProperty("count", out var countProp) == true ? countProp.GetInt32() : 100;
            var level = arguments?.TryGetProperty("level", out var levelProp) == true ? levelProp.GetString() : null;
            var source = arguments?.TryGetProperty("source", out var sourceProp) == true ? sourceProp.GetString() : null;
            var query = arguments?.TryGetProperty("query", out var queryProp) == true ? queryProp.GetString() : null;

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
            if (!string.IsNullOrEmpty(query))
            {
                filteredLogs = filteredLogs.Where(log => 
                    log.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (log.Source?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (log.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }
            
            var finalLogs = filteredLogs.ToList();

            _telemetryCollector.RecordEvent("mcp_logs_retrieved", $"Retrieved {finalLogs.Count} logs via MCP");

            var resultText = $"Retrieved {finalLogs.Count} log events:\n\n";
            foreach (var log in finalLogs.Take(50)) // Limit output for readability
            {
                resultText += $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.Level.ToUpper()}: {log.Message}";
                if (!string.IsNullOrEmpty(log.Source))
                    resultText += $" (Source: {log.Source})";
                if (!string.IsNullOrEmpty(log.Category))
                    resultText += $" (Category: {log.Category})";
                resultText += "\n";
            }

            if (finalLogs.Count > 50)
            {
                resultText += $"\n... and {finalLogs.Count - 50} more entries. Use the HTTP API /api/logs for full results.";
            }

            return Ok(new McpToolResponse
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = resultText
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error retrieving logs: {ex.Message}" });
        }
    }
}

// MCP Protocol Models
public class McpInitializeRequest
{
    public string ProtocolVersion { get; set; } = "";
    public object? Capabilities { get; set; }
    public object? ClientInfo { get; set; }
}

public class McpInitializeResponse
{
    public string ProtocolVersion { get; set; } = "";
    public McpCapabilities? Capabilities { get; set; }
    public McpServerInfo? ServerInfo { get; set; }
}

public class McpCapabilities
{
    public Dictionary<string, object>? Tools { get; set; }
}

public class McpServerInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
}

public class McpToolsResponse
{
    public List<McpTool> Tools { get; set; } = new();
}

public class McpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object? InputSchema { get; set; }
}

public class McpToolCallRequest
{
    public string Name { get; set; } = "";
    public JsonElement? Arguments { get; set; }
}

public class McpToolResponse
{
    public List<McpContent> Content { get; set; } = new();
}

public class McpContent
{
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
}
