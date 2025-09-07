# WoW Sharp Client Networking Abstractions

This document describes the networking abstractions implemented for the WoWSharpClient to provide a clean, composable, and testable networking architecture.

## Overview

The networking abstractions follow the Single Responsibility Principle by breaking down network communication into small, focused components that can be easily tested and composed together.

## Core Abstractions

### Interfaces

#### `IConnection`
Raw duplex byte connection with lifecycle management and events.

```csharp
public interface IConnection : IDisposable
{
    bool IsConnected { get; }
    
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    
    event Action? Connected;
    event Action<Exception?>? Disconnected;
    event Action<ReadOnlyMemory<byte>>? BytesReceived;
}
```

#### `IMessageFramer`
Handles framing and de-framing of messages from raw byte streams.

```csharp
public interface IMessageFramer
{
    ReadOnlyMemory<byte> Frame(ReadOnlyMemory<byte> payload);
    void Append(ReadOnlyMemory<byte> incoming);
    bool TryPop(out ReadOnlyMemory<byte> message);
}
```

#### `IEncryptor`
Provides optional encryption/decryption transforms for data.

```csharp
public interface IEncryptor
{
    ReadOnlyMemory<byte> Encrypt(ReadOnlyMemory<byte> data);
    ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> data);
}
```

#### `IPacketCodec<TOpcode>`
Handles encoding and decoding of packets with opcodes.

```csharp
public interface IPacketCodec<TOpcode> where TOpcode : Enum
{
    ReadOnlyMemory<byte> Encode(TOpcode opcode, ReadOnlyMemory<byte> payload);
    bool TryDecode(ReadOnlyMemory<byte> message, out TOpcode opcode, out ReadOnlyMemory<byte> payload);
}
```

#### `IMessageRouter<TOpcode>`
Routes messages with opcodes to appropriate handlers.

```csharp
public interface IMessageRouter<TOpcode> where TOpcode : Enum
{
    void Register(TOpcode opcode, Func<ReadOnlyMemory<byte>, Task> handler);
    Task RouteAsync(TOpcode opcode, ReadOnlyMemory<byte> payload);
}
```

#### `IReconnectPolicy`
Provides a policy for reconnection attempts with configurable backoff.

```csharp
public interface IReconnectPolicy
{
    TimeSpan? GetDelay(int attempt, Exception? lastError);
}
```

## Implementations

### Building Blocks

#### `TcpConnection`
TCP implementation of `IConnection` using `TcpClient` and `NetworkStream`.

**Features:**
- Thread-safe sending with `SemaphoreSlim`
- Background read loop with proper cancellation
- Automatic cleanup on disposal
- Event-driven connection lifecycle

#### `LengthPrefixedFramer`
Configurable length-prefixed message framing.

**Configuration:**
- Header size: 2 or 4 bytes
- Endianness: Big-endian or little-endian
- Thread-safe operations

#### `WoWMessageFramer`
WoW-specific message framer that handles the WoW protocol header format:
- 2 bytes size (big-endian)
- 2 bytes opcode (little-endian)
- Variable payload

#### `NoEncryption`
Pass-through encryptor for unencrypted communication.

#### `WoWPacketCodec`
WoW protocol packet codec that handles the specific WoW packet format with proper endianness handling.

#### `MessageRouter<TOpcode>`
Generic message router with concurrent dictionary for handler storage.

### Composition Classes

#### `PacketPipeline<TOpcode>`
Central coordination hub that connects all components:

```csharp
public sealed class PacketPipeline<TOpcode> : IDisposable where TOpcode : Enum
{
    public PacketPipeline(
        IConnection connection,
        IEncryptor encryptor,
        IMessageFramer framer,
        IPacketCodec<TOpcode> codec,
        IMessageRouter<TOpcode> router)
    
    public Task SendAsync(TOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken ct)
    public void RegisterHandler(TOpcode opcode, Func<ReadOnlyMemory<byte>, Task> handler)
}
```

**Data Flow:**
- **Outbound:** Codec.Encode ? Framer.Frame ? Encryptor.Encrypt ? Connection.Send
- **Inbound:** Connection.Receive ? Encryptor.Decrypt ? Framer.Append/TryPop ? Codec.Decode ? Router.Route

#### `ConnectionManager`
Wraps `IConnection` with automatic reconnection based on `IReconnectPolicy`.

**Features:**
- Automatic reconnection with configurable policies
- Event forwarding with reconnection awareness
- Graceful shutdown

### Reconnection Policies

#### `ExponentialBackoffPolicy`
Implements exponential backoff with configurable parameters:
- Maximum attempts
- Initial delay
- Maximum delay
- Backoff multiplier

#### `FixedDelayPolicy`
Simple fixed delay between reconnection attempts.

## Usage Examples

### Basic WoW Client Setup

```csharp
// Create components
var connection = new TcpConnection();
var encryptor = new NoEncryption(); // Start with no encryption
var framer = new WoWMessageFramer();
var codec = new WoWPacketCodec();
var router = new MessageRouter<Opcode>();

// Create pipeline
var pipeline = new PacketPipeline<Opcode>(connection, encryptor, framer, codec, router);

// Register handlers
pipeline.RegisterHandler(Opcode.SMSG_AUTH_CHALLENGE, HandleAuthChallenge);
pipeline.RegisterHandler(Opcode.SMSG_PONG, HandlePong);

// Connect and send
await pipeline.ConnectAsync("127.0.0.1", 8085);
await pipeline.SendAsync(Opcode.CMSG_PING, pingPayload);
```

### With Connection Manager

```csharp
var reconnectPolicy = new ExponentialBackoffPolicy(
    maxAttempts: 5,
    initialDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromSeconds(30));

var connectionManager = new ConnectionManager(connection, reconnectPolicy, "127.0.0.1", 8085);

connectionManager.Connected += () => Console.WriteLine("Connected!");
connectionManager.Disconnected += (ex) => Console.WriteLine($"Disconnected: {ex?.Message}");

await connectionManager.ConnectAsync();
```

### Factory Pattern

```csharp
// Use the factory for common configurations
var pipeline = WoWNetworkingFactory.CreateBasicPipeline();
var connectionManager = WoWNetworkingFactory.CreateConnectionManager(
    pipeline.Connection, "127.0.0.1", 8085);
```

## Testing

The abstractions are designed to be easily testable:

```csharp
[Fact]
public void WoWPacketCodec_CanEncodeAndDecodePackets()
{
    var codec = new WoWPacketCodec();
    var originalOpcode = Opcode.CMSG_PING;
    var originalPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

    var encodedPacket = codec.Encode(originalOpcode, originalPayload);
    var success = codec.TryDecode(encodedPacket, out var decodedOpcode, out var decodedPayload);

    Assert.True(success);
    Assert.Equal(originalOpcode, decodedOpcode);
    Assert.Equal(originalPayload, decodedPayload.ToArray());
}
```

## Architecture Benefits

### Single Responsibility
Each component has a single, well-defined responsibility:
- **Connection**: Raw network I/O
- **Framer**: Message boundaries
- **Encryptor**: Data transformation
- **Codec**: Protocol-specific encoding
- **Router**: Message dispatch

### Composability
Components can be mixed and matched:
- Different framers for different protocols
- Swappable encryption (no encryption ? RC4 ? modern crypto)
- Protocol evolution support

### Testability
Each component can be unit tested in isolation:
- Mock implementations for testing
- Clear interfaces for dependency injection
- No hidden dependencies

### Thread Safety
Components are designed to be thread-safe:
- Immutable data structures where possible
- Proper synchronization primitives
- Safe event handling

### Performance
Optimized for high-performance networking:
- Zero-copy operations with `ReadOnlyMemory<byte>`
- Minimal allocations
- Efficient buffering strategies

## Integration with Existing Code

The new abstractions can be gradually integrated with the existing `WorldClient`:

1. **Phase 1**: Use new abstractions for new features
2. **Phase 2**: Migrate existing packet handlers to new router
3. **Phase 3**: Replace existing connection management

This allows for incremental migration without breaking existing functionality.

## Future Enhancements

### Planned Features
- **RC4 Encryptor**: WoW-specific encryption implementation
- **Compression Support**: Optional message compression
- **Metrics Collection**: Performance and reliability metrics
- **Protocol Versioning**: Support for different WoW versions
- **Connection Pooling**: Multiple connections for reliability

### Extension Points
The architecture supports easy extension:
- Custom framers for other protocols
- Advanced encryption schemes
- Custom reconnection policies
- Protocol-specific codecs