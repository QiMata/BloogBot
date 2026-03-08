# BotCommLayer — Protobuf IPC Communication

Cross-service TCP socket communication using Protocol Buffers. Length-prefixed binary protocol: `[4 bytes length][protobuf message]`.

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `ProtobufSocketServer.cs` | 127 | Synchronous TCP server with ThreadPool client handling |
| `ProtobufSocketClient.cs` | 173 | TCP client with exponential backoff retry (10 attempts, 500ms base) |
| `ProtobufAsyncSocketServer.cs` | 143 | Event-driven async server using System.Reactive Subject |
| `Models/Communication.cs` | auto | ActionMessage, ActivitySnapshot, 56+ ActionTypes |
| `Models/Game.cs` | auto | WoWObject, WoWUnit, WoWPlayer, Position |
| `Models/Pathfinding.cs` | auto | PathfindingRequest, PhysicsInput/Output, LineOfSight |
| `Models/Database.cs` | auto | Game database entities |

## Protobuf Code Generation

Source `.proto` files in `Models/ProtoDef/`. **NEVER manually edit** the generated .cs files.

```bash
"tools/protoc/bin/protoc.exe" --csharp_out="Exports/BotCommLayer/Models" \
  -I"Exports/BotCommLayer/Models/ProtoDef" \
  communication.proto game.proto pathfinding.proto
```

Batch script: `Models/ProtoDef/protocsharp.bat`

## Dependencies

- Google.Protobuf 3.27.3
- System.Reactive 6.0.1

## Architecture

- Read/Write timeouts: 5000ms
- Thread-safe via object-level locking in client
- All messages use `IMessage<T>` protobuf interface
