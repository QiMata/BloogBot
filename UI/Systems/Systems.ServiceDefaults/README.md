# Systems.ServiceDefaults

Shared Aspire service defaults for WWoW services. The extensions configure OpenTelemetry, health checks, service discovery, and HTTP resilience with configuration hooks that are useful for FG/BG parity diagnostics.

## Commands

```powershell
dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release
dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"
```

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

## Configuration

Telemetry resource fields:

```json
{
  "ServiceDefaults": {
    "Telemetry": {
      "ServiceName": "BackgroundBotRunner",
      "BotRole": "BG",
      "ScenarioId": "movement-parity",
      "TestId": "corpse-run-001"
    }
  }
}
```

Resource attributes emitted when configured:

- `service.name`
- `wwow.bot.role`
- `wwow.scenario.id`
- `wwow.test.id`

Health endpoints:

```json
{
  "ServiceDefaults": {
    "Health": {
      "ExposeEndpoints": true
    }
  }
}
```

`/health` and `/alive` are always mapped in Development. In non-Development environments they are mapped only when `ServiceDefaults:Health:ExposeEndpoints=true`.

Resilience:

```json
{
  "ServiceDefaults": {
    "Resilience": {
      "EnableStandardHandler": false
    }
  }
}
```

Leave `EnableStandardHandler=true` for normal service runs. Set it to `false` for deterministic integration tests that need no implicit retries.

Service discovery:

```json
{
  "ServiceDefaults": {
    "ServiceDiscovery": {
      "AllowAllSchemes": false,
      "AllowedSchemes": "https,http"
    }
  }
}
```

By default all schemes are allowed, matching Aspire defaults. If `AllowAllSchemes=false` and no explicit list is provided, the fallback allowed scheme is `https`.
