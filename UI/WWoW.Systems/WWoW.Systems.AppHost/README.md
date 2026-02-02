# WWoW.Systems.AppHost

.NET Aspire application host for orchestrating the WWoW distributed services. Provides local development orchestration, service discovery, and observability configuration.
The WWoW.Systems.AppHost is an ASP.NET Core Aspire application host that orchestrates and manages containerized World of Warcraft Vanilla server infrastructure. This project provides a development and testing environment for the BloogBot ecosystem by hosting a complete WoW Vanilla server stack.

## Overview

WWoW.Systems.AppHost is an Aspire AppHost that:
- **Orchestrates Services**: Starts and manages all microservices
- **Configures Dependencies**: Sets up inter-service connections
- **Provides Dashboard**: Unified observability UI
- **Manages Configuration**: Centralized settings distribution
This application host uses .NET Aspire to define and manage the lifecycle of containerized services required for running a World of Warcraft Vanilla server environment. It coordinates the deployment of MySQL database containers and WoW server containers with proper configuration, networking, and data persistence.

## Architecture

The application orchestrates two primary containers:

### MySQL Database Container
- **Image**: `ragedunicorn/mysql`
- **Purpose**: Hosts the World of Warcraft database with character data, world data, and server configuration
- **Port**: 3306 (MySQL default)
- **Persistence**: Volume-mounted data storage for database persistence

### WoW Vanilla Server Container
- **Image**: `ragedunicorn/wow-vanilla`
- **Purpose**: Runs the MaNGOS-based World of Warcraft Vanilla server
- **Ports**: 
  - 8085 (MangosWorld - Game world server)
  - 3724 (MangosRealm - Authentication server)
- **Dependencies**: MySQL database container

## Configuration

### Database Configuration
```csharp
var database = builder.AddContainer("wow-vanilla-database", WowServerConfig.DbContainerImage)
    .WithEnvironment("MYSQL_APP_USER", "app")
    .WithEnvironment("MYSQL_APP_PASSWORD", "app")
    .WithVolume("wow_vanilla_mysql_data", "/var/lib/mysql")
```

### Server Configuration
The WoW server container is configured with:
- Environment variables for database connectivity
- Volume mounts for persistent logging
- Bind mounts for server configuration files and game data
- Network dependencies on the database container

## Project Structure

```
WWoW.Systems.AppHost/
??? Program.cs    # Service orchestration configuration
??? Program.cs              # Main application entry point and container orchestration
??? WowServerConfig.cs      # Configuration constants and settings
??? WWoW.Systems.AppHost.csproj  # Project file with Aspire SDK
??? README.md              # This documentation
```

## Aspire Configuration
## Dependencies

```csharp
var builder = DistributedApplication.CreateBuilder(args);
### Framework Dependencies
- **.NET 8.0**: Target framework
- **Aspire.AppHost.Sdk (9.0.0)**: Aspire application host SDK
- **Aspire.Hosting.AppHost (9.0.0)**: Core Aspire hosting functionality

// Add services
var pathfinding = builder.AddProject<Projects.PathfindingService>("pathfinding");
var stateManager = builder.AddProject<Projects.StateManager>("statemanager");
var decisionEngine = builder.AddProject<Projects.DecisionEngineService>("decisionengine");
var promptHandler = builder.AddProject<Projects.PromptHandlingService>("prompthandler");
### Container Images
- **ragedunicorn/mysql**: MySQL database server for WoW data
- **ragedunicorn/wow-vanilla**: MaNGOS-based WoW Vanilla server

// Configure dependencies
decisionEngine.WithReference(pathfinding)
              .WithReference(stateManager);
## Volume Management

promptHandler.WithReference(decisionEngine);
The application manages persistent data through Docker volumes:

builder.Build().Run();
```
### Database Storage
- **Volume**: `wow_vanilla_mysql_data`
- **Mount Point**: `/var/lib/mysql`
- **Purpose**: Persistent MySQL database storage

## Service Dependencies
### Log Storage
- **Volume**: `wow_vanilla_log_data`
- **Mount Point**: `/var/log/wow`
- **Purpose**: Server log persistence and debugging

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
## Data Binding

## Running
The application requires local data directories for proper server operation:

### Prerequisites
### Configuration Files
- **Local**: `./config/`
- **Container**: `/opt/vanilla/etc/`
- **Files**: `mangosd.conf.tpl`, `realmd.conf.tpl`

- .NET 8 SDK
- .NET Aspire workload: `dotnet workload install aspire`
### Game Data
- **Local**: `./data/`
- **Container**: `/opt/vanilla/data/`
- **Directories**: `dbc/`, `maps/`, `mmaps/`, `vmaps/`

## Usage

### Development
### Prerequisites
1. Docker Desktop installed and running
2. Required WoW client data files in `./data/` directory
3. Server configuration templates in `./config/` directory

### Running the Application
```bash
# From solution root
dotnet run --project UI/WWoW.Systems/WWoW.Systems.AppHost
```

### Dashboard Access
### Accessing Services
Once running, the following endpoints are available:
- **MySQL Database**: `localhost:3306`
- **WoW Realm Server**: `localhost:3724`
- **WoW World Server**: `localhost:8085`

## Development Integration

This AppHost is designed to support the BloogBot development workflow:

1. **Local Testing**: Provides a controlled WoW server environment for bot testing
2. **Development Isolation**: Containerized services prevent conflicts with host system
3. **Data Persistence**: Maintains character and world state across development sessions
4. **Service Orchestration**: Handles complex startup dependencies automatically

## Security Configuration

The application uses .NET User Secrets for sensitive configuration:
- **User Secrets ID**: `b785e8f1-933c-47f0-bd54-a38fb402bd0a`
- **Purpose**: Store sensitive database credentials and API keys

## Build Configuration

After starting, access the Aspire dashboard at:
- **Dashboard**: https://localhost:17000
### Output Path
Build artifacts are directed to `../../Bot` directory for integration with the main BloogBot application.

The dashboard provides:
- Service health status
- Distributed tracing
- Structured logging
- Metrics visualization
### SDK Configuration
- **Aspire Host**: Enabled (`<IsAspireHost>true</IsAspireHost>`)
- **Output Type**: Console executable
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled

## Dependencies
## Troubleshooting

| Package | Version | Purpose |
|---------|---------|---------|
| Aspire.AppHost.Sdk | 9.0.0 | Aspire orchestration SDK |
| Aspire.Hosting.AppHost | 9.0.0 | Hosting infrastructure |
### Common Issues

## Configuration
**Container Startup Failures**
- Verify Docker Desktop is running
- Check that required ports (3306, 3724, 8085) are not in use
- Ensure sufficient disk space for volumes

### appsettings.json
**Database Connection Issues**
- Verify MySQL container is healthy before WoW server startup
- Check database credentials in environment variables
- Review MySQL logs in the `wow_vanilla_log_data` volume

```json
{
  "Aspire": {
    "Dashboard": {
      "Enabled": true
    }
  }
}
```
**Missing Game Data**
- Ensure all required directories exist in `./data/`
- Verify file permissions for bind-mounted directories
- Check that game data files are properly extracted

### User Secrets
## Related Projects

Sensitive configuration stored in user secrets:
- **[WWoW.Systems.ServiceDefaults](../WWoW.Systems.ServiceDefaults/README.md)**: Shared service configuration
- **[BloogBot Services](../../../Services/README.md)**: Bot automation services
- **[WoWSharpClient](../../../Exports/WoWSharpClient/README.md)**: WoW protocol client

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-key"
```
## Contributing

## Adding New Services
1. Follow .NET coding standards and conventions
2. Update configuration constants in `WowServerConfig.cs` for new settings
3. Test container orchestration with various startup scenarios
4. Maintain compatibility with Aspire framework updates
5. Document any new volume mounts or configuration requirements

1. Add project reference to the service
2. Register in Program.cs:
## License

```csharp
var newService = builder.AddProject<Projects.NewService>("newservice")
    .WithReference(existingService);
```
This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Related Documentation
---

- See `UI/WWoW.Systems/WWoW.Systems.ServiceDefaults/README.md` for shared configuration
- See [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- See `ARCHITECTURE.md` for system overview
The WWoW.Systems.AppHost provides essential infrastructure orchestration for the BloogBot development environment, enabling reliable and reproducible WoW server deployments for bot testing and development activities.