# WoWSharpClient Network Agents

This document describes the network agent architecture for WoWSharpClient, which provides specialized agents for different game operations in World of Warcraft.

## Overview

The network agents follow a consistent naming pattern of `{}NetworkAgent` and provide focused functionality for specific game operations:

- **TargetingNetworkAgent** - Handles target selection and assist functionality
- **AttackNetworkAgent** - Manages auto-attack operations
- **QuestNetworkAgent** - Handles quest interactions and management
- **LootingNetworkAgent** - Manages looting operations and loot windows
- **GameObjectNetworkAgent** - Handles interactions with game objects (chests, nodes, doors, etc.)
- **VendorNetworkAgent** - Manages vendor interactions, buying, selling, and item repairs
- **FlightMasterNetworkAgent** - Handles taxi system and flight path operations
- **DeadActorAgent** - Manages death, resurrection, and corpse recovery operations
- **InventoryNetworkAgent** - Manages inventory operations, item movement, and bag management
- **ItemUseNetworkAgent** - Handles item usage, consumables, and item interactions
- **EquipmentNetworkAgent** - Manages equipment operations, equipping, and durability tracking
- **SpellCastingNetworkAgent** - Handles spell casting, channeling, and spell state management

## Architecture

### Core Principles

1. **Single Responsibility** - Each agent focuses on one specific aspect of game functionality
2. **Event-Driven** - Agents use events to communicate state changes and completion of operations
3. **Async Operations** - All network operations are asynchronous with proper cancellation support
4. **Testable** - Full dependency injection and interface-based design for easy testing
5. **Coordinated** - Agents can work together through shared interfaces
6. **Lazy Loading** - Agents are created only when needed through the NetworkAgentFactory

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
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.TargetingAgent.SetTargetAsync(enemyGuid);
await agentFactory.TargetingAgent.AssistAsync(playerGuid);
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
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.AttackAgent.AttackTargetAsync(enemyGuid, agentFactory.TargetingAgent);
await agentFactory.AttackAgent.ToggleAttackAsync();
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
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.QuestAgent.HelloQuestGiverAsync(npcGuid);
await agentFactory.QuestAgent.AcceptQuestAsync(npcGuid, questId);
await agentFactory.QuestAgent.CompleteQuestAsync(npcGuid, questId);
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
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.LootingAgent.QuickLootAsync(corpseGuid);
await agentFactory.LootingAgent.RollForLootAsync(lootGuid, itemSlot, LootRollType.Need);
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
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.GameObjectAgent.OpenChestAsync(chestGuid);
await agentFactory.GameObjectAgent.GatherFromNodeAsync(herbNodeGuid);
await agentFactory.GameObjectAgent.SmartInteractAsync(gameObjectGuid, GameObjectType.Chest);
```

### VendorNetworkAgent

Handles all vendor-related operations including buying, selling, and item repairs.

**Key Features:**
- Open/close vendor windows
- Purchase items from vendors
- Sell items to vendors
- Repair all equipped items
- Repair specific items
- Vendor window state tracking
- Purchase and sale transaction events

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.VendorAgent.OpenVendorAsync(vendorGuid);
await agentFactory.VendorAgent.BuyItemAsync(itemId, quantity);
await agentFactory.VendorAgent.SellItemAsync(itemGuid);
await agentFactory.VendorAgent.RepairAllItemsAsync();
```

### FlightMasterNetworkAgent

Manages all flight master and taxi system operations.

**Key Features:**
- Open/close taxi maps
- Query available flight paths
- Take flights to specific destinations
- Flight path discovery
- Flight cost calculation
- Flight activation events

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.FlightMasterAgent.OpenTaxiMapAsync(flightMasterGuid);
await agentFactory.FlightMasterAgent.TakeFlightAsync(destinationNodeId);
```

### DeadActorAgent

Handles death-related operations and resurrection mechanics.

**Key Features:**
- Death detection and events
- Spirit release operations
- Corpse location tracking
- Resurrection acceptance/decline
- Ghost form state management
- Corpse recovery assistance

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.DeadActorAgent.ReleaseSpiritAsync();
await agentFactory.DeadActorAgent.AcceptResurrectionAsync();
await agentFactory.DeadActorAgent.RetrieveCorpseAsync();
```

### InventoryNetworkAgent

Manages all inventory-related operations including bag management and item movement.

**Key Features:**
- Move items between bags and slots
- Split item stacks
- Swap items in inventory
- Destroy items
- Sort bags automatically
- Open/close bag windows
- Find empty slots
- Count items by ID
- Check inventory space

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.InventoryAgent.MoveItemAsync(0, 5, 1, 10);
await agentFactory.InventoryAgent.SplitItemStackAsync(0, 5, 1, 10, 5);
await agentFactory.InventoryAgent.SortBagAsync(0);
var freeSlots = agentFactory.InventoryAgent.GetFreeSlotCount();
```

### ItemUseNetworkAgent

Handles all item usage operations including consumables, tools, and containers.

**Key Features:**
- Use items from inventory
- Use items on targets
- Use items at specific locations
- Activate equipped items
- Use consumables (food, potions, etc.)
- Open container items
- Use tool items (fishing poles, mining picks)
- Cancel item usage
- Check item cooldowns
- Find and use items by ID

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.ItemUseAgent.UseItemAsync(0, 5);
await agentFactory.ItemUseAgent.UseItemOnTargetAsync(0, 5, targetGuid);
await agentFactory.ItemUseAgent.UseConsumableAsync(0, 10);
await agentFactory.ItemUseAgent.OpenContainerAsync(0, 15);
var canUse = agentFactory.ItemUseAgent.CanUseItem(itemId);
```

### EquipmentNetworkAgent

Manages all equipment-related operations including equipping, unequipping, and durability tracking.

**Key Features:**
- Equip items to specific slots
- Auto-equip items to appropriate slots
- Unequip items from equipment slots
- Swap equipment between slots
- Auto-equip all available items
- Unequip all equipment
- Check equipment slot status
- Get equipped item information
- Track item durability
- Find damaged equipment

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.EquipmentAgent.EquipItemAsync(0, 5, EquipmentSlot.Head);
await agentFactory.EquipmentAgent.AutoEquipItemAsync(0, 10);
await agentFactory.EquipmentAgent.UnequipItemAsync(EquipmentSlot.MainHand);
await agentFactory.EquipmentAgent.SwapEquipmentAsync(EquipmentSlot.Finger1, EquipmentSlot.Finger2);
var isEquipped = agentFactory.EquipmentAgent.IsSlotEquipped(EquipmentSlot.Chest);
```

### SpellCastingNetworkAgent

Handles all spell casting operations including regular spells, channeled spells, and auto-repeat spells.

**Key Features:**
- Cast spells without targets
- Cast spells on specific targets
- Cast spells at locations
- Interrupt spell casting
- Stop channeled spells
- Auto-repeat spell casting
- Cast spells from action bar
- Smart spell casting with auto-targeting
- Check spell availability
- Track spell cooldowns
- Get spell information (cost, cast time, range)

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.SpellCastingAgent.CastSpellAsync(spellId);
await agentFactory.SpellCastingAgent.CastSpellOnTargetAsync(spellId, targetGuid);
await agentFactory.SpellCastingAgent.CastSpellAtLocationAsync(spellId, x, y, z);
await agentFactory.SpellCastingAgent.InterruptCastAsync();
await agentFactory.SpellCastingAgent.StartAutoRepeatSpellAsync(spellId, targetGuid);
var canCast = agentFactory.SpellCastingAgent.CanCastSpell(spellId);
```

## Factory Methods

### Creating the NetworkAgentFactory (Recommended)

The `NetworkAgentFactory` provides lazy-loaded access to all agents and is the recommended approach:

```csharp
// Primary recommended approach
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);

// Access agents as needed (created lazily)
var targetingAgent = agentFactory.TargetingAgent;
var attackAgent = agentFactory.AttackAgent;
var questAgent = agentFactory.QuestAgent;
var lootingAgent = agentFactory.LootingAgent;
var gameObjectAgent = agentFactory.GameObjectAgent;
var vendorAgent = agentFactory.VendorAgent;
var flightMasterAgent = agentFactory.FlightMasterAgent;
var deadActorAgent = agentFactory.DeadActorAgent;
var inventoryAgent = agentFactory.InventoryAgent;
var itemUseAgent = agentFactory.ItemUseAgent;
var equipmentAgent = agentFactory.EquipmentAgent;
var spellCastingAgent = agentFactory.SpellCastingAgent;
// ... etc
```

### Creating Individual Agents (Legacy Support)

```csharp
// Individual agents (not recommended for new code)
var targetingAgent = WoWClientFactory.CreateTargetingNetworkAgent(worldClient, loggerFactory);
var attackAgent = WoWClientFactory.CreateAttackNetworkAgent(worldClient, loggerFactory);
var questAgent = WoWClientFactory.CreateQuestNetworkAgent(worldClient, loggerFactory);
var lootingAgent = WoWClientFactory.CreateLootingNetworkAgent(worldClient, loggerFactory);
var gameObjectAgent = WoWClientFactory.CreateGameObjectNetworkAgent(worldClient, loggerFactory);
var vendorAgent = WoWClientFactory.CreateVendorNetworkAgent(worldClient, loggerFactory);
var flightMasterAgent = WoWClientFactory.CreateFlightMasterNetworkAgent(worldClient, loggerFactory);
var deadActorAgent = WoWClientFactory.CreateDeadActorAgent(worldClient, loggerFactory);
var inventoryAgent = WoWClientFactory.CreateInventoryNetworkAgent(worldClient, loggerFactory);
var itemUseAgent = WoWClientFactory.CreateItemUseNetworkAgent(worldClient, loggerFactory);
var equipmentAgent = WoWClientFactory.CreateEquipmentNetworkAgent(worldClient, loggerFactory);
var spellCastingAgent = WoWClientFactory.CreateSpellCastingNetworkAgent(worldClient, loggerFactory);
```

### Creating Agent Sets

```csharp
// All agents at once (creates all immediately)
var allAgents = WoWClientFactory.CreateAllNetworkAgents(worldClient, loggerFactory);
var targetingAgent = allAgents.TargetingAgent;
var attackAgent = allAgents.AttackAgent;
var inventoryAgent = allAgents.InventoryAgent;
var itemUseAgent = allAgents.ItemUseAgent;
var equipmentAgent = allAgents.EquipmentAgent;
var spellCastingAgent = allAgents.SpellCastingAgent;
// ... etc

// Combat agents only
var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatNetworkAgents(worldClient, loggerFactory);
```

## Integration Examples

### Basic Combat Integration

```csharp
public class CombatManager
{
    private readonly IAgentFactory _agentFactory;

    public CombatManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task EngageEnemyAsync(ulong enemyGuid)
    {
        await _agentFactory.AttackAgent.AttackTargetAsync(enemyGuid, _agentFactory.TargetingAgent);
    }

    public async Task StopCombatAsync()
    {
        await _agentFactory.AttackAgent.StopAttackAsync();
        await _agentFactory.TargetingAgent.ClearTargetAsync();
    }
}
```

### Quest Management

```csharp
public class QuestManager
{
    private readonly IAgentFactory _agentFactory;

    public QuestManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task AcceptQuestChainAsync(ulong npcGuid, uint[] questIds)
    {
        foreach (var questId in questIds)
        {
            await _agentFactory.QuestAgent.HelloQuestGiverAsync(npcGuid);
            await _agentFactory.QuestAgent.QueryQuestAsync(npcGuid, questId);
            await _agentFactory.QuestAgent.AcceptQuestAsync(npcGuid, questId);
        }
    }
}
```

### Resource Gathering and Looting

```csharp
public class GatheringManager
{
    private readonly IAgentFactory _agentFactory;

    public GatheringManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task GatherAndLootAsync(ulong nodeGuid)
    {
        await _agentFactory.GameObjectAgent.GatherFromNodeAsync(nodeGuid);
        
        // If gathering creates loot, handle it
        if (_agentFactory.LootingAgent.IsLootWindowOpen)
        {
            await _agentFactory.LootingAgent.LootAllAsync();
        }
    }
}
```

### Vendor Operations

```csharp
public class VendorManager
{
    private readonly IAgentFactory _agentFactory;

    public VendorManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task BuySuppliesAsync(ulong vendorGuid, (uint ItemId, uint Quantity)[] items)
    {
        await _agentFactory.VendorAgent.OpenVendorAsync(vendorGuid);
        
        foreach (var (itemId, quantity) in items)
        {
            await _agentFactory.VendorAgent.BuyItemAsync(itemId, quantity);
        }
        
        await _agentFactory.VendorAgent.CloseVendorAsync();
    }

    public async Task RepairAllEquipmentAsync(ulong vendorGuid)
    {
        await _agentFactory.VendorAgent.OpenVendorAsync(vendorGuid);
        await _agentFactory.VendorAgent.RepairAllItemsAsync();
        await _agentFactory.VendorAgent.CloseVendorAsync();
    }
}
```

### Death and Resurrection Handling

```csharp
public class DeathManager
{
    private readonly IAgentFactory _agentFactory;

    public DeathManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
        
        // Subscribe to death events
        _agentFactory.DeadActorAgent.OnDeath += HandlePlayerDeath;
        _agentFactory.DeadActorAgent.OnResurrectionRequest += HandleResurrectionRequest;
    }

    private async void HandlePlayerDeath()
    {
        // Auto-release spirit on death
        await _agentFactory.DeadActorAgent.ReleaseSpiritAsync();
    }

    private async void HandleResurrectionRequest(ulong resurrectorGuid, string resurrectorName)
    {
        // Auto-accept resurrection from players
        await _agentFactory.DeadActorAgent.AcceptResurrectionAsync();
    }
}
```

### Inventory Management

```csharp
public class InventoryManager
{
    private readonly IAgentFactory _agentFactory;

    public InventoryManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task OrganizeInventoryAsync()
    {
        // Sort all bags
        for (byte bagId = 0; bagId < 5; bagId++)
        {
            await _agentFactory.InventoryAgent.SortBagAsync(bagId);
        }
    }

    public async Task MoveItemToEmptySlotAsync(byte sourceBag, byte sourceSlot)
    {
        var emptySlot = _agentFactory.InventoryAgent.FindEmptySlot();
        if (emptySlot.HasValue)
        {
            await _agentFactory.InventoryAgent.MoveItemAsync(sourceBag, sourceSlot, 
                emptySlot.Value.BagId, emptySlot.Value.SlotId);
        }
    }
}
```

### Item Usage and Equipment Management

```csharp
public class ItemManager
{
    private readonly IAgentFactory _agentFactory;

    public ItemManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task UseHealthPotionAsync()
    {
        const uint healthPotionId = 118; // Minor Healing Potion
        var success = await _agentFactory.ItemUseAgent.FindAndUseItemAsync(healthPotionId);
        if (!success)
        {
            Console.WriteLine("No health potion found in inventory");
        }
    }

    public async Task AutoEquipBestGearAsync()
    {
        await _agentFactory.EquipmentAgent.AutoEquipAllAsync();
        
        // Check if any equipment needs repair
        if (_agentFactory.EquipmentAgent.HasDamagedEquipment())
        {
            var damagedSlots = _agentFactory.EquipmentAgent.GetDamagedEquipmentSlots();
            Console.WriteLine($"Equipment needs repair in slots: {string.Join(", ", damagedSlots)}");
        }
    }
}
```

### Spell Casting and Combat

```csharp
public class SpellManager
{
    private readonly IAgentFactory _agentFactory;

    public SpellManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task CastHealSpellAsync(ulong targetGuid)
    {
        const uint healSpellId = 2061; // Flash Heal
        
        if (_agentFactory.SpellCastingAgent.CanCastSpell(healSpellId))
        {
            await _agentFactory.SpellCastingAgent.CastSpellOnTargetAsync(healSpellId, targetGuid);
        }
        else
        {
            var cooldown = _agentFactory.SpellCastingAgent.GetSpellCooldown(healSpellId);
            Console.WriteLine($"Heal spell on cooldown for {cooldown}ms");
        }
    }

    public async Task StartWandAttackAsync(ulong targetGuid)
    {
        const uint wandSpellId = 5019; // Shoot
        await _agentFactory.SpellCastingAgent.StartAutoRepeatSpellAsync(wandSpellId, targetGuid);
    }

    public async Task CastAreaOfEffectSpellAsync(float x, float y, float z)
    {
        const uint aoeSpellId = 1449; // Arcane Explosion
        await _agentFactory.SpellCastingAgent.CastSpellAtLocationAsync(aoeSpellId, x, y, z);
    }
}
```

## Event Handling

All agents provide events for monitoring operations:

```csharp
public class EventMonitor
{
    public EventMonitor(IAgentFactory agentFactory)
    {
        // Targeting events
        agentFactory.TargetingAgent.TargetChanged += (newTarget) => 
        {
            Console.WriteLine($"Target changed to: {newTarget:X}");
        };

        // Attack events
        agentFactory.AttackAgent.AttackStarted += (victimGuid) => 
        {
            Console.WriteLine($"Attack started on: {victimGuid:X}");
        };
        agentFactory.AttackAgent.AttackError += (error) => 
        {
            Console.WriteLine($"Attack error: {error}");
        };

        // Quest events
        agentFactory.QuestAgent.QuestAccepted += (questId) => 
        {
            Console.WriteLine($"Quest {questId} accepted");
        };

        // Loot events
        agentFactory.LootingAgent.ItemLooted += (itemId, quantity) => 
        {
            Console.WriteLine($"Looted {quantity}x item {itemId}");
        };

        // Game object events
        agentFactory.GameObjectAgent.ChestOpened += (chestGuid) => 
        {
            Console.WriteLine($"Chest {chestGuid:X} opened");
        };

        // Vendor events
        agentFactory.VendorAgent.ItemPurchased += (itemId, quantity, cost) => 
        {
            Console.WriteLine($"Purchased {quantity}x item {itemId} for {cost} copper");
        };

        // Flight events
        agentFactory.FlightMasterAgent.FlightActivated += (sourceNode, destNode, cost) => 
        {
            Console.WriteLine($"Flight from {sourceNode} to {destNode} for {cost} copper");
        };

        // Death events
        agentFactory.DeadActorAgent.OnDeath += () => 
        {
            Console.WriteLine("Character died");
        };

        // Inventory events
        agentFactory.InventoryAgent.ItemMoved += (itemGuid, sourceBag, sourceSlot, destBag, destSlot) => 
        {
            Console.WriteLine($"Item {itemGuid:X} moved from {sourceBag}:{sourceSlot} to {destBag}:{destSlot}");
        };
        agentFactory.InventoryAgent.ItemDestroyed += (itemGuid, quantity) => 
        {
            Console.WriteLine($"Destroyed {quantity}x item {itemGuid:X}");
        };

        // Item use events
        agentFactory.ItemUseAgent.ItemUsed += (itemGuid, itemId, targetGuid) => 
        {
            Console.WriteLine($"Used item {itemId} (GUID: {itemGuid:X}) on target {targetGuid:X}");
        };
        agentFactory.ItemUseAgent.ConsumableEffectApplied += (itemId, spellId) => 
        {
            Console.WriteLine($"Consumable {itemId} applied effect {spellId}");
        };

        // Equipment events
        agentFactory.EquipmentAgent.ItemEquipped += (itemGuid, slot) => 
        {
            Console.WriteLine($"Equipped item {itemGuid:X} to slot {slot}");
        };
        agentFactory.EquipmentAgent.DurabilityChanged += (slot, current, max) => 
        {
            Console.WriteLine($"Durability for {slot}: {current}/{max}");
        };

        // Spell casting events
        agentFactory.SpellCastingAgent.SpellCastStarted += (spellId, castTime, targetGuid) => 
        {
            Console.WriteLine($"Started casting spell {spellId} (cast time: {castTime}ms)");
        };
        agentFactory.SpellCastingAgent.SpellCastCompleted += (spellId, targetGuid) => 
        {
            Console.WriteLine($"Completed casting spell {spellId}");
        };
        agentFactory.SpellCastingAgent.SpellHit += (spellId, targetGuid, damage, healed) => 
        {
            Console.WriteLine($"Spell {spellId} hit {targetGuid:X} - Damage: {damage}, Healed: {healed}");
        };
    }
}
```

## BackgroundService Integration

For .NET 8 Worker Service projects, integrate the NetworkAgentFactory like this:

```csharp
public class BackgroundBotWorker : BackgroundService
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<BackgroundBotWorker> _logger;

    public BackgroundBotWorker(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<BackgroundBotWorker>();
        
        // Initialize WoW client and agent factory
        var worldClient = WoWClientFactory.CreateWorldClient();
        _agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Example bot logic using agents
                await PerformBotActionsAsync(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bot execution");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task PerformBotActionsAsync(CancellationToken cancellationToken)
    {
        // Simple bot actions using the agent factory
        if (!_agentFactory.TargetingAgent.HasTarget)
        {
            // Find and target an enemy
            var enemyGuid = FindNearestEnemy();
            if (enemyGuid != 0)
            {
                await _agentFactory.TargetingAgent.SetTargetAsync(enemyGuid);
            }
        }

        if (_agentFactory.TargetingAgent.HasTarget && !_agentFactory.AttackAgent.IsAttacking)
        {
            await _agentFactory.AttackAgent.StartAttackAsync();
        }

        // Handle looting
        var lootTargets = FindLootTargets();
        foreach (var lootTarget in lootTargets)
        {
            await _agentFactory.LootingAgent.QuickLootAsync(lootTarget);
        }

        // Use consumables if needed
        if (IsHealthLow())
        {
            const uint healthPotionId = 118; // Minor Healing Potion
            await _agentFactory.ItemUseAgent.FindAndUseItemAsync(healthPotionId);
        }

        // Cast spells if available
        if (_agentFactory.TargetingAgent.HasTarget && !_agentFactory.SpellCastingAgent.IsCasting)
        {
            const uint fireBoltSpell = 133; // Fireball
            if (_agentFactory.SpellCastingAgent.CanCastSpell(fireBoltSpell))
            {
                var targetGuid = _agentFactory.TargetingAgent.CurrentTarget!.Value;
                await _agentFactory.SpellCastingAgent.CastSpellOnTargetAsync(fireBoltSpell, targetGuid);
            }
        }

        // Manage inventory if it's getting full
        if (!_agentFactory.InventoryAgent.HasEnoughSpace(5))
        {
            await _agentFactory.InventoryAgent.SortBagAsync(0); // Sort main backpack
        }

        // Auto-equip better gear found
        await _agentFactory.EquipmentAgent.AutoEquipAllAsync();
    }

    // Placeholder methods - implement based on your game state access
    private ulong FindNearestEnemy() => 0;
    private ulong[] FindLootTargets() => Array.Empty<ulong>();
    private bool IsHealthLow() => false;
}
```

## Testing

All agents are fully testable with comprehensive test suites:

- **Unit Tests** - Test individual agent functionality with mocked dependencies
- **Integration Tests** - Test agent coordination and workflow scenarios
- **Event Tests** - Verify proper event firing and state management
- **Error Handling Tests** - Test exception handling and error scenarios
- **Factory Tests** - Test agent creation and lazy loading behavior

Example test structure:

```csharp
[Fact]
public void NetworkAgentFactory_LazyLoading_CreatesAgentsOnDemand()
{
    // Arrange
    var mockWorldClient = new Mock<IWorldClient>();
    var mockLoggerFactory = new Mock<ILoggerFactory>();
    var factory = new NetworkAgentFactory(mockWorldClient.Object, mockLoggerFactory.Object);

    // Act & Assert - Agents are created only when accessed
    var targetingAgent = factory.TargetingAgent;
    var attackAgent = factory.AttackAgent;
    
    Assert.NotNull(targetingAgent);
    Assert.NotNull(attackAgent);
}
```

## Migration from Legacy Patterns

### From Individual Agent Creation

```csharp
// Before - Managing individual agents
private readonly ITargetingNetworkAgent _targeting;
private readonly IAttackNetworkAgent _attack;
private readonly IQuestNetworkAgent _quest;

public SomeClass(IWorldClient worldClient, ILoggerFactory loggerFactory)
{
    _targeting = WoWClientFactory.CreateTargetingNetworkAgent(worldClient, loggerFactory);
    _attack = WoWClientFactory.CreateAttackNetworkAgent(worldClient, loggerFactory);
    _quest = WoWClientFactory.CreateQuestNetworkAgent(worldClient, loggerFactory);
}

// After - Using NetworkAgentFactory
private readonly IAgentFactory _agentFactory;

public SomeClass(IWorldClient worldClient, ILoggerFactory loggerFactory)
{
    _agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
}

// Access agents as needed
await _agentFactory.TargetingAgent.SetTargetAsync(enemyGuid);
await _agentFactory.AttackAgent.StartAttackAsync();
```

### Benefits of Migration

1. **Lazy Loading** - Agents are created only when needed, reducing startup time
2. **Memory Efficiency** - Unused agents don't consume memory
3. **Simplified Dependency Management** - Single factory instead of multiple agent dependencies
4. **Event Coordination** - Automatic event handling setup between agents
5. **Thread Safety** - Built-in thread-safe lazy initialization
6. **Testability** - Easier to mock and test with factory pattern

## Best Practices

1. **Use NetworkAgentFactory** - Prefer the factory pattern over individual agent creation
2. **Handle Events** - Subscribe to agent events for monitoring and coordination
3. **Handle Errors** - Wrap agent operations in try-catch blocks
4. **Coordinate Agents** - Use agents together for complex workflows through the factory
5. **Manage State** - Check agent state before performing operations
6. **Async Patterns** - Always use async/await for agent operations
7. **Resource Cleanup** - Ensure proper disposal of resources when done
8. **Cancellation Support** - Use CancellationTokens for long-running operations

## Performance Considerations

- **Lazy Loading**: Agents are created only when first accessed, improving startup performance
- **Thread Safety**: All agent creation is thread-safe with proper locking
- **Memory Usage**: Unused agents don't consume memory resources
- **Event Overhead**: Events are set up only once when agents are created

## Future Enhancements

Potential future additions to the network agent system:

- **TradingNetworkAgent** - For player-to-player trading operations
- **AuctionHouseNetworkAgent** - For auction house interactions
- **GuildNetworkAgent** - For guild management operations
- **MailNetworkAgent** - For mail system interactions
- **PetNetworkAgent** - For pet management and combat
- **GroupNetworkAgent** - For party and raid management
- **TalentNetworkAgent** - For talent tree management and skill point allocation
- **SocialNetworkAgent** - For friends list, ignore list, and social features
- **ChatNetworkAgent** - For chat channel management and messaging

The NetworkAgentFactory architecture is designed to easily accommodate these future additions without breaking existing code.