# WoWSharpClient - Pure C# WoW Protocol Implementation

Implements the World of Warcraft client-server protocol for versions 1.12.1, 2.4.3, and 3.3.5a entirely in C#.

## Key Files

| File | Purpose |
|------|---------|
| `OpCodeDispatcher.cs` | Routes incoming packets to handlers |
| `WoWSharpEventEmitter.cs` | Event system for game state changes |
| `WoWSharpObjectManager.cs` | Tracks all game objects from server updates |

## Key Subdirectories

| Directory | Purpose |
|-----------|---------|
| `Client/` | Auth and world server connections |
| `Networking/` | TCP socket management, encryption |
| `Handlers/` | Individual opcode packet handlers |
| `Parsers/` | Binary packet parsing |
| `Models/` | Data models for game entities |
| `Movement/` | Movement packet construction |
| `Frames/` | UI frame state tracking |
| `Screens/` | Game screen state (login, char select, etc.) |

## Protocol Documentation

Detailed protocol docs: `docs/server-protocol/` (7 files covering auth, world, movement, combat, etc.)

## Dependencies

- **GameData.Core** — Interfaces this library implements
- **BotCommLayer** — IPC layer
