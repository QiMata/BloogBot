# BotRunner

Core bot execution framework providing behavior tree infrastructure, pathfinding client, and state management for the WWoW bot system.
A sophisticated World of Warcraft bot automation library built on .NET 8 that provides intelligent character behavior through behavior trees and pathfinding integration.

## Overview

BotRunner provides:
- **Behavior Tree Framework**: Fluent API for building bot decision trees
- **State Machine Integration**: Stateless library for complex state transitions
- **Pathfinding Client**: IPC client for the PathfindingService
- **Character State Client**: Communication with StateManager service
- **Utility Functions**: Name generation, context management
BotRunner is a core automation engine that orchestrates World of Warcraft character actions using behavior trees, pathfinding services, and real-time game state management. It provides a comprehensive framework for creating intelligent bots that can perform complex game activities with human-like decision making.

## Project Structure
## Features

### Core Functionality
- **Behavior Tree Engine**: Utilizes `Xas.FluentBehaviourTree` for creating complex, hierarchical bot behaviors
- **Pathfinding Integration**: Advanced movement with collision detection and optimized routing
- **Real-time State Management**: Continuous monitoring and updating of character and game state
- **Multi-character Support**: Handles multiple characters with unique behaviors and coordination

### Game Actions
- **Movement & Navigation**: Intelligent pathfinding, facing, and movement control
- **Combat System**: Auto-attack management, spell casting, and target selection
- **Trading & Economy**: Item trading, gold transactions, and merchant interactions
- **Social Features**: Group management, invitations, and party coordination
- **Character Progression**: Quest handling, skill training, and talent management
- **Inventory Management**: Item usage, equipment handling, and bag organization

### Advanced Features
- **Dynamic Name Generation**: Procedural character name creation based on race and gender
- **State Persistence**: Character state tracking and coordination across sessions
- **Network Communication**: Client-server architecture for distributed bot management
- **Error Handling**: Robust exception management and recovery mechanisms

## Architecture

### Core Components

#### BotRunnerService
The main orchestration service that manages the bot's lifecycle and behavior execution.

```csharp
public class BotRunnerService
{
    private readonly IObjectManager _objectManager;
    private readonly CharacterStateUpdateClient _characterStateUpdateClient;
    private readonly PathfindingClient _pathfindingClient;
    private IBehaviourTreeNode _behaviorTree;
}
```
BotRunner/
??? BotRunnerService.cs              # Main service orchestrator
??? WoWNameGenerator.cs              # Random character name generation
??? Clients/
?   ??? PathfindingClient.cs         # Pathfinding service IPC client
?   ??? CharacterStateUpdateClient.cs # State manager IPC client
??? Constants/
?   ??? BotContext.cs                # Shared bot context/state
??? Nodes/                           # Behavior tree nodes (empty)
```

## Dependencies
#### Behavior Tree System
Creates dynamic behavior trees based on character actions and game state:

| Package | Version | Purpose |
|---------|---------|---------|
| BehaviourTree | 1.0.73 | Behavior tree core |
| BehaviourTree.FluentBuilder | 1.0.70 | Fluent behavior tree API |
| Stateless | 5.16.0 | Finite state machine |
| Xas.FluentBehaviourTree | 1.0.0 | Additional fluent BT support |
- **Action Sequences**: Login, character creation, quest handling, combat
- **Conditional Logic**: State-based decision making and prerequisites
- **Parallel Execution**: Multiple concurrent behaviors and activities

## Behavior Tree Usage
#### Client Architecture
- **PathfindingClient**: Handles navigation, line-of-sight, and physics calculations
- **CharacterStateUpdateClient**: Manages state synchronization with external services
- **ProtobufSocketClient**: Network communication layer for service integration

### Building a Simple Tree
### Dependencies

- **.NET 8**: Modern C# runtime with latest language features
- **BehaviourTree (1.0.73)**: Core behavior tree implementation
- **BehaviourTree.FluentBuilder (1.0.70)**: Fluent API for behavior tree construction
- **Xas.FluentBehaviourTree (1.0.0)**: Enhanced behavior tree functionality
- **Stateless (5.16.0)**: State machine implementation for complex workflows
- **GameData.Core**: Game data models and interfaces

## Usage

### Basic Setup

```csharp
using BehaviourTree;
using BehaviourTree.FluentBuilder;
// Initialize combat helpers
var combatState = new BotCombatState();
var engagementService = new TargetEngagementService(agentFactory, combatState);
var lootingService = new LootingService(agentFactory, combatState);
var positioningService = new TargetPositioningService(objectManager, pathfindingClient);

var tree = FluentBuilder.Create<BotContext>()
    .Selector("Root")
        .Sequence("Combat")
            .Condition("HasTarget", ctx => ctx.HasTarget)
            .Do("Attack", ctx => ctx.Attack())
// Initialize the bot runner with required dependencies
var botRunner = new BotRunnerService(
    objectManager,
    characterStateUpdateClient,
    engagementService,
    lootingService,
    positioningService
);

// Start the bot
botRunner.Start();
```

### Creating Custom Behaviors

```csharp
// Example: Creating a quest completion behavior
private IBehaviourTreeNode BuildQuestSequence() => new BehaviourTreeBuilder()
    .Sequence("Quest Sequence")
        .Splice(BuildGoToSequence(questGiver.X, questGiver.Y, questGiver.Z, 0))
        .Splice(BuildInteractWithSequence(questGiver.Guid))
        .Splice(AcceptQuestSequence)
        .Splice(BuildGoToSequence(questObjective.X, questObjective.Y, questObjective.Z, 0))
        // ... additional quest steps
    .End()
        .Sequence("Grind")
            .Condition("NeedsTarget", ctx => !ctx.HasTarget)
            .Do("FindTarget", ctx => ctx.FindNearestEnemy())
            .Do("MoveToTarget", ctx => ctx.MoveToTarget())
        .End()
    .End()
    .Build();
```

// Execute tick
var status = tree.Tick(context);
### Character Action System

The bot supports a comprehensive set of character actions:

```csharp
public enum CharacterAction
{
    // Movement
    GoTo, Wait, 
    
    // Interaction
    InteractWith, SelectGossip, SelectTaxiNode,
    
    // Quests
    AcceptQuest, DeclineQuest, CompleteQuest, SelectReward,
    
    // Combat
    CastSpell, StopCast, StopAttack,
    
    // Trading
    OfferTrade, AcceptTrade, OfferItem, OfferGold,
    
    // Group Management
    SendGroupInvite, AcceptGroupInvite, KickPlayer,
    
    // Character Management
    Login, Logout, CreateCharacter, EnterWorld
}
```

## Configuration

### Pathfinding Service
Configure the pathfinding service connection:

```json
{
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": 8080
  }
}
```

### Common Node Types
### Character State Management
Configure state update service:

```json
{
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": 8081
  }
}
```

- **Selector**: Tries children until one succeeds (OR logic)
- **Sequence**: Runs children until one fails (AND logic)
- **Condition**: Returns success/failure based on predicate
- **Do**: Executes an action
## Integration

## Pathfinding Client
### Background Service Integration
BotRunner integrates seamlessly with .NET Worker Services:

```csharp
using BotRunner.Clients;

var pathfindingClient = new PathfindingClient("localhost", 5000);
public class BackgroundBotWorker : BackgroundService
{
    private readonly BotRunnerService _botRunner;
    
// Calculate path
var path = await pathfindingClient.CalculatePathAsync(
    mapId: 0,
    start: new Position(100, 200, 50),
    end: new Position(500, 600, 55)
);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botRunner.Start();
        
// Check line of sight
var hasLos = await pathfindingClient.CheckLineOfSightAsync(
    mapId: 0,
    from: playerPosition,
    to: targetPosition
);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

## State Machine Integration
### Service Dependencies
- **PathfindingService**: Provides navigation and collision detection
- **StateManager**: Coordinates character state across multiple bots
- **WoWSharpClient**: Low-level game client interface

## Character Name Generation

BotRunner includes a sophisticated name generation system that creates lore-appropriate names:

```csharp
using Stateless;
string characterName = WoWNameGenerator.GenerateName(Race.Human, Gender.Male);
// Example output: "Cedricwin", "Alberton", "Godarden"
```

### Supported Races & Classes
- **Alliance**: Human, Dwarf, Night Elf, Gnome
- **Horde**: Orc, Undead, Tauren, Troll
- **Classes**: All classic WoW classes with appropriate name generation

public enum BotState { Idle, Moving, Combat, Looting, Resting }
public enum Trigger { TargetFound, TargetDead, HealthLow, Rested }
## Development

var machine = new StateMachine<BotState, Trigger>(BotState.Idle);
### Project Structure
```
BotRunner/
??? BotRunnerService.cs          # Main orchestration service
??? WoWNameGenerator.cs          # Character name generation
??? Clients/                     # Network communication
?   ??? PathfindingClient.cs     # Navigation service client
?   ??? CharacterStateUpdateClient.cs  # State management client
??? Constants/
    ??? BotContext.cs            # Game state context
```

machine.Configure(BotState.Idle)
    .Permit(Trigger.TargetFound, BotState.Combat)
    .OnEntry(() => Console.WriteLine("Entering Idle"));
### Building
```bash
dotnet build BotRunner.csproj
```

machine.Configure(BotState.Combat)
    .Permit(Trigger.TargetDead, BotState.Looting)
    .Permit(Trigger.HealthLow, BotState.Resting)
    .OnEntry(() => StartCombat());
### Testing
Tests are available in the `BotRunner.Tests` project:
```bash
dotnet test ../../Tests/BotRunner.Tests/
```

## Project References
## Contributing

- **GameData.Core**: Game object interfaces and models
1. Follow the existing code style and patterns
2. Add comprehensive behavior tree sequences for new actions
3. Ensure proper error handling and state validation
4. Update tests for any new functionality

## Consumers
## License

This library is used by:
- **ForegroundBotRunner**: In-process bot execution
- **BackgroundBotRunner**: Network-based bot execution
- **WoWSharpClient**: Movement and action coordination
This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Related Documentation
## Related Projects

- See `ARCHITECTURE.md` for overall system design
- See `Services/PathfindingService/README.md` for pathfinding details
- See `Services/StateManager/README.md` for state management
- **PathfindingService**: Advanced navigation and collision detection
- **StateManager**: Multi-character state coordination
- **WoWSharpClient**: Low-level game client interface
- **GameData.Core**: Shared game data models and enumerations