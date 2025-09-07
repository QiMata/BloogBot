# WoWSharpClient

A comprehensive C# client library for World of Warcraft 1.12.1 (Vanilla) servers, implementing the complete network protocol without memory injection or game client modification.

## Modern Composable Architecture

WoWSharpClient has been completely redesigned with a modern, composable networking architecture that separates concerns and provides maximum flexibility:

### New Client Components
- **`AuthClient`** - Modern authentication server client with full async support
- **`NewWorldClient`** - Advanced world server client with complete packet handling
- **`WoWClientOrchestrator`** - High-level orchestration for complete WoW client experience
- **`WoWClientFactory`** - Factory for creating pre-configured client instances
- **`PacketPipeline<T>`** - Composable networking pipeline with pluggable components

### Networking Abstractions
The new architecture is built on clean abstractions that can be composed and tested independently:

```csharp
// Composable networking stack
IConnection (TCP) ? IEncryptor (None/RC4) ? IMessageFramer (WoW Protocol) 
    ? IPacketCodec (Opcode Handling) ? IMessageRouter (Handler Dispatch)
```

### Legacy Support
- **`WoWClient`** - Updated to use new architecture internally (100% backward compatible)
- **`AuthLoginClient`** - Legacy auth client (deprecated, use `AuthClient`)  
- **`WorldClient`** - Legacy world client (deprecated, use `NewWorldClient`)

## Quick Start

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

## Architecture Overview

### Client Layer Hierarchy
```
WoWClientOrchestrator (High-level coordination)
??? AuthClient (Authentication)
?   ??? PacketPipeline<Opcode>
?       ??? TcpConnection (IConnection)
?       ??? NoEncryption (IEncryptor)  
?       ??? LengthPrefixedFramer (IMessageFramer)
?       ??? WoWPacketCodec (IPacketCodec)
?       ??? MessageRouter (IMessageRouter)
??? NewWorldClient (World server communication)
    ??? PacketPipeline<Opcode>
        ??? TcpConnection (IConnection)
        ??? NoEncryption/RC4 (IEncryptor)
        ??? WoWMessageFramer (IMessageFramer)
        ??? WoWPacketCodec (IPacketCodec)
        ??? MessageRouter (IMessageRouter)
```

### Object Management
- **`WoWSharpObjectManager`** - Central object state manager implementing `IObjectManager`
- **Object Models** - Complete hierarchy of game objects (Players, Units, Items, GameObjects, etc.)
- **Update System** - Processes server object updates and maintains client-side object cache

### Protocol Handling
- **`OpCodeDispatcher`** - Routes incoming server packets to appropriate handlers
- **Specialized Handlers** - Dedicated handlers for movement, objects, chat, spells, etc.
- **Movement System** - Advanced movement control with server synchronization

## Key Features

### Modern Networking
- **Async/Await Support** - Non-blocking operations throughout
- **Composable Architecture** - Mix and match networking components
- **Runtime Encryption Switching** - NoEncryption ? RC4 after authentication
- **Automatic Reconnection** - Configurable retry policies with exponential backoff
- **Thread-Safe Operations** - Proper synchronization for multi-threaded usage

### Authentication & Connection
- **SRP Authentication** - Secure Remote Password protocol implementation
- **Realm Selection** - Query and connect to available game realms  
- **Session Management** - Maintains encrypted sessions with proper key rotation
- **Character Management** - Create, enumerate, and login with characters

### Game Object System
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

### Modern Communication
```csharp
// Async communication patterns
var orchestrator = WoWClientFactory.CreateOrchestrator();
await orchestrator.LoginAsync("127.0.0.1", "username", "password");

// Send chat messages
await orchestrator.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, Language.Common, "", "Hello World!");
```

## Object Model Hierarchy

```
WoWObject (Base)
??? WoWItem
?   ??? WoWContainer
??? WoWUnit
?   ??? WoWPlayer
?   ?   ??? WoWLocalPlayer
?   ??? WoWLocalPet
??? WoWGameObject
??? WoWDynamicObject
??? WoWCorpse
```

### Key Object Properties
- **Position & Movement** - 3D coordinates, facing, movement flags, speeds
- **Stats & Attributes** - Health, mana, level, stats, resistances  
- **Equipment & Inventory** - Items, containers, visible gear
- **Combat State** - Target, auras, combat flags, faction relations
- **Splines & Animation** - Server-controlled movement paths

## Network Protocol Features

### Packet Handling
- **Complete Opcode Coverage** - Handles 100+ different packet types
- **Compression Support** - Automatic decompression of compressed object updates
- **Encryption** - Full vanilla WoW encryption/decryption support
- **Movement Synchronization** - Precise client-server movement coordination

### Object Updates
```csharp
// The system automatically processes all object update types:
// - CREATE_OBJECT: New objects entering view
// - PARTIAL: Field updates for existing objects  
// - MOVEMENT: Position and movement state changes
// - OUT_OF_RANGE: Objects leaving view range
```

### Event System
```csharp
// Subscribe to game events
WoWSharpEventEmitter.Instance.OnChatMessage += (sender, args) => {
    Console.WriteLine($"[{args.MsgType}] {args.Text}");
};

WoWSharpEventEmitter.Instance.OnTeleport += (sender, args) => {
    Console.WriteLine($"Teleported to new location");
};
```

## Movement System

The movement system provides precise control over character movement with proper server synchronization:

### Movement Controller
- **Physics Integration** - Realistic movement with collision detection
- **Server Synchronization** - Maintains sync with server movement validation
- **Spline Following** - Handles server-controlled movement (knockbacks, teleports)
- **Heartbeat System** - Regular position updates to prevent desynchronization

### Control Interface
```csharp
// Control bits for different movement types
[Flags]
public enum ControlBits
{
    Front = 1, Back = 2, Left = 4, Right = 8,
    StrafeLeft = 16, StrafeRight = 32, Jump = 64
}
```

## Integration with BloogBot

WoWSharpClient integrates seamlessly with the BloogBot ecosystem:

### Dependencies
- **GameData.Core** - Shared enumerations and data models
- **BotCommLayer** - Communication infrastructure  
- **BotRunner** - High-level bot orchestration
- **Pathfinding Service** - Navigation and collision detection

### Service Integration
```csharp
// Initialize with dependency injection
var objectManager = WoWSharpObjectManager.Instance;
objectManager.Initialize(
    wowClient: WoWClientFactory.CreateModernWoWClient(),
    pathfindingClient: pathfindingClient,
    logger: logger
);
```

## Configuration

### Project Configuration
- **Target Framework**: .NET 8.0
- **Language Features**: C# 12.0 with nullable reference types
- **Unsafe Code**: Enabled for memory operations
- **Output Path**: `..\..\Bot` (shared build directory)

### Dependencies
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Portable.BouncyCastle" Version="1.9.0" />
<PackageReference Include="WowSrp" Version="0.3.0" />
```

## Screen Management

The client includes screen management for different game states:

- **`LoginScreen`** - Handles login process and authentication
- **`RealmSelectScreen`** - Realm selection and connection
- **`CharacterSelectScreen`** - Character management and world entry

## Advanced Features

### Memory-Safe Operations
- Uses safe managed code instead of memory injection
- No direct memory manipulation or game client modification
- Implements the protocol from scratch for educational purposes

### Performance Optimizations
- **Zero-Copy Operations** - `ReadOnlyMemory<byte>` for efficient memory usage
- **Queued Updates** - Batched object processing for performance
- **Efficient Parsing** - Optimized binary readers for packet processing
- **Async I/O** - Non-blocking network operations

### Debugging & Diagnostics
```csharp
// Comprehensive logging for debugging
Console.WriteLine($"[{timestamp}][Movement-Update] Guid={guid:X} " +
                 $"Pos=({x:F2}, {y:F2}, {z:F2}) Flags=0x{flags:X8}");
```

## Usage Examples

### Complete Modern Client Setup
```csharp
// Initialize the client system with modern architecture
var orchestrator = WoWClientFactory.CreateOrchestrator();

// Subscribe to events
orchestrator.WorldConnected += () => Console.WriteLine("Connected to world!");
orchestrator.WorldDisconnected += (ex) => Console.WriteLine($"Disconnected: {ex?.Message}");

// Connect and authenticate
await orchestrator.LoginAsync("127.0.0.1", "username", "password");

// Select realm and character
var realms = await orchestrator.GetRealmListAsync();
await orchestrator.ConnectToRealmAsync(realms.First());

// Enter world
await orchestrator.EnterWorldAsync(characterGuid);
```

### Advanced Networking Configuration
```csharp
// Custom networking setup with reconnection
var worldClient = WoWClientFactory.CreateWorldClientWithReconnection("127.0.0.1", 8085);

// Or with custom encryption
var rc4Encryptor = new RC4Encryptor(sessionKey);
var encryptedClient = WoWClientFactory.CreateWorldClientWithEncryption(rc4Encryptor);
```

### Movement Control
```csharp
// Start moving forward
objectManager.StartMovement(ControlBits.Front);

// Turn towards a target
var target = objectManager.Objects.OfType<WoWUnit>().First();
var angle = Math.Atan2(target.Position.Y - player.Position.Y, 
                      target.Position.X - player.Position.X);
objectManager.SetFacing((float)angle);

// Stop movement
objectManager.StopMovement(ControlBits.Front);
```

## Testing

The project includes comprehensive test coverage:

- **Unit Tests** - `WoWSharpClient.Tests` project with xUnit framework
- **Integration Tests** - Real packet parsing from captured game data
- **Network Tests** - `WowSharpClient.NetworkTests` for networking abstractions
- **Mock Support** - Uses Moq for service isolation in tests

### Test Resources
```
Tests/WoWSharpClient.Tests/Resources/
??? SMSG_UPDATE_OBJECT/     # Captured object update packets
??? MSG_MOVE_*/             # Movement packet samples  
??? SMSG_AUTH_RESPONSE/     # Authentication test data
```

## Migration Guide

### From Legacy Clients
The new architecture maintains 100% backward compatibility:

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

## Contributing

When contributing to WoWSharpClient:

1. **Protocol Accuracy** - Ensure changes match official WoW protocol specifications
2. **Async Patterns** - Use proper async/await patterns for new code
3. **Testing** - Add tests for new packet types or networking components
4. **Performance** - Consider impact on packet processing performance
5. **Architecture** - Follow the composable design principles
6. **Documentation** - Update this README for significant changes

## Security & Legal

**Important**: This library is designed for educational and research purposes. It implements the WoW network protocol from publicly available specifications without any reverse engineering or game client modification.

- Does not inject code into or modify the game client
- Does not access game memory directly
- Implements protocol through network communication only
- Users are responsible for compliance with applicable terms of service

## License

This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Related Projects

- **[BotRunner](../BotRunner/)** - High-level bot orchestration and behavior trees
- **[GameData.Core](../GameData.Core/)** - Shared game data models and enumerations  
- **[PathfindingService](../../Services/PathfindingService/)** - Navigation and collision detection
- **[BotCommLayer](../BotCommLayer/)** - Communication infrastructure
- **[StateManager](../../Services/StateManager/)** - Multi-bot coordination service

---

WoWSharpClient provides the foundational network communication layer that enables BloogBot to interact with World of Warcraft servers through a clean, modern, composable C# API while maintaining full protocol compliance and educational value.