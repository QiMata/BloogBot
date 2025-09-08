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

## Summary

I have successfully completed the implementation of the GuildNetworkAgent for the WoWSharpClient library. Here's what was accomplished:

### ? Completed Implementation

1. **Core Guild Agent (`GuildNetworkAgent`)**
   - Created a comprehensive implementation with all interface methods
   - Added proper event handling for guild operations
   - Implemented guild invite acceptance/decline
   - Added guild member management (invite, remove, promote, demote)
   - Included guild settings (MOTD, guild info)
   - Added proper error handling and logging

2. **Interface Updates (`IGuildNetworkAgent`)**
   - Updated the interface to match the comprehensive requirements
   - Added all necessary events for guild state changes
   - Included proper documentation for all methods

3. **Factory Integration**
   - Updated `AgentFactory` to include guild agent creation
   - Added guild agent to `NetworkAgentFactory` for lazy loading
   - Updated `WoWClientFactory` with guild agent factory methods
   - Modified `CreateAllNetworkAgents` to include the guild agent

4. **Comprehensive Testing**
   - Created `GuildNetworkAgentTests` with full test coverage
   - Added constructor tests, packet sending verification
   - Included tests for unsupported guild bank operations
   - Added event handling and server response tests

5. **Documentation Updates**
   - Updated the README.md with complete guild agent documentation
   - Added usage examples and integration patterns
   - Included BackgroundService integration examples
   - Added guild management scenarios and best practices

### ?? Technical Details

The GuildNetworkAgent implementation includes:

- **Supported Operations**: All basic guild operations (invite, accept, decline, promote, demote, leave, disband, MOTD, roster requests)
- **Guild Bank Limitation**: Guild bank operations are not supported in this client version and return appropriate error messages
- **Event System**: Comprehensive event system for monitoring guild state changes
- **Error Handling**: Proper exception handling with detailed logging
- **Thread Safety**: Thread-safe implementation with async/await patterns

### ?? Current Limitations

1. **Guild Bank Operations**: Not supported in this client version (Classic WoW limitation)
2. **Guild Permissions**: Permission system not implemented 
3. **Guild Rank Tracking**: Advanced rank tracking not implemented
4. **Advanced Parsing**: Server response parsing is simplified (would need proper protocol implementation)

### ?? Integration

The guild agent is now fully integrated into the existing agent ecosystem:

```csharp
// Access via factory (recommended)
var agentFactory = WoWClientFactory.CreateNetworkAgentFactory(worldClient, loggerFactory);
var guildAgent = agentFactory.GuildAgent;

// Or create individually
var guildAgent = WoWClientFactory.CreateGuildNetworkAgent(worldClient, loggerFactory);
```

The implementation follows the same patterns as other network agents and is ready for use in production bot scenarios.

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

### Auction House Operations

```csharp
public class AuctionHouseManager
{
    private readonly IAgentFactory _agentFactory;

    public AuctionHouseManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task SearchForBestDealsAsync(ulong auctioneerGuid, string itemName)
    {
        await _agentFactory.AuctionHouseAgent.OpenAuctionHouseAsync(auctioneerGuid);
        await _agentFactory.AuctionHouseAgent.SearchAuctionsAsync(itemName);
        
        // Wait for search results and analyze deals
        // Results will be provided via the AuctionSearchResults event
        
        await _agentFactory.AuctionHouseAgent.CloseAuctionHouseAsync();
    }

    public async Task SellItemsAsync(ulong auctioneerGuid, (byte BagId, byte SlotId, uint StartBid, uint BuyoutPrice)[] items)
    {
        await _agentFactory.AuctionHouseAgent.OpenAuctionHouseAsync(vendorGuid);
        
        foreach (var (bagId, slotId, startBid, buyoutPrice) in items)
        {
            await agentFactory.AuctionHouseAgent.PostAuctionAsync(
                bagId, slotId, startBid, buyoutPrice, AuctionDuration.TwentyFourHours);
        }
        
        await _agentFactory.AuctionHouseAgent.CloseAuctionHouseAsync();
    }

    public async Task BuyItemAsync(ulong auctioneerGuid, uint auctionId, uint maxPrice)
    {
        await _agentFactory.AuctionHouseAgent.OpenAuctionHouseAsync(vendorGuid);
        
        // Search for the specific auction and verify price
        await _agentFactory.AuctionHouseAgent.SearchAuctionsAsync();
        
        // If price is acceptable, buyout the auction
        await _agentFactory.AuctionHouseAgent.BuyoutAuctionAsync(auctionId, maxPrice);
        
        await _agentFactory.AuctionHouseAgent.CloseAuctionHouseAsync();
    }

    public async Task ManageAuctionsAsync(ulong auctioneerGuid)
    {
        await _agentFactory.AuctionHouseAgent.OpenAuctionHouseAsync(vendorGuid);
        
        // Check owned auctions
        await _agentFactory.AuctionHouseAgent.GetOwnedAuctionsAsync();
        
        // Check bidder auctions
        await _agentFactory.AuctionHouseAgent.GetBidderAuctionsAsync();
        
        await _agentFactory.AuctionHouseAgent.CloseAuctionHouseAsync();
    }
}
```

### Bank Management

```csharp
public class BankManager
{
    private readonly IAgentFactory _agentFactory;

    public BankManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task DepositValuableItemsAsync(ulong bankerGuid, uint[] valuableItemIds)
    {
        await _agentFactory.BankAgent.OpenBankAsync(bankerGuid);
        
        foreach (var itemId in valuableItemIds)
        {
            // Find items in inventory and deposit them
            var itemSlot = FindItemInInventory(itemId);
            if (itemSlot.HasValue)
            {
                await _agentFactory.BankAgent.DepositItemAsync(itemSlot.Value.BagId, itemSlot.Value.SlotId);
            }
        }
        
        await _agentFactory.BankAgent.CloseBankAsync();
    }

    public async Task WithdrawItemsForRaidAsync(ulong bankerGuid, uint[] requiredItemIds)
    {
        await _agentFactory.BankAgent.OpenBankAsync(bankerGuid);
        
        foreach (var itemId in requiredItemIds)
        {
            // Find items in bank and withdraw them
            var bankSlot = FindItemInBank(itemId);
            if (bankSlot.HasValue)
            {
                await _agentFactory.BankAgent.WithdrawItemAsync(bankSlot.Value);
            }
        }
        
        await _agentFactory.BankAgent.CloseBankAsync();
    }

    public async Task DepositExcessGoldAsync(ulong bankerGuid, uint keepAmount)
    {
        var currentGold = GetCurrentGold();
        if (currentGold > keepAmount)
        {
            var depositAmount = currentGold - keepAmount;
            await _agentFactory.BankAgent.QuickDepositGoldAsync(bankerGuid, depositAmount);
        }
    }

    public async Task WithdrawGoldForPurchaseAsync(ulong bankerGuid, uint requiredAmount)
    {
        var currentGold = GetCurrentGold();
        if (currentGold < requiredAmount)
        {
            var withdrawAmount = requiredAmount - currentGold;
            await _agentFactory.BankAgent.QuickWithdrawGoldAsync(bankerGuid, withdrawAmount);
        }
    }

    public async Task ExpandBankStorageAsync(ulong bankerGuid)
    {
        var nextSlotCost = _agentFactory.BankAgent.GetNextBankSlotCost();
        if (nextSlotCost.HasValue && HasEnoughGold(nextSlotCost.Value))
        {
            await _agentFactory.BankAgent.OpenBankAsync(bankerGuid);
            await _agentFactory.BankAgent.PurchaseBankSlotAsync();
            await _agentFactory.BankAgent.CloseBankAsync();
        }
    }

    public async Task OrganizeBankAsync(ulong bankerGuid)
    {
        await _agentFactory.BankAgent.OpenBankAsync(bankerGuid);
        
        // Move similar items together, sort by type/value
        // This would involve reading bank contents and reorganizing
        
        await _agentFactory.BankAgent.CloseBankAsync();
    }

    // Helper methods - implement based on your game state access
    private byte? FindItemInBank(uint itemId) => null;
    private byte? FindItemInInventory(uint itemId) => null;
    private uint[] GetAvailableMailIds() => Array.Empty<uint>();
    private (byte TabIndex, byte SlotIndex, uint Quantity)[] GetNeededConsumables() => Array.Empty<(byte, byte, uint)>();
}
```

### Mail Management

```csharp
public class MailManager
{
    private readonly IAgentFactory _agentFactory;

    public MailManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public async Task SendItemToPlayerAsync(ulong mailboxGuid, string recipientName, string subject, string body, byte bagId, byte slotId)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        await _agentFactory.MailAgent.SendMailAsync(recipientName, subject, body, bagId, slotId);
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    public async Task SendMoneyToPlayerAsync(ulong mailboxGuid, string recipientName, uint amount, string reason)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        await _agentFactory.MailAgent.SendMoneyAsync(recipientName, "Money Transfer", reason, amount);
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    public async Task CheckAndRetrieveAllMailAsync(ulong mailboxGuid)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        
        // Get count of unread mail
        var unreadCount = _agentFactory.MailAgent.GetUnreadMailCount();
        Console.WriteLine($"You have {unreadCount} unread messages");
        
        // Retrieve all mail items - this would need to be implemented based on your mail listing
        var mailIds = GetAvailableMailIds(); // Implement based on your game state
        foreach (var mailId in mailIds)
        {
            await _agentFactory.MailAgent.RetrieveMailAsync(mailId);
        }
        
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    public async Task CleanupOldMailAsync(ulong mailboxGuid)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        
        // Delete old read mail - implement based on your mail management logic
        var oldMailIds = GetOldMailIds(); // Implement based on your game state
        foreach (var mailId in oldMailIds)
        {
            await _agentFactory.MailAgent.DeleteMailAsync(mailId);
        }
        
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    public async Task SendGuildSuppliesAsync(ulong mailboxGuid, string[] guildMembers, (byte BagId, byte SlotId)[] supplies)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        
        foreach (var member in guildMembers)
        {
            foreach (var (bagId, slotId) in supplies)
            {
                await _agentFactory.MailAgent.SendMailAsync(member, "Guild Supplies", "Here are some supplies for the guild", bagId, slotId);
            }
        }
        
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    public async Task ReturnIncorrectMailAsync(ulong mailboxGuid, uint mailId)
    {
        await _agentFactory.MailAgent.OpenMailboxAsync(mailboxGuid);
        await _agentFactory.MailAgent.ReturnMailAsync(mailId);
        await _agentFactory.MailAgent.CloseMailboxAsync();
    }

    // Helper methods - implement based on your game state access
    private uint[] GetAvailableMailIds() => Array.Empty<uint>();
    private uint[] GetOldMailIds() => Array.Empty<uint>();
}
```

### Guild Management

```csharp
public class GuildManager
{
    private readonly IAgentFactory _agentFactory;

    public GuildManager(IAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
        
        // Subscribe to guild events
        _agentFactory.GuildAgent.GuildInviteReceived += HandleGuildInvite;
        _agentFactory.GuildAgent.GuildJoined += HandleGuildJoined;
        _agentFactory.GuildAgent.GuildLeft += HandleGuildLeft;
    }

    public async Task AutoAcceptGuildInviteAsync(string expectedGuildName)
    {
        // Only accept invites from specific guilds
        _agentFactory.GuildAgent.GuildInviteReceived += async (inviterName, guildName) =>
        {
            if (guildName.Equals(expectedGuildName, StringComparison.OrdinalIgnoreCase))
            {
                await _agentFactory.GuildAgent.AcceptGuildInviteAsync();
            }
            else
            {
                await _agentFactory.GuildAgent.DeclineGuildInviteAsync();
            }
        };
    }

    public async Task ManageGuildBankAsync(ulong guildBankGuid)
    {
        if (!_agentFactory.GuildAgent.IsInGuild)
        {
            Console.WriteLine("Not in a guild");
            return;
        }

        await _agentFactory.GuildAgent.OpenGuildBankAsync(guildBankGuid);

        // Deposit valuable items to guild bank
        var valuableItemIds = new uint[] { 2589, 3858, 7912 }; // Linen, Mithril, Solid Stone
        foreach (var itemId in valuableItemIds)
        {
            var itemSlot = FindItemInInventory(itemId);
            if (itemSlot.HasValue)
            {
                await _agentFactory.GuildAgent.DepositItemToGuildBankAsync(
                    itemSlot.Value.BagId, itemSlot.Value.SlotId, 0, 0, 1); // Deposit to tab 0
            }
        }

        // Deposit excess gold (keep 50 gold)
        var currentGold = GetCurrentGold();
        if (currentGold > 500000) // 50 gold in copper
        {
            var depositAmount = currentGold - 500000;
            await _agentFactory.GuildAgent.DepositMoneyToGuildBankAsync(depositAmount);
        }

        await _agentFactory.GuildAgent.CloseGuildBankAsync();
    }

    public async Task PerformOfficerDutiesAsync()
    {
        if (!_agentFactory.GuildAgent.IsInGuild)
            return;

        // Update guild MOTD daily
        if (ShouldUpdateMOTD())
        {
            var motd = GenerateDailyMOTD();
            await _agentFactory.GuildAgent.SetGuildMOTDAsync(motd);
        }

        // Request guild roster to check member activity
        await _agentFactory.GuildAgent.RequestGuildRosterAsync();

        // Set notes for active members
        var activeMemberNotes = GetActiveMemberNotes();
        foreach (var (memberName, note) in activeMemberNotes)
        {
            await _agentFactory.GuildAgent.SetGuildMemberNoteAsync(memberName, note, false);
        }
    }

    public async Task InvitePlayersToGuildAsync(string[] playerNames)
    {
        if (!_agentFactory.GuildAgent.IsInGuild)
            return;

        foreach (var playerName in playerNames)
        {
            await _agentFactory.GuildAgent.InvitePlayerToGuildAsync(playerName);
            await Task.Delay(1000); // Respect rate limits
        }
    }

    private void HandleGuildInvite(string inviterName, string guildName)
    {
        Console.WriteLine($"Received guild invite from {inviterName} to join {guildName}");
    }

    private void HandleGuildJoined(uint guildId, string guildName)
    {
        Console.WriteLine($"Successfully joined guild: {guildName} (ID: {guildId})");
    }

    private void HandleGuildLeft(uint guildId, string reason)
    {
        Console.WriteLine($"Left guild {guildId}. Reason: {reason}");
    }

    // Helper methods - implement based on your game state access
    private (byte BagId, byte SlotId)? FindItemInInventory(uint itemId) => null;
    private uint GetCurrentGold() => 0;
    private bool ShouldUpdateMOTD() => DateTime.Now.Hour == 0; // Update at midnight
    private string GenerateDailyMOTD() => $"Guild MOTD for {DateTime.Now:yyyy-MM-dd}";
    private (string PlayerName, string Note)[] GetActiveMemberNotes() => Array.Empty<(string, string)>();
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

        // Check auction house for deals (periodically)
        var currentTime = DateTime.UtcNow;
        if (ShouldCheckAuctionHouse(currentTime))
        {
            var auctioneerGuid = FindNearestAuctioneer();
            if (auctioneerGuid != 0)
            {
                await _agentFactory.AuctionHouseAgent.QuickSearchAsync(auctioneerGuid, "Health Potion");
            }
        }

        // Bank valuable items when inventory is nearly full
        if (!_agentFactory.InventoryAgent.HasEnoughSpace(3))
        {
            var bankerGuid = FindNearestBanker();
            if (bankerGuid != 0)
            {
                await DepositValuableItemsAsync(bankerGuid);
            }
        }

        // Deposit excess gold periodically
        if (ShouldDepositGold(currentTime))
        {
            var bankerGuid = FindNearestBanker();
            if (bankerGuid != 0 && GetCurrentGold() > 100000) // Keep 10 gold, deposit rest
            {
                var excessGold = GetCurrentGold() - 100000;
                await _agentFactory.BankAgent.QuickDepositGoldAsync(bankerGuid, excessGold);
            }
        }

        // Check mail periodically
        if (ShouldCheckMail(currentTime))
        {
            var mailboxGuid = FindNearestMailbox();
            if (mailboxGuid != 0)
            {
                await CheckMailAsync(mailboxGuid);
            }
        }

        // Handle guild operations periodically
        if (_agentFactory.GuildAgent.IsInGuild)
        {
            // Check guild bank for items we need
            if (ShouldCheckGuildBank(currentTime))
            {
                var guildBankGuid = FindNearestGuildBank();
                if (guildBankGuid != 0)
                {
                    await CheckGuildBankAsync(guildBankGuid);
                }
            }

            // Update guild roster periodically
            if (ShouldUpdateGuildRoster(currentTime))
            {
                await _agentFactory.GuildAgent.RequestGuildRosterAsync();
            }
        }
        else
        {
            // Auto-accept guild invites from approved guilds
            var approvedGuilds = GetApprovedGuildNames();
            if (approvedGuilds.Length > 0)
            {
                // This would be handled by the event system
                // The guild invite handling is done through events
            }
        }
    }

    // Helper methods - implement based on your game state access
    private ulong FindNearestEnemy() => 0;
    private ulong[] FindLootTargets() => Array.Empty<ulong>();
    private bool IsHealthLow() => false;
    private bool ShouldCheckAuctionHouse(DateTime currentTime) => false;
    private ulong FindNearestAuctioneer() => 0;
    private ulong FindNearestBanker() => 0;
    private bool ShouldDepositGold(DateTime currentTime) => false;
    private uint GetCurrentGold() => 0;
    private bool ShouldCheckMail(DateTime currentTime) => false;
    private ulong FindNearestMailbox() => 0;

    private bool ShouldCheckGuildBank(DateTime currentTime) => false;
    private ulong FindNearestGuildBank() => 0;
    private bool ShouldUpdateGuildRoster(DateTime currentTime) => false;
    private string[] GetApprovedGuildNames() => Array.Empty<string>();

    private async Task CheckGuildBankAsync(ulong guildBankGuid)
    {
        await _agentFactory.GuildAgent.OpenGuildBankAsync(guildBankGuid);
        
        // Withdraw needed consumables from guild bank
        var neededItems = GetNeededConsumables();
        foreach (var (tabIndex, slotIndex, quantity) in neededItems)
        {
            await _agentFactory.GuildAgent.WithdrawItemFromGuildBankAsync(tabIndex, slotIndex, quantity);
        }
        
        await _agentFactory.GuildAgent.CloseGuildBankAsync();
    }    }