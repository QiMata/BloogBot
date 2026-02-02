# BackgroundBotRunner

Background worker service that executes bot logic using the WoWSharpClient network implementation. Runs as a standalone service without requiring the game client, communicating purely via network protocols.

## Overview

BackgroundBotRunner is a .NET Worker Service that:
- **Runs Headless**: No game client required
- **Uses Network Protocol**: Pure network-based game interaction via WoWSharpClient
- **Integrates AI**: Connects to PromptHandlingService for decision making
- **Scales Horizontally**: Run multiple instances for multiple characters

## Architecture

```
BackgroundBotRunner/
??? BackgroundBotWorker.cs    # Main background service
```

## How It Works

```
???????????????????????     ????????????????????
? BackgroundBotRunner ???????   WoWSharpClient ?
?   (Worker Service)  ?     ? (Network Client) ?
???????????????????????     ????????????????????
         ?                           ?
         ?                           ?
         ?                  ??????????????????
         ?                  ?  Game Server   ?
         ?                  ?  (1.12.1)      ?
         ?                  ??????????????????
         ?
         ?
???????????????????????
?PromptHandlingService?
?   (AI Decisions)    ?
???????????????????????
```

## Implementation

The `BackgroundBotWorker` inherits from `BackgroundService`:

```csharp
public class BackgroundBotWorker : BackgroundService
{
    private readonly ILogger<BackgroundBotWorker> _logger;
    private readonly WoWClient _client;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to game server
        await _client.ConnectAsync();
        await _client.LoginAsync(username, password);
        await _client.EnterWorldAsync(characterName);
        
        // Main bot loop
        while (!stoppingToken.IsCancellationRequested)
        {
            // Process game state
            await ProcessGameTickAsync();
            
            // Execute bot logic
            await ExecuteBotDecisionAsync();
            
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

## Configuration

Configure via `appsettings.json`:

```json
{
  "GameServer": {
    "Host": "logon.server.com",
    "AuthPort": 3724,
    "WorldPort": 8085
  },
  "Account": {
    "Username": "botaccount",
    "Password": "password",
    "Character": "BotChar"
  },
  "Services": {
    "PathfindingHost": "localhost",
    "PathfindingPort": 5000,
    "StateManagerHost": "localhost",
    "StateManagerPort": 5001
  }
}
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3 | Configuration parsing |

## Project References

- **BotRunner**: Behavior tree framework and pathfinding client
- **WoWSharpClient**: Network game client implementation
- **PromptHandlingService**: AI decision integration

## Running as a Service

### Development

```bash
dotnet run --project Services/BackgroundBotRunner
```

### Production (Windows Service)

```bash
# Publish
dotnet publish -c Release -o ./publish

# Install as service
sc create BackgroundBot binPath="C:\path\to\BackgroundBotRunner.exe"
sc start BackgroundBot
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "BackgroundBotRunner.dll"]
```

## Use Cases

1. **Multi-boxing**: Run multiple characters without multiple game clients
2. **Server-side Bots**: Run bots on a headless server
3. **Testing**: Test bot logic without game client overhead
4. **CI/CD**: Automated testing of bot behaviors

## Limitations

- No visual feedback (headless operation)
- Cannot interact with game UI directly
- Movement is server-authoritative (no client prediction)
- Some private servers may detect pure network clients

## Related Documentation

- See `Exports/WoWSharpClient/README.md` for network client details
- See `Services/PromptHandlingService/README.md` for AI integration
- See `ARCHITECTURE.md` for system overview
