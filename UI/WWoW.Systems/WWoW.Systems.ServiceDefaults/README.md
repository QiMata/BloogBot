# WWoW.Systems.ServiceDefaults

Shared service configuration library for WWoW microservices. Provides common defaults for observability, resilience, and service discovery across all Aspire-managed services.
A shared .NET 8 library that provides common service configuration and cross-cutting concerns for the WWoW (World of Warcraft) Systems ecosystem. This project implements the .NET Aspire service defaults pattern to ensure consistent observability, resilience, and health monitoring across all services.

## Overview
## Purpose

WWoW.Systems.ServiceDefaults is a shared project that configures:
- **OpenTelemetry**: Distributed tracing, metrics, and logging
- **Health Checks**: Service liveness and readiness endpoints
- **Service Discovery**: Automatic service location
- **HTTP Resilience**: Retry policies and circuit breakers
The ServiceDefaults project centralizes common service configuration patterns to provide:

## Project Structure
- **Standardized OpenTelemetry**: Consistent logging, metrics, and tracing across all services
- **Service Discovery**: Automatic service location and communication configuration
- **Resilience Patterns**: Built-in retry policies and circuit breakers for HTTP clients
- **Health Monitoring**: Standardized health check endpoints and monitoring
- **Development Experience**: Simplified service configuration and startup

```
WWoW.Systems.ServiceDefaults/
??? Extensions.cs    # Service configuration extensions
```
## Features

## Configuration
### OpenTelemetry Integration
- **Structured Logging**: Enhanced logging with formatted messages and scopes
- **Metrics Collection**: ASP.NET Core, HTTP client, and .NET runtime metrics
- **Distributed Tracing**: Request tracing across service boundaries
- **OTLP Export**: Support for OpenTelemetry Protocol exporters

### Adding to a Service
### Service Discovery
- **Automatic Resolution**: Services can discover and communicate with each other
- **HTTP Client Configuration**: Automatic service discovery for outbound HTTP calls
- **Configurable Schemes**: Support for restricting allowed communication protocols

### Resilience
- **Standard Handlers**: Built-in retry policies and circuit breakers
- **HTTP Client Defaults**: Automatic resilience for all HTTP communications
- **Fault Tolerance**: Improved service reliability under failure conditions

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

// Map service default endpoints
// Map default endpoints (health checks)
app.MapDefaultEndpoints();

app.Run();
// Add your application-specific endpoints
app.MapControllers();

await app.RunAsync();
```

## OpenTelemetry Configuration
### Custom OpenTelemetry Configuration

### Tracing
```csharp
var builder = WebApplication.CreateBuilder(args);

Automatically instruments:
- ASP.NET Core requests
- HTTP client calls
- Custom spans
// Add only OpenTelemetry configuration
builder.ConfigureOpenTelemetry();

### Metrics
// Add other services manually
builder.Services.AddServiceDiscovery();
```

Collects:
- HTTP request metrics
- Runtime metrics (GC, threads, etc.)
- Custom metrics
## Configuration

### Logging
### Environment Variables

Exports structured logs to:
- Console (development)
- OTLP endpoint (production)
The service defaults respond to several standard environment variables:

## HTTP Resilience
- **OTEL_EXPORTER_OTLP_ENDPOINT**: OpenTelemetry collector endpoint
- **APPLICATIONINSIGHTS_CONNECTION_STRING**: Azure Application Insights connection

Configures Polly policies for outgoing HTTP calls:
### Service Discovery Options

Service discovery behavior can be customized through configuration:

```csharp
// Automatic retry with exponential backoff
// Circuit breaker for failing services
services.ConfigureHttpClientDefaults(http =>
builder.Services.Configure<ServiceDiscoveryOptions>(options =>
{
    http.AddStandardResilienceHandler();
    options.AllowedSchemes = ["https"]; // Restrict to HTTPS only
});
```

### Retry Policy
## Health Check Endpoints

- 3 retries with exponential backoff
- Jitter to prevent thundering herd
When running in development mode, the following endpoints are available:

### Circuit Breaker
- **GET /health**: Complete health check (all registered checks must pass)
- **GET /alive**: Liveness check (only checks tagged with "live")

- Opens after 5 consecutive failures
- 30 second break duration
## Project Structure

## Service Discovery
```
WWoW.Systems.ServiceDefaults/
??? Extensions.cs                    # Main extension methods
??? WWoW.Systems.ServiceDefaults.csproj  # Project configuration
??? README.md                       # This documentation
```

## Dependencies

### NuGet Packages
- **Microsoft.Extensions.Http.Resilience**: HTTP resilience patterns
- **Microsoft.Extensions.ServiceDiscovery**: Service discovery capabilities
- **OpenTelemetry.Exporter.OpenTelemetryProtocol**: OTLP export support
- **OpenTelemetry.Extensions.Hosting**: OpenTelemetry hosting integration
- **OpenTelemetry.Instrumentation.AspNetCore**: ASP.NET Core telemetry
- **OpenTelemetry.Instrumentation.Http**: HTTP client telemetry
- **OpenTelemetry.Instrumentation.Runtime**: .NET runtime telemetry

### Framework References
- **Microsoft.AspNetCore.App**: ASP.NET Core framework

## Target Framework

- **.NET 8.0**: Modern .NET with improved performance and features
- **Nullable Reference Types**: Enhanced null safety
- **Implicit Usings**: Simplified using statements

Automatic service location via Aspire:
## Build Configuration

```csharp
// In a service that needs PathfindingService
services.AddHttpClient("pathfinding", client =>
{
    client.BaseAddress = new Uri("http://pathfinding");
});
```
### Output Directory
Build artifacts are directed to `../../Bot` for integration with the BloogBot ecosystem.

## Health Checks
### Aspire Integration
- **IsAspireSharedProject**: Marked as an Aspire shared project
- **Service Defaults Pattern**: Implements standard Aspire service configuration

### Endpoints
## Integration Points

- `/health` - Overall health
- `/alive` - Liveness probe
- `/ready` - Readiness probe
### Related Projects
- **WWoW.Systems.AppHost**: Orchestration host that uses these service defaults
- **BloogBot Services**: Backend services that implement these defaults
- **StateManager**: Central coordination service using standard configuration
- **PathfindingService**: Navigation service with observability

### Usage
### BloogBot Ecosystem
This project is part of the larger BloogBot automation ecosystem:
- Provides consistent configuration across all WWoW services
- Enables observability and monitoring for bot operations
- Supports distributed service communication patterns

```csharp
app.MapDefaultEndpoints(); // Maps health check endpoints
```
## Development Guidelines

## Dependencies
### Adding New Defaults
1. **Extension Methods**: Add new configuration as extension methods in `Extensions.cs`
2. **Conditional Logic**: Use environment variables or configuration for optional features
3. **Documentation**: Update this README with new configuration options
4. **Testing**: Verify defaults work across different service types

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Http.Resilience | 9.0.0 | HTTP retry/circuit breaker |
| Microsoft.Extensions.ServiceDiscovery | 9.0.0 | Service location |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.9.0 | OTLP export |
| OpenTelemetry.Extensions.Hosting | 1.9.0 | Hosting integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.9.0 | ASP.NET tracing |
| OpenTelemetry.Instrumentation.Http | 1.9.0 | HTTP client tracing |
| OpenTelemetry.Instrumentation.Runtime | 1.9.0 | Runtime metrics |
### Best Practices
- **Consistent Naming**: Follow established naming conventions
- **Environment Awareness**: Different behavior for development vs production
- **Performance**: Minimize overhead of default configurations
- **Backwards Compatibility**: Avoid breaking changes to existing services

## Environment Configuration
## Troubleshooting

### Development
### Common Issues

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```
**OpenTelemetry Export Failures**
- Verify OTEL_EXPORTER_OTLP_ENDPOINT is correctly configured
- Check network connectivity to telemetry collectors
- Review OpenTelemetry service logs for export errors

### Production
**Service Discovery Problems**
- Ensure service names are correctly registered
- Verify network policies allow inter-service communication
- Check service discovery configuration and endpoints

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://otel-collector:4317",
  "OTEL_SERVICE_NAME": "service-name"
}
```
**Health Check Failures**
- Review registered health checks for failing components
- Check dependencies required for service readiness
- Verify health check endpoints are accessible

## Consuming Services
## Contributing

All services in the WWoW system should reference this project:
1. **Follow Patterns**: Use established extension method patterns
2. **Add Tests**: Include unit tests for new functionality
3. **Update Documentation**: Document new configuration options
4. **Validate Integration**: Test with existing WWoW services
5. **Performance Testing**: Ensure minimal overhead

```xml
<ProjectReference Include="..\WWoW.Systems.ServiceDefaults\WWoW.Systems.ServiceDefaults.csproj" />
```
## License

Then call `AddServiceDefaults()` and `MapDefaultEndpoints()`.
This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Related Documentation
---

- See `UI/WWoW.Systems/WWoW.Systems.AppHost/README.md` for orchestration
- See [.NET Aspire Service Defaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults)
- See `ARCHITECTURE.md` for system overview
The WWoW.Systems.ServiceDefaults project provides essential foundational services for the BloogBot ecosystem, ensuring consistent observability, resilience, and operational excellence across all distributed services.