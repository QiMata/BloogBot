# WoWSharpClient Network Agents

This document describes the network agent architecture for WoWSharpClient, which provides specialized agents for different game operations in World of Warcraft.

## Overview

The network agents follow a consistent naming pattern of `{}NetworkAgent` and provide focused functionality for specific game operations:

- **TargetingNetworkAgent** - Handles target selection and assist functionality
- **AttackNetworkAgent** - Manages auto-attack operations
- **QuestNetworkAgent** - Handles quest interactions and management
- **LootingNetworkAgent** - Manages looting operations and loot windows
- **GameObjectNetworkAgent** - Handles interactions with game objects (chests, nodes, doors, etc.)

## Architecture

### Core Principles

1. **Single Responsibility** - Each agent focuses on one specific aspect of game functionality
2. **Event-Driven** - Agents use events to communicate state changes and completion of operations
3. **Async Operations** - All network operations are asynchronous with proper cancellation support
4. **Testable** - Full dependency injection and interface-based design for easy testing
5. **Coordinated** - Agents can work together through shared interfaces

### Agent Structure

Each network agent follows a consistent structure:

```csharp
public interface I{Name}NetworkAgent
{
    // Properties for current state
    // Events for state changes
    // Async methods for operations
    // Synchronous helper methods
}

public class {Name}NetworkAgent : I{Name}NetworkAgent
{
    // Constructor with IWorldClient and ILogger
    // Implementation of all interface members
    // Public methods for server response handling
}
```

## Network Agents

### TargetingNetworkAgent

Manages target selection without combat functionality.

**Key Features:**
- Set/clear target operations
- Assist functionality for targeting what another player is targeting
- Target change events
- Current target state tracking

**Example Usage:**
```csharp
var targetingAgent = WoWClientFactory.CreateTargetingNetworkAgent(worldClient);
await targetingAgent.SetTargetAsync(enemyGuid);
await targetingAgent.AssistAsync(playerGuid);
```

### AttackNetworkAgent

Handles auto-attack operations and combat state management.

**Key Features:**
- Start/stop auto-attack
- Toggle attack state
- Attack specific targets (coordinates with targeting agent)
- Attack events (started, stopped, error)
- Attack state tracking

**Example Usage:**
```csharp
var attackAgent = WoWClientFactory.CreateAttackNetworkAgent(worldClient);
await attackAgent.AttackTargetAsync(enemyGuid, targetingAgent);
await attackAgent.ToggleAttackAsync();
```

### QuestNetworkAgent

Manages all quest-related operations.

**Key Features:**
- Quest giver interactions (hello, status query)
- Quest operations (accept, complete, query details)
- Quest reward selection
- Quest log management
- Party quest sharing

**Example Usage:**
```csharp
var questAgent = WoWClientFactory.CreateQuestNetworkAgent(worldClient);
await questAgent.HelloQuestGiverAsync(npcGuid);
await questAgent.AcceptQuestAsync(npcGuid, questId);
await questAgent.CompleteQuestAsync(npcGuid, questId);
```

### LootingNetworkAgent

Handles all looting operations including money, items, and group loot.

**Key Features:**
- Open/close loot windows
- Loot money and items
- Quick loot functionality (open, loot all, close)
- Group loot rolling (need/greed/pass)
- Loot window state tracking

**Example Usage:**
```csharp
var lootingAgent = WoWClientFactory.CreateLootingNetworkAgent(worldClient);
await lootingAgent.QuickLootAsync(corpseGuid);
await lootingAgent.RollForLootAsync(lootGuid, itemSlot, LootRollType.Need);
```

### GameObjectNetworkAgent

Manages interactions with game objects like chests, resource nodes, doors, and buttons.

**Key Features:**
- Generic game object interaction
- Specialized operations (open chest, gather node, use door, activate button)
- Smart interaction (automatically determines interaction type)
- Interaction distance calculations
- Game object state tracking

**Example Usage:**
```csharp
var gameObjectAgent = WoWClientFactory.CreateGameObjectNetworkAgent(worldClient);
await gameObjectAgent.OpenChestAsync(chestGuid);
await gameObjectAgent.GatherFromNodeAsync(herbNodeGuid);
await gameObjectAgent.SmartInteractAsync(gameObjectGuid, GameObjectType.Chest);
```

## Factory Methods

### Creating Individual Agents

```csharp
// With logger factory
var targetingAgent = WoWClientFactory.CreateTargetingNetworkAgent(worldClient, loggerFactory);
var attackAgent = WoWClientFactory.CreateAttackNetworkAgent(worldClient, loggerFactory);

// With specific logger
var questAgent = WoWClientFactory.CreateQuestNetworkAgent(worldClient, logger);

// Without logging
var lootingAgent = WoWClientFactory.CreateLootingNetworkAgent(worldClient);
```

### Creating Agent Sets

```csharp
// All agents
var allAgents = WoWClientFactory.CreateAllNetworkAgents(worldClient, loggerFactory);
var targetingAgent = allAgents.TargetingAgent;
var attackAgent = allAgents.AttackAgent;
// ... etc

// Combat agents only
var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatNetworkAgents(worldClient, loggerFactory);
```

## Integration Examples

### Basic Combat Integration

```csharp
public class CombatManager
{
    private readonly ITargetingNetworkAgent _targeting;
    private readonly IAttackNetworkAgent _attack;

    public async Task EngageEnemyAsync(ulong enemyGuid)
    {
        await _attack.AttackTargetAsync(enemyGuid, _targeting);
    }

    public async Task StopCombatAsync()
    {
        await _attack.StopAttackAsync();
        await _targeting.ClearTargetAsync();
    }
}
```

### Quest Management

```csharp
public class QuestManager
{
    private readonly IQuestNetworkAgent _quest;

    public async Task AcceptQuestChainAsync(ulong npcGuid, uint[] questIds)
    {
        foreach (var questId in questIds)
        {
            await _quest.HelloQuestGiverAsync(npcGuid);
            await _quest.QueryQuestAsync(npcGuid, questId);
            await _quest.AcceptQuestAsync(npcGuid, questId);
        }
    }
}
```

### Resource Gathering

```csharp
public class GatheringManager
{
    private readonly IGameObjectNetworkAgent _gameObject;
    private readonly ILootingNetworkAgent _looting;

    public async Task GatherAndLootAsync(ulong nodeGuid)
    {
        await _gameObject.GatherFromNodeAsync(nodeGuid);
        // If gathering creates loot, handle it
        if (_looting.IsLootWindowOpen)
        {
            await _looting.LootAllAsync();
        }
    }
}
```

## Event Handling

All agents provide events for monitoring operations:

```csharp
// Targeting events
targetingAgent.TargetChanged += (newTarget) => 
{
    Console.WriteLine($"Target changed to: {newTarget:X}");
};

// Attack events
attackAgent.AttackStarted += (victimGuid) => 
{
    Console.WriteLine($"Attack started on: {victimGuid:X}");
};

attackAgent.AttackError += (error) => 
{
    Console.WriteLine($"Attack error: {error}");
};

// Quest events
questAgent.QuestAccepted += (questId) => 
{
    Console.WriteLine($"Quest {questId} accepted");
};

// Loot events
lootingAgent.ItemLooted += (itemId, quantity) => 
{
    Console.WriteLine($"Looted {quantity}x item {itemId}");
};

// Game object events
gameObjectAgent.ChestOpened += (chestGuid) => 
{
    Console.WriteLine($"Chest {chestGuid:X} opened");
};
```

## Testing

All agents are fully testable with comprehensive test suites:

- **Unit Tests** - Test individual agent functionality with mocked dependencies
- **Integration Tests** - Test agent coordination and workflow scenarios
- **Event Tests** - Verify proper event firing and state management
- **Error Handling Tests** - Test exception handling and error scenarios

## Migration from Legacy Agents

The new network agents replace the previous `TargetingAgent` and `AttackAgent` classes:

### Before
```csharp
var targetingAgent = WoWClientFactory.CreateTargetingAgent(worldClient);
var attackAgent = WoWClientFactory.CreateAttackAgent(worldClient);
```

### After
```csharp
var targetingAgent = WoWClientFactory.CreateTargetingNetworkAgent(worldClient);
var attackAgent = WoWClientFactory.CreateAttackNetworkAgent(worldClient);
```

### Benefits of Migration

1. **Expanded Functionality** - Quest, looting, and game object operations
2. **Better Architecture** - Cleaner separation of concerns
3. **Enhanced Events** - More detailed event system for monitoring
4. **Improved Testing** - Better testability and test coverage
5. **Future-Proof** - Extensible design for additional game operations

## Best Practices

1. **Use Events** - Subscribe to agent events for monitoring and coordination
2. **Handle Errors** - Wrap agent operations in try-catch blocks
3. **Coordinate Agents** - Use agents together for complex workflows
4. **Manage State** - Check agent state before performing operations
5. **Async Patterns** - Always use async/await for agent operations
6. **Resource Cleanup** - Ensure proper disposal of resources when done

## Future Enhancements

Potential future additions to the network agent system:

- **TradingNetworkAgent** - For player-to-player trading operations
- **AuctionHouseNetworkAgent** - For auction house interactions
- **GuildNetworkAgent** - For guild management operations
- **MailNetworkAgent** - For mail system interactions
- **SpellNetworkAgent** - For spell casting and magic operations

# AgentNetworkOrchestrator

The `AgentNetworkOrchestrator` is a comprehensive facade that provides coordinated access to all network agents in the WoWSharpClient system. It acts as a single entry point for all game operations, combining the functionality of individual network agents into a unified, easy-to-use interface.

## Overview

The `AgentNetworkOrchestrator` simplifies bot development by:

1. **Unified Interface** - Single point of access for all game operations
2. **Coordinated Operations** - Complex workflows that combine multiple agents
3. **Simplified Error Handling** - Centralized logging and error management
4. **Event Coordination** - Automatic event handling between agents
5. **State Management** - Unified state information across all agents

## Architecture

```
AgentNetworkOrchestrator
??? ITargetingNetworkAgent     - Target selection and management
??? IAttackNetworkAgent        - Combat and auto-attack operations
??? IQuestNetworkAgent         - Quest interactions and management
??? ILootingNetworkAgent       - Looting and loot window operations
??? IGameObjectNetworkAgent    - Game object interactions
```

## Basic Usage

### Creating the Orchestrator

```csharp
// Recommended approach - let the factory create all agents
var worldClient = WoWClientFactory.CreateWorldClient();
var orchestrator = WoWClientFactory.CreateAgentNetworkOrchestrator(worldClient, loggerFactory);

// Alternative - with individual agents
var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatNetworkAgents(worldClient, loggerFactory);
var questAgent = WoWClientFactory.CreateQuestNetworkAgent(worldClient, loggerFactory);
var lootingAgent = WoWClientFactory.CreateLootingNetworkAgent(worldClient, loggerFactory);
var gameObjectAgent = WoWClientFactory.CreateGameObjectNetworkAgent(worldClient, loggerFactory);

var orchestrator = WoWClientFactory.CreateAgentNetworkOrchestrator(
    targetingAgent, attackAgent, questAgent, lootingAgent, gameObjectAgent, logger);
```

### Integration in BackgroundBotWorker

```csharp
public class BackgroundBotWorker : BackgroundService
{
    private readonly IAgentNetworkOrchestrator _agentOrchestrator;

    public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        // ... other initialization ...
        
        var worldClient = WoWClientFactory.CreateWorldClient();
        _agentOrchestrator = WoWClientFactory.CreateAgentNetworkOrchestrator(worldClient, loggerFactory);
    }

    // Simple operations through orchestrator
    public async Task SetTargetAsync(ulong targetGuid) =>
        await _agentOrchestrator.SetTargetAsync(targetGuid);

    public async Task AttackTargetAsync(ulong targetGuid) =>
        await _agentOrchestrator.AttackTargetAsync(targetGuid);

    public async Task AcceptQuestAsync(ulong questGiverGuid, uint questId) =>
        await _agentOrchestrator.AcceptQuestAsync(questGiverGuid, questId);
}
```

## Core Operations

### Targeting Operations

```csharp
// Basic targeting
await orchestrator.SetTargetAsync(enemyGuid);
await orchestrator.ClearTargetAsync();
await orchestrator.AssistPlayerAsync(playerGuid);

// Check targeting state
bool hasTarget = orchestrator.HasTarget;
ulong? currentTarget = orchestrator.CurrentTarget;
```

### Combat Operations

```csharp
// Basic combat
await orchestrator.StartAttackAsync();
await orchestrator.StopAttackAsync();
await orchestrator.ToggleAttackAsync();

// Coordinated combat (sets target + attacks)
await orchestrator.AttackTargetAsync(enemyGuid);

// Stop everything
await orchestrator.StopCombatAsync(); // Stops attack + clears target

// Check combat state
bool isAttacking = orchestrator.IsAttacking;
```

### Quest Operations

```csharp
// Individual quest operations
await orchestrator.HelloQuestGiverAsync(npcGuid);
await orchestrator.QueryQuestAsync(npcGuid, questId);
await orchestrator.AcceptQuestAsync(npcGuid, questId);
await orchestrator.CompleteQuestAsync(npcGuid, questId);
await orchestrator.ChooseQuestRewardAsync(npcGuid, questId, rewardIndex);
```

### Looting Operations

```csharp
// Individual loot operations
await orchestrator.OpenLootAsync(corpseGuid);
await orchestrator.LootMoneyAsync();
await orchestrator.LootItemAsync(slotIndex);
await orchestrator.CloseLootAsync();

// Quick loot (open + loot all + close)
await orchestrator.QuickLootAsync(corpseGuid);

// Group loot
await orchestrator.RollForLootAsync(lootGuid, itemSlot, LootRollType.Need);

// Check loot state
bool isLootWindowOpen = orchestrator.IsLootWindowOpen;
```

### Game Object Operations

```csharp
// Basic interactions
await orchestrator.InteractWithGameObjectAsync(gameObjectGuid);
await orchestrator.OpenChestAsync(chestGuid);
await orchestrator.GatherFromNodeAsync(herbNodeGuid);
await orchestrator.UseDoorAsync(doorGuid);
await orchestrator.ActivateButtonAsync(buttonGuid);

// Smart interaction (auto-detects type)
await orchestrator.SmartInteractAsync(gameObjectGuid, GameObjectType.Chest);
```

## Complex Workflows

The orchestrator provides high-level workflow methods that combine multiple operations:

### Quest Chain Management

```csharp
// Accept multiple quests in sequence
uint[] questIds = { 1001, 1002, 1003 };
await orchestrator.AcceptQuestChainAsync(questGiverGuid, questIds);

// Complete multiple quests in sequence
await orchestrator.CompleteQuestChainAsync(questGiverGuid, questIds);
```

### Area Harvesting

```csharp
// Loot multiple targets
ulong[] lootTargets = { corpse1Guid, corpse2Guid, corpse3Guid };
await orchestrator.LootMultipleTargetsAsync(lootTargets);

// Gather from multiple nodes
ulong[] gatherNodes = { herb1Guid, herb2Guid, ore1Guid };
await orchestrator.GatherFromMultipleNodesAsync(gatherNodes);

// Combined harvesting (loot + gather)
await orchestrator.HarvestAreaAsync(lootTargets, gatherNodes);
```

### Combat Sequences

```csharp
// Fight multiple enemies in sequence
ulong[] enemies = { enemy1Guid, enemy2Guid, enemy3Guid };
await orchestrator.CombatSequenceAsync(enemies);
```

### Advanced Workflows

```csharp
// Complex gameplay combining all operations
public async Task AdvancedGameplayWorkflowAsync()
{
    try
    {
        // 1. Accept quest chain
        uint[] questIds = { 1001, 1002 };
        await orchestrator.AcceptQuestChainAsync(questGiverGuid, questIds);

        // 2. Travel and clear enemies
        ulong[] enemies = { orc1Guid, orc2Guid };
        await orchestrator.CombatSequenceAsync(enemies);

        // 3. Harvest the area
        ulong[] loot = { corpse1Guid, corpse2Guid };
        ulong[] nodes = { herb1Guid, ore1Guid };
        await orchestrator.HarvestAreaAsync(loot, nodes);

        // 4. Return and complete quests
        await orchestrator.CompleteQuestChainAsync(questGiverGuid, questIds);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Advanced workflow failed");
    }
}
```

## State Management

The orchestrator provides unified state information:

```csharp
public class BotStateInfo
{
    public bool HasTarget { get; set; }
    public ulong? CurrentTarget { get; set; }
    public bool IsAttacking { get; set; }
    public bool IsLootWindowOpen { get; set; }
}

public BotStateInfo GetCurrentState()
{
    return new BotStateInfo
    {
        HasTarget = orchestrator.HasTarget,
        CurrentTarget = orchestrator.CurrentTarget,
        IsAttacking = orchestrator.IsAttacking,
        IsLootWindowOpen = orchestrator.IsLootWindowOpen
    };
}
```

## Error Handling

The orchestrator provides centralized error handling with comprehensive logging:

```csharp
try
{
    await orchestrator.AttackTargetAsync(enemyGuid);
}
catch (ArgumentException ex)
{
    // Handle invalid GUID
    logger.LogWarning("Invalid enemy GUID: {Error}", ex.Message);
}
catch (InvalidOperationException ex)
{
    // Handle operation not allowed
    logger.LogWarning("Attack not allowed: {Error}", ex.Message);
}
catch (Exception ex)
{
    // Handle unexpected errors
    logger.LogError(ex, "Unexpected error during attack");
}
```

## Event Handling

The orchestrator automatically coordinates events between agents and provides consolidated logging:

```csharp
// Events are handled internally and logged appropriately
// Target changed -> "Target changed to: 0x12345678"
// Attack started -> "Attack started on: 0x12345678"
// Quest accepted -> "Quest accepted: 1001"
// Item looted -> "Item looted: 5x 12345"
// etc.
```

## Best Practices

### 1. Use the Orchestrator as Your Primary Interface

```csharp
// ? Good - Use orchestrator
await orchestrator.AttackTargetAsync(enemyGuid);

// ? Avoid - Direct agent access
await targetingAgent.SetTargetAsync(enemyGuid);
await attackAgent.StartAttackAsync();
```

### 2. Leverage Complex Workflows

```csharp
// ? Good - Use workflow methods
await orchestrator.AcceptQuestChainAsync(npcGuid, questIds);

// ? Avoid - Manual coordination
foreach (var questId in questIds)
{
    await orchestrator.HelloQuestGiverAsync(npcGuid);
    await orchestrator.QueryQuestAsync(npcGuid, questId);
    await orchestrator.AcceptQuestAsync(npcGuid, questId);
}
```

### 3. Handle Cancellation Properly

```csharp
public async Task BotWorkflowAsync(CancellationToken cancellationToken)
{
    try
    {
        await orchestrator.CombatSequenceAsync(enemies, cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Bot workflow was cancelled");
        await orchestrator.StopCombatAsync(); // Cleanup
    }
}
```

### 4. Check State Before Operations

```csharp
public async Task SafeAttackAsync(ulong enemyGuid)
{
    if (orchestrator.IsAttacking)
    {
        await orchestrator.StopAttackAsync();
    }
    
    await orchestrator.AttackTargetAsync(enemyGuid);
}
```

## Testing

The orchestrator is fully testable with comprehensive mock support:

```csharp
[Fact]
public async Task AttackTargetAsync_CallsCorrectAgents()
{
    // Arrange
    var mockTargeting = new Mock<ITargetingNetworkAgent>();
    var mockAttack = new Mock<IAttackNetworkAgent>();
    // ... other mocks ...
    
    var orchestrator = new AgentNetworkOrchestrator(
        mockTargeting.Object, mockAttack.Object, 
        mockQuest.Object, mockLooting.Object, 
        mockGameObject.Object, mockLogger.Object);

    // Act
    await orchestrator.AttackTargetAsync(enemyGuid);

    // Assert
    mockAttack.Verify(x => x.AttackTargetAsync(enemyGuid, mockTargeting.Object, It.IsAny<CancellationToken>()), Times.Once);
}
```

## Migration Guide

### From Individual Agents

```csharp
// Before - Managing individual agents
private readonly ITargetingNetworkAgent _targeting;
private readonly IAttackNetworkAgent _attack;
private readonly IQuestNetworkAgent _quest;
// ... etc ...

public async Task AttackEnemyAsync(ulong enemyGuid)
{
    await _targeting.SetTargetAsync(enemyGuid);
    await _attack.StartAttackAsync();
}

// After - Using orchestrator
private readonly IAgentNetworkOrchestrator _orchestrator;

public async Task AttackEnemyAsync(ulong enemyGuid)
{
    await _orchestrator.AttackTargetAsync(enemyGuid);
}
```

### Benefits of Migration

1. **Simplified Code** - Fewer dependencies and method calls
2. **Better Error Handling** - Centralized error management
3. **Coordinated Operations** - Built-in workflow methods
4. **Unified Logging** - Consistent logging across all operations
5. **State Management** - Single source of truth for bot state
6. **Future-Proof** - Easy to extend with new workflow methods

The `AgentNetworkOrchestrator` represents the recommended approach for integrating WoWSharpClient network agents into your bot applications, providing a clean, maintainable, and powerful interface for all game operations.