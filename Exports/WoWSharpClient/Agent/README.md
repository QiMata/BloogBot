# WoWSharpClient Agent System

The Agent system provides high-level abstraction for World of Warcraft character operations. This folder contains classes that handle specific aspects of character behavior, with clear separation of concerns between targeting and attacking.

## Overview

The Agent system is designed to:
- Provide clean, high-level APIs for common WoW operations
- Handle the complex protocol details for you
- Integrate seamlessly with the WoWSharpClient networking architecture
- Support both manual and automated bot operations
- Follow single responsibility principle with separate agents for different concerns

## Architecture

The agent system is built around two core agents that work together:

### Targeting Agent (`ITargetingAgent`, `TargetingAgent`)
Handles target selection without any combat functionality:
- **`CMSG_SET_SELECTION (0x013D)`** - Tells the server what you currently have targeted
- Target selection and clearing
- Assist functionality (targeting what another player is targeting)
- Target state tracking and events

### Attack Agent (`IAttackAgent`, `AttackAgent`)
Handles combat operations without target selection:
- **`CMSG_ATTACKSWING (0x0141)`** - Starts auto-attack on current target
- **`CMSG_ATTACKSTOP (0x0142)`** - Stops auto-attack
- Attack state management
- Coordination with targeting agent for combined operations

## Basic Usage

### Separate Agent Usage

```csharp
// Create both agents
var worldClient = WoWClientFactory.CreateWorldClient();
var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatAgents(worldClient, loggerFactory);

// OR create them individually
var targetingAgent = WoWClientFactory.CreateTargetingAgent(worldClient, loggerFactory);
var attackAgent = WoWClientFactory.CreateAttackAgent(worldClient, loggerFactory);

// Pure targeting operations
await targetingAgent.SetTargetAsync(enemyGuid);
await targetingAgent.ClearTargetAsync();
await targetingAgent.AssistAsync(playerGuid);

// Pure attack operations
await attackAgent.StartAttackAsync();  // Requires a target to be set first
await attackAgent.StopAttackAsync();
await attackAgent.ToggleAttackAsync();

// Combined operations
await attackAgent.AttackTargetAsync(enemyGuid, targetingAgent); // Sets target AND attacks
```

### Events

Both agents provide events to track their respective states:

```csharp
// Targeting events
targetingAgent.TargetChanged += (newTarget) => {
    Console.WriteLine($"Target changed to: {newTarget:X}");
};

// Attack events
attackAgent.AttackStarted += (targetGuid) => {
    Console.WriteLine($"Started attacking: {targetGuid:X}");
};

attackAgent.AttackStopped += () => {
    Console.WriteLine("Stopped attacking");
};

attackAgent.AttackError += (errorMessage) => {
    Console.WriteLine($"Attack error: {errorMessage}");
};
```

### Integration with BackgroundBotRunner

See `CombatIntegrationExample.cs` for a complete example:

```csharp
public class BackgroundBotWorker : BackgroundService
{
    private readonly ITargetingAgent _targetingAgent;
    private readonly IAttackAgent _attackAgent;
    
    public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        var worldClient = WoWClientFactory.CreateWorldClient();
        var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatAgents(worldClient, loggerFactory);
        _targetingAgent = targetingAgent;
        _attackAgent = attackAgent;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Your bot logic here
            if (ShouldAttackNearbyEnemy())
            {
                var enemy = FindNearestEnemy();
                if (enemy != null)
                {
                    // Combined operation
                    await _attackAgent.AttackTargetAsync(enemy.Guid, _targetingAgent);
                }
            }
            
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

## Design Principles

### Single Responsibility
- **TargetingAgent**: Only handles target selection
- **AttackAgent**: Only handles combat actions
- Clear boundaries between concerns

### Coordination
- Agents can work together when needed
- `AttackAgent.AttackTargetAsync()` coordinates with targeting agent
- Events allow reactive coordination between agents

### Flexibility
- Use agents separately for fine-grained control
- Use convenience methods for common combined operations
- Easy to extend with additional agent types

## Server Response Handling

The `WorldClient` automatically handles server responses related to combat:

### Attack Responses
- **`SMSG_ATTACKSTART`** - Confirms attack has started
- **`SMSG_ATTACKSTOP`** - Confirms attack has stopped

### Attack Errors
- **`SMSG_ATTACKSWING_NOTINRANGE`** - Target not in range
- **`SMSG_ATTACKSWING_BADFACING`** - Bad facing for attack
- **`SMSG_ATTACKSWING_NOTSTANDING`** - Must be standing to attack
- **`SMSG_ATTACKSWING_DEADTARGET`** - Target is dead
- **`SMSG_ATTACKSWING_CANT_ATTACK`** - Cannot attack target

These responses are automatically processed and trigger appropriate events on the attack agent.

## Finding Targets

Use the `WoWSharpObjectManager` to find targets:

```csharp
// Find nearby hostile units
var nearbyEnemies = WoWSharpObjectManager.Instance.Objects
    .OfType<WoWUnit>()
    .Where(u => IsHostileUnit(u) && u.Position.DistanceTo(player.Position) < 30)
    .OrderBy(u => u.Position.DistanceTo(player.Position))
    .ToList();

// Helper method to check hostility
private static bool IsHostileUnit(WoWUnit unit)
{
    return unit.UnitReaction == UnitReaction.Hostile ||
           unit.UnitReaction == UnitReaction.Unfriendly;
}
```

## Error Handling

Both agents include comprehensive error handling:

```csharp
try
{
    await attackAgent.AttackTargetAsync(enemyGuid, targetingAgent);
}
catch (InvalidOperationException ex)
{
    // Handle cases like "no target selected" for attack
    logger.LogWarning($"Combat error: {ex.Message}");
}
catch (Exception ex)
{
    // Handle network or other errors
    logger.LogError(ex, "Unexpected error during combat operation");
}
```

## Testing

Comprehensive unit tests are provided for both agents:

- **`TargetingAgentTests`** - Tests targeting functionality
- **`AttackAgentTests`** - Tests attack functionality  
- **`AgentFactoryTests`** - Tests agent creation

Run tests with:
```bash
dotnet test Tests/WoWSharpClient.Tests/Agent/
```

## Thread Safety

All agent methods are thread-safe and use async/await patterns for non-blocking operations.

## Future Extensions

The Agent system is designed to be extensible. Planned future agents include:

- **SpellCastingAgent** - For casting spells and managing cooldowns
- **MovementAgent** - For advanced movement and pathfinding
- **InventoryAgent** - For item management and equipment
- **SocialAgent** - For group management and communication
- **QuestAgent** - For quest automation
- **AuctionAgent** - For auction house operations

## Architecture Integration

The Agent system integrates with the existing WoWSharpClient architecture:

```
BackgroundBotRunner
??? WoWClient (Legacy compatibility)
??? WorldClient (Modern networking)
??? TargetingAgent (Target selection)
??? AttackAgent (Combat actions)
??? WoWSharpObjectManager (Game state)
??? PathfindingClient (Navigation)
```

Each agent focuses on a specific aspect of character behavior while leveraging the robust networking and state management provided by the underlying WoWSharpClient infrastructure.