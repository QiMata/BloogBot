# WWoW.Systems.AppHost

A .NET Aspire application host that orchestrates containerized World of Warcraft Vanilla server infrastructure for the WWoW ecosystem.

## Overview

WWoW.Systems.AppHost is an ASP.NET Core Aspire application host that provides a complete development and testing environment by orchestrating containerized World of Warcraft Vanilla server infrastructure. It manages the lifecycle of MySQL database containers and WoW server containers with proper configuration, networking, and data persistence.

The application host coordinates the deployment of essential services required for running a World of Warcraft Vanilla server environment, handling complex startup dependencies automatically and providing a controlled server environment for bot testing and development.

Key capabilities include container orchestration for MySQL database and MaNGOS server containers, volume management for database and log persistence, automated port mapping and service discovery, and configuration management through bind mounts for server configs and game data.

## Architecture

The application orchestrates two primary containers in a dependent relationship:

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
- **Dependencies**: Requires MySQL database container to be running

## Project Structure

```
WWoW.Systems.AppHost/
+-- Program.cs              # Main application entry point and container orchestration
+-- WowServerConfig.cs      # Configuration constants and settings
+-- WWoW.Systems.AppHost.csproj  # Project file with Aspire SDK
+-- README.md              # This documentation
```

## Configuration

### Database Configuration

```csharp
var database = builder.AddContainer("wow-vanilla-database", WowServerConfig.DbContainerImage)
    .WithEnvironment("MYSQL_APP_USER", "app")
    .WithEnvironment("MYSQL_APP_PASSWORD", "app")
    .WithVolume("wow_vanilla_mysql_data", "/var/lib/mysql")
    .WithHttpEndpoint(port: 3306, targetPort: 3306);
```

### Server Configuration

The WoW server container is configured with:

- Environment variables for database connectivity
- Volume mounts for persistent logging
- Bind mounts for server configuration files and game data
- Network dependencies on the database container

## Volume Management

The application manages persistent data through Docker volumes:

### Database Storage

- **Volume**: `wow_vanilla_mysql_data`
- **Mount Point**: `/var/lib/mysql`
- **Purpose**: Persistent MySQL database storage

### Log Storage

- **Volume**: `wow_vanilla_log_data`
- **Mount Point**: `/var/log/wow`
- **Purpose**: Server log persistence and debugging

## Data Binding

The application requires local data directories for proper server operation:

### Configuration Files

- **Local**: `./config/`
- **Container**: `/opt/vanilla/etc/`
- **Files**: `mangosd.conf.tpl`, `realmd.conf.tpl`

### Game Data

- **Local**: `./data/`
- **Container**: `/opt/vanilla/data/`
- **Directories**: `dbc/`, `maps/`, `mmaps/`, `vmaps/`

## Dependencies

### Framework Dependencies

- **.NET 8.0**: Target framework
- **Aspire.AppHost.Sdk (9.0.0)**: Aspire application host SDK
- **Aspire.Hosting.AppHost (9.0.0)**: Core Aspire hosting functionality

### Container Images

- **ragedunicorn/mysql**: MySQL database server for WoW data
- **ragedunicorn/wow-vanilla**: MaNGOS-based WoW Vanilla server

## Usage

### Prerequisites

1. Docker Desktop installed and running
2. Required WoW client data files in `./data/` directory
3. Server configuration templates in `./config/` directory

### Running the Application

```bash
# From solution root
dotnet run --project UI/WWoW.Systems/WWoW.Systems.AppHost
```

### Accessing Services

Once running, the following endpoints are available:

- **MySQL Database**: `localhost:3306`
- **WoW Realm Server**: `localhost:3724`
- **WoW World Server**: `localhost:8085`

### Dashboard Access

After starting, access the Aspire dashboard at:

- **Dashboard**: https://localhost:17000

The dashboard provides:

- Service health status
- Distributed tracing
- Structured logging
- Metrics visualization

## Development Integration

This AppHost is designed to support the WWoW development workflow:

1. **Local Testing**: Provides a controlled WoW server environment for bot testing
2. **Development Isolation**: Containerized services prevent conflicts with host system
3. **Data Persistence**: Maintains character and world state across development sessions
4. **Service Orchestration**: Handles complex startup dependencies automatically

## Build Configuration

### Output Path

Build artifacts are directed to `../../Bot` directory for integration with the main WWoW application.

### SDK Configuration

- **Aspire Host**: Enabled (`<IsAspireHost>true</IsAspireHost>`)
- **Output Type**: Console executable
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled

## Security Configuration

The application uses .NET User Secrets for sensitive configuration:

- **User Secrets ID**: `b785e8f1-933c-47f0-bd54-a38fb402bd0a`
- **Purpose**: Store sensitive database credentials and API keys

### User Secrets

Sensitive configuration stored in user secrets:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-key"
```

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

## Troubleshooting

### Common Issues

**Container Startup Failures**
- Verify Docker Desktop is running
- Check that required ports (3306, 3724, 8085) are not in use
- Ensure sufficient disk space for volumes
- Review container logs: `docker logs <container_name>`

**Database Connection Issues**
- Verify MySQL container is healthy before WoW server startup
- Check database credentials in environment variables
- Review MySQL logs in the `wow_vanilla_log_data` volume

**Missing Game Data**
- Ensure all required directories exist in `./data/`
- Verify file permissions for bind-mounted directories
- Check that game data files are properly extracted

### Diagnostic Commands

```bash
# Check container status
docker ps -a

# View container logs
docker logs wow-vanilla-server

# Inspect volumes
docker volume inspect wow_vanilla_mysql_data
```

## Adding New Services

1. Add project reference to the service
2. Register in Program.cs:

```csharp
var newService = builder.AddProject<Projects.NewService>("newservice")
    .WithReference(existingService);
```

## Contributing

1. Follow .NET coding standards and conventions
2. Update configuration constants in `WowServerConfig.cs` for new settings
3. Test container orchestration with various startup scenarios
4. Maintain compatibility with Aspire framework updates
5. Document any new volume mounts or configuration requirements

## Related Documentation

- See [WWoW.Systems.ServiceDefaults README](../WWoW.Systems.ServiceDefaults/README.md) for shared service configuration
- See [WWoW Services README](../../../Services/README.md) for bot automation services
- See [WoWSharpClient README](../../../Exports/WoWSharpClient/README.md) for WoW protocol client
- See [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/) for Aspire framework
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
