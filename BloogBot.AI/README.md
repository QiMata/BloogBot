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

## ?? State Machine Behavior

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
??? States/                    # Activity and trigger definitions
?   ??? BotActivity.cs        # Comprehensive activity enumeration
?   ??? Trigger.cs            # State transition triggers
??? StateMachine/             # State management
?   ??? BotActivityStateMachine.cs  # Core state orchestration
??? Semantic/                 # AI integration
?   ??? KernelCoordinator.cs  # AI plugin coordination
?   ??? PluginCatalog.cs      # Plugin discovery and management
?   ??? DictionaryExtensions.cs  # Utility extensions
??? Annotations/              # Metadata attributes
    ??? ActivityPluginAttribute.cs  # Plugin registration
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