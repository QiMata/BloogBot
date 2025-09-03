# WWoW.Systems.ServiceDefaults

A shared .NET 8 library that provides common service configuration and cross-cutting concerns for the WWoW (World of Warcraft) Systems ecosystem. This project implements the .NET Aspire service defaults pattern to ensure consistent observability, resilience, and health monitoring across all services.

## Purpose

The ServiceDefaults project centralizes common service configuration patterns to provide:

- **Standardized OpenTelemetry**: Consistent logging, metrics, and tracing across all services
- **Service Discovery**: Automatic service location and communication configuration
- **Resilience Patterns**: Built-in retry policies and circuit breakers for HTTP clients
- **Health Monitoring**: Standardized health check endpoints and monitoring
- **Development Experience**: Simplified service configuration and startup

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

## Health Check Endpoints

When running in development mode, the following endpoints are available:

- **GET /health**: Complete health check (all registered checks must pass)
- **GET /alive**: Liveness check (only checks tagged with "live")

## Project Structure

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

## Build Configuration

### Output Directory
Build artifacts are directed to `../../Bot` for integration with the BloogBot ecosystem.

### Aspire Integration
- **IsAspireSharedProject**: Marked as an Aspire shared project
- **Service Defaults Pattern**: Implements standard Aspire service configuration

## Integration Points

### Related Projects
- **WWoW.Systems.AppHost**: Orchestration host that uses these service defaults
- **BloogBot Services**: Backend services that implement these defaults
- **StateManager**: Central coordination service using standard configuration
- **PathfindingService**: Navigation service with observability

### BloogBot Ecosystem
This project is part of the larger BloogBot automation ecosystem:
- Provides consistent configuration across all WWoW services
- Enables observability and monitoring for bot operations
- Supports distributed service communication patterns

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

## License

This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

---

The WWoW.Systems.ServiceDefaults project provides essential foundational services for the BloogBot ecosystem, ensuring consistent observability, resilience, and operational excellence across all distributed services.