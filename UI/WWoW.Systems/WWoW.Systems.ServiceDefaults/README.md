# WWoW.Systems.ServiceDefaults

Shared service configuration library for WWoW microservices. Provides common defaults for observability, resilience, and service discovery across all Aspire-managed services.

## Overview

WWoW.Systems.ServiceDefaults is a shared project that configures:
- **OpenTelemetry**: Distributed tracing, metrics, and logging
- **Health Checks**: Service liveness and readiness endpoints
- **Service Discovery**: Automatic service location
- **HTTP Resilience**: Retry policies and circuit breakers

## Project Structure

```
WWoW.Systems.ServiceDefaults/
??? Extensions.cs    # Service configuration extensions
```

## Configuration

### Adding to a Service

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add service defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Map service default endpoints
app.MapDefaultEndpoints();

app.Run();
```

## OpenTelemetry Configuration

### Tracing

Automatically instruments:
- ASP.NET Core requests
- HTTP client calls
- Custom spans

### Metrics

Collects:
- HTTP request metrics
- Runtime metrics (GC, threads, etc.)
- Custom metrics

### Logging

Exports structured logs to:
- Console (development)
- OTLP endpoint (production)

## HTTP Resilience

Configures Polly policies for outgoing HTTP calls:

```csharp
// Automatic retry with exponential backoff
// Circuit breaker for failing services
services.ConfigureHttpClientDefaults(http =>
{
    http.AddStandardResilienceHandler();
});
```

### Retry Policy

- 3 retries with exponential backoff
- Jitter to prevent thundering herd

### Circuit Breaker

- Opens after 5 consecutive failures
- 30 second break duration

## Service Discovery

Automatic service location via Aspire:

```csharp
// In a service that needs PathfindingService
services.AddHttpClient("pathfinding", client =>
{
    client.BaseAddress = new Uri("http://pathfinding");
});
```

## Health Checks

### Endpoints

- `/health` - Overall health
- `/alive` - Liveness probe
- `/ready` - Readiness probe

### Usage

```csharp
app.MapDefaultEndpoints(); // Maps health check endpoints
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Http.Resilience | 9.0.0 | HTTP retry/circuit breaker |
| Microsoft.Extensions.ServiceDiscovery | 9.0.0 | Service location |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.9.0 | OTLP export |
| OpenTelemetry.Extensions.Hosting | 1.9.0 | Hosting integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.9.0 | ASP.NET tracing |
| OpenTelemetry.Instrumentation.Http | 1.9.0 | HTTP client tracing |
| OpenTelemetry.Instrumentation.Runtime | 1.9.0 | Runtime metrics |

## Environment Configuration

### Development

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

### Production

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://otel-collector:4317",
  "OTEL_SERVICE_NAME": "service-name"
}
```

## Consuming Services

All services in the WWoW system should reference this project:

```xml
<ProjectReference Include="..\WWoW.Systems.ServiceDefaults\WWoW.Systems.ServiceDefaults.csproj" />
```

Then call `AddServiceDefaults()` and `MapDefaultEndpoints()`.

## Related Documentation

- See `UI/WWoW.Systems/WWoW.Systems.AppHost/README.md` for orchestration
- See [.NET Aspire Service Defaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults)
- See `ARCHITECTURE.md` for system overview
