# WoWSharpClient

A pure C# implementation of a World of Warcraft 1.12.1 (Vanilla) game client for the WWoW (Westworld of Warcraft) ecosystem.

## Overview

WoWSharpClient provides a complete network protocol implementation for World of Warcraft servers without requiring the actual game client. This library handles all network communication, authentication, and game state management through a modern, composable architecture.

Key capabilities:

- **Authentication**: SRP6 login protocol with realm list retrieval
- **World Connection**: Full world server packet handling
- **Object Management**: Real-time tracking of all game objects (players, NPCs, items, etc.)
- **Movement System**: Client-side movement and spline interpolation
- **Event System**: Observable events for game state changes

## Architecture

```
+------------------------------------------------------------------+
|                       WoWSharpClient                              |
+------------------------------------------------------------------+
|                                                                   |
|  WoWClientOrchestrator (High-level coordination)                 |
|  +-- AuthClient (Authentication)                                 |
|  |   +-- PacketPipeline<Opcode>                                  |
|  |       +-- TcpConnection (IConnection)                         |
|  |       +-- NoEncryption (IEncryptor)                           |
|  |       +-- LengthPrefixedFramer (IMessageFramer)               |
|  |       +-- WoWPacketCodec (IPacketCodec)                       |
|  |       +-- MessageRouter (IMessageRouter)                      |
|  +-- NewWorldClient (World server communication)                 |
|      +-- PacketPipeline<Opcode>                                  |
|          +-- TcpConnection (IConnection)                         |
|          +-- NoEncryption/RC4 (IEncryptor)                       |
|          +-- WoWMessageFramer (IMessageFramer)                   |
|          +-- WoWPacketCodec (IPacketCodec)                       |
|          +-- MessageRouter (IMessageRouter)                      |
|                                                                   |
+------------------------------------------------------------------+
```

### Composable Networking Stack

```
IConnection (TCP) -> IEncryptor (None/RC4) -> IMessageFramer (WoW Protocol) 
    -> IPacketCodec (Opcode Handling) -> IMessageRouter (Handler Dispatch)
```

## Project Structure

```
WoWSharpClient/
+-- Client/
|   +-- AuthClient.cs            # Modern authentication server client
|   +-- NewWorldClient.cs        # Advanced world server client
|   +-- WoWClientOrchestrator.cs # High-level client orchestration
|   +-- WoWClientFactory.cs      # Factory for client instances
|   +-- WoWClient.cs             # Legacy client (backward compatible)
|   +-- AuthLoginClient.cs       # Legacy auth client (deprecated)
|   +-- WorldClient.cs           # Legacy world client (deprecated)
|   +-- PacketManager.cs         # Packet send/receive coordination
|   +-- PacketParser.cs          # Binary packet parsing
+-- Handlers/
|   +-- LoginHandler.cs          # Authentication flow
|   +-- CharacterSelectHandler.cs # Character list/selection
|   +-- ObjectUpdateHandler.cs   # SMSG_UPDATE_OBJECT processing
|   +-- MovementHandler.cs       # Movement packet handling
|   +-- SpellHandler.cs          # Spell cast events
|   +-- ChatHandler.cs           # Chat message processing
+-- Models/
|   +-- BaseWoWObject.cs         # Base object with GUID
|   +-- WoWObject.cs             # Generic world object
|   +-- WoWUnit.cs               # NPCs and creatures
|   +-- WoWPlayer.cs             # Other players
|   +-- WoWLocalPlayer.cs        # The controlled character
|   +-- WoWItem.cs               # Items
|   +-- WoWContainer.cs          # Bags
+-- Movement/
|   +-- MovementController.cs    # Movement state machine
|   +-- SplineController.cs      # Path interpolation
+-- Screens/
|   +-- LoginScreen.cs           # Login UI state
+-- Parsers/
|   +-- MovementPacketHandler.cs # Movement block parsing
+-- WoWSharpObjectManager.cs     # Central object registry
+-- WoWSharpEventEmitter.cs      # Event dispatch system
+-- OpCodeDispatcher.cs          # Packet routing
```

## Key Components

### Client Layer

| Component | Purpose |
|-----------|---------|
| `WoWClientOrchestrator` | High-level orchestration for complete WoW client experience |
| `AuthClient` | Modern authentication server client with full async support |
| `NewWorldClient` | Advanced world server client with complete packet handling |
| `WoWClientFactory` | Factory for creating pre-configured client instances |
| `PacketPipeline<T>` | Composable networking pipeline with pluggable components |

### Object Management

- **WoWSharpObjectManager**: Central object state manager implementing `IObjectManager`
- **Object Models**: Complete hierarchy of game objects (Players, Units, Items, GameObjects, etc.)
- **Update System**: Processes server object updates and maintains client-side object cache

### Protocol Handling

- **OpCodeDispatcher**: Routes incoming server packets to appropriate handlers
- **Specialized Handlers**: Dedicated handlers for movement, objects, chat, spells, etc.
- **Movement System**: Advanced movement control with server synchronization

## Object Model Hierarchy

```
WoWObject (Base)
+-- WoWItem
|   +-- WoWContainer
+-- WoWUnit
|   +-- WoWPlayer
|   |   +-- WoWLocalPlayer
|   +-- WoWLocalPet
+-- WoWGameObject
+-- WoWDynamicObject
+-- WoWCorpse
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| WowSrp | 0.3.0 | SRP6 authentication protocol |
| Portable.BouncyCastle | 1.9.0 | Cryptography (RC4 encryption) |
| Newtonsoft.Json | 13.0.3 | Configuration serialization |

## Project References

- **BotCommLayer**: Protobuf message definitions
- **GameData.Core**: Shared game data interfaces

## Usage

### Using the Orchestrator (Recommended)

```csharp
// Create orchestrator for complete WoW client experience
var orchestrator = WoWClientFactory.CreateOrchestrator();

// Complete authentication and realm connection flow
await orchestrator.LoginAsync("127.0.0.1", "username", "password");
var realms = await orchestrator.GetRealmListAsync();
await orchestrator.ConnectToRealmAsync(realms[0]);

// World server operations
await orchestrator.RefreshCharacterListAsync();
await orchestrator.EnterWorldAsync(characterGuid);
await orchestrator.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, Language.Common, "", "Hello World!");
```

### Using Individual Clients (Advanced)

```csharp
// Authentication server
var authClient = WoWClientFactory.CreateAuthClient();
await authClient.ConnectAsync("127.0.0.1");
await authClient.LoginAsync("username", "password");

// World server  
var worldClient = WoWClientFactory.CreateWorldClient();
await worldClient.ConnectAsync("username", "127.0.0.1", authClient.SessionKey);
await worldClient.SendCharEnumAsync();
```

### Backward Compatibility

```csharp
// Existing code continues to work unchanged
var client = new WoWClient(); // Now uses modern architecture internally
client.SetIpAddress("127.0.0.1");
await client.LoginAsync("username", "password"); // Async recommended
// OR
client.Login("username", "password"); // Legacy sync (deprecated but functional)
```

### Object Management

```csharp
// Access game objects through the object manager
var nearbyEnemies = WoWSharpObjectManager.Instance.Objects
    .OfType<WoWUnit>()
    .Where(u => u.IsHostile && u.Position.DistanceTo(Player.Position) < 30);

// Get player information
var player = WoWSharpObjectManager.Instance.Player;
Console.WriteLine($"Player: {player.Name} Level {player.Level} at {player.Position}");
```

### Movement Control

```csharp
var objectManager = WoWSharpObjectManager.Instance;

// Basic movement controls
objectManager.StartMovement(ControlBits.Front);
objectManager.SetFacing(1.57f); // Face east
objectManager.StopMovement(ControlBits.Front);

// Toggle walk/run mode
objectManager.ToggleWalkMode();
```

### Event Handling

```csharp
// Subscribe to game events
WoWSharpEventEmitter.Instance.OnChatMessage += (sender, args) => {
    Console.WriteLine($"[{args.MsgType}] {args.Text}");
};

WoWSharpEventEmitter.Instance.OnTeleport += (sender, args) => {
    Console.WriteLine($"Teleported to new location");
};
```

## Configuration

### Project Configuration

- **Target Framework**: .NET 8.0
- **Language Features**: C# 12.0 with nullable reference types
- **Unsafe Code**: Enabled for memory operations
- **Output Path**: `..\..\Bot` (shared build directory)

## Network Protocol Features

### Packet Handling

- **Complete Opcode Coverage**: Handles 100+ different packet types
- **Compression Support**: Automatic decompression of compressed object updates
- **Encryption**: Full vanilla WoW encryption/decryption support
- **Movement Synchronization**: Precise client-server movement coordination

### Object Updates

The system automatically processes all object update types:
- `CREATE_OBJECT`: New objects entering view
- `PARTIAL`: Field updates for existing objects  
- `MOVEMENT`: Position and movement state changes
- `OUT_OF_RANGE`: Objects leaving view range

### Key Object Properties

- **Position and Movement**: 3D coordinates, facing, movement flags, speeds
- **Stats and Attributes**: Health, mana, level, stats, resistances  
- **Equipment and Inventory**: Items, containers, visible gear
- **Combat State**: Target, auras, combat flags, faction relations
- **Splines and Animation**: Server-controlled movement paths

## Modern Networking Features

- **Async/Await Support**: Non-blocking operations throughout
- **Composable Architecture**: Mix and match networking components
- **Runtime Encryption Switching**: NoEncryption to RC4 after authentication
- **Automatic Reconnection**: Configurable retry policies with exponential backoff
- **Thread-Safe Operations**: Proper synchronization for multi-threaded usage

### Performance Optimizations

- **Zero-Copy Operations**: `ReadOnlyMemory<byte>` for efficient memory usage
- **Queued Updates**: Batched object processing for performance
- **Efficient Parsing**: Optimized binary readers for packet processing
- **Async I/O**: Non-blocking network operations

## Migration Guide

### From Legacy Clients

```csharp
// OLD CODE - Still works with deprecation warnings
var client = new WoWClient();
client.Login("user", "pass");               // Warning: Deprecated

// NEW CODE - Recommended approach  
var client = WoWClientFactory.CreateModernWoWClient();
await client.LoginAsync("user", "pass");    // Modern async

// ADVANCED - Direct orchestrator usage
var orchestrator = WoWClientFactory.CreateOrchestrator();
await orchestrator.LoginAsync("127.0.0.1", "user", "pass");
```

### Benefits of Migration

- **Performance**: Async I/O and memory optimizations
- **Reliability**: Automatic reconnection and error recovery
- **Maintainability**: Clean separation of concerns
- **Testability**: Mock-friendly architecture
- **Extensibility**: Easy to add new protocols or encryption

## Related Documentation

- See `Exports/BotRunner/README.md` for high-level bot orchestration
- See `Exports/GameData.Core/README.md` for shared game data models
- See `Services/PathfindingService/README.md` for navigation and collision detection
- See `Exports/BotCommLayer/README.md` for communication infrastructure
- See `ARCHITECTURE.md` for system-wide communication patterns

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
