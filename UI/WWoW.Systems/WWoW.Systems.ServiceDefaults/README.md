# WWoW.Systems.ServiceDefaults

A shared .NET 8 library that provides common service configuration and cross-cutting concerns for the WWoW Systems ecosystem.

## Overview

WWoW.Systems.ServiceDefaults is a shared library that implements the .NET Aspire service defaults pattern to ensure consistent observability, resilience, and health monitoring across all WWoW services. It centralizes common service configuration patterns to provide standardized OpenTelemetry integration, service discovery, resilience patterns, and health monitoring.

The library provides standardized OpenTelemetry configuration for consistent logging, metrics, and tracing across all services. It includes automatic service discovery for service location and communication configuration, built-in resilience patterns with retry policies and circuit breakers for HTTP clients, and standardized health check endpoints for monitoring.

Key capabilities include structured logging with enhanced formatted messages and scopes, metrics collection for ASP.NET Core, HTTP client, and .NET runtime, distributed tracing for request tracking across service boundaries, OTLP export support for OpenTelemetry Protocol exporters, and simplified service configuration for improved development experience.

## Architecture

The ServiceDefaults library provides extension methods that configure the following cross-cutting concerns:

```
ServiceDefaults
+-- OpenTelemetry Integration
|   +-- Logging (structured logs, OTLP export)
|   +-- Metrics (HTTP, runtime, custom)
|   +-- Tracing (ASP.NET Core, HTTP client)
+-- Service Discovery
|   +-- Automatic service resolution
|   +-- HTTP client configuration
|   +-- Configurable schemes
+-- Resilience Patterns
|   +-- Retry policies with exponential backoff
|   +-- Circuit breakers for fault tolerance
|   +-- HTTP client defaults
+-- Health Checks
    +-- Liveness probes (/alive)
    +-- Readiness checks (/health)
    +-- Development endpoints
```

## Project Structure

```
WWoW.Systems.ServiceDefaults/
+-- Extensions.cs                    # Main extension methods
+-- WWoW.Systems.ServiceDefaults.csproj  # Project configuration
+-- README.md                       # This documentation
```

## Features

### OpenTelemetry Integration

- **Structured Logging**: Enhanced logging with formatted messages and scopes
- **Metrics Collection**: ASP.NET Core, HTTP client, and .NET runtime metrics
- **Distributed Tracing**: Request tracing across service boundaries
- **OTLP Export**: Support for OpenTelemetry Protocol exporters

### Service Discovery

- **Automatic Resolution**: Services can discover and communicate with each other
- **HTTP Client Configuration**: Automatic service discovery for outbound HTTP calls
- **Configurable Schemes**: Support for restricting allowed communication protocols

### Resilience

- **Standard Handlers**: Built-in retry policies and circuit breakers
- **HTTP Client Defaults**: Automatic resilience for all HTTP communications
- **Fault Tolerance**: Improved service reliability under failure conditions

#### Retry Policy

- 3 retries with exponential backoff
- Jitter to prevent thundering herd

#### Circuit Breaker

- Opens after 5 consecutive failures
- 30 second break duration

### Health Checks

- **Liveness Probes**: Basic health check to verify service responsiveness
- **Readiness Checks**: Comprehensive health validation before accepting traffic
- **Development Endpoints**: Health check endpoints available in development mode

## Usage

### Basic Service Configuration

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Add all service defaults
builder.AddServiceDefaults();

// Add your service-specific configuration here
builder.Services.AddScoped<IMyService, MyService>();

var host = builder.Build();
await host.RunAsync();
```

### Web Application Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add service defaults
builder.AddServiceDefaults();

// Add your web-specific services
builder.Services.AddControllers();

var app = builder.Build();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Add your application-specific endpoints
app.MapControllers();

await app.RunAsync();
```

### Custom OpenTelemetry Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add only OpenTelemetry configuration
builder.ConfigureOpenTelemetry();

// Add other services manually
builder.Services.AddServiceDiscovery();
```

### HTTP Resilience

Configures Polly policies for outgoing HTTP calls:

```csharp
// Automatic retry with exponential backoff
// Circuit breaker for failing services
services.ConfigureHttpClientDefaults(http =>
{
    http.AddStandardResilienceHandler();
});
```

### Service Discovery

Automatic service location via Aspire:

```csharp
// In a service that needs PathfindingService
services.AddHttpClient("pathfinding", client =>
{
    client.BaseAddress = new Uri("http://pathfinding");
});
```

## Configuration

### Environment Variables

The service defaults respond to several standard environment variables:

- **OTEL_EXPORTER_OTLP_ENDPOINT**: OpenTelemetry collector endpoint
- **APPLICATIONINSIGHTS_CONNECTION_STRING**: Azure Application Insights connection

### Service Discovery Options

Service discovery behavior can be customized through configuration:

```csharp
builder.Services.Configure<ServiceDiscoveryOptions>(options =>
{
    options.AllowedSchemes = ["https"]; // Restrict to HTTPS only
});
```

### Development Configuration

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

### Production Configuration

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://otel-collector:4317",
  "OTEL_SERVICE_NAME": "service-name"
}
```

## Health Check Endpoints

When running in development mode, the following endpoints are available:

- **GET /health**: Complete health check (all registered checks must pass)
- **GET /alive**: Liveness check (only checks tagged with "live")

Usage:

```csharp
app.MapDefaultEndpoints(); // Maps health check endpoints
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Http.Resilience | 9.0.0 | HTTP retry/circuit breaker |
| Microsoft.Extensions.ServiceDiscovery | 9.0.0 | Service location |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.9.0 | OTLP export |
| OpenTelemetry.Extensions.Hosting | 1.9.0 | Hosting integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.9.0 | ASP.NET tracing |
| OpenTelemetry.Instrumentation.Http | 1.9.0 | HTTP client tracing |
| OpenTelemetry.Instrumentation.Runtime | 1.9.0 | Runtime metrics |

### Framework References

- **Microsoft.AspNetCore.App**: ASP.NET Core framework

## Target Framework

- **.NET 8.0**: Modern .NET with improved performance and features
- **Nullable Reference Types**: Enhanced null safety
- **Implicit Usings**: Simplified using statements

## Build Configuration

### Output Directory

Build artifacts are directed to `../../Bot` for integration with the WWoW ecosystem.

### Aspire Integration

- **IsAspireSharedProject**: Marked as an Aspire shared project
- **Service Defaults Pattern**: Implements standard Aspire service configuration

## Integration Points

### Related Projects

- **WWoW.Systems.AppHost**: Orchestration host that uses these service defaults
- **WWoW Services**: Backend services that implement these defaults
- **StateManager**: Central coordination service using standard configuration
- **PathfindingService**: Navigation service with observability

### WWoW Ecosystem

This project is part of the larger WWoW automation ecosystem:

- Provides consistent configuration across all WWoW services
- Enables observability and monitoring for bot operations
- Supports distributed service communication patterns

## Consuming Services

All services in the WWoW system should reference this project:

```xml
<ProjectReference Include="..\WWoW.Systems.ServiceDefaults\WWoW.Systems.ServiceDefaults.csproj" />
```

Then call `AddServiceDefaults()` and `MapDefaultEndpoints()`.

## Development Guidelines

### Adding New Defaults

1. **Extension Methods**: Add new configuration as extension methods in `Extensions.cs`
2. **Conditional Logic**: Use environment variables or configuration for optional features
3. **Documentation**: Update this README with new configuration options
4. **Testing**: Verify defaults work across different service types

### Best Practices

- **Consistent Naming**: Follow established naming conventions
- **Environment Awareness**: Different behavior for development vs production
- **Performance**: Minimize overhead of default configurations
- **Backwards Compatibility**: Avoid breaking changes to existing services

## Troubleshooting

### Common Issues

**OpenTelemetry Export Failures**
- Verify OTEL_EXPORTER_OTLP_ENDPOINT is correctly configured
- Check network connectivity to telemetry collectors
- Review OpenTelemetry service logs for export errors

**Service Discovery Problems**
- Ensure service names are correctly registered
- Verify network policies allow inter-service communication
- Check service discovery configuration and endpoints

**Health Check Failures**
- Review registered health checks for failing components
- Check dependencies required for service readiness
- Verify health check endpoints are accessible

## Contributing

1. **Follow Patterns**: Use established extension method patterns
2. **Add Tests**: Include unit tests for new functionality
3. **Update Documentation**: Document new configuration options
4. **Validate Integration**: Test with existing WWoW services
5. **Performance Testing**: Ensure minimal overhead

## Related Documentation

- See [WWoW.Systems.AppHost README](../WWoW.Systems.AppHost/README.md) for orchestration
- See [.NET Aspire Service Defaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults) for framework docs
- See [WWoW Services README](../../../Services/README.md) for service integration
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
