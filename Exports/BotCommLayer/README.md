# BotCommLayer

The **BotCommLayer** project provides Protobuf-based inter-process communication (IPC) infrastructure for the WWoW system. It enables typed, efficient message passing between services using socket-based communication.

## Overview

This library contains:
- **Protobuf Message Definitions**: Strongly-typed message contracts for all IPC communication
- **Socket Infrastructure**: Synchronous and asynchronous socket servers/clients
- **Model Categories**: Game state, pathfinding, communication, and database models

## Project Structure

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

## Protobuf Code Generation

### Prerequisites

Install the Protobuf compiler (`protoc`). Options:
- **Windows**: Download from [GitHub Releases](https://github.com/protocolbuffers/protobuf/releases) and add to PATH
- **Chocolatey**: `choco install protoc`
- **Scoop**: `scoop install protobuf`

### Regenerating C# Files

When you modify any `.proto` file in `Models/ProtoDef/`, regenerate the C# code using the provided batch script.

**From the repository root directory:**

```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\path\to\protoc.exe"
```

**Example with VS Code's protoc:**
```powershell
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\Microsoft VS Code\bin\protoc.exe"
```

### Batch Script Parameters

| Parameter | Purpose | Default |
|-----------|---------|---------|
| `%1` | Path to `.proto` files (relative to script location) | `.` (current directory) |
| `%2` | Output directory for generated C# files | `..` (parent directory) |
| `%3` | Path to `protoc.exe` | `C:\protoc\bin\protoc.exe` |

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
communication.proto ? imports game.proto
pathfinding.proto  ? imports game.proto
database.proto     ? standalone
game.proto         ? standalone
```

**Build order** (if generating one at a time):
1. `game.proto` (base types)
2. `database.proto` (independent)
3. `communication.proto` (depends on game)
4. `pathfinding.proto` (depends on game)

## Message Categories

### Game Messages (`game.proto` ? `Game.cs`)

Core game object representations:
- `Position` - 3D coordinates (X, Y, Z)
- `WoWObject` - Base game object with GUID, position, facing
- `WoWGameObject` - Game objects (chests, doors, etc.)
- `WoWUnit` - NPCs and creatures with health, auras, flags
- `WoWPlayer` - Player characters with inventory, quests, skills
- `WoWItem` - Items with enchantments, durability
- `WoWContainer` - Bags with item slots

### Pathfinding Messages (`pathfinding.proto` ? `Pathfinding.cs`)

Navigation and physics:
- `PathfindingRequest` / `PathfindingResponse` - Main request/response wrapper
- `CalculatePathRequest` / `CalculatePathResponse` - A* pathfinding
- `LineOfSightRequest` / `LineOfSightResponse` - LoS checks
- `PhysicsInput` / `PhysicsOutput` - Client physics simulation state

### Communication Messages (`communication.proto` ? `Communication.cs`)

Bot coordination and actions:
- `AsyncRequest` - Async message wrapper
- `ActionMessage` / `ActionType` - Game action definitions (56 action types)
- `StateChangeRequest` / `StateChangeResponse` - Bot state mutations
- `ActivitySnapshot` - Complete bot state snapshot for AI decisions
- `CharacterDefinition` - Bot personality traits (Big Five model)

### Database Messages (`database.proto` ? `Database.cs`)

Game database access:
- `DatabaseRequest` / `DatabaseResponse` - Generic DB queries
- `AreaTrigger*` - Area trigger definitions
- `Creature*` - NPC spawn and AI data
- `BattlegroundTemplate` - PvP battleground config
- And many more game data types...

## Usage Examples

### Socket Server (Service Side)

```csharp
using BotCommLayer;
using Pathfinding;

// Create and start server
var server = new ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(port: 5000);
server.OnMessageReceived += (request) =>
{
    // Handle request and return response
    return new PathfindingResponse
    {
        Path = new CalculatePathResponse
        {
            Corners = { /* path points */ }
        }
    };
};
server.Start();
```

### Socket Client (Consumer Side)

```csharp
using BotCommLayer;
using Pathfinding;

// Create client and send request
var client = new ProtobufSocketClient<PathfindingRequest, PathfindingResponse>("localhost", 5000);
var response = await client.SendAsync(new PathfindingRequest
{
    Path = new CalculatePathRequest
    {
        MapId = 0,
        Start = new Game.Position { X = 0, Y = 0, Z = 0 },
        End = new Game.Position { X = 100, Y = 100, Z = 0 }
    }
});
```

### Async Server

```csharp
using BotCommLayer;
using Communication;

var server = new ProtobufAsyncSocketServer<AsyncRequest, StateChangeResponse>(port: 5001);
server.OnMessageReceived += async (request) =>
{
    // Process async request
    return new StateChangeResponse { Response = ResponseResult.Success };
};
await server.StartAsync();
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Google.Protobuf | 3.27.3 | Protobuf runtime serialization |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.0 | Hosting integration |
| System.Reactive | 6.0.1 | Reactive extensions for event streams |

## Adding New Messages

1. **Create/modify `.proto` file** in `Models/ProtoDef/`
2. **Regenerate C# code** using the protoc command above
3. **Update consumers** to use the new message types
4. **Build solution** to verify compilation

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

## Related Documentation

- [Protocol Buffers Language Guide](https://protobuf.dev/programming-guides/proto3/)
- [C# Generated Code Guide](https://protobuf.dev/reference/csharp/csharp-generated/)
- See `ARCHITECTURE.md` for system-wide communication patterns
