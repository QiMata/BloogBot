# BackgroundBotRunner

A .NET 8 Worker Service that provides background execution of bot automation through the WWoW ecosystem using pure network protocol implementation.

## Overview

BackgroundBotRunner is a headless worker service that executes bot logic without requiring the game client to be running. It operates entirely through network protocols via WoWSharpClient, making it ideal for server-side automation, multi-boxing scenarios, and automated testing environments.

The service integrates with multiple WWoW components to provide autonomous character control with intelligent behavior trees, pathfinding, state synchronization, and AI-driven decision making. Unlike ForegroundBotRunner which injects into the game process, BackgroundBotRunner runs as a standalone service and scales horizontally - you can run multiple instances to control multiple characters simultaneously.

Key capabilities include AI integration through PromptHandlingService for intelligent decision making, coordination with PathfindingService for navigation, real-time state management through StateManager, and pure C# WoW network protocol implementation for game communication.

## Architecture

```
+------------------------------------------------------------------+
|                     BackgroundBotRunner                           |
+------------------------------------------------------------------+
|                                                                   |
|  +-----------------------------------------------------------+   |
|  |         BackgroundBotWorker (BackgroundService)           |   |
|  |              Main service execution loop                  |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|         +--------------------+--------------------+               |
|         |                    |                    |               |
|  +--------------+    +----------------+    +---------------+     |
|  | BotRunner    |    | WoWClient      |    | Prompt        |     |
|  | Service      |    | (Network)      |    | Runner        |     |
|  |              |    |                |    | (AI)          |     |
|  | - Behavior   |    | - Auth/Login   |    | - Decision    |     |
|  | - State      |    | - World Comms  |    | - Context     |     |
|  | - Actions    |    | - Protocol     |    |               |     |
|  +--------------+    +----------------+    +---------------+     |
|         |                    |                    |               |
|  +-----------------------------------------------------------+   |
|  |                    External Clients                        |   |
|  |  +-----------------+    +--------------------------+       |   |
|  |  | Pathfinding     |    | Character State Update   |       |   |
|  |  | Client          |    | Client                   |       |   |
|  |  +-----------------+    +--------------------------+       |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|                              v                                    |
|                    +-------------------+                          |
|                    | Game Server       |                          |
|                    | (Network Protocol)|                          |
|                    +-------------------+                          |
+------------------------------------------------------------------+
```

## Project Structure

```
Services/BackgroundBotRunner/
+-- BackgroundBotRunner.csproj  # .NET 8 Worker Service project
+-- BackgroundBotWorker.cs      # Main BackgroundService implementation
+-- README.md                   # This documentation
```

## Key Components

### BackgroundBotWorker

The main `BackgroundService` that orchestrates all bot operations:

```csharp
public class BackgroundBotWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize clients
        await InitializeServicesAsync();

        // Connect to game server
        await _client.ConnectAsync();
        await _client.LoginAsync(username, password);
        await _client.EnterWorldAsync(characterName);

        // Main bot execution loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessGameTickAsync();
            await ExecuteBotDecisionAsync();
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

### BotRunnerService

Orchestrates bot behavior using behavior trees:
- Quest execution and tracking
- Combat management
- Trading and economy interactions
- Social interactions and chat
- Integrates with pathfinding for intelligent movement

### WoWSharpObjectManager

Manages game object state and updates:
- Game object tracking and lifecycle
- Unit and player management
- Interface to WoW game world
- Real-time object enumeration

### Client Integrations

| Client | Purpose |
|--------|---------|
| **PathfindingClient** | Navigation and collision detection |
| **CharacterStateUpdateClient** | Real-time state synchronization with StateManager |
| **WoWClient** | Direct game server communication via network protocol |

### AI Integration

Uses PromptRunner to interface with Ollama AI models:
- Intelligent decision making based on game state
- Adaptive behavior patterns
- Natural language command processing
- Configurable model selection and endpoint management

## Agent Factory

Once connected to the realm and established a world session, the network client component factory provides a comprehensive catalog of agents for gameplay automation:

| Category | Agents |
|----------|--------|
| **Combat** | TargetingAgent, AttackAgent, SpellCastingAgent |
| **Progression** | QuestAgent, TrainerAgent, TalentAgent, ProfessionsAgent |
| **Economy** | LootingAgent, VendorAgent, AuctionHouseAgent, BankAgent |
| **Social** | ChatAgent, GuildAgent, PartyAgent, MailAgent |
| **Travel & Utility** | FlightMasterAgent, GameObjectAgent, InventoryAgent, ItemUseAgent, EquipmentAgent, EmoteAgent |

Each agent can be retrieved from the current `IAgentFactory` instance:

```csharp
var targetingAgent = agentFactory.TargetingAgent;
var vendorAgent = agentFactory.VendorAgent;
var lootingAgent = agentFactory.LootingAgent;
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3 | JSON serialization for configuration |

### Project References

- **BotRunner**: Core bot automation engine with behavior trees
- **WoWSharpClient**: Pure C# WoW network protocol implementation
- **PromptHandlingService**: AI prompt processing and response handling

### External Services

- **PathfindingService**: Advanced navigation and collision detection (port 5000)
- **StateManager**: Multi-character state coordination (port 5001)
- **Character State Listener**: Real-time character state updates (port 8081)
- **Ollama AI Service**: Large language model integration for intelligent decisions

## Configuration

Configure via `appsettings.json`:

```json
{
  "Ollama": {
    "BaseUri": "http://localhost:11434",
    "Model": "llama2"
  },
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": 5000
  },
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": 8081
  },
  "RealmEndpoint": {
    "IpAddress": "127.0.0.1"
  }
}
```

### Configuration Parameters

| Section | Parameter | Description |
|---------|-----------|-------------|
| **Ollama** | BaseUri | URI of the Ollama AI service endpoint |
| **Ollama** | Model | AI model name (e.g., "llama2", "codellama") |
| **PathfindingService** | IpAddress | IP address of pathfinding service |
| **PathfindingService** | Port | Port number for pathfinding service |
| **CharacterStateListener** | IpAddress | IP address of character state service |
| **CharacterStateListener** | Port | Port number for character state service |
| **RealmEndpoint** | IpAddress | WoW realm server IP address |

## Usage

### Integration with StateManager

The BackgroundBotWorker is typically managed by the StateManager service:

```csharp
public void StartBackgroundBotWorker(string accountName)
{
    var scope = _serviceProvider.CreateScope();
    var tokenSource = new CancellationTokenSource();
    var service = ActivatorUtilities.CreateInstance<BackgroundBotWorker>(
        scope.ServiceProvider,
        _loggerFactory,
        _configuration
    );

    var task = Task.Run(async () => await service.StartAsync(tokenSource.Token));
    _managedServices.Add(accountName, (service, tokenSource, task));
}
```

### Standalone Usage

For development or testing:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole());

var worker = new BackgroundBotWorker(loggerFactory, configuration);
await worker.StartAsync(CancellationToken.None);
```

### Service Lifecycle

1. **Initialization**: Clients and services configured during construction
2. **Startup**: `ExecuteAsync` called when service starts
3. **Execution**: Bot runner operates continuously until cancellation
4. **Shutdown**: Graceful shutdown when cancellation requested

## Development

### Building the Project

```bash
# Build the project
dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj

# Run in development
dotnet run --project Services/BackgroundBotRunner
```

### Project Configuration

The project is configured as a library (`OutputType=Library`) with:
- **.NET 8** target framework
- **Nullable reference types** enabled
- **Implicit usings** for cleaner code
- **User secrets** support for development configuration
- **Shared output path** to `Bot/` directory for ecosystem integration

### Testing

Integration with test projects:
- **BotRunner.Tests**: Tests for core bot automation logic
- **WoWSharpClient.Tests**: Tests for network protocol implementation
- **PromptHandlingService.Tests**: Tests for AI integration

## Integration Points

### Service Dependencies

| Service | Purpose |
|---------|---------|
| **PathfindingService** | Navigation meshes and pathfinding algorithms |
| **StateManager** | Multi-bot orchestration and character state |
| **DecisionEngineService** | Advanced decision making and strategy planning |
| **PromptHandlingService** | AI-driven behavior and response generation |

### Communication Protocols

- **TCP Sockets**: Direct communication with pathfinding and state services
- **WoW Network Protocol**: Encrypted communication with game servers
- **HTTP/REST**: Communication with Ollama AI services
- **Protocol Buffers**: Structured messaging through BotCommLayer

## Error Handling

Comprehensive error handling implementation:

```csharp
try
{
    _botRunner.Start();

    while (!stoppingToken.IsCancellationRequested)
    {
        await Task.Delay(100, stoppingToken);
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in BackgroundBotWorker");
}
```

### Common Error Scenarios

- **Service Connectivity**: PathfindingService or StateManager unavailable
- **Network Issues**: WoW server disconnections or timeouts
- **AI Service Errors**: Ollama service unavailable or model loading issues
- **Configuration Errors**: Missing or invalid configuration parameters

## Performance Considerations

- **Async/Await**: Non-blocking operations for network communication
- **Resource Management**: Proper disposal of clients and services
- **Memory Efficiency**: Optimized object lifecycle management
- **CPU Usage**: 100ms delay loops for balanced performance and responsiveness

## Security

- **Configuration Security**: Uses .NET user secrets for sensitive configuration
- **Network Security**: Encrypted communication protocols where applicable
- **Service Isolation**: Each bot instance runs in isolated service scope

## Logging

Comprehensive logging through Microsoft.Extensions.Logging:
- **Information**: Service startup, client initialization, state changes
- **Error**: Exception details, connection failures, service errors
- **Debug**: Detailed operation tracing (in debug builds)

## Use Cases

- **Multi-boxing**: Run multiple characters without multiple game clients
- **Server-side Bots**: Run bots on a headless server
- **Testing**: Test bot logic without game client overhead
- **CI/CD**: Automated testing of bot behaviors

## Limitations

- No visual feedback (headless operation)
- Cannot interact with game UI directly
- Movement is server-authoritative (no client prediction)
- Some private servers may detect pure network clients

## Related Documentation

- See [WoWSharpClient README](../../Exports/WoWSharpClient/README.md) for network client details
- See [PromptHandlingService README](../PromptHandlingService/README.md) for AI integration
- See [StateManager README](../StateManager/README.md) for multi-bot coordination
- See [BotRunner README](../../Exports/BotRunner/README.md) for behavior tree framework
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
