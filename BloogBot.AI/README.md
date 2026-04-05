# WWoW.AI

AI-driven decision making engine for the WWoW (Westworld of Warcraft) simulation platform.

## Overview

WWoW.AI is the intelligent decision-making core of the WWoW ecosystem, providing sophisticated AI-driven behavior coordination using Microsoft Semantic Kernel and state machine patterns. It orchestrates complex bot activities through dynamic plugin management and context-aware state transitions.

The library provides:

- **Semantic Kernel Integration**: Microsoft Semantic Kernel for AI orchestration
- **Activity State Machine**: Stateless-based activity transitions with 26 major states
- **Plugin System**: Extensible activity plugins with attribute-based registration
- **LLM Advisory System**: AI suggestions validated by deterministic logic
- **Persistent Memory**: Character memory with PostgreSQL persistence

## Architecture

```
+------------------------------------------------------------------+
|                          WWoW.AI                                  |
+------------------------------------------------------------------+
|                                                                   |
|  +-----------------------------------------------------------+   |
|  |              BotActivityStateMachine                       |   |
|  |        State orchestration and transition logic            |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|         +--------------------+--------------------+               |
|         |                    |                    |               |
|  +--------------+    +---------------+    +---------------+      |
|  | Plugin       |    | Observable    |    | Advisory      |      |
|  | Catalog      |    | State Stream  |    | Validator     |      |
|  |              |    |               |    |               |      |
|  | - Discovery  |    | - Events      |    | - LLM Input   |      |
|  | - Loading    |    | - Filtering   |    | - Override    |      |
|  | - Mapping    |    | - Subscriptions|   | - Safety      |      |
|  +--------------+    +---------------+    +---------------+      |
|         |                    |                    |               |
|  +-----------------------------------------------------------+   |
|  |                   KernelCoordinator                        |   |
|  |          AI plugin management and kernel coordination      |   |
|  +-----------------------------------------------------------+   |
|                                                                   |
+------------------------------------------------------------------+
```

## Project Structure

```
BloogBot.AI/
+-- States/
|   +-- BotActivity.cs            # Major activity enumeration (25 states)
|   +-- MinorState.cs             # Minor state record type
|   +-- MinorStateDefinitions.cs  # All minor states per activity
+-- StateMachine/
|   +-- BotActivityStateMachine.cs # Core state orchestration
|   +-- Trigger.cs                # State transition triggers
+-- Observable/
|   +-- IBotStateObservable.cs    # Observable contract
|   +-- BotStateObservable.cs     # Observable implementation
|   +-- StateChangeEvent.cs       # State change record
|   +-- StateChangeSource.cs      # Change source enum
+-- Transitions/
|   +-- IForbiddenTransitionRegistry.cs  # Registry contract
|   +-- ForbiddenTransitionRegistry.cs   # Registry implementation
|   +-- ForbiddenTransitionRule.cs       # Rule definition
|   +-- TransitionContext.cs             # Context for predicates
+-- Advisory/
|   +-- IAdvisoryValidator.cs     # Validator contract
|   +-- AdvisoryValidator.cs      # Deterministic override logic
|   +-- LlmAdvisoryResult.cs      # LLM output type
|   +-- AdvisoryResolution.cs     # Validation result
|   +-- IAdvisoryOverrideLog.cs   # Audit logging
+-- Configuration/
|   +-- DecisionInvocationSettings.cs        # Settings POCO
|   +-- DecisionInvocationSettingsResolver.cs # Precedence resolver
+-- Invocation/
|   +-- IDecisionInvoker.cs       # Invoker contract
|   +-- DecisionInvoker.cs        # Timer-based implementation
+-- Summary/
|   +-- ISummaryPipeline.cs       # Pipeline contract
|   +-- SummaryPipeline.cs        # Multi-pass implementation
|   +-- SummaryContext.cs         # Input context types
|   +-- DistilledSummary.cs       # Output summary type
+-- Memory/
|   +-- CharacterMemory.cs        # Memory model
|   +-- ICharacterMemoryRepository.cs     # Repository contract
|   +-- PostgresCharacterMemoryRepository.cs  # PostgreSQL impl
|   +-- CharacterMemoryService.cs         # Lazy load + batch save
+-- Semantic/
|   +-- KernelCoordinator.cs      # AI plugin coordination
|   +-- PluginCatalog.cs          # Plugin discovery
+-- Annotations/
    +-- ActivityPluginAttribute.cs  # Plugin registration
```

## Key Components

### BotActivityStateMachine

State orchestration and transition logic supporting 26 activity states with global triggers.

### KernelCoordinator

AI plugin management and kernel coordination with activity-based AI switching.

### PluginCatalog

Plugin discovery and categorization system with attribute-based registration.

### ActivityPluginAttribute

Plugin metadata and activity association enabling declarative configuration.

## Activity Categories

### Character Development
- **Questing**: Quest completion and progression
- **Grinding**: Experience and reputation farming
- **Professions**: Crafting and gathering activities
- **Talenting**: Talent point allocation and builds
- **Equipping**: Gear optimization and management

### Social Activities
- **Trading**: Player-to-player transactions
- **Guilding**: Guild management and participation
- **Chatting**: Social interaction and communication
- **Partying**: Group formation and coordination
- **RolePlaying**: Immersive character interaction

### Combat and PvP
- **Combat**: PvE combat encounters
- **Battlegrounding**: Structured PvP battlegrounds
- **Dungeoning**: Instance group content
- **Raiding**: Large-scale PvE encounters
- **WorldPvPing**: Open-world player combat

### Economy
- **Auction**: Auction house trading
- **Banking**: Inventory and storage management
- **Vending**: NPC vendor interactions

### Movement and Exploration
- **Exploring**: World discovery and exploration
- **Traveling**: Point-to-point movement
- **Escaping**: Danger avoidance and retreat

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.SemanticKernel.Core | 1.72.0 | AI orchestration framework |
| Microsoft.Extensions.Logging.Abstractions | 10.0.3 | Logging interface |
| Stateless | 5.17.0 | State machine library |

## Project References

- **GameData.Core**: Game object interfaces for AI context

## Usage

### Initialize the State Machine

```csharp
using BloogBot.AI.StateMachine;
using BloogBot.AI.States;

var stateMachine = new BotActivityStateMachine(
    loggerFactory,
    objectManager,
    BotActivity.Resting  // Initial state
);
```

### Create AI Coordinator

```csharp
using BloogBot.AI.Semantic;
using Microsoft.SemanticKernel;

var catalog = new PluginCatalog();
var coordinator = new KernelCoordinator(kernel, catalog);
```

### Handle State Changes

```csharp
// Automatically load appropriate AI plugins when activity changes
stateMachine.StateObservable.ActivityChanged.Subscribe(change =>
{
    coordinator.OnActivityChanged(change.Activity);
});
```

### Create Custom Plugins

```csharp
using BloogBot.AI.Annotations;
using BloogBot.AI.States;

[ActivityPlugin(BotActivity.Questing)]
[ActivityPlugin(BotActivity.Grinding)]
public class QuestingPlugin
{
    [KernelFunction]
    public async Task<string> FindOptimalQuest()
    {
        // AI-driven quest selection logic
        return "Recommended quest: [Quest Name]";
    }
    
    [KernelFunction]
    public async Task<bool> ShouldAcceptQuest(string questName)
    {
        // AI decision making for quest acceptance
        return true;
    }
}
```

### LLM Advisory System

LLM decisions are advisory only - deterministic logic has final authority:

```csharp
// LLM suggests an activity
var advisory = LlmAdvisoryResult.Create(
    BotActivity.Questing,
    MinorStateDefinitions.Questing.Navigating,
    "Player should continue questing",
    confidence: 0.8);

// Deterministic validation may override
var resolution = advisoryValidator.Validate(advisory, currentState, objectManager);

if (resolution.WasOverridden)
{
    Console.WriteLine($"LLM overridden by {resolution.OverrideRule}: {resolution.OverrideReason}");
}
```

Override rules include:
- **Combat Safety**: Enter combat if aggressors present
- **Health Safety**: Rest if health < 40%
- **Forbidden Transitions**: Block invalid state combinations
- **UI Frame Priority**: Active UI frames take precedence

### Persistent Memory

Character memory with lazy loading and batch persistence to PostgreSQL:

```csharp
// Lazy load (only loads when first accessed)
var memory = await memoryService.GetOrLoadAsync(characterId, "MyCharacter", "MyRealm");

// Add facts and memories
memoryService.AddFact(characterId, "preferred_weapon", "sword");
memoryService.AddMemoryEntry(characterId, MemoryEntry.CreatePermanent(
    "Defeated rare mob in Westfall",
    MemoryCategory.Combat,
    importance: 0.9));

// Batch persisted automatically every minute
```

## Configuration

### Decision Invocation Control

Configure decision timing with precedence: CLI > Environment > appsettings > Defaults

```bash
# CLI
--decision-interval=10 --reset-timer-on-adhoc

# Environment
export WWOW_DECISION_INTERVAL_SECONDS=10
```

```json
// appsettings.json
{
  "DecisionInvocation": {
    "DefaultIntervalSeconds": 5,
    "ResetTimerOnAdHocInvocation": true,
    "EnableAutomaticInvocation": true
  }
}
```

## State Machine Behavior

### Global Triggers

All activities respond to universal triggers:
- **Combat Events**: `CombatStarted`, `CombatEnded`
- **Social Events**: `PartyInvite`, `GuildInvite`, `TradeRequested`
- **Character Events**: `LowHealth`, `TalentPointsAvailable`
- **Communication**: `ChatMessageReceived`, `HelpRequested`

### Dynamic State Transitions

```csharp
// Example: Automatic combat response
stateMachine.Fire(Trigger.CombatStarted);  // -> BotActivity.Combat
stateMachine.Fire(Trigger.CombatEnded);    // -> Intelligent next activity
```

### Minor States

Each `BotActivity` has associated minor states for granular tracking:

| Activity | Minor States |
|----------|-------------|
| Combat | Approaching, Engaging, Casting, Looting, Fleeing, Recovering |
| Questing | Accepting, Navigating, Completing, TurningIn, Reading |
| Grinding | Searching, Pulling, Fighting, Resting |
| Trading | Initiating, Negotiating, Confirming, Completing |

## Integration

### Background Service Integration

```csharp
public class AIBotService : BackgroundService
{
    private readonly BotActivityStateMachine _stateMachine;
    private readonly KernelCoordinator _coordinator;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // AI-driven decision making loop
            await ProcessGameState();
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### WWoW Ecosystem Integration

- **StateManager**: Coordinates AI decisions across multiple bot instances
- **BotRunner**: Executes AI-determined actions through behavior trees
- **GameData.Core**: Provides game state context for AI decision making
- **WoWSharpClient**: Interfaces with game client for state information

## Related Documentation

- See `Services/StateManager/README.md` for multi-bot coordination
- See `Exports/BotRunner/README.md` for behavior tree execution
- See `Services/PromptHandlingService/README.md` for AI prompt processing
- See `Exports/GameData.Core/README.md` for core game data structures
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
