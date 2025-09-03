# WoWSharpClient

## Overview

WoWSharpClient is a comprehensive C# library that provides a complete client implementation for World of Warcraft Classic (Vanilla) server communication. It handles authentication, world connection, object management, movement control, and game state tracking through a pure C# implementation of the WoW network protocol.

## Architecture

WoWSharpClient consists of several key components:

### Client Layer
- **`WoWClient`** - Main client facade coordinating authentication and world connections
- **`AuthLoginClient`** - Handles SRP authentication with login servers
- **`WorldClient`** - Manages encrypted communication with world servers
- **`PacketManager`** - Low-level packet encryption, compression, and parsing utilities

### Object Management
- **`WoWSharpObjectManager`** - Central object state manager implementing `IObjectManager`
- **Object Models** - Complete hierarchy of game objects (Players, Units, Items, GameObjects, etc.)
- **Update System** - Processes server object updates and maintains client-side object cache

### Protocol Handling
- **`OpCodeDispatcher`** - Routes incoming server packets to appropriate handlers
- **Specialized Handlers** - Dedicated handlers for movement, objects, chat, spells, etc.
- **Movement System** - Advanced movement control with server synchronization

## Features

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

### Communication
```csharp
var client = new WoWClient();
client.SetIpAddress("127.0.0.1");

// Login and select realm
client.Login("username", "password");
var realms = client.GetRealmList();
client.SelectRealm(realms.First());

// Send chat messages
client.SendChatMessage(ChatMsg.CHAT_MSG_SAY, Language.COMMON, "", "Hello World!");
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
    wowClient: new WoWClient(),
    pathfindingClient: pathfindingClient,
    logger: logger
);
```

## Configuration

### Project Configuration
- **Target Framework**: .NET 8.0
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

### Debugging & Diagnostics
```csharp
// Comprehensive logging for debugging
Console.WriteLine($"[{timestamp}][Movement-Update] Guid={guid:X} " +
                 $"Pos=({x:F2}, {y:F2}, {z:F2}) Flags=0x{flags:X8}");
```

### Performance Optimizations
- **Queued Updates** - Batched object processing for performance
- **Efficient Parsing** - Optimized binary readers for packet processing
- **Thread Safety** - Proper synchronization for multi-threaded usage

## Usage Examples

### Complete Client Setup
```csharp
// Initialize the client system
var client = new WoWClient();
var pathfindingClient = new PathfindingClient();
var logger = new Logger<WoWSharpObjectManager>();

var objectManager = WoWSharpObjectManager.Instance;
objectManager.Initialize(client, pathfindingClient, logger);

// Connect and authenticate
client.SetIpAddress("127.0.0.1");
client.Login("myusername", "mypassword");

// Select realm and character
var realms = client.GetRealmList();
client.SelectRealm(realms.First());

var characterScreen = objectManager.CharacterSelectScreen;
characterScreen.RefreshCharacterListFromServer();
// ... character selection logic

// Enter world
objectManager.EnterWorld(characterGuid);
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
- **Mock Support** - Uses Moq for service isolation in tests

### Test Resources
```
Tests/WoWSharpClient.Tests/Resources/
??? SMSG_UPDATE_OBJECT/     # Captured object update packets
??? MSG_MOVE_*/             # Movement packet samples  
??? SMSG_AUTH_RESPONSE/     # Authentication test data
```

## Contributing

When contributing to WoWSharpClient:

1. **Protocol Accuracy** - Ensure changes match official WoW protocol specifications
2. **Thread Safety** - All public APIs must be thread-safe
3. **Performance** - Consider impact on packet processing performance
4. **Testing** - Add tests for new packet types or object models
5. **Documentation** - Update this README for significant changes

## Security & Legal

?? **Important**: This library is designed for educational and research purposes. It implements the WoW network protocol from publicly available specifications without any reverse engineering or game client modification.

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

---

WoWSharpClient provides the foundational network communication layer that enables BloogBot to interact with World of Warcraft servers through a clean, type-safe C# API while maintaining full protocol compliance and educational value.