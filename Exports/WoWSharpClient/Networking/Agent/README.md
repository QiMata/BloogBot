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
- **AuctionHouseNetworkAgent** - Manages auction house operations, browsing, bidding, and posting auctions
- **BankNetworkAgent** - Handles personal bank access, depositing and withdrawing items or gold
- **MailNetworkAgent** - Manages mail system interactions, sending mail to other players and retrieving mail from mailboxes
- **GuildNetworkAgent** - Manages guild operations, invites, guild bank interactions, and member management
- **PartyNetworkAgent** - Manages party/raid group operations, invites, member management, loot settings, and leadership
- **TrainerNetworkAgent** - Handles class trainer interactions, learning spells, abilities, and skills from NPC trainers

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

### AuctionHouseNetworkAgent

Manages all auction house operations including browsing, bidding, and posting auctions.

**Key Features:**
- Open/close auction house windows
- Search for auctions with filtering criteria
- Browse owned auctions and bidder auctions
- Place bids on auctions
- Post items for auction
- Cancel owned auctions
- Buyout auctions instantly
- Auction house state tracking
- Auction operation events and notifications

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.AuctionHouseAgent.OpenAuctionHouseAsync(auctioneerGuid);
await agentFactory.AuctionHouseAgent.SearchAuctionsAsync("Sword", quality: AuctionQuality.Epic);
await agentFactory.AuctionHouseAgent.PlaceBidAsync(auctionId, bidAmount);
await agentFactory.AuctionHouseAgent.PostAuctionAsync(bagId, slotId, startBid, buyoutPrice, AuctionDuration.TwentyFourHours);
await agentFactory.AuctionHouseAgent.BuyoutAuctionAsync(auctionId, buyoutPrice);
```

### BankNetworkAgent

Manages all personal bank operations including depositing and withdrawing items or gold.

**Key Features:**
- Open/close bank windows
- Deposit items from inventory to bank
- Withdraw items from bank to inventory
- Swap items between inventory and bank
- Deposit and withdraw gold
- Purchase additional bank bag slots
- Bank slot management and space tracking
- Quick bank operations (open, operate, close)
- Bank operation events and error handling

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.BankAgent.OpenBankAsync(bankerGuid);
await agentFactory.BankAgent.DepositItemAsync(0, 5); // Deposit item from bag 0, slot 5
await agentFactory.BankAgent.WithdrawItemAsync(10); // Withdraw item from bank slot 10
await agentFactory.BankAgent.DepositGoldAsync(10000); // Deposit 100 silver
await agentFactory.BankAgent.PurchaseBankSlotAsync(); // Buy additional bank storage
await agentFactory.BankAgent.CloseBankAsync();
```

### MailNetworkAgent

Manages all mail system interactions including sending mail to other players and retrieving mail from mailboxes.

**Key Features:**
- Open/close mailbox windows
- Send mail to other players with optional items and money
- Retrieve mail from mailbox
- Delete mail messages
- Return mail to sender
- Mark mail as read
- Get unread mail count
- Check postage costs
- Mail system state tracking
- Mail operation events and error handling

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
await agentFactory.MailAgent.SendMailAsync("PlayerName", "Subject", "Message body", 0, 5); // Send with bag 0, slot 5 item
await agentFactory.MailAgent.SendMoneyAsync("PlayerName", "Money Transfer", "Here's your gold", 100000); // Send 10 gold
await agentFactory.MailAgent.RetrieveMailAsync(mailId);
await agentFactory.MailAgent.DeleteMailAsync(mailId);
await agentFactory.MailAgent.CloseMailboxAsync();
```

### GuildNetworkAgent

Manages all guild-related operations including invites, member management, and guild bank interactions.

**Key Features:**
- Accept/decline guild invitations
- Invite/remove guild members
- Promote/demote guild members
- Guild information and MOTD management
- Guild bank operations (deposit/withdraw items and money)
- Guild creation and management
- Guild roster and information queries
- Officer note and public note management

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.GuildAgent.AcceptGuildInviteAsync();
await agentFactory.GuildAgent.InvitePlayerToGuildAsync("PlayerName");
await agentFactory.GuildAgent.OpenGuildBankAsync(guildBankGuid);
await agentFactory.GuildAgent.DepositItemToGuildBankAsync(0, 5, 1, 10); // Bag 0, slot 5 to tab 1, slot 10
await agentFactory.GuildAgent.DepositMoneyToGuildBankAsync(50000); // Deposit 5 gold
await agentFactory.GuildAgent.SetGuildMOTDAsync("Welcome to our guild!");
```

### PartyNetworkAgent

Manages all party/raid group operations including invites, member management, loot settings, and leadership.

**Key Features:**
- Send/accept/decline party invites
- Invite/kick group members
- Promote players to leader or assistant
- Set loot methods and thresholds
- Convert party to raid
- Manage raid subgroups
- Initiate and respond to ready checks
- Group member status tracking
- Leave/disband groups

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.PartyAgent.InvitePlayerAsync("PlayerName");
await agentFactory.PartyAgent.AcceptInviteAsync();
await agentFactory.PartyAgent.SetLootMethodAsync(LootMethod.GroupLoot, null, LootQuality.Rare);
await agentFactory.PartyAgent.ConvertToRaidAsync();
await agentFactory.PartyAgent.PromoteToLeaderAsync("NewLeaderName");
await agentFactory.PartyAgent.InitiateReadyCheckAsync();
var members = agentFactory.PartyAgent.GetGroupMembers();
```

### TrainerNetworkAgent

Manages all class trainer interactions for learning spells, abilities, and skills from NPC trainers.

**Key Features:**
- Open/close trainer windows
- Request trainer services (available spells/abilities)
- Learn individual spells and abilities
- Learn multiple spells in sequence
- Query spell costs and availability
- Check learning prerequisites
- Filter affordable services
- Trainer window state tracking
- Learning operation events and error handling

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
await agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
await agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);

// Learn a specific spell
await agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spellId);

// Quick learn multiple spells
var spellIds = new uint[] { 1234, 5678, 9012 };
await agentFactory.TrainerAgent.LearnMultipleSpellsAsync(trainerGuid, spellIds);

// Check what's available and affordable
var availableServices = agentFactory.TrainerAgent.GetAvailableServices();
var affordableServices = agentFactory.TrainerAgent.GetAffordableServices(currentMoney);

// Quick learn a single spell (open, learn, close)
await agentFactory.TrainerAgent.QuickLearnSpellAsync(trainerGuid, spellId);

await agentFactory.TrainerAgent.CloseTrainerAsync();
```

## Summary

I have successfully completed the implementation of the TrainerNetworkAgent for the WoWSharpClient library. Here's what was accomplished:

### ? Completed Implementation

1. **Core Trainer Agent (`TrainerNetworkAgent`)**
   - Created a comprehensive implementation with all interface methods
   - Added proper event handling for trainer operations
   - Implemented spell/ability learning functionality
   - Added trainer service management (query, filter, cost checking)
   - Included proper error handling and logging
   - Added support for bulk operations and quick learning

2. **Interface Definition (`ITrainerNetworkAgent`)**
   - Comprehensive interface with all necessary methods
   - Added all necessary events for trainer state changes
   - Included proper documentation for all methods
   - Defined TrainerService and TrainerServiceType models

3. **Factory Integration**
   - Updated `AgentFactory` to include trainer agent creation
   - Added trainer agent to `NetworkAgentFactory` for lazy loading
   - Updated `WoWClientFactory` with trainer agent factory methods
   - Modified `CreateAllNetworkAgents` to include the trainer agent

4. **Comprehensive Testing**
   - Created `TrainerNetworkAgentTests` with full test coverage
   - Added constructor tests, packet sending verification
   - Included tests for spell learning operations
   - Added event handling and server response tests
   - Added service management and filtering tests

5. **Documentation Updates**
   - Updated the README.md with complete trainer agent documentation
   - Added usage examples and integration patterns
   - Included trainer management scenarios and best practices

### ?? Technical Details

The TrainerNetworkAgent implementation includes:

- **Supported Operations**: All trainer operations (open, request services, learn spells, query costs, check availability)
- **Service Management**: Comprehensive service filtering and cost checking
- **Event System**: Complete event system for monitoring trainer operations
- **Error Handling**: Proper exception handling with detailed logging
- **Thread Safety**: Thread-safe implementation with async/await patterns
- **Bulk Operations**: Support for learning multiple spells and quick operations

### ?? Integration Features

1. **Service Querying**: Check available spells, costs, and prerequisites
2. **Smart Learning**: Learn individual spells or multiple spells in sequence
3. **Cost Management**: Filter services by affordability and check individual costs
4. **Quick Operations**: One-shot operations that handle open, operate, and close
5. **State Tracking**: Track trainer window state and current services

### ?? Integration

The trainer agent is now fully integrated into the existing agent ecosystem:

```csharp
// Access via factory (recommended)
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
var trainerAgent = agentFactory.TrainerAgent;

// Or create individually
var trainerAgent = WoWClientFactory.CreateTrainerNetworkAgent(worldClient, loggerFactory);
```

The implementation follows the same patterns as other network agents and is ready for use in production bot scenarios for automated spell and ability learning from class trainers.

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
var auctionHouseAgent = agentFactory.AuctionHouseAgent;
var bankAgent = agentFactory.BankAgent;
var mailAgent = agentFactory.MailAgent;
var guildAgent = agentFactory.GuildAgent;
var partyAgent = agentFactory.PartyAgent;
var trainerAgent = agentFactory.TrainerAgent;
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
var auctionHouseAgent = WoWClientFactory.CreateAuctionHouseNetworkAgent(worldClient, loggerFactory);
var bankAgent = WoWClientFactory.CreateBankNetworkAgent(worldClient, loggerFactory);
var mailAgent = WoWClientFactory.CreateMailNetworkAgent(worldClient, loggerFactory);
var guildAgent = WoWClientFactory.CreateGuildNetworkAgent(worldClient, loggerFactory);
var partyAgent = WoWClientFactory.CreatePartyNetworkAgent(worldClient, loggerFactory);
var trainerAgent = WoWClientFactory.CreateTrainerNetworkAgent(worldClient, loggerFactory);
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
var auctionHouseAgent = allAgents.AuctionHouseAgent;
var bankAgent = allAgents.BankAgent;
var mailAgent = allAgents.MailAgent;
var guildAgent = allAgents.GuildAgent;
var partyAgent = allAgents.PartyAgent;
var trainerAgent = allAgents.TrainerAgent;
```

## Integration Examples

### Trainer Management

```csharp
public class TrainerManager
{
    private readonly IAgentFactory _agentFactory;

    public TrainerManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
        
        // Subscribe to trainer events
        _agentFactory.TrainerAgent.TrainerWindowOpened += HandleTrainerOpened;
        _agentFactory.TrainerAgent.SpellLearned += HandleSpellLearned;
        _agentFactory.TrainerAgent.TrainerError += HandleTrainerError;
    }

    public async Task LearnAvailableSpellsAsync(ulong trainerGuid, uint maxCost)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Small delay to allow services to load
        await Task.Delay(200);
        
        // Get affordable spells within budget
        var affordableSpells = _agentFactory.TrainerAgent.GetAffordableServices(maxCost);
        
        foreach (var service in affordableSpells)
        {
            try
            {
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, service.SpellId);
                await Task.Delay(100); // Respect rate limits
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to learn spell {service.SpellId}: {ex.Message}");
            }
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    public async Task LearnClassSpellsAsync(ulong trainerGuid, uint level)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Small delay to allow services to load
        await Task.Delay(200);

        // First, try to learn priority spells
        foreach (var spellId in prioritySpells)
        {
            if (_agentFactory.TrainerAgent.IsSpellAvailable(spellId))
            {
                var cost = _agentFactory.TrainerAgent.GetSpellCost(spellId);
                Console.WriteLine($"Learning priority spell {spellId} for {cost} copper");
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spellId);
                await Task.Delay(100);
            }
        }

        // Then learn any other affordable spells
        var currentMoney = GetCurrentMoney();
        var remainingSpells = _agentFactory.TrainerAgent.GetAffordableServices(currentMoney)
            .Where(s => !prioritySpells.Contains(s.SpellId))
            .OrderBy(s => s.Cost);

        foreach (var service in remainingSpells)
        {
            await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, service.SpellId);
            await Task.Delay(100);
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    public async Task QuickLearnSpecificSpellsAsync(ulong trainerGuid, uint[] spellIds)
    {
        // Use the multi-spell learning method for efficiency
        await _agentFactory.TrainerAgent.LearnMultipleSpellsAsync(trainerGuid, spellIds);
    }

    public async Task CheckTrainerServicesAsync(ulong trainerGuid)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Small delay to allow services to load
        await Task.Delay(200);
        
        var availableServices = agentFactory.TrainerAgent.GetAvailableServices();
        var affordableServices = agentFactory.TrainerAgent.GetAffordableServices(currentMoney);
        
        Console.WriteLine($"Trainer offers {availableServices.Length} learnable services:");
        foreach (var service in availableServices)
        {
            Console.WriteLine($"- {service.Name} (Rank {service.Rank}): {service.Cost} copper");
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    private void HandleTrainerOpened(ulong trainerGuid)
    {
        Console.WriteLine($"Trainer window opened: {trainerGuid:X}");
    }

    private void HandleSpellLearned(uint spellId, uint cost)
    {
        Console.WriteLine($"Learned spell {spellId} for {cost} copper");
    }

    private void HandleTrainerError(string error)
    {
        Console.WriteLine($"Trainer error: {error}");
    }

    // Helper method - implement based on your game state access
    private uint GetCurrentMoney() => 0; // Implement based on character state
}
```

### Class-Specific Spell Learning

```csharp
public class ClassSpellManager
{
    private readonly IAgentFactory _agentFactory;
    private readonly Dictionary<string, uint[]> _classPrioritySpells;

    public ClassSpellManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
        
        // Define priority spells for different classes
        _classPrioritySpells = new Dictionary<string, uint[]>
        {
            ["Warrior"] = new uint[] { 78, 284, 1160, 6673 }, // Heroic Strike, Charge, Demoralizing Shout, Battle Shout
            ["Paladin"] = new uint[] { 635, 21084, 1152, 639 }, // Holy Light, Seal of Righteousness, Purify, Blessing of Might
            ["Mage"] = new uint[] { 133, 168, 116, 1459 }, // Fireball, Frost Bolt, Frostbolt, Arcane Intellect
            ["Priest"] = new uint[] { 2061, 139, 1244, 1243 }, // Flash Heal, Renew, Power Word: Fortitude, Power Word: Shield
            ["Rogue"] = new uint[] { 1752, 53, 921, 1776 }, // Sinister Strike, Backstab, Pick Pocket, Gouge
            ["Warlock"] = new uint[] { 686, 348, 702, 1120 }, // Shadow Bolt, Immolate, Curse of Weakness, Drain Soul
            ["Hunter"] = new uint[] { 75, 1978, 1515, 3044 }, // Auto Shot, Serpent Sting, Tame Beast, Arcane Shot
            ["Shaman"] = new uint[] { 403, 8042, 324, 8024 }, // Lightning Bolt, Earth Shock, Lightning Shield, Flametongue Weapon
            ["Druid"] = new uint[] { 5176, 8921, 774, 467 } // Wrath, Moonfire, Rejuvenation, Thorns
        };
    }

    public async Task LearnClassSpellsAsync(ulong trainerGuid, string playerClass, uint level)
    {
        if (!_classPrioritySpells.TryGetValue(playerClass, out var prioritySpells))
        {
            Console.WriteLine($"No priority spells defined for class: {playerClass}");
            return;
        }

        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Small delay to allow services to load
        await Task.Delay(200);

        var currentMoney = GetCurrentMoney();
        var affordableServices = _agentFactory.TrainerAgent.GetAffordableServices(currentMoney);
        
        // Filter by level and priority
        var spellsToLearn = affordableServices
            .Where(s => s.RequiredLevel <= level)
            .OrderBy(s => prioritySpells.Contains(s.SpellId) ? 0 : 1) // Priority spells first
            .ThenBy(s => s.RequiredLevel)
            .ThenBy(s => s.Cost);

        Console.WriteLine($"Learning spells for {playerClass} (Level {level}):");
        
        foreach (var service in spellsToLearn)
        {
            try
            {
                var isPriority = prioritySpells.Contains(service.SpellId) ? " (Priority)" : "";
                Console.WriteLine($"Learning: {service.Name} (Rank {service.Rank}){isPriority} - {service.Cost} copper");
                
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, service.SpellId);
                await Task.Delay(100); // Rate limiting
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to learn {service.Name}: {ex.Message}");
            }
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    public async Task LearnTalentPrerequisitesAsync(ulong trainerGuid, uint[] prerequisiteSpells)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Small delay to allow services to load
        await Task.Delay(200);

        foreach (var spellId in prerequisiteSpells)
        {
            if (_agentFactory.TrainerAgent.IsSpellAvailable(spellId))
            {
                var cost = _agentFactory.TrainerAgent.GetSpellCost(spellId);
                Console.WriteLine($"Learning talent prerequisite spell {spellId} for {cost} copper");
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spellId);
                await Task.Delay(100);
            }
            else
            {
                Console.WriteLine($"Prerequisite spell {spellId} not available from this trainer");
            }
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    // Helper method - implement based on your game state access
    private uint GetCurrentMoney() => 0;
}
```

### Automated Spell Learning Strategy

```csharp
public class SpellLearningStrategy
{
    private readonly IAgentFactory _agentFactory;

    public SpellLearningStrategy(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task ExecuteOptimalSpellLearningAsync(ulong trainerGuid, uint currentLevel, uint availableMoney)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        // Wait for services to load
        await Task.Delay(200);

        var allServices = _agentFactory.TrainerAgent.GetAvailableServices();
        var affordableSpells = _agentFactory.TrainerAgent.GetAffordableServices(availableMoney);

        // Strategy 1: Learn all spells within level range and budget
        var optimalSpells = affordableSpells
            .Where(s => s.RequiredLevel <= currentLevel)
            .Where(s => IsUsefulSpell(s)) // Custom logic to determine usefulness
            .OrderBy(s => GetSpellPriority(s)) // Custom priority scoring
            .ThenBy(s => s.RequiredLevel)
            .ThenBy(s => s.Cost);

        uint totalCost = 0;
        var spellsToLearn = new List<TrainerService>();

        foreach (var service in optimalSpells)
        {
            if (totalCost + service.Cost <= availableMoney)
            {
                spellsToLearn.Add(service);
                totalCost += service.Cost;
            }
        }

        Console.WriteLine($"Planned to learn {spellsToLearn.Count} spells for {totalCost} copper:");
        
        foreach (var service in spellsToLearn)
        {
            Console.WriteLine($"- {service.Name} (Rank {service.Rank}): {service.Cost} copper");
        }

        // Execute the learning plan
        foreach (var service in spellsToLearn)
        {
            try
            {
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, service.SpellId);
                Console.WriteLine($"? Learned {service.Name}");
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Failed to learn {service.Name}: {ex.Message}");
            }
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
        
        Console.WriteLine($"Spell learning completed. Spent {totalCost} copper on {spellsToLearn.Count} spells.");
    }

    private bool IsUsefulSpell(TrainerService service)
    {
        // Implement logic to determine if a spell is useful
        // This could consider class, specialization, current spells known, etc.
        
        // Example: Skip certain spell types or low-rank versions
        if (service.ServiceType == TrainerServiceType.Profession)
        {
            return false; // Skip profession spells for now
        }

        // Always learn class spells
        return service.ServiceType == TrainerServiceType.Spell || 
               service.ServiceType == TrainerServiceType.ClassSkill;
    }

    private int GetSpellPriority(TrainerService service)
    {
        // Return lower numbers for higher priority
        // This is a simple example - implement based on your strategy
        
        if (service.ServiceType == TrainerServiceType.ClassSkill)
            return 1; // Highest priority
        
        if (service.ServiceType == TrainerServiceType.Spell)
            return 2; // Medium priority
            
        return 3; // Lowest priority
    }
}
```

### Integration with Automated Leveling

```csharp
public class AutoLevelingManager
{
    private readonly IAgentFactory _agentFactory;
    private readonly Dictionary<uint, uint[]> _levelMilestoneSpells;

    public AutoLevelingManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
        
        // Define important spells to learn at specific level milestones
        _levelMilestoneSpells = new Dictionary<uint, uint[]>
        {
            [10] = new uint[] { /* Key level 10 spells */ },
            [20] = new uint[] { /* Key level 20 spells */ },
            [30] = new uint[] { /* Key level 30 spells */ },
            [40] = new uint[] { /* Key level 40 spells */ }
        };
    }

    public async Task HandleLevelUpAsync(uint newLevel, ulong nearestTrainerGuid)
    {
        Console.WriteLine($"Leveled up to {newLevel}! Checking for new spells to learn...");

        if (_levelMilestoneSpells.TryGetValue(newLevel, out var milestoneSpells))
        {
            // Learn milestone spells immediately
            await _agentFactory.TrainerAgent.LearnMultipleSpellsAsync(nearestTrainerGuid, milestoneSpells);
        }

        // Learn any other available spells within budget
        var currentMoney = GetCurrentMoney();
        var reserveAmount = GetReserveAmount(newLevel); // Keep some money for other expenses
        var spellBudget = currentMoney > reserveAmount ? currentMoney - reserveAmount : 0;

        if (spellBudget > 0)
        {
            await LearnAffordableSpellsAsync(nearestTrainerGuid, newLevel, spellBudget);
        }
    }

    private async Task LearnAffordableSpellsAsync(ulong trainerGuid, uint level, uint budget)
    {
        await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
        await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
        
        await Task.Delay(200);

        var affordableServices = _agentFactory.TrainerAgent.GetAffordableServices(budget)
            .Where(s => s.RequiredLevel <= level)
            .OrderBy(s => s.RequiredLevel)
            .ThenBy(s => s.Cost);

        foreach (var service in affordableServices)
        {
            try
            {
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, service.SpellId);
                Console.WriteLine($"Auto-learned: {service.Name} for {service.Cost} copper");
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to auto-learn {service.Name}: {ex.Message}");
            }
        }
        
        await _agentFactory.TrainerAgent.CloseTrainerAsync();
    }

    // Helper methods
    private uint GetCurrentMoney() => 0; // Implement based on character state
    private uint GetReserveAmount(uint level) => level * 1000; // Keep 10 silver per level
}
```

### Event Handling Integration

All agents provide comprehensive event monitoring:

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

        // Auction house events
        agentFactory.AuctionHouseAgent.AuctionHouseOpened += (auctioneerGuid) => 
        {
            Console.WriteLine($"Auction house opened with auctioneer: {auctioneerGuid:X}");
        };
        agentFactory.AuctionHouseAgent.AuctionSearchResults += (auctions) => 
        {
            Console.WriteLine($"Received {auctions.Count} auction search results");
        };
        agentFactory.AuctionHouseAgent.BidPlaced += (auctionId, bidAmount) => 
        {
            Console.WriteLine($"Bid of {bidAmount} copper placed on auction {auctionId}");
        };
        agentFactory.AuctionHouseAgent.AuctionPosted += (itemId, startBid, buyoutPrice, duration) => 
        {
            Console.WriteLine($"Posted auction for item {itemId} - Start: {startBid}, Buyout: {buyoutPrice}, Duration: {duration}h");
        };
        agentFactory.AuctionHouseAgent.AuctionOperationFailed += (operation, error) => 
        {
            Console.WriteLine($"Auction operation {operation} failed: {error}");
        };

        // Bank events
        agentFactory.BankAgent.BankWindowOpened += (bankerGuid) => 
        {
            Console.WriteLine($"Bank window opened with banker: {bankerGuid:X}");
        };
        agentFactory.BankAgent.BankWindowClosed += () => 
        {
            Console.WriteLine("Bank window closed");
        };
        agentFactory.BankAgent.ItemDeposited += (itemGuid, itemId, quantity, bankSlot) => 
        {
            Console.WriteLine($"Deposited {quantity}x item {itemId} (GUID: {itemGuid:X}) to bank slot {bankSlot}");
        };
        agentFactory.BankAgent.ItemWithdrawn += (itemGuid, itemId, quantity, bagSlot) => 
        {
            Console.WriteLine($"Withdrew {quantity}x item {itemId} (GUID: {itemGuid:X}) to bag slot {bagSlot}");
        };
        agentFactory.BankAgent.GoldDeposited += (amount) => 
        {
            Console.WriteLine($"Deposited {amount} copper to bank");
        };
        agentFactory.BankAgent.GoldWithdrawn += (amount) => 
        {
            Console.WriteLine($"Withdrew {amount} copper from bank");
        };
        agentFactory.BankAgent.BankSlotPurchased += (slotIndex, cost) => 
        {
            Console.WriteLine($"Purchased bank slot {slotIndex} for {cost} copper");
        };
        agentFactory.BankAgent.BankOperationFailed += (operation, error) => 
        {
            Console.WriteLine($"Bank operation {operation} failed: {error}");
        };

        // Mail events
        agentFactory.MailAgent.MailboxOpened += (mailboxGuid) => 
        {
            Console.WriteLine($"Mailbox opened: {mailboxGuid:X}");
        };
        agentFactory.MailAgent.MailboxClosed += () => 
        {
            Console.WriteLine("Mailbox closed");
        };
        agentFactory.MailAgent.MailSent += (recipientName, subject, hasItem, hasGold) => 
        {
            Console.WriteLine($"Mail sent to {recipientName}: '{subject}' (Item: {hasItem}, Gold: {hasGold})");
        };
        agentFactory.MailAgent.MailRetrieved += (mailId, itemId, goldAmount) => 
        {
            Console.WriteLine($"Retrieved mail {mailId} - Item: {itemId}, Gold: {goldAmount}");
        };
        agentFactory.MailAgent.MailDeleted += (mailId) => 
        {
            Console.WriteLine($"Deleted mail {mailId}");
        };
        agentFactory.MailAgent.MailReturned += (mailId, recipientName) => 
        {
            Console.WriteLine($"Returned mail {mailId} to {recipientName}");
        };
        agentFactory.MailAgent.MailOperationFailed += (operation, error) => 
        {
            Console.WriteLine($"Mail operation {operation} failed: {error}");
        };

        // Guild events
        agentFactory.GuildAgent.GuildInviteReceived += (inviterName, guildName) => 
        {
            Console.WriteLine($"Guild invite received from {inviterName} for guild {guildName}");
        };
        agentFactory.GuildAgent.GuildJoined += (guildId, guildName) => 
        {
            Console.WriteLine($"Joined guild: {guildName} (ID: {guildId})");
        };
        agentFactory.GuildAgent.GuildLeft += (guildId, reason) => 
        {
            Console.WriteLine($"Left guild {guildId}: {reason}");
        };
        agentFactory.GuildAgent.GuildMemberOnline += (memberName) => 
        {
            Console.WriteLine($"Guild member {memberName} came online");
        };
        agentFactory.GuildAgent.GuildMemberOffline += (memberName) => 
        {
            Console.WriteLine($"Guild member {memberName} went offline");
        };
        agentFactory.GuildAgent.GuildRosterReceived += (memberCount) => 
        {
            Console.WriteLine($"Guild roster received with {memberCount} members");
        };
        agentFactory.GuildAgent.GuildInfoReceived += (guildId, guildName, guildInfo) => 
        {
            Console.WriteLine($"Guild info received for {guildName}: {guildInfo}");
        };
        agentFactory.GuildAgent.GuildMOTDReceived += (motd) => 
        {
            Console.WriteLine($"Guild MOTD: {motd}");
        };
        agentFactory.GuildAgent.GuildBankWindowOpened += (bankGuid) => 
        {
            Console.WriteLine($"Guild bank opened: {bankGuid:X}");
        };
        agentFactory.GuildAgent.GuildBankWindowClosed += () => 
        {
            Console.WriteLine("Guild bank closed");
        };
        agentFactory.GuildAgent.ItemDepositedToGuildBank += (itemId, quantity, tabIndex, slotIndex) => 
        {
            Console.WriteLine($"Deposited {quantity}x item {itemId} to guild bank tab {tabIndex}, slot {slotIndex}");
        };
        agentFactory.GuildAgent.ItemWithdrawnFromGuildBank += (itemId, quantity, tabIndex, slotIndex) => 
        {
            Console.WriteLine($"Withdrew {quantity}x item {itemId} from guild bank tab {tabIndex}, slot {slotIndex}");
        };
        agentFactory.GuildAgent.MoneyDepositedToGuildBank += (amount) => 
        {
            Console.WriteLine($"Deposited {amount} copper to guild bank");
        };
        agentFactory.GuildAgent.MoneyWithdrawnFromGuildBank += (amount) => 
        {
            Console.WriteLine($"Withdrew {amount} copper from guild bank");
        };
        agentFactory.GuildAgent.GuildOperationFailed += (operation, error) => 
        {
            Console.WriteLine($"Guild operation {operation} failed: {error}");
        };
    }
}
```

## BackgroundService Integration

For .NET 8 Worker Service projects, integrate the NetworkAgentFactory with trainer support:

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
                // Example bot logic using agents including trainer
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
        // ... existing bot logic ...

        // Check for spell learning opportunities
        var currentTime = DateTime.UtcNow;
        if (ShouldCheckTrainer(currentTime))
        {
            var trainerGuid = FindNearestClassTrainer();
            if (trainerGuid != 0)
            {
                await CheckForNewSpellsAsync(trainerGuid);
            }
        }

        // Auto-learn spells when leveling up
        if (HasLeveledUp())
        {
            var trainerGuid = FindNearestClassTrainer();
            if (trainerGuid != 0)
            {
                await LearnLevelAppropriateSpellsAsync(trainerGuid);
            }
        }
    }

    private async Task CheckForNewSpellsAsync(ulong trainerGuid)
    {
        try
        {
            await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
            await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
            
            await Task.Delay(200);
            
            var currentMoney = GetCurrentMoney();
            var affordableSpells = _agentFactory.TrainerAgent.GetAffordableServices(currentMoney)
                .Where(s => s.RequiredLevel <= GetCurrentLevel())
                .Take(3); // Limit to avoid spending too much time
            
            foreach (var spell in affordableSpells)
            {
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spell.SpellId);
                await Task.Delay(100);
            }
            
            await _agentFactory.TrainerAgent.CloseTrainerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check trainer for new spells");
        }
    }

    private async Task LearnLevelAppropriateSpellsAsync(ulong trainerGuid)
    {
        try
        {
            var currentLevel = GetCurrentLevel();
            var currentMoney = GetCurrentMoney();
            var spellBudget = Math.Min(currentMoney / 2, currentLevel * 1000); // Spend up to half money or level*10 silver
            
            await _agentFactory.TrainerAgent.OpenTrainerAsync(trainerGuid);
            await _agentFactory.TrainerAgent.RequestTrainerServicesAsync(trainerGuid);
            
            await Task.Delay(200);
            
            var spellsToLearn = _agentFactory.TrainerAgent.GetAffordableServices(spellBudget)
                .Where(s => s.RequiredLevel <= currentLevel)
                .OrderBy(s => s.RequiredLevel)
                .Take(5); // Learn up to 5 spells per level
            
            foreach (var spell in spellsToLearn)
            {
                await _agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spell.SpellId);
                _logger.LogInformation($"Auto-learned spell: {spell.Name} for {spell.Cost} copper");
                await Task.Delay(100);
            }
            
            await _agentFactory.TrainerAgent.CloseTrainerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to learn level-appropriate spells");
        }
    }

    // Helper methods - implement based on your game state access
    private bool ShouldCheckTrainer(DateTime currentTime) => false;
    private ulong FindNearestClassTrainer() => 0;
    private bool HasLeveledUp() => false;
    private uint GetCurrentMoney() => 0;
    private uint GetCurrentLevel() => 0;
}