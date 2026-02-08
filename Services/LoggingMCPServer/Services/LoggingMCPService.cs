using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoggingMCPServer.Services;

public class LoggingMCPService : BackgroundService
{
    private readonly ILogger<LoggingMCPService> _logger;

    public LoggingMCPService(ILogger<LoggingMCPService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BloogBot Logging MCP Server starting with stdio transport");

        try
        {
            // Read from stdin and process MCP messages
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();
            using var reader = new StreamReader(stdin);
            using var writer = new StreamWriter(stdout) { AutoFlush = true };

            while (!stoppingToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) 
                {
                    _logger.LogInformation("Received null input, waiting briefly before shutdown");
                    // Wait a bit to ensure any pending responses are sent
                    await Task.Delay(100, stoppingToken);
                    break;
                }

                _logger.LogDebug("Received MCP message: {Message}", line);

                try
                {
                    var response = await ProcessMCPMessage(line);
                    if (response != null)
                    {
                        await writer.WriteLineAsync(response);
                        await writer.FlushAsync();
                        _logger.LogDebug("Sent MCP response: {Response}", response);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MCP message: {Message}", line);
                    
                    // Send error response
                    var errorResponse = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        error = new
                        {
                            code = -32603,
                            message = "Internal error",
                            data = ex.Message
                        }
                    });
                    await writer.WriteLineAsync(errorResponse);
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server");
            throw;
        }
    }

    private async Task<string?> ProcessMCPMessage(string message)
    {
        try
        {
            var jsonNode = JsonNode.Parse(message);
            var method = jsonNode?["method"]?.ToString();
            var id = jsonNode?["id"];

            _logger.LogDebug("Processing method: {Method}", method);

            return method switch
            {
                "initialize" => HandleInitialize(id),
                "tools/list" => HandleToolsList(id),
                "tools/call" => await HandleToolCall(jsonNode, id),
                _ => null // Ignore unknown methods
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MCP message");
            return null;
        }
    }

    private string HandleInitialize(JsonNode? id)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id = id?.ToString(),
            result = new
            {
                protocolVersion = "1.0.0",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "BloogBot-Logging-MCP-Server",
                    version = "1.0.0"
                }
            }
        };

        return JsonSerializer.Serialize(response);
    }

    private string HandleToolsList(JsonNode? id)
    {
        var tools = new object[]
        {
            new
            {
                name = "log_event",
                description = "Log an event to the BloogBot logging system",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        level = new
                        {
                            type = "string",
                            description = "Log level (trace, debug, info, warn, error, critical)",
                            @enum = new[] { "trace", "debug", "info", "warn", "error", "critical" }
                        },
                        message = new
                        {
                            type = "string",
                            description = "The log message"
                        },
                        source = new
                        {
                            type = "string",
                            description = "Source of the log event (optional)"
                        }
                    },
                    required = new[] { "level", "message" }
                }
            },
            new
            {
                name = "get_logs",
                description = "Retrieve recent log events",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        count = new
                        {
                            type = "integer",
                            description = "Number of recent logs to retrieve (default: 10)",
                            minimum = 1,
                            maximum = 100
                        }
                    }
                }
            }
        };

        var response = new
        {
            jsonrpc = "2.0",
            id = id?.ToString(),
            result = new { tools }
        };

        return JsonSerializer.Serialize(response);
    }

    private async Task<string> HandleToolCall(JsonNode? jsonNode, JsonNode? id)
    {
        try
        {
            var toolName = jsonNode?["params"]?["name"]?.ToString();
            var arguments = jsonNode?["params"]?["arguments"];

            _logger.LogDebug("Calling tool: {ToolName}", toolName);

            var result = toolName switch
            {
                "log_event" => await HandleLogEvent(arguments),
                "get_logs" => await HandleGetLogs(arguments),
                _ => new { error = "Unknown tool", tool = toolName }
            };

            var response = new
            {
                jsonrpc = "2.0",
                id = id?.ToString(),
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

            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call");
            
            var errorResponse = new
            {
                jsonrpc = "2.0",
                id = id?.ToString(),
                result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private Task<object> HandleLogEvent(JsonNode? arguments)
    {
        try
        {
            var level = arguments?["level"]?.ToString() ?? "info";
            var message = arguments?["message"]?.ToString() ?? "";
            var source = arguments?["source"]?.ToString() ?? "MCP";

            // Log the event using the configured logger
            var logLevel = level.ToLowerInvariant() switch
            {
                "trace" => LogLevel.Trace,
                "debug" => LogLevel.Debug,
                "info" => LogLevel.Information,
                "warn" => LogLevel.Warning,
                "error" => LogLevel.Error,
                "critical" => LogLevel.Critical,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, "[{Source}] {Message}", source, message);

            return Task.FromResult<object>(new
            {
                success = true,
                logEventId = Guid.NewGuid(),
                message = "Log event processed successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling log_event tool call");
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private Task<object> HandleGetLogs(JsonNode? arguments)
    {
        try
        {
            var count = arguments?["count"]?.GetValue<int>() ?? 10;
            
            // For now, return a mock response since we don't have a log storage system
            var logs = Enumerable.Range(1, Math.Min(count, 10))
                .Select(i => new
                {
                    id = Guid.NewGuid(),
                    timestamp = DateTime.UtcNow.AddMinutes(-i),
                    level = "info",
                    message = $"Sample log entry {i}",
                    source = "BloogBot"
                })
                .ToList();

            return Task.FromResult<object>(new
            {
                success = true,
                count = logs.Count,
                logs = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling get_logs tool call");
            return Task.FromResult<object>(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
