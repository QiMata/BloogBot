# BotCommLayer

The **BotCommLayer** project provides Protobuf-based inter-process communication (IPC) infrastructure for the WWoW system. It enables typed, efficient message passing between services using socket-based communication.
A robust inter-service communication library for the BloogBot ecosystem, providing Protocol Buffers-based messaging over TCP sockets with support for both synchronous and asynchronous communication patterns.

## Overview

This library contains:
- **Protobuf Message Definitions**: Strongly-typed message contracts for all IPC communication
- **Socket Infrastructure**: Synchronous and asynchronous socket servers/clients
- **Model Categories**: Game state, pathfinding, communication, and database models
BotCommLayer serves as the foundational communication infrastructure for BloogBot services, enabling reliable data exchange between different components of the bot automation system. It uses Google Protocol Buffers for efficient binary serialization and provides both client-server socket patterns for different communication needs.

## Project Structure
## Features

```
BotCommLayer/
??? BotCommLayer.csproj
??? README.md
??? ProtobufSocketServer.cs          # Synchronous socket server
??? ProtobufAsyncSocketServer.cs     # Async socket server
??? ProtobufSocketClient.cs          # Socket client
??? Models/
    ??? Communication.cs              # Generated from communication.proto
    ??? Database.cs                   # Generated from database.proto
    ??? Game.cs                       # Generated from game.proto
    ??? Pathfinding.cs                # Generated from pathfinding.proto
    ??? ProtoDef/                     # Source .proto files
        ??? protocsharp.bat           # Build script for C# generation
        ??? communication.proto
        ??? database.proto
        ??? game.proto
        ??? pathfinding.proto
```
- **Protocol Buffers Integration**: Efficient binary serialization using Google.Protobuf
- **Generic Socket Communication**: Type-safe client-server communication with generic protobuf message types
- **Synchronous and Asynchronous Patterns**: Support for both blocking and non-blocking communication modes
- **World of Warcraft Data Models**: Comprehensive protobuf definitions for WoW game objects, players, items, and actions
- **System.Reactive Integration**: Reactive programming support for event-driven architectures
- **Thread-Safe Operations**: Safe multi-threaded communication with proper locking mechanisms
- **Auto-Reconnection**: Built-in connection management and error handling

## Protobuf Code Generation
## Architecture

### Prerequisites
The library is organized into several key components:

Install the Protobuf compiler (`protoc`). Options:
- **Windows**: Download from [GitHub Releases](https://github.com/protocolbuffers/protobuf/releases) and add to PATH
- **Chocolatey**: `choco install protoc`
- **Scoop**: `scoop install protobuf`
### Core Communication Classes

### Regenerating C# Files
- **`ProtobufSocketServer<TRequest, TResponse>`**: Generic TCP server for handling protobuf-based request-response patterns
- **`ProtobufSocketClient<TRequest, TResponse>`**: Generic TCP client for sending protobuf messages and receiving responses
- **`ProtobufAsyncSocketServer`**: Asynchronous server implementation for high-throughput scenarios

When you modify any `.proto` file in `Models/ProtoDef/`, regenerate the C# code using the provided batch script.
### Data Models

**From the repository root directory:**
#### Game Object Models (`Models/Game.cs`)
Comprehensive protobuf definitions for World of Warcraft entities:

```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\path\to\protoc.exe"
```
- **`WoWObject`**: Base game object with position, GUID, map/zone information
- **`WoWGameObject`**: Extended objects with state and faction data
- **`WoWUnit`**: Living entities with health, mana, stats, auras, and combat properties
- **`WoWPlayer`**: Player characters with inventory, skills, quests, and progression data
- **`WoWItem`**: Items with durability, enchantments, and properties
- **`WoWContainer`**: Bags and containers with slot management
- **`Position`**: 3D coordinates for world positioning

**Example with VS Code's protoc:**
```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\Microsoft VS Code\bin\protoc.exe"
#### Communication Models (`Models/Communication.cs`)
Messaging infrastructure for bot coordination:

- **`AsyncRequest`**: Asynchronous message wrapper with request IDs
- **`ActionMessage`**: Bot action definitions with parameters and results
- **`ActivitySnapshot`**: Complete game state snapshot with player and environment data
- **`StateChangeRequest/Response`**: Character personality and behavior modifications
- **`CharacterDefinition`**: AI personality traits using Big Five model (Openness, Conscientiousness, etc.)

#### Additional Models
- **Database Models** (`Models/Database.cs`): Data persistence schemas
- **Pathfinding Models** (`Models/Pathfinding.cs`): Navigation and movement data structures

## Usage Examples

### Creating a Simple Server

```csharp
using BotCommLayer;
using Microsoft.Extensions.Logging;

// Define your request and response message types
public class MyServer : ProtobufSocketServer<MyRequest, MyResponse>
{
    public MyServer(string ipAddress, int port, ILogger logger) 
        : base(ipAddress, port, logger)
    {
    }

    protected override MyResponse HandleRequest(MyRequest request)
    {
        // Process the request and return a response
        return new MyResponse
        {
            Success = true,
            Message = $"Processed request: {request.Data}"
        };
    }
}

// Start the server
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MyServer>();
var server = new MyServer("127.0.0.1", 8080, logger);
```

### Batch Script Parameters
### Creating a Client

| Parameter | Purpose | Default |
|-----------|---------|---------|
| `%1` | Path to `.proto` files (relative to script location) | `.` (current directory) |
| `%2` | Output directory for generated C# files | `..` (parent directory) |
| `%3` | Path to `protoc.exe` | `C:\protoc\bin\protoc.exe` |
```csharp
using BotCommLayer;
using Microsoft.Extensions.Logging;

### Manual Generation (Alternative)
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ProtobufSocketClient<MyRequest, MyResponse>>();
var client = new ProtobufSocketClient<MyRequest, MyResponse>("127.0.0.1", 8080, logger);

If you prefer to run protoc directly:
// Send a request and get response
var request = new MyRequest { Data = "Hello Server" };
var response = client.SendMessage(request);

```powershell
# Navigate to the proto definitions folder
cd Exports\BotCommLayer\Models\ProtoDef
Console.WriteLine($"Server responded: {response.Message}");

# Generate all proto files
protoc --csharp_out=.. --proto_path=. communication.proto database.proto game.proto pathfinding.proto
// Clean up
client.Close();
```

### Proto File Dependencies
### Working with Game Objects

The proto files have the following import dependencies:
```csharp
using Communication;
using Game;

// Create a player snapshot
var player = new WoWPlayer
{
    Unit = new WoWUnit
    {
        GameObject = new WoWGameObject
        {
            Base = new WoWObject
            {
                Guid = 12345,
                Position = new Position { X = 100.5f, Y = 200.3f, Z = 15.0f }
            }
        },
        Health = 850,
        MaxHealth = 1000
    },
    PlayerXP = 15420,
    Coinage = 50000
};

// Create an activity snapshot
var snapshot = new ActivitySnapshot
{
    Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    AccountName = "TestAccount",
    Player = player,
    CurrentAction = new ActionMessage
    {
        ActionType = ActionType.CastSpell,
        ActionResult = ResponseResult.Success
    }
};
```
communication.proto ? imports game.proto
pathfinding.proto  ? imports game.proto
database.proto     ? standalone
game.proto         ? standalone

### Implementing Bot Actions

```csharp
using Communication;

// Define a bot action
var moveAction = new ActionMessage
{
    ActionType = ActionType.Goto,
    Parameters = {
        new RequestParameter { FloatParam = 150.0f }, // X coordinate
        new RequestParameter { FloatParam = 250.0f }, // Y coordinate
        new RequestParameter { FloatParam = 20.0f }   // Z coordinate
    }
};

// Create an async request
var asyncRequest = new AsyncRequest
{
    Id = 12345,
    ActivitySnapshot = snapshot
};
```

**Build order** (if generating one at a time):
1. `game.proto` (base types)
2. `database.proto` (independent)
3. `communication.proto` (depends on game)
4. `pathfinding.proto` (depends on game)
## Configuration

## Message Categories
### Project Dependencies

### Game Messages (`game.proto` ? `Game.cs`)
The library requires the following NuGet packages:

Core game object representations:
- `Position` - 3D coordinates (X, Y, Z)
- `WoWObject` - Base game object with GUID, position, facing
- `WoWGameObject` - Game objects (chests, doors, etc.)
- `WoWUnit` - NPCs and creatures with health, auras, flags
- `WoWPlayer` - Player characters with inventory, quests, skills
- `WoWItem` - Items with enchantments, durability
- `WoWContainer` - Bags with item slots
```xml
<PackageReference Include="Google.Protobuf" Version="3.27.3" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

### Pathfinding Messages (`pathfinding.proto` ? `Pathfinding.cs`)
### Network Configuration

Navigation and physics:
- `PathfindingRequest` / `PathfindingResponse` - Main request/response wrapper
- `CalculatePathRequest` / `CalculatePathResponse` - A* pathfinding
- `LineOfSightRequest` / `LineOfSightResponse` - LoS checks
- `PhysicsInput` / `PhysicsOutput` - Client physics simulation state
Default socket settings can be customized:

```csharp
// Client timeouts
var client = new ProtobufSocketClient<TRequest, TResponse>("127.0.0.1", 8080, logger);
// Read timeout: 5000ms
// Write timeout: 5000ms

// Server configuration
var server = new ProtobufSocketServer<TRequest, TResponse>("0.0.0.0", 8080, logger);
// Listens on all interfaces by default
// Uses ThreadPool for client handling
```

## Protocol Buffer Schema

The communication protocol defines several key message types:

### Action Types
Over 50 predefined action types including:
- Movement: `GOTO`, `START_MELEE_ATTACK`, `STOP_ATTACK`
- Interaction: `INTERACT_WITH`, `SELECT_GOSSIP`, `ACCEPT_QUEST`
- Combat: `CAST_SPELL`, `USE_ITEM`, `STOP_CAST`
- Social: `SEND_GROUP_INVITE`, `ACCEPT_TRADE`, `OFFER_ITEM`
- System: `LOGIN`, `LOGOUT`, `CREATE_CHARACTER`

### Communication Messages (`communication.proto` ? `Communication.cs`)
### Character Personality Traits
Based on the Big Five personality model:
- **Openness**: Creativity and curiosity (0.0-1.0)
- **Conscientiousness**: Organization and discipline (0.0-1.0)
- **Extraversion**: Social energy and assertiveness (0.0-1.0)
- **Agreeableness**: Cooperation and trust (0.0-1.0)
- **Neuroticism**: Emotional stability (0.0-1.0)

Bot coordination and actions:
- `AsyncRequest` - Async message wrapper
- `ActionMessage` / `ActionType` - Game action definitions (56 action types)
- `StateChangeRequest` / `StateChangeResponse` - Bot state mutations
- `ActivitySnapshot` - Complete bot state snapshot for AI decisions
- `CharacterDefinition` - Bot personality traits (Big Five model)
## Thread Safety

### Database Messages (`database.proto` ? `Database.cs`)
All communication classes implement thread-safe operations:

Game database access:
- `DatabaseRequest` / `DatabaseResponse` - Generic DB queries
- `AreaTrigger*` - Area trigger definitions
- `Creature*` - NPC spawn and AI data
- `BattlegroundTemplate` - PvP battleground config
- And many more game data types...
- **ProtobufSocketClient**: Uses object-level locking for `SendMessage()` operations
- **ProtobufSocketServer**: Handles multiple clients concurrently using ThreadPool
- **Message Serialization**: Protocol Buffers provides thread-safe serialization

## Usage Examples
## Error Handling

### Socket Server (Service Side)
The library provides comprehensive error handling:

```csharp
using BotCommLayer;
using Pathfinding;

// Create and start server
var server = new ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(port: 5000);
server.OnMessageReceived += (request) =>
try
{
    // Handle request and return response
    return new PathfindingResponse
    var response = client.SendMessage(request);
    // Handle successful response
}
catch (IOException ex)
{
        Path = new CalculatePathResponse
    // Handle network errors
    logger.LogError($"Network error: {ex.Message}");
}
catch (Exception ex)
{
            Corners = { /* path points */ }
    // Handle other errors
    logger.LogError($"Unexpected error: {ex.Message}");
}
    };
};
server.Start();
```

### Socket Client (Consumer Side)
## Performance Considerations

- **Binary Serialization**: Protocol Buffers provide efficient binary encoding
- **Connection Reuse**: Clients maintain persistent connections for multiple requests
- **Memory Management**: Automatic buffer management with proper disposal patterns
- **Concurrent Processing**: Server handles multiple clients simultaneously

## Integration with BloogBot Services

BotCommLayer integrates with various BloogBot components:

- **PromptHandlingService**: AI command processing and natural language understanding
- **StateManager**: Bot state synchronization and behavior coordination
- **PathfindingService**: Navigation data exchange
- **DecisionEngineService**: High-level decision making coordination
- **UI Components**: Real-time bot monitoring and control

## Development and Extension

### Adding New Message Types

1. Define protobuf schema in appropriate `.proto` file
2. Regenerate C# classes using Protocol Buffer compiler
3. Implement custom server/client logic
4. Add message type to communication enums

### Custom Server Implementation

```csharp
using BotCommLayer;
using Pathfinding;
public class CustomBotServer : ProtobufSocketServer<BotRequest, BotResponse>
{
    private readonly IBotService _botService;

    public CustomBotServer(string ip, int port, ILogger logger, IBotService botService)
        : base(ip, port, logger)
    {
        _botService = botService;
    }

// Create client and send request
var client = new ProtobufSocketClient<PathfindingRequest, PathfindingResponse>("localhost", 5000);
var response = await client.SendAsync(new PathfindingRequest
    protected override BotResponse HandleRequest(BotRequest request)
    {
    Path = new CalculatePathRequest
        return request.RequestType switch
        {
        MapId = 0,
        Start = new Game.Position { X = 0, Y = 0, Z = 0 },
        End = new Game.Position { X = 100, Y = 100, Z = 0 }
            BotRequestType.GetStatus => _botService.GetStatus(),
            BotRequestType.ExecuteAction => _botService.ExecuteAction(request.Action),
            _ => new BotResponse { Success = false, Error = "Unknown request type" }
        };
    }
}
});
```

### Async Server
## Testing

Unit testing with the communication layer:

```csharp
using BotCommLayer;
using Communication;

var server = new ProtobufAsyncSocketServer<AsyncRequest, StateChangeResponse>(port: 5001);
server.OnMessageReceived += async (request) =>
[Test]
public void TestMessageSerialization()
{
    var original = new ActionMessage
    {
    // Process async request
    return new StateChangeResponse { Response = ResponseResult.Success };
        ActionType = ActionType.CastSpell,
        ActionResult = ResponseResult.Success
    };
await server.StartAsync();

    var bytes = original.ToByteArray();
    var deserialized = ActionMessage.Parser.ParseFrom(bytes);

    Assert.AreEqual(original.ActionType, deserialized.ActionType);
    Assert.AreEqual(original.ActionResult, deserialized.ActionResult);
}
```

## Dependencies
## Troubleshooting

### Common Issues

**Connection Refused**
- Verify server is listening on correct IP/port
- Check firewall settings
- Ensure network connectivity

| Package | Version | Purpose |
|---------|---------|---------|
| Google.Protobuf | 3.27.3 | Protobuf runtime serialization |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.0 | Hosting integration |
| System.Reactive | 6.0.1 | Reactive extensions for event streams |
**Serialization Errors**
- Verify protobuf message compatibility
- Check for null required fields
- Validate message size limits

## Adding New Messages
**Threading Issues**
- Use proper locking when sharing clients
- Dispose connections properly
- Handle concurrent access patterns

1. **Create/modify `.proto` file** in `Models/ProtoDef/`
2. **Regenerate C# code** using the protoc command above
3. **Update consumers** to use the new message types
4. **Build solution** to verify compilation
### Debug Logging

### Proto3 Syntax Reference
Enable detailed logging for troubleshooting:

```protobuf
syntax = "proto3";
```csharp
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<ProtobufSocketClient<TRequest, TResponse>>();
```

package mypackage;
option csharp_namespace = "MyNamespace";  // Optional: override C# namespace
## License

import "game.proto";  // Import other proto files
This project is part of the BloogBot ecosystem and follows the same licensing terms as the main project.

message MyMessage {
    string name = 1;           // Required field number
    uint32 id = 2;
    repeated Position path = 3; // List/array
    map<string, int32> data = 4; // Dictionary
## Contributing

    oneof payload {            // Union type
        TypeA a = 10;
        TypeB b = 11;
    }
}
When contributing to BotCommLayer:

enum MyEnum {
    UNKNOWN = 0;  // First enum value must be 0
    VALUE_A = 1;
    VALUE_B = 2;
}
```
1. Maintain protocol buffer schema compatibility
2. Follow thread-safety patterns established in existing code
3. Add comprehensive logging for debugging
4. Include unit tests for new message types
5. Update documentation for new communication patterns

## Related Documentation
---

- [Protocol Buffers Language Guide](https://protobuf.dev/programming-guides/proto3/)
- [C# Generated Code Guide](https://protobuf.dev/reference/csharp/csharp-generated/)
- See `ARCHITECTURE.md` for system-wide communication patterns
BotCommLayer provides the essential communication infrastructure that enables BloogBot's distributed architecture, allowing seamless integration between AI services, game interfaces, and management tools.