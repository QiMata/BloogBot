# WoWSharpClient

A pure C# implementation of a World of Warcraft 1.12.1 (Vanilla) game client. This library handles all network protocol communication, authentication, and game state management without requiring the actual game client.

## Overview

WoWSharpClient provides:
- **Authentication**: SRP6 login protocol with realm list retrieval
- **World Connection**: Full world server packet handling
- **Object Management**: Real-time tracking of all game objects (players, NPCs, items, etc.)
- **Movement System**: Client-side movement and spline interpolation
- **Event System**: Observable events for game state changes

## Architecture

```
WoWSharpClient/
??? Client/
?   ??? AuthLoginClient.cs       # Authentication server connection
?   ??? WorldClient.cs           # World server connection
?   ??? WoWClient.cs             # Main client orchestrator
?   ??? PacketManager.cs         # Packet send/receive coordination
?   ??? PacketParser.cs          # Binary packet parsing
??? Handlers/
?   ??? LoginHandler.cs          # Authentication flow
?   ??? CharacterSelectHandler.cs # Character list/selection
?   ??? ObjectUpdateHandler.cs   # SMSG_UPDATE_OBJECT processing
?   ??? MovementHandler.cs       # Movement packet handling
?   ??? SpellHandler.cs          # Spell cast events
?   ??? ChatHandler.cs           # Chat message processing
?   ??? ...
??? Models/
?   ??? BaseWoWObject.cs         # Base object with GUID
?   ??? WoWObject.cs             # Generic world object
?   ??? WoWUnit.cs               # NPCs and creatures
?   ??? WoWPlayer.cs             # Other players
?   ??? WoWLocalPlayer.cs        # The controlled character
?   ??? WoWItem.cs               # Items
?   ??? WoWContainer.cs          # Bags
?   ??? ...
??? Movement/
?   ??? MovementController.cs    # Movement state machine
?   ??? SplineController.cs      # Path interpolation
??? Screens/
?   ??? LoginScreen.cs           # Login UI state
??? Parsers/
?   ??? MovementPacketHandler.cs # Movement block parsing
??? WoWSharpObjectManager.cs     # Central object registry
??? WoWSharpEventEmitter.cs      # Event dispatch system
??? OpCodeDispatcher.cs          # Packet routing
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| WowSrp | 0.3.0 | SRP6 authentication protocol |
| Portable.BouncyCastle | 1.9.0 | Cryptography (RC4 encryption) |
| Newtonsoft.Json | 13.0.3 | Configuration serialization |

## Usage

### Basic Connection

```csharp
using WoWSharpClient;

// Create client
var client = new WoWClient();

// Connect to auth server
await client.ConnectToAuthServer("logon.server.com", 3724);

// Login
await client.Login("username", "password");

// Get realm list and select
var realms = await client.GetRealmList();
await client.SelectRealm(realms[0]);

// Get characters and enter world
var characters = await client.GetCharacterList();
await client.EnterWorld(characters[0]);
```

### Object Management

```csharp
// Access object manager
var objectManager = client.ObjectManager;

// Get local player
var player = objectManager.LocalPlayer;

// Find nearby units
var nearbyUnits = objectManager.Units
    .Where(u => u.Position.DistanceTo(player.Position) < 40);

// Subscribe to object updates
objectManager.OnObjectCreated += (obj) => Console.WriteLine($"New object: {obj.Guid}");
objectManager.OnObjectRemoved += (guid) => Console.WriteLine($"Object removed: {guid}");
```

### Event Handling

```csharp
// Subscribe to events
client.EventEmitter.OnChatMessage += (sender, channel, message) =>
{
    Console.WriteLine($"[{channel}] {sender}: {message}");
};

client.EventEmitter.OnSpellCast += (caster, spellId, target) =>
{
    Console.WriteLine($"{caster} casting spell {spellId} on {target}");
};
```

## Packet Handler Registration

Handlers are registered via attributes:

```csharp
[PacketHandler(OpCode.SMSG_UPDATE_OBJECT)]
public void HandleUpdateObject(PacketReader reader)
{
    // Parse and process update
}
```

## Project References

- **BotCommLayer**: Protobuf message definitions
- **BotRunner**: Bot execution framework
- **GameData.Core**: Shared game data interfaces

## Related Documentation

- See `ARCHITECTURE.md` for system-wide communication patterns
- See `Exports/BotCommLayer/README.md` for IPC protocol details
