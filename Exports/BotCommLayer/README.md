# BotCommLayer

A robust inter-service communication library providing Protocol Buffers-based messaging over TCP sockets with support for both synchronous and asynchronous communication patterns.

## Overview

BotCommLayer serves as the foundational communication infrastructure for WWoW services, enabling reliable data exchange between different components of the bot automation system. It uses Google Protocol Buffers for efficient binary serialization and provides both client-server socket patterns for different communication needs.

The library contains strongly-typed message contracts for all IPC communication, synchronous and asynchronous socket servers/clients, and comprehensive data models organized into categories: game state, pathfinding, communication protocols, and database schemas.

Key capabilities include type-safe generic protobuf message communication, thread-safe multi-threaded operations, built-in auto-reconnection and error handling, comprehensive World of Warcraft data models, and System.Reactive integration for event-driven architectures.

## Architecture

```
+------------------------------------------------------------------+
|                        BotCommLayer                              |
+------------------------------------------------------------------+
|                                                                  |
|  +-----------------------------------------------------------+  |
|  |              Core Communication Classes                    |  |
|  |                                                            |  |
|  |  +---------------------+    +-------------------------+   |  |
|  |  | ProtobufSocket      |    | ProtobufAsyncSocket     |   |  |
|  |  | Server<TReq, TResp> |    | Server                  |   |  |
|  |  |                     |    |                         |   |  |
|  |  | - HandleRequest()   |    | - Async operations      |   |  |
|  |  | - ThreadPool mgmt   |    | - High throughput       |   |  |
|  |  +---------------------+    +-------------------------+   |  |
|  |                                                            |  |
|  |  +---------------------+                                   |  |
|  |  | ProtobufSocket      |                                   |  |
|  |  | Client<TReq, TResp> |                                   |  |
|  |  |                     |                                   |  |
|  |  | - SendMessage()     |                                   |  |
|  |  | - Connection mgmt   |                                   |  |
|  |  +---------------------+                                   |  |
|  +-----------------------------------------------------------+  |
|                              |                                   |
|         +--------------------+--------------------+              |
|         |                    |                    |              |
|  +-------------+      +-------------+      +-------------+       |
|  | Game Models |      | Communication|      | Pathfinding |       |
|  |             |      | Models      |      | Models      |       |
|  | - WoWObject |      | - AsyncReq  |      | - PathReq   |       |
|  | - WoWUnit   |      | - ActionMsg |      | - LOSReq    |       |
|  | - WoWPlayer |      | - StateChg  |      | - Physics   |       |
|  +-------------+      +-------------+      +-------------+       |
|                                                                  |
+------------------------------------------------------------------+
```

## Project Structure

```
BotCommLayer/
+-- BotCommLayer.csproj
+-- README.md
+-- ProtobufSocketServer.cs          # Synchronous socket server
+-- ProtobufAsyncSocketServer.cs     # Async socket server
+-- ProtobufSocketClient.cs          # Socket client
+-- Models/
    +-- Communication.cs              # Generated from communication.proto
    +-- Database.cs                   # Generated from database.proto
    +-- Game.cs                       # Generated from game.proto
    +-- Pathfinding.cs                # Generated from pathfinding.proto
    +-- ProtoDef/                     # Source .proto files
        +-- protocsharp.bat           # Build script for C# generation
        +-- communication.proto
        +-- database.proto
        +-- game.proto
        +-- pathfinding.proto
```

## Key Components

### Core Communication Classes

**ProtobufSocketServer&lt;TRequest, TResponse&gt;**

Generic TCP server for handling protobuf-based request-response patterns:

```csharp
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
```

**ProtobufSocketClient&lt;TRequest, TResponse&gt;**

Generic TCP client for sending protobuf messages and receiving responses:

```csharp
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<ProtobufSocketClient<MyRequest, MyResponse>>();
var client = new ProtobufSocketClient<MyRequest, MyResponse>("127.0.0.1", 8080, logger);

var request = new MyRequest { Data = "Hello Server" };
var response = client.SendMessage(request);

Console.WriteLine($"Server responded: {response.Message}");
client.Close();
```

**ProtobufAsyncSocketServer**

Asynchronous server implementation for high-throughput scenarios:

```csharp
var server = new ProtobufAsyncSocketServer<AsyncRequest, StateChangeResponse>(port: 5001);
server.OnMessageReceived += async (request) =>
{
    // Process async request
    return new StateChangeResponse { Response = ResponseResult.Success };
};
await server.StartAsync();
```

### Data Models

#### Game Object Models (Models/Game.cs)

Comprehensive protobuf definitions for World of Warcraft entities:

| Model | Description |
|-------|-------------|
| **WoWObject** | Base game object with position, GUID, map/zone information |
| **WoWGameObject** | Extended objects with state and faction data |
| **WoWUnit** | Living entities with health, mana, stats, auras, and combat properties |
| **WoWPlayer** | Player characters with inventory, skills, quests, and progression data |
| **WoWItem** | Items with durability, enchantments, and properties |
| **WoWContainer** | Bags and containers with slot management |
| **Position** | 3D coordinates for world positioning |

Example usage:

```csharp
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
```

#### Communication Models (Models/Communication.cs)

Messaging infrastructure for bot coordination:

| Model | Description |
|-------|-------------|
| **AsyncRequest** | Asynchronous message wrapper with request IDs |
| **ActionMessage** | Bot action definitions with parameters and results |
| **ActivitySnapshot** | Complete game state snapshot with player and environment data |
| **StateChangeRequest/Response** | Character personality and behavior modifications |
| **CharacterDefinition** | AI personality traits using Big Five model (Openness, Conscientiousness, etc.) |
| **ActionType** | Enum with 56 predefined action types |

Action types include movement (GOTO, START_MELEE_ATTACK), interaction (INTERACT_WITH, ACCEPT_QUEST), combat (CAST_SPELL, USE_ITEM), social (SEND_GROUP_INVITE, ACCEPT_TRADE), and system (LOGIN, LOGOUT, CREATE_CHARACTER).

Example usage:

```csharp
using Communication;

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
```

#### Character Personality Traits

Based on the Big Five personality model:

| Trait | Description | Range |
|-------|-------------|-------|
| **Openness** | Creativity and curiosity | 0.0-1.0 |
| **Conscientiousness** | Organization and discipline | 0.0-1.0 |
| **Extraversion** | Social energy and assertiveness | 0.0-1.0 |
| **Agreeableness** | Cooperation and trust | 0.0-1.0 |
| **Neuroticism** | Emotional stability | 0.0-1.0 |

#### Pathfinding Models (Models/Pathfinding.cs)

Navigation and physics:

| Model | Description |
|-------|-------------|
| **PathfindingRequest/Response** | Main request/response wrapper |
| **CalculatePathRequest/Response** | A* pathfinding |
| **LineOfSightRequest/Response** | Line-of-sight checks |
| **PhysicsInput/Output** | Client physics simulation state |

#### Database Models (Models/Database.cs)

Game database access including area triggers, creature spawns, NPC AI data, battleground templates, and many more game data types.

## Protobuf Code Generation

### Prerequisites

A bundled `protoc.exe` is included in the repo at `tools/protoc/bin/protoc.exe`. No system installation is required.

### Regenerating C# Files

When you modify any `.proto` file in `Models/ProtoDef/`, regenerate the C# code.

**Canonical command (from repo root):**

```bash
"tools/protoc/bin/protoc.exe" --csharp_out="Exports/BotCommLayer/Models" -I"Exports/BotCommLayer/Models/ProtoDef" Exports/BotCommLayer/Models/ProtoDef/communication.proto Exports/BotCommLayer/Models/ProtoDef/database.proto Exports/BotCommLayer/Models/ProtoDef/game.proto Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto
```

**Batch script alternative:**

```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat
```

The batch script auto-resolves protoc from `tools/protoc/bin/protoc.exe`. Override with explicit arguments if needed:

```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\path\to\protoc.exe"
```

| Parameter | Purpose | Default |
|-----------|---------|---------|
| `%1` | Path to `.proto` files (relative to script location) | `.` (current directory) |
| `%2` | Output directory for generated C# files | `..` (parent directory) |
| `%3` | Path to `protoc.exe` | `tools/protoc/bin/protoc.exe` (repo-local) |

**Never manually edit** `Communication.cs`, `Game.cs`, `Database.cs`, or `Pathfinding.cs` — always regenerate with protoc.

### C++ Generation (External)

`protocpp.bat` generates C++ protobuf files for an external `ActivityManager` project. It is **not used within this repo**. If the `ActivityManager` project is available:

```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocpp.bat .\ "..\..\..\..\ActivityManager" "..\..\..\..\ActivityManager\vcpkg_installed\x64-windows\tools\protobuf\protoc"
```

### Manual Generation (Alternative)

If you prefer to run protoc directly:

```powershell
# Navigate to the proto definitions folder
cd Exports\BotCommLayer\Models\ProtoDef

# Generate all proto files
protoc --csharp_out=.. --proto_path=. communication.proto database.proto game.proto pathfinding.proto
```

### Proto File Dependencies

The proto files have the following import dependencies:

```
communication.proto → imports game.proto
pathfinding.proto  → imports game.proto
database.proto     → standalone
game.proto         → standalone
```

Build order (if generating one at a time):
1. `game.proto` (base types)
2. `database.proto` (independent)
3. `communication.proto` (depends on game)
4. `pathfinding.proto` (depends on game)

### Proto3 Syntax Reference

```protobuf
syntax = "proto3";

package mypackage;
option csharp_namespace = "MyNamespace";  // Optional: override C# namespace

import "game.proto";  // Import other proto files

message MyMessage {
    string name = 1;           // Required field number
    uint32 id = 2;
    repeated Position path = 3; // List/array
    map<string, int32> data = 4; // Dictionary

    oneof payload {            // Union type
        TypeA a = 10;
        TypeB b = 11;
    }
}

enum MyEnum {
    UNKNOWN = 0;  // First enum value must be 0
    VALUE_A = 1;
    VALUE_B = 2;
}
```

## Configuration

### Network Configuration

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

### Thread Safety

All communication classes implement thread-safe operations:

- **ProtobufSocketClient**: Uses object-level locking for `SendMessage()` operations
- **ProtobufSocketServer**: Handles multiple clients concurrently using ThreadPool
- **Message Serialization**: Protocol Buffers provides thread-safe serialization

## Error Handling

The library provides comprehensive error handling:

```csharp
try
{
    var response = client.SendMessage(request);
    // Handle successful response
}
catch (IOException ex)
{
    // Handle network errors
    logger.LogError($"Network error: {ex.Message}");
}
catch (Exception ex)
{
    // Handle other errors
    logger.LogError($"Unexpected error: {ex.Message}");
}
```

### Common Issues

**Connection Refused**
- Verify server is listening on correct IP/port
- Check firewall settings
- Ensure network connectivity

**Serialization Errors**
- Verify protobuf message compatibility
- Check for null required fields
- Validate message size limits

**Threading Issues**
- Use proper locking when sharing clients
- Dispose connections properly
- Handle concurrent access patterns

### Debug Logging

Enable detailed logging for troubleshooting:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<ProtobufSocketClient<TRequest, TResponse>>();
```

## Performance Considerations

- **Binary Serialization**: Protocol Buffers provide efficient binary encoding
- **Connection Reuse**: Clients maintain persistent connections for multiple requests
- **Memory Management**: Automatic buffer management with proper disposal patterns
- **Concurrent Processing**: Server handles multiple clients simultaneously

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Google.Protobuf | 3.27.3 | Protobuf runtime serialization |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.0 | Hosting integration |
| System.Reactive | 6.0.1 | Reactive extensions for event streams |

## Project References

BotCommLayer integrates with various WWoW components:

- **PromptHandlingService**: AI command processing and natural language understanding
- **StateManager**: Bot state synchronization and behavior coordination
- **PathfindingService**: Navigation data exchange
- **DecisionEngineService**: High-level decision making coordination
- **UI Components**: Real-time bot monitoring and control

## Testing

Unit testing with the communication layer:

```csharp
using BotCommLayer;
using Communication;

[Test]
public void TestMessageSerialization()
{
    var original = new ActionMessage
    {
        ActionType = ActionType.CastSpell,
        ActionResult = ResponseResult.Success
    };

    var bytes = original.ToByteArray();
    var deserialized = ActionMessage.Parser.ParseFrom(bytes);

    Assert.AreEqual(original.ActionType, deserialized.ActionType);
    Assert.AreEqual(original.ActionResult, deserialized.ActionResult);
}
```

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

    protected override BotResponse HandleRequest(BotRequest request)
    {
        return request.RequestType switch
        {
            BotRequestType.GetStatus => _botService.GetStatus(),
            BotRequestType.ExecuteAction => _botService.ExecuteAction(request.Action),
            _ => new BotResponse { Success = false, Error = "Unknown request type" }
        };
    }
}
```

## Contributing

When contributing to BotCommLayer:

1. Maintain protocol buffer schema compatibility
2. Follow thread-safety patterns established in existing code
3. Add comprehensive logging for debugging
4. Include unit tests for new message types
5. Update documentation for new communication patterns

## Related Documentation

- [Protocol Buffers Language Guide](https://protobuf.dev/programming-guides/proto3/)
- [C# Generated Code Guide](https://protobuf.dev/reference/csharp/csharp-generated/)
- See `ARCHITECTURE.md` for system-wide communication patterns

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
