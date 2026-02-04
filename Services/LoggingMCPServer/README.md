# BloogBot HTTP MCP Server

A fully functional HTTP-based Model Context Protocol (MCP) server that provides logging and telemetry capabilities for the BloogBot AI system.

## Overview

This server implements the MCP protocol over HTTP REST API, providing structured logging, telemetry collection, and system monitoring capabilities. It runs on `http://localhost:5001` and integrates with VS Code via the MCP extension.

## Features

- **HTTP-based MCP Protocol**: Full MCP 2024-11-05 protocol implementation over HTTP
- **Structured Logging**: Create and retrieve log events with filtering and search
- **Telemetry Collection**: System metrics and event tracking
- **Swagger API Documentation**: Interactive API explorer at root URL
- **VS Code Integration**: Compatible with VS Code MCP extension
- **Real-time Monitoring**: Health checks and system status endpoints

## API Endpoints

### Health Check
- `GET /health` - Server health status

### MCP Protocol Endpoints
- `POST /api/mcp/initialize` - Initialize MCP protocol session
- `GET /api/mcp/tools` - List available MCP tools
- `POST /api/mcp/tools/call` - Execute MCP tools

### Logging Endpoints
- `POST /api/logs/event` - Create a new log event
- `GET /api/logs` - Retrieve recent log events
- `GET /api/logs/query` - Advanced log querying with filters
- `GET /api/logs/stats` - Log statistics and summary

### Telemetry Endpoints
- `GET /api/telemetry/metrics` - Get all system metrics
- `GET /api/telemetry/events` - Get recent system events
- `GET /api/telemetry/status` - Comprehensive system status
- `POST /api/telemetry/metrics/{name}` - Record custom metric
- `POST /api/telemetry/events` - Record custom event
- `GET /api/telemetry/metrics/{name}` - Get specific metric value

## MCP Tools

### log_event
Create a new log event with structured data.

**Parameters:**
- `message` (required): The log message
- `level` (optional): Log level (info, warn, error, debug) - default: "info"
- `source` (optional): Source component or system
- `category` (optional): Log category or classification
- `properties` (optional): Additional structured properties

**Example:**
```json
{
  "name": "log_event",
  "arguments": {
    "message": "User authentication successful",
    "level": "info",
    "source": "AuthService",
    "category": "Security",
    "properties": {
      "userId": "12345",
      "sessionId": "abc-def-789"
    }
  }
}
```

### get_logs
Retrieve recent log events with optional filtering.

**Parameters:**
- `count` (optional): Number of logs to retrieve - default: 100
- `level` (optional): Filter by log level
- `source` (optional): Filter by source
- `query` (optional): Text search query

**Example:**
```json
{
  "name": "get_logs",
  "arguments": {
    "count": 50,
    "level": "error",
    "source": "AuthService"
  }
}
```

## Quick Start

### 1. Build and Run
```bash
cd Services/LoggingMCPServer
dotnet build
dotnet run
```

### 2. Access Swagger UI
Open http://localhost:5001 in your browser to access the interactive API documentation.

### 3. Test MCP Protocol
```bash
# List available tools
curl -X GET "http://localhost:5001/api/mcp/tools"

# Create a log event
curl -X POST "http://localhost:5001/api/mcp/tools/call" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "log_event",
    "arguments": {
      "message": "Test log from HTTP API",
      "level": "info",
      "source": "HTTP_Test"
    }
  }'

# Retrieve logs
curl -X POST "http://localhost:5001/api/mcp/tools/call" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "get_logs",
    "arguments": {
      "count": 10
    }
  }'
```

### 4. VS Code Integration
The server is configured in `.vscode/mcp.json`:
```json
{
  "servers": {
    "logging": {
      "url": "http://localhost:5001/api/mcp",
      "type": "http"
    }
  }
}
```

## Logging Configuration

The server uses Serilog for structured logging:
- Console output for development
- Rolling file logs in `logs/loggingmcp-{date}.txt`
- JSON structured format
- Log context enrichment

## Telemetry Features

### Automatic Metrics
- Memory usage tracking
- Uptime monitoring
- Request counting
- Event statistics

### Custom Metrics
Record custom metrics via API:
```bash
curl -X POST "http://localhost:5001/api/telemetry/metrics/custom_metric" \
  -H "Content-Type: application/json" \
  -d '{"value": 42.5}'
```

### Event Tracking
All API calls and system events are automatically tracked with:
- Timestamp
- Event type
- Description
- Additional context data

## Integration Examples

### Direct HTTP API
```csharp
// Create log event
var logRequest = new {
    message = "Process completed successfully",
    level = "info",
    source = "ProcessingService",
    category = "Operations"
};

var response = await httpClient.PostAsync(
    "http://localhost:5001/api/logs/event",
    new StringContent(JsonSerializer.Serialize(logRequest), Encoding.UTF8, "application/json")
);
```

### MCP Protocol
```csharp
// Call MCP tool
var mcpRequest = new {
    name = "log_event",
    arguments = new {
        message = "MCP integration test",
        level = "debug",
        source = "MCPClient"
    }
};

var response = await httpClient.PostAsync(
    "http://localhost:5001/api/mcp/tools/call",
    new StringContent(JsonSerializer.Serialize(mcpRequest), Encoding.UTF8, "application/json")
);
```

## Configuration

The server is configured for AI-readiness:
- **Port**: localhost:5001 (matches AI-readiness requirements)
- **CORS**: Enabled for cross-origin requests
- **Swagger**: Available at root for API exploration
- **Health Checks**: Monitoring endpoint available
- **Structured Logging**: JSON format with context
- **Telemetry**: Comprehensive system monitoring

## Dependencies

- .NET 8.0 SDK
- ASP.NET Core Web API
- Serilog for logging
- Swashbuckle for Swagger/OpenAPI
- System.Text.Json for serialization

## Architecture

```
HTTP Client/VS Code MCP Extension
           ↓
    localhost:5001 (Kestrel)
           ↓
   ASP.NET Core Web API
           ↓
    ┌─────────────────┐
    │   Controllers   │
    │ - McpController │
    │ - LogsController│
    │ - TelemetryCtrl │
    └─────────────────┘
           ↓
    ┌─────────────────┐
    │    Services     │
    │ - LogEventProc  │
    │ - TelemetryCol  │
    └─────────────────┘
           ↓
    ┌─────────────────┐
    │   Storage       │
    │ - Memory Lists  │
    │ - File Logs     │
    └─────────────────┘
```

## Status

✅ **COMPLETE**: HTTP-based MCP server with full AI-readiness compliance
- HTTP API on localhost:5001
- Structured JSON logging with context
- Telemetry collection and analysis
- MCP protocol implementation
- VS Code integration ready
- Swagger documentation
- Health monitoring

This implementation fulfills all AI-readiness requirements for the BloogBot logging infrastructure.
