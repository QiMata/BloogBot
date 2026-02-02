# BotRunner

Core bot execution framework providing behavior tree infrastructure, pathfinding client, and state management for the WWoW bot system.

## Overview

BotRunner provides:
- **Behavior Tree Framework**: Fluent API for building bot decision trees
- **State Machine Integration**: Stateless library for complex state transitions
- **Pathfinding Client**: IPC client for the PathfindingService
- **Character State Client**: Communication with StateManager service
- **Utility Functions**: Name generation, context management

## Project Structure

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

| Package | Version | Purpose |
|---------|---------|---------|
| BehaviourTree | 1.0.73 | Behavior tree core |
| BehaviourTree.FluentBuilder | 1.0.70 | Fluent behavior tree API |
| Stateless | 5.16.0 | Finite state machine |
| Xas.FluentBehaviourTree | 1.0.0 | Additional fluent BT support |

## Behavior Tree Usage

### Building a Simple Tree

```csharp
using BehaviourTree;
using BehaviourTree.FluentBuilder;

var tree = FluentBuilder.Create<BotContext>()
    .Selector("Root")
        .Sequence("Combat")
            .Condition("HasTarget", ctx => ctx.HasTarget)
            .Do("Attack", ctx => ctx.Attack())
        .End()
        .Sequence("Grind")
            .Condition("NeedsTarget", ctx => !ctx.HasTarget)
            .Do("FindTarget", ctx => ctx.FindNearestEnemy())
            .Do("MoveToTarget", ctx => ctx.MoveToTarget())
        .End()
    .End()
    .Build();

// Execute tick
var status = tree.Tick(context);
```

### Common Node Types

- **Selector**: Tries children until one succeeds (OR logic)
- **Sequence**: Runs children until one fails (AND logic)
- **Condition**: Returns success/failure based on predicate
- **Do**: Executes an action

## Pathfinding Client

```csharp
using BotRunner.Clients;

var pathfindingClient = new PathfindingClient("localhost", 5000);

// Calculate path
var path = await pathfindingClient.CalculatePathAsync(
    mapId: 0,
    start: new Position(100, 200, 50),
    end: new Position(500, 600, 55)
);

// Check line of sight
var hasLos = await pathfindingClient.CheckLineOfSightAsync(
    mapId: 0,
    from: playerPosition,
    to: targetPosition
);
```

## State Machine Integration

```csharp
using Stateless;

public enum BotState { Idle, Moving, Combat, Looting, Resting }
public enum Trigger { TargetFound, TargetDead, HealthLow, Rested }

var machine = new StateMachine<BotState, Trigger>(BotState.Idle);

machine.Configure(BotState.Idle)
    .Permit(Trigger.TargetFound, BotState.Combat)
    .OnEntry(() => Console.WriteLine("Entering Idle"));

machine.Configure(BotState.Combat)
    .Permit(Trigger.TargetDead, BotState.Looting)
    .Permit(Trigger.HealthLow, BotState.Resting)
    .OnEntry(() => StartCombat());
```

## Project References

- **GameData.Core**: Game object interfaces and models

## Consumers

This library is used by:
- **ForegroundBotRunner**: In-process bot execution
- **BackgroundBotRunner**: Network-based bot execution
- **WoWSharpClient**: Movement and action coordination

## Related Documentation

- See `ARCHITECTURE.md` for overall system design
- See `Services/PathfindingService/README.md` for pathfinding details
- See `Services/StateManager/README.md` for state management
