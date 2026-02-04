using Microsoft.AspNetCore.Mvc;
using BloogBot.LoggingMCPServer.Services;
using System.Text.Json;

namespace BloogBot.LoggingMCPServer.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpBaseController : ControllerBase
{
    private readonly LogEventProcessor _logProcessor;
    private readonly TelemetryCollector _telemetryCollector;
    private readonly ILogger<McpBaseController> _logger;

    public McpBaseController(
        LogEventProcessor logProcessor,
        TelemetryCollector telemetryCollector,
        ILogger<McpBaseController> logger)
    {
        _logProcessor = logProcessor;
        _telemetryCollector = telemetryCollector;
        _logger = logger;
    }

    /// <summary>
    /// MCP Base endpoint - handles protocol negotiation and routing
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest()
    {
        try
        {
            if (Request.Method == "GET")
            {
                // Return server capabilities for GET requests
                var capabilities = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new Dictionary<string, object>()
                    },
                    serverInfo = new
                    {
                        name = "BloogBot Logging MCP Server",
                        version = "1.0.0",
                        description = "HTTP-based MCP server for BloogBot logging and telemetry"
                    },
                    endpoints = new
                    {
                        initialize = "/api/mcp/initialize",
                        tools = "/api/mcp/tools",
                        toolsCall = "/api/mcp/tools/call"
                    }
                };

                _telemetryCollector.RecordEvent("mcp_capabilities_requested", "MCP capabilities requested via base endpoint");
                return Ok(capabilities);
            }
            else if (Request.Method == "POST")
            {
                // Handle JSON-RPC style requests
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                
                if (string.IsNullOrEmpty(body))
                {
                    return BadRequest(new { error = "Empty request body" });
                }

                var jsonDoc = JsonDocument.Parse(body);
                
                // Handle both method-based and direct requests
                var method = jsonDoc.RootElement.TryGetProperty("method", out var methodProp) 
                    ? methodProp.GetString() 
                    : null;
                
                // Check if this is a notification (no 'id' field) or a request (has 'id' field)
                var hasId = jsonDoc.RootElement.TryGetProperty("id", out var idProp);
                
                _logger.LogInformation("Received MCP method: {Method}, HasId: {HasId}", method, hasId);

                return method switch
                {
                    "initialize" => await ForwardToInitialize(body),
                    "tools/list" => await ForwardToToolsList(),
                    "tools/call" => await ForwardToToolsCall(body),
                    "notifications/initialized" => HandleNotificationInitialized(hasId),
                    "ping" => HandlePing(jsonDoc),
                    _ => HandleUnknownMethod(method, jsonDoc)
                };
            }

            return BadRequest(new { error = "Unsupported HTTP method" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP base request");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private Task<IActionResult> ForwardToInitialize(string body)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(body);
            var protocolVersion = jsonDoc.RootElement
                .GetProperty("params")
                .GetProperty("protocolVersion")
                .GetString();

            var response = new
            {
                jsonrpc = "2.0",
                id = jsonDoc.RootElement.GetProperty("id").GetInt32(),
                result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new Dictionary<string, object>()
                    },
                    serverInfo = new
                    {
                        name = "BloogBot Logging MCP Server",
                        version = "1.0.0"
                    }
                }
            };

            _telemetryCollector.RecordEvent("mcp_initialized", "MCP protocol initialized via base endpoint", new { protocolVersion });
            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding to initialize");
            return Task.FromResult<IActionResult>(BadRequest(new { error = ex.Message }));
        }
    }

    private Task<IActionResult> ForwardToToolsList()
    {
        try
        {
            var tools = new object[]
            {
                new
                {
                    name = "log_event",
                    description = "Create a new log event with structured data",
                    inputSchema = new
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
                new
                {
                    name = "get_logs",
                    description = "Retrieve recent log events with optional filtering",
                    inputSchema = new
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

            var response = new
            {
                jsonrpc = "2.0",
                id = 1,
                result = new { tools }
            };

            _telemetryCollector.RecordEvent("mcp_tools_listed", $"Listed {tools.Length} tools via base endpoint");
            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding to tools list");
            return Task.FromResult<IActionResult>(BadRequest(new { error = ex.Message }));
        }
    }

    private async Task<IActionResult> ForwardToToolsCall(string body)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(body);
            var toolName = jsonDoc.RootElement
                .GetProperty("params")
                .GetProperty("name")
                .GetString();
            var arguments = jsonDoc.RootElement
                .GetProperty("params")
                .GetProperty("arguments");

            var result = toolName switch
            {
                "log_event" => await HandleLogEventForward(arguments),
                "get_logs" => await HandleGetLogsForward(arguments),
                _ => new { error = $"Unknown tool: {toolName}" }
            };

            var response = new
            {
                jsonrpc = "2.0",
                id = jsonDoc.RootElement.GetProperty("id").GetInt32(),
                result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding to tools call");
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<object> HandleLogEventForward(JsonElement arguments)
    {
        try
        {
            var message = arguments.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
            var level = arguments.TryGetProperty("level", out var levelProp) ? levelProp.GetString() : "info";
            var source = arguments.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() : "MCP";
            var category = arguments.TryGetProperty("category", out var categoryProp) ? categoryProp.GetString() : null;

            Dictionary<string, string?>? properties = null;
            if (arguments.TryGetProperty("properties", out var propertiesProp))
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, string?>>(propertiesProp.GetRawText());
            }

            var logEvent = new LogEvent
            {
                Timestamp = DateTime.UtcNow,
                Level = level ?? "info",
                Message = message ?? "",
                Source = source ?? "",
                Category = category ?? "",
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                Properties = properties?.ToDictionary(kvp => kvp.Key, kvp => (object)(kvp.Value ?? "")) ?? new Dictionary<string, object>()
            };

            await _logProcessor.ProcessLogEvent(logEvent);

            _telemetryCollector.RecordEvent("mcp_log_event_created", $"Log event created via base MCP endpoint: {level}");

            return new
            {
                success = true,
                message = "Log event created successfully",
                level = logEvent.Level,
                timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = $"Error processing log event: {ex.Message}" };
        }
    }

    private async Task<object> HandleGetLogsForward(JsonElement arguments)
    {
        try
        {
            var count = arguments.TryGetProperty("count", out var countProp) ? countProp.GetInt32() : 100;
            var level = arguments.TryGetProperty("level", out var levelProp) ? levelProp.GetString() : null;
            var source = arguments.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() : null;
            var query = arguments.TryGetProperty("query", out var queryProp) ? queryProp.GetString() : null;

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

            _telemetryCollector.RecordEvent("mcp_logs_retrieved", $"Retrieved {logs.Count} logs via base MCP endpoint", new { count, level, source, query });

            return new
            {
                success = true,
                count = logs.Count,
                logs = logs.Take(50).Select(log => new // Limit for readability
                {
                    timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    level = log.Level,
                    message = log.Message,
                    source = log.Source,
                    category = log.Category
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = $"Error retrieving logs: {ex.Message}" };
        }
    }

    private IActionResult HandleNotificationInitialized(bool hasId)
    {
        try
        {
            _logger.LogInformation("Received MCP notifications/initialized, HasId: {HasId}", hasId);
            _telemetryCollector.RecordEvent("mcp_notification_initialized", "MCP client sent notifications/initialized");
            
            // JSON-RPC spec: notifications (no id) should not return any response
            // If it has an id, it's a request and needs a response
            if (!hasId)
            {
                // True notification - return 204 No Content or just acknowledge silently
                return NoContent();
            }
            else
            {
                // Request with id - return empty result
                return Ok(new { });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notifications/initialized");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private IActionResult HandlePing(JsonDocument jsonDoc)
    {
        try
        {
            var id = jsonDoc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 1;
            
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = new { }
            };

            _telemetryCollector.RecordEvent("mcp_ping_received", "MCP ping request received");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ping");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private IActionResult HandleUnknownMethod(string? method, JsonDocument jsonDoc)
    {
        try
        {
            _logger.LogWarning("Unknown MCP method received: {Method}", method);
            _telemetryCollector.RecordEvent("mcp_unknown_method", $"Unknown MCP method: {method}");

            var id = jsonDoc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 1;
            
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                error = new
                {
                    code = -32601,
                    message = "Method not found",
                    data = $"Unknown method: {method}"
                }
            };

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling unknown method");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
