# StateManager Service

A .NET 8 Worker Service that manages the state and coordination of World of Warcraft bot instances in the BloogBot ecosystem.

## Overview

The StateManager service is a background service that orchestrates multiple bot instances, manages character states, and provides centralized coordination for the bot ecosystem. It acts as the central hub for managing bot accounts, character definitions, and communication between various services.

## Features

- **Character State Management**: Tracks and manages individual character states and activities
- **Background Bot Orchestration**: Automatically starts and manages BackgroundBotRunner instances for each configured character
- **Socket-based Communication**: Provides real-time communication via TCP socket listeners
- **Account Management**: Integrates with MaNGOS SOAP API for automatic account creation and GM level management
- **Service Coordination**: Automatically launches and manages dependent services like PathfindingService
- **Configuration-driven**: Fully configurable via JSON settings files

## Architecture

### Core Components

- **StateManagerWorker**: Main background service that orchestrates all operations
- **CharacterStateSocketListener**: Handles character state updates and queries
- **StateManagerSocketListener**: Manages state change requests and responses
- **MangosSOAPClient**: Communicates with MaNGOS server for account management

### Dependencies

- **BackgroundBotRunner**: Individual bot instance management
- **DecisionEngineService**: AI decision-making capabilities
- **PromptHandlingService**: Natural language processing
- **PathfindingService**: Navigation and movement coordination

## Configuration

### appsettings.json

```json
{
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": "5000"
  },
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": "5002"
  },
  "StateManagerListener": {
    "IpAddress": "127.0.0.1",
    "Port": "8088"
  },
  "MangosSOAP": {
    "IpAddress": "http://localhost:7878"
  }
}
```

### Character Definitions

Character configurations are managed through `StateManagerSettings.json` which defines:
- Character account names
- Character-specific settings
- Bot behavior configurations

## Usage

### Running the Service

The StateManager runs as a Windows Service or console application:

```bash
dotnet run
```

### Service Flow

1. **Initialization**: Loads configuration and character definitions
2. **Dependency Check**: Verifies PathfindingService is running, launches if needed
3. **Account Management**: Creates MaNGOS accounts for configured characters if they don't exist
4. **Bot Spawning**: Starts BackgroundBotRunner instances for each character
5. **State Monitoring**: Continuously monitors and manages character states
6. **Communication**: Handles incoming requests via socket listeners

### Socket Communication

#### Character State Listener (Port 5002)
- Receives character activity snapshots
- Provides current character state information
- Handles character state queries

#### State Manager Listener (Port 8088)
- Processes state change requests
- Coordinates state transitions
- Manages inter-service communication

## Development

### Prerequisites

- .NET 8 SDK
- Access to MaNGOS server with SOAP interface enabled
- SQLite (for state persistence)

### Key Classes

- `StateManagerWorker`: Main orchestration logic
- `StateManagerSettings`: Configuration management
- `CharacterStateSocketListener`: Character state communication
- `MangosSOAPClient`: Server integration
- `ActivitySnapshot`: Character state data structure

### Building

```bash
dotnet build
```

### Testing

```bash
dotnet test
```

## Integration

The StateManager integrates with several other services in the BloogBot ecosystem:

- **UI Integration**: StateManagerUI provides a WPF interface for monitoring and control
- **Bot Runners**: Manages ForegroundBotRunner and BackgroundBotRunner instances
- **AI Services**: Coordinates with DecisionEngineService and PromptHandlingService
- **Game Integration**: Communicates with WoW client through BotCommLayer

## Troubleshooting

### Common Issues

1. **Port Already in Use**: Ensure configured ports are available
2. **MaNGOS Connection Failed**: Verify MaNGOS server is running and SOAP is enabled
3. **PathfindingService Not Found**: Check PathfindingService configuration and availability
4. **Character Creation Failed**: Verify MaNGOS SOAP credentials and permissions

### Logging

The service uses structured logging with configurable levels. Check logs for detailed error information and service status.

### Performance Considerations

- Each character spawns a separate BackgroundBotRunner instance
- Socket listeners run on separate threads
- Database operations are optimized for concurrent access
- Memory usage scales with the number of managed characters

## Contributing

When contributing to the StateManager service:

1. Follow established patterns for background service implementation
2. Ensure proper error handling and logging
3. Test with multiple character configurations
4. Verify socket communication protocols
5. Document any new configuration options

## License

Part of the BloogBot project ecosystem.