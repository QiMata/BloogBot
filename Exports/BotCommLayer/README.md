# BotCommLayer

A robust inter-service communication library for the BloogBot ecosystem, providing Protocol Buffers-based messaging over TCP sockets with support for both synchronous and asynchronous communication patterns.

## Overview

BotCommLayer serves as the foundational communication infrastructure for BloogBot services, enabling reliable data exchange between different components of the bot automation system. It uses Google Protocol Buffers for efficient binary serialization and provides both client-server socket patterns for different communication needs.

## Features

- **Protocol Buffers Integration**: Efficient binary serialization using Google.Protobuf
- **Generic Socket Communication**: Type-safe client-server communication with generic protobuf message types
- **Synchronous and Asynchronous Patterns**: Support for both blocking and non-blocking communication modes
- **World of Warcraft Data Models**: Comprehensive protobuf definitions for WoW game objects, players, items, and actions
- **System.Reactive Integration**: Reactive programming support for event-driven architectures
- **Thread-Safe Operations**: Safe multi-threaded communication with proper locking mechanisms
- **Auto-Reconnection**: Built-in connection management and error handling

## Architecture

The library is organized into several key components:

### Core Communication Classes

- **`ProtobufSocketServer<TRequest, TResponse>`**: Generic TCP server for handling protobuf-based request-response patterns
- **`ProtobufSocketClient<TRequest, TResponse>`**: Generic TCP client for sending protobuf messages and receiving responses
- **`ProtobufAsyncSocketServer`**: Asynchronous server implementation for high-throughput scenarios

### Data Models

#### Game Object Models (`Models/Game.cs`)
Comprehensive protobuf definitions for World of Warcraft entities:

- **`WoWObject`**: Base game object with position, GUID, map/zone information
- **`WoWGameObject`**: Extended objects with state and faction data
- **`WoWUnit`**: Living entities with health, mana, stats, auras, and combat properties
- **`WoWPlayer`**: Player characters with inventory, skills, quests, and progression data
- **`WoWItem`**: Items with durability, enchantments, and properties
- **`WoWContainer`**: Bags and containers with slot management
- **`Position`**: 3D coordinates for world positioning

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

### Creating a Client

```csharp
using BotCommLayer;
using Microsoft.Extensions.Logging;

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ProtobufSocketClient<MyRequest, MyResponse>>();
var client = new ProtobufSocketClient<MyRequest, MyResponse>("127.0.0.1", 8080, logger);

// Send a request and get response
var request = new MyRequest { Data = "Hello Server" };
var response = client.SendMessage(request);

Console.WriteLine($"Server responded: {response.Message}");

// Clean up
client.Close();
```

### Working with Game Objects

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

## Configuration

### Project Dependencies

The library requires the following NuGet packages:

```xml
<PackageReference Include="Google.Protobuf" Version="3.27.3" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

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

## Protocol Buffer Schema

The communication protocol defines several key message types:

### Action Types
Over 50 predefined action types including:
- Movement: `GOTO`, `START_MELEE_ATTACK`, `STOP_ATTACK`
- Interaction: `INTERACT_WITH`, `SELECT_GOSSIP`, `ACCEPT_QUEST`
- Combat: `CAST_SPELL`, `USE_ITEM`, `STOP_CAST`
- Social: `SEND_GROUP_INVITE`, `ACCEPT_TRADE`, `OFFER_ITEM`
- System: `LOGIN`, `LOGOUT`, `CREATE_CHARACTER`

### Character Personality Traits
Based on the Big Five personality model:
- **Openness**: Creativity and curiosity (0.0-1.0)
- **Conscientiousness**: Organization and discipline (0.0-1.0)
- **Extraversion**: Social energy and assertiveness (0.0-1.0)
- **Agreeableness**: Cooperation and trust (0.0-1.0)
- **Neuroticism**: Emotional stability (0.0-1.0)

## Thread Safety

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

## Testing

Unit testing with the communication layer:

```csharp
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

## Troubleshooting

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

## License

This project is part of the BloogBot ecosystem and follows the same licensing terms as the main project.

## Contributing

When contributing to BotCommLayer:

1. Maintain protocol buffer schema compatibility
2. Follow thread-safety patterns established in existing code
3. Add comprehensive logging for debugging
4. Include unit tests for new message types
5. Update documentation for new communication patterns

---

BotCommLayer provides the essential communication infrastructure that enables BloogBot's distributed architecture, allowing seamless integration between AI services, game interfaces, and management tools.