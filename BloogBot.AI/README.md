# BloogBot.AI

**AI-Driven Decision Making Engine for World of Warcraft Automation**

BloogBot.AI is the intelligent decision-making core of the BloogBot ecosystem, providing sophisticated AI-driven behavior coordination using Microsoft Semantic Kernel and state machine patterns. It orchestrates complex bot activities through dynamic plugin management and context-aware state transitions.

## ?? Features

### ?? AI-Powered Decision Making
- **Microsoft Semantic Kernel Integration**: Leverages advanced AI capabilities for intelligent bot behavior
- **Dynamic Plugin Management**: Context-aware plugin loading based on current bot activities
- **Activity-Based AI Coordination**: Different AI plugins activate based on the bot's current state

### ?? Advanced State Management
- **Comprehensive Activity States**: 25+ distinct bot activities from questing to PvP
- **Intelligent State Transitions**: Dynamic state changes based on game events and conditions
- **Global Trigger System**: Universal event handling across all bot activities

### ?? Plugin Architecture
- **Activity-Specific Plugins**: Modular AI components for different bot behaviors
- **Attribute-Based Registration**: Automatic plugin discovery and categorization
- **Kernel Coordination**: Seamless integration between state changes and AI capabilities

## ??? Architecture

### Core Components

| Component | Purpose | Key Features |
|-----------|---------|--------------|
| **BotActivityStateMachine** | State orchestration and transition logic | 25+ activity states, global triggers, dynamic transitions |
| **KernelCoordinator** | AI plugin management and kernel coordination | Plugin loading/unloading, activity-based AI switching |
| **PluginCatalog** | Plugin discovery and categorization system | Attribute-based registration, activity mapping |
| **ActivityPluginAttribute** | Plugin metadata and activity association | Multiple activity support, declarative configuration |

### Activity Categories

#### ?? Character Development
- **Questing**: Quest completion and progression
- **Grinding**: Experience and reputation farming
- **Professions**: Crafting and gathering activities
- **Talenting**: Talent point allocation and builds
- **Equipping**: Gear optimization and management

#### ?? Social Activities
- **Trading**: Player-to-player transactions
- **Guilding**: Guild management and participation
- **Chatting**: Social interaction and communication
- **Helping**: Assisting other players
- **Mailing**: In-game mail management
- **Partying**: Group formation and coordination
- **RolePlaying**: Immersive character interaction

#### ?? Combat & PvP
- **Combat**: PvE combat encounters
- **Battlegrounding**: Structured PvP battlegrounds
- **Dungeoning**: Instance group content
- **Raiding**: Large-scale PvE encounters
- **WorldPvPing**: Open-world player combat
- **Camping**: Strategic positioning and waiting

#### ?? Economy
- **Auction**: Auction house trading
- **Banking**: Inventory and storage management
- **Vending**: NPC vendor interactions

#### ??? Movement & Exploration
- **Exploring**: World discovery and exploration
- **Traveling**: Point-to-point movement
- **Escaping**: Danger avoidance and retreat

#### ?? Events & Passive
- **Eventing**: Special event participation
- **Resting**: Recovery and idle states

## ?? Getting Started

### Prerequisites

- **.NET 8.0**: Modern runtime with enhanced performance
- **Microsoft.SemanticKernel.Core**: AI orchestration framework
- **Stateless**: State machine implementation
- **GameData.Core**: BloogBot core data structures

### Installation

1. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd BloogBot.AI
   dotnet build
   ```

2. **Add Package Reference**:
   ```xml
   <PackageReference Include="BloogBot.AI" Version="1.0.0" />
   ```

### Basic Usage

#### 1. Initialize the State Machine
```csharp
using BloogBot.AI.StateMachine;
using BloogBot.AI.States;

var stateMachine = new BotActivityStateMachine(
    loggerFactory,
    objectManager,
    BotActivity.Resting  // Initial state
);
```

#### 2. Create AI Coordinator
```csharp
using BloogBot.AI.Semantic;

var catalog = new PluginCatalog();
var coordinator = new KernelCoordinator(kernel, catalog);
```

#### 3. Handle State Changes
```csharp
// Automatically load appropriate AI plugins when activity changes
stateMachine.OnStateChanged += (state) => {
    coordinator.OnActivityChanged(state);
};
```

## ?? Creating Custom Plugins

### 1. Define Activity Plugin
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

### 2. Multi-Activity Support
```csharp
[ActivityPlugin(BotActivity.Combat)]
[ActivityPlugin(BotActivity.Dungeoning)]
[ActivityPlugin(BotActivity.Raiding)]
public class CombatPlugin
{
    [KernelFunction]
    public async Task<string> SelectOptimalTarget()
    {
        // AI-powered target selection
        return "target_guid";
    }
}
```

## Desired-State Architecture

BloogBot.AI implements a sophisticated desired-state strategy with the following core components:

### Global State Observable

The `IBotStateObservable` provides a single source of truth for all state information:

```csharp
// Subscribe to state changes
stateObservable.StateChanges.Subscribe(state =>
{
    Console.WriteLine($"Activity: {state.Activity}, Minor: {state.MinorState.Name}");
    Console.WriteLine($"Source: {state.Source}, Reason: {state.Reason}");
});

// Filter to specific activities
stateObservable.WhenActivity(BotActivity.Combat).Subscribe(OnCombatState);

// React only to activity changes
stateObservable.ActivityChanged.Subscribe(OnActivityChanged);
```

Each `StateChangeEvent` includes:
- **Activity**: Current major state (e.g., Combat, Questing)
- **MinorState**: Granular state within activity (e.g., Combat.Engaging)
- **Source**: What triggered the change (Deterministic, LlmAdvisory, Trigger, etc.)
- **Reason**: Human-readable explanation
- **Timestamp**: When the change occurred

### Minor States

Each `BotActivity` has associated minor states for granular tracking:

| Activity | Minor States |
|----------|-------------|
| Combat | Approaching, Engaging, Casting, Looting, Fleeing, Recovering |
| Questing | Accepting, Navigating, Completing, TurningIn, Reading |
| Grinding | Searching, Pulling, Fighting, Resting |
| Trading | Initiating, Negotiating, Confirming, Completing |

Access minor states via `MinorStateDefinitions`:
```csharp
var combatStates = MinorStateDefinitions.ForActivity(BotActivity.Combat);
stateMachine.SetMinorState(MinorStateDefinitions.Combat.Engaging, "Engaging target");
```

### LLM Advisory System

LLM decisions are **advisory only** - deterministic logic has final authority:

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

### Forbidden Transitions

Configure transition rules with templates:

```csharp
// Block specific transition
registry.RegisterRule(ForbiddenTransitionRule.Block(
    "CombatToChat",
    BotActivity.Combat,
    BotActivity.Chatting,
    "Cannot chat during combat"));

// Conditional blocking
registry.RegisterRule(ForbiddenTransitionRule.BlockWhen(
    "GhostFormRestriction",
    ForbiddenTransitionRule.Any,  // wildcard
    BotActivity.Combat,
    ctx => ctx.ObjectManager.Player?.InGhostForm == true,
    "Cannot combat in ghost form"));
```

### Decision Invocation Control

Configure decision timing with precedence: **CLI > Environment > appsettings > Defaults**

```bash
# CLI
--decision-interval=10 --reset-timer-on-adhoc

# Environment
export WWOW_DECISION_INTERVAL_SECONDS=10

# appsettings.json
{
  "DecisionInvocation": {
    "DefaultIntervalSeconds": 5,
    "ResetTimerOnAdHocInvocation": true,
    "EnableAutomaticInvocation": true
  }
}
```

### Distilled Summary Pipeline

Multi-pass LLM summarization for context:

```csharp
var summary = await summaryPipeline.DistillAsync(context);

// Pass 1: Extract key facts
// Pass 2: Compress and prioritize
// Pass 3: Generate summaries

Console.WriteLine(summary.CompactSummary);   // Max 200 chars
Console.WriteLine(summary.DetailedSummary);  // Max 2000 chars
foreach (var insight in summary.KeyInsights)
    Console.WriteLine($"- {insight}");
```

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
stateMachine.Fire(Trigger.CombatStarted);  // ? BotActivity.Combat
stateMachine.Fire(Trigger.CombatEnded);    // ? Intelligent next activity
```

### Intelligent Decision Making
The state machine uses AI-driven logic to determine optimal next activities:
```csharp
BotActivity DecideNextActiveState()
{
    // AI evaluates:
    // - Current character state (health, mana, level)
    // - Available quests and objectives
    // - Inventory and equipment status
    // - Social context (party, guild events)
    // - Economic opportunities
    
    return optimalActivity;
}
```

## ?? Integration

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

### BloogBot Ecosystem Integration
- **StateManager**: Coordinates AI decisions across multiple bot instances
- **BotRunner**: Executes AI-determined actions through behavior trees
- **GameData.Core**: Provides game state context for AI decision making
- **WoWSharpClient**: Interfaces with game client for state information

## ?? Testing

### Unit Tests
```bash
dotnet test ../Tests/BloogBot.AI.Tests/
```

### Integration Testing
```csharp
[Test]
public async Task StateMachine_CombatTrigger_TransitionsToCombat()
{
    var stateMachine = CreateTestStateMachine();
    
    stateMachine.Fire(Trigger.CombatStarted);
    
    Assert.AreEqual(BotActivity.Combat, stateMachine.Current);
}
```

## ?? Performance

### Memory Efficiency
- **Plugin Lazy Loading**: Plugins loaded only when activities are active
- **Efficient State Storage**: Minimal memory footprint for state tracking
- **Garbage Collection Friendly**: Proper resource disposal patterns

### Processing Speed
- **Fast State Transitions**: Sub-millisecond state changes
- **Optimized Plugin Switching**: Minimal overhead for AI context switching
- **Cached Plugin Discovery**: One-time reflection-based plugin enumeration

## ?? Security & Safety

### AI Safety Measures
- **Bounded Decision Making**: AI decisions constrained to valid game actions
- **State Validation**: Ensures state transitions are logically consistent
- **Timeout Protection**: Prevents infinite loops in decision making

### Game Compliance
- **Non-Intrusive AI**: AI operates within game's intended mechanics
- **Realistic Behavior Patterns**: Human-like decision timing and patterns
- **Adaptive Learning**: AI improves behavior based on success metrics

## ??? Development

### Project Structure
```
BloogBot.AI/
+-- States/                    # Activity and state definitions
|   +-- BotActivity.cs        # Major activity enumeration (24 states)
|   +-- MinorState.cs         # Minor state record type
|   +-- MinorStateDefinitions.cs  # All minor states per activity
+-- StateMachine/             # State management
|   +-- BotActivityStateMachine.cs  # Core state orchestration
|   +-- Trigger.cs            # State transition triggers
+-- Observable/               # Reactive state streams
|   +-- IBotStateObservable.cs    # Observable contract
|   +-- BotStateObservable.cs     # Observable implementation
|   +-- StateChangeEvent.cs       # State change record
|   +-- StateChangeSource.cs      # Change source enum
+-- Transitions/              # Transition validation
|   +-- IForbiddenTransitionRegistry.cs  # Registry contract
|   +-- ForbiddenTransitionRegistry.cs   # Registry implementation
|   +-- ForbiddenTransitionRule.cs       # Rule definition
|   +-- TransitionContext.cs             # Context for predicates
+-- Advisory/                 # LLM advisory system
|   +-- IAdvisoryValidator.cs     # Validator contract
|   +-- AdvisoryValidator.cs      # Deterministic override logic
|   +-- LlmAdvisoryResult.cs      # LLM output type
|   +-- AdvisoryResolution.cs     # Validation result
|   +-- IAdvisoryOverrideLog.cs   # Audit logging
+-- Configuration/            # Settings and resolution
|   +-- DecisionInvocationSettings.cs        # Settings POCO
|   +-- DecisionInvocationSettingsResolver.cs # Precedence resolver
+-- Invocation/               # Decision timing
|   +-- IDecisionInvoker.cs   # Invoker contract
|   +-- DecisionInvoker.cs    # Timer-based implementation
+-- Summary/                  # Summarization pipeline
|   +-- ISummaryPipeline.cs   # Pipeline contract
|   +-- SummaryPipeline.cs    # Multi-pass implementation
|   +-- SummaryContext.cs     # Input context types
|   +-- DistilledSummary.cs   # Output summary type
+-- Memory/                   # Character persistence
|   +-- CharacterMemory.cs    # Memory model
|   +-- ICharacterMemoryRepository.cs     # Repository contract
|   +-- PostgresCharacterMemoryRepository.cs  # PostgreSQL impl
|   +-- CharacterMemoryService.cs         # Lazy load + batch save
+-- Semantic/                 # AI integration
|   +-- KernelCoordinator.cs  # AI plugin coordination
|   +-- PluginCatalog.cs      # Plugin discovery
+-- Annotations/              # Metadata attributes
    +-- ActivityPluginAttribute.cs  # Plugin registration
```

### Code Style
- **Modern C# Patterns**: Records, nullable reference types, pattern matching
- **Dependency Injection**: Constructor injection for all services
- **Async/Await**: Non-blocking operations throughout
- **SOLID Principles**: Clean architecture with clear separation of concerns

## ?? Contributing

1. **Fork the Repository**: Create your feature branch from `main`
2. **Follow Conventions**: Use existing code style and patterns
3. **Add Tests**: Include unit tests for new functionality
4. **Update Documentation**: Document new activities or plugins
5. **Performance Testing**: Ensure changes don't degrade performance

### Adding New Activities
1. Add activity to `BotActivity` enum
2. Add relevant triggers to `Trigger` enum
3. Configure state transitions in `BotActivityStateMachine`
4. Create activity-specific plugins with `[ActivityPlugin]` attribute

## ?? License

This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## ?? Related Projects

- **[StateManager](../Services/StateManager/README.md)**: Multi-bot coordination service
- **[BotRunner](../Exports/BotRunner/README.md)**: Behavior tree execution engine  
- **[PromptHandlingService](../Services/PromptHandlingService/README.md)**: AI prompt processing
- **[GameData.Core](../Exports/GameData.Core/README.md)**: Core game data structures
- **[WoWSharpClient](../Exports/WoWSharpClient/README.md)**: Game client interface

---

*BloogBot.AI provides the cognitive foundation for intelligent World of Warcraft automation, combining state-of-the-art AI with robust state management for sophisticated bot behavior coordination.*