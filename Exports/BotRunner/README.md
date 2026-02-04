# BotRunner

Core bot execution framework providing behavior tree infrastructure, pathfinding client, and state management for the WWoW bot system.

## Overview

BotRunner is a core automation engine that orchestrates World of Warcraft character actions using behavior trees, pathfinding services, and real-time game state management. It provides a comprehensive framework for creating intelligent bots that can perform complex game activities with human-like decision making.

BotRunner provides:
- **Behavior Tree Framework**: Fluent API for building bot decision trees using `Xas.FluentBehaviourTree`
- **State Machine Integration**: Stateless library for complex state transitions
- **Pathfinding Client**: IPC client for the PathfindingService
- **Character State Client**: Communication with StateManager service
- **Name Generation**: Procedural character name creation based on race and gender
- **Multi-character Support**: Handles multiple characters with unique behaviors and coordination

## Architecture

```
+------------------------------------------------------------------+
|                         BotRunner                                 |
+------------------------------------------------------------------+
|                                                                   |
|  +------------------------+    +-----------------------------+   |
|  |   BotRunnerService     |    |     Behavior Tree System    |   |
|  |  (Main Orchestrator)   |--->|  - Action Sequences         |   |
|  +------------------------+    |  - Conditional Logic        |   |
|             |                  |  - Parallel Execution       |   |
|             v                  +-----------------------------+   |
|  +------------------------+                                      |
|  |       Clients/         |                                      |
|  |  PathfindingClient     |---> PathfindingService (TCP)         |
|  |  CharacterStateClient  |---> StateManager (TCP)               |
|  +------------------------+                                      |
|             |                                                     |
|             v                                                     |
|  +------------------------+                                      |
|  |     WoWNameGenerator   |    Character name generation         |
|  +------------------------+                                      |
+------------------------------------------------------------------+
```

## Project Structure

```
BotRunner/
+-- BotRunnerService.cs              # Main service orchestrator
+-- WoWNameGenerator.cs              # Random character name generation
+-- Clients/
|   +-- PathfindingClient.cs         # Pathfinding service IPC client
|   +-- CharacterStateUpdateClient.cs # State manager IPC client
+-- Constants/
|   +-- BotContext.cs                # Shared bot context/state
+-- Nodes/                           # Behavior tree nodes
```

## Key Components

| Component | Description |
|-----------|-------------|
| `BotRunnerService` | Main orchestration service managing bot lifecycle and behavior execution |
| `PathfindingClient` | Handles navigation, line-of-sight, and physics calculations |
| `CharacterStateUpdateClient` | Manages state synchronization with external services |
| `WoWNameGenerator` | Generates lore-appropriate character names by race and gender |
| `BotContext` | Shared context for behavior tree state |

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| BehaviourTree | 1.0.73 | Behavior tree core |
| BehaviourTree.FluentBuilder | 1.0.70 | Fluent behavior tree API |
| Xas.FluentBehaviourTree | 1.0.0 | Enhanced fluent BT support |
| Stateless | 5.16.0 | Finite state machine |

## Project References

- **GameData.Core**: Game object interfaces and models

## Usage

### Behavior Tree Example

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

### State Machine Integration

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

### Pathfinding Client

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

### Character Name Generation

```csharp
string characterName = WoWNameGenerator.GenerateName(Race.Human, Gender.Male);
// Example output: "Cedricwin", "Alberton", "Godarden"
```

Supported races: Human, Dwarf, Night Elf, Gnome, Orc, Undead, Tauren, Troll

## Configuration

```json
{
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": 8080
  },
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": 8081
  }
}
```

## Consumers

This library is used by:
- **ForegroundBotRunner**: In-process bot execution
- **BackgroundBotRunner**: Network-based bot execution
- **WoWSharpClient**: Movement and action coordination

## Related Documentation

- See `ARCHITECTURE.md` for overall system design
- See `Services/PathfindingService/README.md` for pathfinding details
- See `Services/StateManager/README.md` for state management

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
