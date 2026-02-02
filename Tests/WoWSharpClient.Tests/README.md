# WoWSharpClient.Tests

Unit and integration tests for the WoWSharpClient network client library. Uses captured packet data to validate packet parsing, object updates, and protocol handling.

## Overview

This test project validates:
- **Packet Parsing**: Correct deserialization of all packet types
- **Object Updates**: Proper handling of SMSG_UPDATE_OBJECT packets
- **Compression**: Decompression of SMSG_COMPRESSED_* packets
- **Movement**: Movement packet handling and state updates
- **Chat/Spells**: Message and spell event processing

## Test Resources

The `Resources/` directory contains captured binary packet data organized by opcode:

```
Resources/
??? MSG_MOVE_FALL_LAND/           # Movement landing packets
??? MSG_MOVE_TIME_SKIPPED/        # Time sync packets
??? SMSG_ACCOUNT_DATA_TIMES/      # Account data
??? SMSG_AUTH_RESPONSE/           # Auth responses
??? SMSG_CHAR_ENUM/               # Character list
??? SMSG_COMPRESSED_MOVES/        # Compressed movement
??? SMSG_COMPRESSED_UPDATE_OBJECT/ # Compressed updates (many samples)
??? SMSG_DESTROY_OBJECT/          # Object removal
??? SMSG_MESSAGECHAT/             # Chat messages
?   ??? ServerWelcome.bin
?   ??? Say.bin
?   ??? Whisper.bin
?   ??? Yell.bin
??? SMSG_SPELL_GO/                # Spell execution
??? SMSG_UPDATE_OBJECT/           # Object updates
??? ... (many more)
```

## Test Categories

### Packet Parsing Tests

```csharp
public class PacketParsingTests
{
    [Theory]
    [MemberData(nameof(GetUpdateObjectPackets))]
    public void ParseUpdateObject_ValidPacket_DeserializesCorrectly(string packetFile)
    {
        var bytes = File.ReadAllBytes(packetFile);
        var result = ObjectUpdateHandler.Parse(bytes);
        Assert.NotNull(result);
    }
}
```

### Compression Tests

```csharp
public class CompressionTests
{
    [Fact]
    public void DecompressPacket_CompressedUpdateObject_DecompressesCorrectly()
    {
        // Test zlib decompression
    }
    
    [Fact]
    public void DecompressMoves_CompressedMoves_ParsesAllMovements()
    {
        // Test compressed movement batch
    }
}
```

### Chat Handler Tests

```csharp
public class ChatHandlerTests
{
    [Theory]
    [InlineData("SMSG_MESSAGECHAT/Say.bin", ChatType.Say)]
    [InlineData("SMSG_MESSAGECHAT/Whisper.bin", ChatType.Whisper)]
    [InlineData("SMSG_MESSAGECHAT/Yell.bin", ChatType.Yell)]
    public void ParseChatMessage_ValidPacket_ExtractsCorrectType(string file, ChatType expected)
    {
        // Test chat type parsing
    }
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test Tests/WoWSharpClient.Tests

# Run with verbose output
dotnet test Tests/WoWSharpClient.Tests -v normal

# Run with coverage
dotnet test Tests/WoWSharpClient.Tests --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test Tests/WoWSharpClient.Tests --filter "FullyQualifiedName~PacketParsingTests"
```

### Visual Studio

1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All" or select specific tests

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | VS Test integration |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| Moq | 4.20.70 | Mocking framework |
| coverlet.collector | 6.0.0 | Code coverage |

## Project References

- **WoWSharpClient**: System under test
- **BotRunner**: Bot framework utilities
- **BotCommLayer**: Message types
- **PathfindingService**: Movement integration

## Adding New Test Data

1. Capture packets using a packet sniffer
2. Save as `.bin` files in appropriate `Resources/OPCODE_NAME/` directory
3. Add to `.csproj` with `CopyToOutputDirectory`:

```xml
<None Update="Resources\NEW_OPCODE\packet.bin">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

## Packet Capture Notes

Test packets were captured on 2024-08-15 and represent actual server communication. File naming convention: `YYYYMMDD_HHMMSSmmm.bin` (timestamp in milliseconds).

## Related Documentation

- See `Exports/WoWSharpClient/README.md` for client implementation
- See `Exports/BotCommLayer/README.md` for message definitions
