# WWoW.Systems.AppHost

.NET Aspire application host for orchestrating the WWoW distributed services. Provides local development orchestration, service discovery, and observability configuration.

## Overview

WWoW.Systems.AppHost is an Aspire AppHost that:
- **Orchestrates Services**: Starts and manages all microservices
- **Configures Dependencies**: Sets up inter-service connections
- **Provides Dashboard**: Unified observability UI
- **Manages Configuration**: Centralized settings distribution

## Project Structure

```
WWoW.Systems.AppHost/
??? Program.cs    # Service orchestration configuration
```

## Aspire Configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add services
var pathfinding = builder.AddProject<Projects.PathfindingService>("pathfinding");
var stateManager = builder.AddProject<Projects.StateManager>("statemanager");
var decisionEngine = builder.AddProject<Projects.DecisionEngineService>("decisionengine");
var promptHandler = builder.AddProject<Projects.PromptHandlingService>("prompthandler");

// Configure dependencies
decisionEngine.WithReference(pathfinding)
              .WithReference(stateManager);

promptHandler.WithReference(decisionEngine);

builder.Build().Run();
```

## Service Dependencies

```
                    ???????????????????
                    ?   AppHost       ?
                    ?  (Orchestrator) ?
                    ???????????????????
                             ?
        ???????????????????????????????????????????
        ?                    ?                    ?
        ?                    ?                    ?
?????????????????   ?????????????????   ?????????????????
? Pathfinding   ?   ? StateManager  ?   ? Prompt        ?
? Service       ?   ?               ?   ? Handler       ?
?????????????????   ?????????????????   ?????????????????
        ?                    ?                  ?
        ?                    ?                  ?
        ?                    ?          ?????????????????
        ????????????????????????????????? Decision      ?
                                        ? Engine        ?
                                        ?????????????????
```

## Running

### Prerequisites

- .NET 8 SDK
- .NET Aspire workload: `dotnet workload install aspire`

### Development

```bash
# From solution root
dotnet run --project UI/WWoW.Systems/WWoW.Systems.AppHost
```

### Dashboard Access

After starting, access the Aspire dashboard at:
- **Dashboard**: https://localhost:17000

The dashboard provides:
- Service health status
- Distributed tracing
- Structured logging
- Metrics visualization

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Aspire.AppHost.Sdk | 9.0.0 | Aspire orchestration SDK |
| Aspire.Hosting.AppHost | 9.0.0 | Hosting infrastructure |

## Configuration

### appsettings.json

```json
{
  "Aspire": {
    "Dashboard": {
      "Enabled": true
    }
  }
}
```

### User Secrets

Sensitive configuration stored in user secrets:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-key"
```

## Adding New Services

1. Add project reference to the service
2. Register in Program.cs:

```csharp
var newService = builder.AddProject<Projects.NewService>("newservice")
    .WithReference(existingService);
```

## Related Documentation

- See `UI/WWoW.Systems/WWoW.Systems.ServiceDefaults/README.md` for shared configuration
- See [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- See `ARCHITECTURE.md` for system overview
