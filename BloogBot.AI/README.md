# BloogBot.AI

AI and machine learning integration library for the WWoW bot system. Provides semantic kernel integration, state machine coordination, and plugin-based activity management.

## Overview

BloogBot.AI provides:
- **Semantic Kernel Integration**: Microsoft Semantic Kernel for AI orchestration
- **Activity State Machine**: Stateless-based activity transitions
- **Plugin System**: Extensible activity plugins with annotations
- **Kernel Coordination**: Centralized AI model management

## Project Structure

```
BloogBot.AI/
??? Semantic/
?   ??? KernelCoordinator.cs      # Semantic Kernel setup and management
?   ??? PluginCatalog.cs          # Plugin discovery and registration
?   ??? DictionaryExtensions.cs   # Utility extensions
??? StateMachine/
?   ??? BotActivityStateMachine.cs # Activity state transitions
??? States/
?   ??? BotActivity.cs            # Activity enumeration
?   ??? Trigger.cs                # State transition triggers
??? Annotations/
    ??? ActivityPluginAttribute.cs # Plugin metadata attribute
```

## Semantic Kernel Integration

### Kernel Setup

```csharp
using BloogBot.AI.Semantic;
using Microsoft.SemanticKernel;

var coordinator = new KernelCoordinator();
coordinator.ConfigureKernel(kernel =>
{
    kernel.AddAzureOpenAIChatCompletion(
        deploymentName: "gpt-4",
        endpoint: "https://your-resource.openai.azure.com/",
        apiKey: "your-key"
    );
});
```

### Plugin Registration

```csharp
// Register activity plugins
coordinator.RegisterPlugins(new PluginCatalog());
```

## State Machine

### Bot Activities

```csharp
public enum BotActivity
{
    Idle,
    Traveling,
    Grinding,
    Questing,
    Combat,
    Looting,
    Resting,
    Vendoring,
    Training
}
```

### Triggers

```csharp
public enum Trigger
{
    Start,
    Stop,
    TargetFound,
    TargetDead,
    HealthLow,
    HealthFull,
    BagsFull,
    ArrivedAtDestination,
    QuestAccepted,
    QuestComplete
}
```

### State Machine Usage

```csharp
var stateMachine = new BotActivityStateMachine(BotActivity.Idle);

// Configure transitions
stateMachine.Configure(BotActivity.Idle)
    .Permit(Trigger.Start, BotActivity.Grinding)
    .Permit(Trigger.QuestAccepted, BotActivity.Questing);

stateMachine.Configure(BotActivity.Grinding)
    .Permit(Trigger.TargetFound, BotActivity.Combat)
    .Permit(Trigger.BagsFull, BotActivity.Vendoring)
    .Permit(Trigger.Stop, BotActivity.Idle);

stateMachine.Configure(BotActivity.Combat)
    .Permit(Trigger.TargetDead, BotActivity.Looting)
    .Permit(Trigger.HealthLow, BotActivity.Resting);

// Fire triggers
stateMachine.Fire(Trigger.Start);    // Idle -> Grinding
stateMachine.Fire(Trigger.TargetFound); // Grinding -> Combat
```

## Plugin System

### Activity Plugin Attribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ActivityPluginAttribute : Attribute
{
    public BotActivity Activity { get; }
    public string Description { get; }
    
    public ActivityPluginAttribute(BotActivity activity, string description)
    {
        Activity = activity;
        Description = description;
    }
}
```

### Creating a Plugin

```csharp
[ActivityPlugin(BotActivity.Grinding, "Handles grinding/farming activities")]
public class GrindingPlugin
{
    [KernelFunction("find_targets")]
    public async Task<string> FindTargets(
        [Description("The type of creature to target")] string creatureType,
        [Description("Maximum distance to search")] int range = 40)
    {
        // AI-assisted target selection
        return "Found 3 targets matching criteria";
    }
    
    [KernelFunction("set_grinding_path")]
    public async Task<string> SetGrindingPath(
        [Description("Waypoints as comma-separated coordinates")] string waypoints)
    {
        // Configure grinding route
        return "Grinding path configured with 5 waypoints";
    }
}
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.SemanticKernel.Core | 1.54.0 | AI orchestration framework |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Logging interface |
| Stateless | 5.17.0 | State machine library |

## Project References

- **GameData.Core**: Game object interfaces for AI context

## Use Cases

### Natural Language Commands

```csharp
// User says: "Go grind murlocs near Goldshire until you have 50 murloc fins"

var result = await kernel.InvokeAsync("GrindingPlugin", "configure_grind", new()
{
    ["creature_type"] = "Murloc",
    ["location"] = "Goldshire",
    ["stop_condition"] = "item_count:Murloc Fin:50"
});
```

### Dynamic Decision Making

```csharp
// AI decides next action based on game state
var prompt = $"""
    Current state: {stateMachine.State}
    Player health: {player.HealthPercent}%
    Bag space: {player.FreeBagSlots}
    Nearby enemies: {nearbyEnemies.Count}
    
    What should the bot do next?
    """;

var decision = await kernel.InvokePromptAsync(prompt);
var trigger = ParseTrigger(decision);
stateMachine.Fire(trigger);
```

## Related Documentation

- See `Services/PromptHandlingService/README.md` for prompt processing
- See `Services/DecisionEngineService/README.md` for decision coordination
- See `ARCHITECTURE.md` for system overview
