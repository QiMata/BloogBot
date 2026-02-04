# WoWSharpClient Network Agents

This document describes the network agent architecture for WoWSharpClient, which provides specialized agents for different game operations in World of Warcraft.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Network Agents](#network-agents)
- [Factory Methods](#factory-methods)
- [Integration Examples](#integration-examples)
- [Performance Considerations](#performance-considerations)
- [Testing Strategy](#testing-strategy)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)

## Overview

The network agents follow a consistent naming pattern of `{Name}NetworkClientComponent` and provide focused functionality for specific game operations:

### Core Agents
- **TargetingNetworkClientComponent** - Handles target selection and assist functionality
- **AttackNetworkClientComponent** - Manages auto-attack operations
- **LootingNetworkClientComponent** - Manages looting operations and loot windows
- **QuestNetworkClientComponent** - Handles quest interactions and management

### Communication Agents
- **ChatNetworkClientComponent** - Handles sending and receiving chat messages, specialized channels, commands, and reactive observables for chat events

### Interaction Agents
- **GameObjectNetworkClientComponent** - Handles interactions with game objects (chests, nodes, doors, etc.)
- **VendorNetworkClientComponent** - Manages vendor interactions, buying, selling, and item repairs
- **FlightMasterNetworkClientComponent** - Handles taxi system and flight path operations
- **TrainerNetworkClientComponent** - Handles class trainer interactions, learning spells, abilities, and skills from NPC trainers

### Character Management Agents
- **InventoryNetworkClientComponent** - Manages inventory operations, item movement, and bag management
- **ItemUseNetworkClientComponent** - Handles item usage, consumables, and item interactions
- **EquipmentNetworkClientComponent** - Manages equipment operations, equipping, and durability tracking
- **SpellCastingNetworkClientComponent** - Handles spell casting, channeling, and spell state management
- **TalentNetworkClientComponent** - Manages talent point allocation, talent builds, and respec operations
- **DeadActorAgent** - Manages death, resurrection, and corpse recovery operations

### Economic Agents
- **AuctionHouseNetworkClientComponent** - Manages auction house operations, browsing, bidding, and posting auctions
- **BankNetworkClientComponent** - Handles personal bank access, depositing and withdrawing items or gold
- **MailNetworkClientComponent** - Manages mail system interactions, sending mail to other players and retrieving mail from mailboxes

### Social Agents
- **GuildNetworkClientComponent** - Manages guild operations, invites, guild bank interactions, and member management
- **PartyNetworkClientComponent** - Manages party/raid group operations, invites, member management, loot settings, and leadership
- **EmoteNetworkClientComponent** - Manages character emotes and animations via network packets

### Profession Agents
- **ProfessionsNetworkClientComponent** - Handles profession skill training, crafting, and gathering interactions

## Architecture

### Core Principles

1. **Single Responsibility** - Each agent focuses on one specific aspect of game functionality
2. **Event-Driven** - Agents use events to communicate state changes and completion of operations
3. **Async Operations** - All network operations are asynchronous with proper cancellation support
4. **Testable** - Full dependency injection and interface-based design for easy testing
5. **Coordinated** - Agents can work together through shared interfaces
6. **Lazy Loading** - Agents are created only when needed through the NetworkClientComponentFactory
7. **Thread Safety** - All agents are designed to be thread-safe for concurrent operations
8. **Reactive Programming** - Advanced agents use reactive observables for better composability and filtering

### Agent Structure

Each network agent follows a consistent structure:

```csharp
public interface I{Name}NetworkClientComponent
{
    // Properties for current state
    bool IsOperationInProgress { get; }
    DateTime? LastOperationTime { get; }
    
    // Events for state changes (or reactive observables for advanced agents)
    event Action<StateChangeEventArgs> StateChanged;
    event Action<string> OperationError;
    
    // Async methods for operations
    Task PerformOperationAsync(params object[] parameters, CancellationToken cancellationToken = default);
    
    // Synchronous helper methods
    bool CanPerformOperation();
    ValidationResult ValidateOperation(params object[] parameters);
}

public class {Name}NetworkClientComponent : I{Name}NetworkClientComponent
{
    private readonly IWorldClient _worldClient;
    private readonly ILogger<{Name}NetworkClientComponent> _logger;
    
    // Constructor with IWorldClient and ILogger
    public {Name}NetworkClientComponent(IWorldClient worldClient, ILogger<{Name}NetworkClientComponent> logger)
    {
        _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // Implementation of all interface members
    // Public methods for server response handling
}
```

### Dependency Graph

```
NetworkClientComponentFactory
├── TargetingNetworkClientComponent
├── AttackNetworkClientComponent (depends on TargetingNetworkClientComponent)
├── ChatNetworkClientComponent (reactive observables)
├── QuestNetworkClientComponent
├── LootingNetworkClientComponent
├── GameObjectNetworkClientComponent
├── VendorNetworkClientComponent
├── FlightMasterNetworkClientComponent
├── DeadActorAgent
├── InventoryNetworkClientComponent
├── ItemUseNetworkClientComponent (coordinates with InventoryNetworkClientComponent)
├── EquipmentNetworkClientComponent (coordinates with InventoryNetworkClientComponent)
├── SpellCastingNetworkClientComponent (coordinates with TargetingNetworkClientComponent)
├── AuctionHouseNetworkClientComponent
├── BankNetworkClientComponent
├── MailNetworkClientComponent
├── GuildNetworkClientComponent
├── PartyNetworkClientComponent
├── TrainerNetworkClientComponent
├── TalentNetworkClientComponent
├── ProfessionsNetworkClientComponent
└── EmoteNetworkClientComponent
```

## Network Agents

### TargetingNetworkClientComponent

Manages target selection without combat functionality using reactive observables.

**Key Features:**
- **Reactive Programming**: Uses reactive observables instead of traditional events for better composability
- Set/clear target operations with validation
- Assist functionality for targeting what another player is targeting
- Target change events with detailed information
- Current target state tracking and caching
- Distance calculations and range validation

**Network Packets:**
- `CMSG_SET_SELECTION` - Sets the current target
- `CMSG_ASSIST` - Assists another player's target

**Reactive Observables:**
```csharp
// Observable streams for targeting operations
IObservable<TargetingData> TargetChanges;
IObservable<AssistData> AssistOperations;
IObservable<TargetingErrorData> TargetingErrors;
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Basic targeting
await agentFactory.TargetingAgent.SetTargetAsync(enemyGuid);
await agentFactory.TargetingAgent.ClearTargetAsync();

// Assist another player
await agentFactory.TargetingAgent.AssistAsync(playerGuid);

// Reactive event handling
agentFactory.TargetingAgent.TargetChanges
    .Subscribe(data => Console.WriteLine($"Target changed from {data.PreviousTarget:X} to {data.CurrentTarget:X}"));

// Monitor assist operations
agentFactory.TargetingAgent.AssistOperations
    .Subscribe(data => Console.WriteLine($"Assisted player {data.PlayerGuid:X}, now targeting {data.AssistTarget:X}"));

// Handle targeting errors
agentFactory.TargetingAgent.TargetingErrors
    .Subscribe(error => Console.WriteLine($"Targeting error: {error.ErrorMessage}"));
```

### AttackNetworkClientComponent

Handles auto-attack operations and combat state management using reactive observables.

**Key Features:**
- **Reactive Programming**: Uses reactive observables for real-time combat state monitoring
- Start/stop auto-attack with state validation
- Toggle attack state with safety checks
- Attack specific targets (coordinates with targeting agent)
- Attack events (started, stopped, error) with detailed information
- Attack state tracking and combat detection
- Weapon swing timer integration

**Network Packets:**
- `CMSG_ATTACKSWING` - Starts auto-attack on current target
- `CMSG_ATTACKSTOP` - Stops auto-attack

**Reactive Observables:**
```csharp
// Observable streams for attack operations
IObservable<AttackStateData> AttackStateChanges;
IObservable<WeaponSwingData> WeaponSwings;
IObservable<AttackErrorData> AttackErrors;
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Coordinated attack (targets and attacks in one operation)
await agentFactory.AttackAgent.AttackTargetAsync(enemyGuid, agentFactory.TargetingAgent);

// Manual attack control
await agentFactory.AttackAgent.StartAttackAsync();
await agentFactory.AttackAgent.StopAttackAsync();
await agentFactory.AttackAgent.ToggleAttackAsync();

// Reactive event handling
agentFactory.AttackAgent.AttackStateChanges
    .Subscribe(state => Console.WriteLine($"Attack state: {(state.IsAttacking ? "Started" : "Stopped")} attacking {state.VictimGuid:X}"));

// Monitor weapon swings
agentFactory.AttackAgent.WeaponSwings
    .Subscribe(swing => Console.WriteLine($"Weapon swing: {swing.Damage} damage to {swing.VictimGuid:X} (Critical: {swing.IsCritical})"));

// Handle attack errors
agentFactory.AttackAgent.AttackErrors
    .Subscribe(error => Console.WriteLine($"Attack error: {error.ErrorMessage}"));
```

### ChatNetworkClientComponent

Handles all chat operations including sending messages, receiving messages, managing channels, and executing commands. Uses reactive observables for advanced event handling and filtering.

**Key Features:**
- **Reactive Programming**: Uses reactive observables instead of traditional events for better composability
- **Specialized Channels**: Support for Say, Yell, Whisper, Party, Guild, Officer, Raid, Raid Warning, and custom channels
- **Advanced Commands**: Execute chat commands like /afk, /dnd, /join, /leave, /who, etc.
- **Rate Limiting**: Intelligent rate limiting per chat type to prevent being throttled by the server
- **Smart Filtering**: Pre-filtered observables for different message types (guild messages, whispers, etc.)
- **State Management**: Track AFK/DND status and custom messages
- **Channel Management**: Join, leave, and list channel members
- **Global Event Integration**: Connects to WoWSharpEventEmitter for seamless message handling
- **Thread Safety**: Fully thread-safe with concurrent collection support
- **Validation**: Message validation for length, destination requirements, etc.

**Network Packets:**
- `CMSG_MESSAGECHAT` - Sends chat messages to various channels
- `CMSG_JOIN_CHANNEL` - Joins a custom chat channel
- `CMSG_LEAVE_CHANNEL` - Leaves a chat channel
- `CMSG_CHANNEL_LIST` - Lists channel members

**Reactive Observables:**
```csharp
// Global message streams
IObservable<ChatMessageData> IncomingMessages;
IObservable<OutgoingChatMessageData> OutgoingMessages;
IObservable<ChatNotificationData> ChatNotifications;
IObservable<ChatCommandData> ExecutedCommands;

// Filtered message streams
IObservable<ChatMessageData> SayMessages;
IObservable<ChatMessageData> WhisperMessages;
IObservable<ChatMessageData> PartyMessages;
IObservable<ChatMessageData> GuildMessages;
IObservable<ChatMessageData> RaidMessages;
IObservable<ChatMessageData> ChannelMessages;
IObservable<ChatMessageData> SystemMessages;
IObservable<ChatMessageData> EmoteMessages;
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);
var chatAgent = agentFactory.ChatAgent;

// Basic messaging
await chatAgent.SayAsync("Hello World!");
await chatAgent.WhisperAsync("PlayerName", "Secret message");
await chatAgent.GuildAsync("Guild announcement");
await chatAgent.PartyAsync("Party message");

// Channel management
await chatAgent.JoinChannelAsync("LookingForGroup");
await chatAgent.ChannelAsync("LookingForGroup", "LF2M for dungeon");
await chatAgent.LeaveChannelAsync("LookingForGroup");

// Player state management
await chatAgent.SetAfkAsync("Gone for lunch, back in 30 minutes");
await chatAgent.SetDndAsync("Do not disturb - in important meeting");
await chatAgent.SetAfkAsync(); // Remove AFK status

// Command execution
await chatAgent.ExecuteCommandAsync("who", new[] { "80", "Stormwind" });
await chatAgent.ExecuteCommandAsync("time");
await chatAgent.ExecuteCommandAsync("guild", new[] { "info" });

// Reactive event handling - filter specific message types
chatAgent.GuildMessages
    .Where(msg => msg.Text.Contains("raid"))
    .Subscribe(msg => Console.WriteLine($"Raid message from {msg.SenderName}: {msg.Text}"));

// Listen for whispers
chatAgent.WhisperMessages
    .Subscribe(msg => Console.WriteLine($"Whisper from {msg.SenderName}: {msg.Text}"));

// Monitor chat notifications
chatAgent.ChatNotifications
    .Where(notif => notif.NotificationType == ChatNotificationType.ChannelJoined)
    .Subscribe(notif => Console.WriteLine($"Joined channel: {notif.ChannelName}"));

// Track outgoing messages
chatAgent.OutgoingMessages
    .Subscribe(msg => Console.WriteLine($"Sent [{msg.ChatType}]: {msg.Text}"));

// Advanced filtering - complex message processing
chatAgent.IncomingMessages
    .Where(msg => msg.ChatType == ChatMsg.CHAT_MSG_GUILD)
    .Where(msg => msg.Text.ToLower().Contains("help"))
    .Subscribe(msg => 
    {
        // Auto-respond to guild help requests
        chatAgent.WhisperAsync(msg.SenderName, "I saw your help request in guild chat!");
    });


// Rate limiting check
if (chatAgent.CanSendMessage(ChatMsg.CHAT_MSG_YELL))
{
    await chatAgent.YellAsync("Important announcement!");
}

// Validation
var validation = chatAgent.ValidateMessage(ChatMsg.CHAT_MSG_WHISPER, "Hello", "PlayerName");
if (validation.IsValid)
{
    await chatAgent.WhisperAsync("PlayerName", "Hello");
}

// Check active channels
var activeChannels = chatAgent.GetActiveChannels();
Console.WriteLine($"Currently in {activeChannels.Count} channels: {string.Join(", ", activeChannels)}");

// Check player state
if (chatAgent.IsAfk)
{
    Console.WriteLine($"Player is AFK: {chatAgent.AfkMessage}");
}
if (chatAgent.IsDnd)
{
    Console.WriteLine($"Player is DND: {chatAgent.DndMessage}");
}
```

**Advanced Chat Features:**
- **Smart Rate Limiting**: Different cooldowns for different chat types (Say: 1s, Yell: 2s, Whisper: 500ms, etc.)
- **Message Filtering**: Pre-built observables for specific message types enable easy filtering and processing
- **Command System**: Execute any WoW chat command with parameter support
- **State Tracking**: Monitor AFK/DND status and custom messages
- **Channel Coordination**: Automatic tracking of joined channels
- **Error Handling**: Comprehensive error handling and logging
- **Performance Optimized**: Lazy-loaded filtered observables for minimal memory usage
- **Integration Ready**: Seamlessly integrates with global WoWSharpEventEmitter system

### QuestNetworkClientComponent

Manages all quest-related operations with comprehensive quest state tracking using reactive observables.

**Key Features:**
- **Reactive Programming**: Uses reactive observables for real-time quest state monitoring
- Quest giver interactions (hello, status query) with dialogue handling
- Quest operations (accept, complete, query details) with validation
- Quest reward selection with optimization suggestions
- Quest log management and progress tracking
- Party quest sharing and coordination
- Quest chain detection and progression

**Network Packets:**
- `CMSG_QUESTGIVER_HELLO` - Initiates dialogue with quest giver
- `CMSG_QUESTGIVER_ACCEPT_QUEST` - Accepts a quest
- `CMSG_QUESTGIVER_COMPLETE_QUEST` - Completes a quest
- `CMSG_QUESTGIVER_CHOOSE_REWARD` - Selects quest reward

**Reactive Observables:**
```csharp
// Observable streams for quest operations
IObservable<QuestData> QuestOperations;
IObservable<QuestProgressData> QuestProgress;
IObservable<QuestRewardData> QuestRewards;
IObservable<QuestErrorData> QuestErrors;

// Pre-filtered observables for specific quest operations
IObservable<QuestData> QuestOffered;
IObservable<QuestData> QuestAccepted;
IObservable<QuestData> QuestCompleted;
IObservable<QuestData> QuestAbandoned;
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Complete quest workflow
await agentFactory.QuestAgent.HelloQuestGiverAsync(npcGuid);
await agentFactory.QuestAgent.AcceptQuestAsync(npcGuid, questId);

// Quest completion with reward selection
await agentFactory.QuestAgent.CompleteQuestAsync(npcGuid, questId);
await agentFactory.QuestAgent.ChooseQuestRewardAsync(npcGuid, questId, rewardIndex: 0);

// Party quest operations
await agentFactory.QuestAgent.PushQuestToPartyAsync(questId);

// Reactive event handling - monitor quest acceptance
agentFactory.QuestAgent.QuestAccepted
    .Subscribe(quest => Console.WriteLine($"Accepted quest: {quest.QuestTitle} ({quest.QuestId})"));

// Monitor quest completion
agentFactory.QuestAgent.QuestCompleted
    .Subscribe(quest => Console.WriteLine($"Completed quest: {quest.QuestTitle}"));

// Track quest progress
agentFactory.QuestAgent.QuestProgress
    .Subscribe(progress => Console.WriteLine($"Quest progress: {progress.QuestTitle} - {progress.CompletedObjectives}/{progress.TotalObjectives}"));

// Handle quest errors
agentFactory.QuestAgent.QuestErrors
    .Subscribe(error => Console.WriteLine($"Quest error: {error.ErrorMessage}"));

// Advanced filtering - auto-accept specific quests
agentFactory.QuestAgent.QuestOffered
    .Where(quest => quest.QuestTitle.Contains("Important"))
    .Subscribe(async quest => await agentFactory.QuestAgent.AcceptQuestAsync(quest.QuestGiverGuid, quest.QuestId));
```

### LootingNetworkClientComponent

Handles all looting operations including money, items, and group loot with smart filtering using reactive observables.

**Key Features:**
- **Reactive Programming**: Uses reactive observables for real-time loot monitoring
- Open/close loot windows with automatic detection
- Loot money and items with quality filtering
- Quick loot functionality (open, loot all, close) with customizable filters
- Group loot rolling (need/greed/pass) with intelligent decision making
- Loot window state tracking and caching
- Item value assessment for optimal looting decisions

**Network Packets:**
- `CMSG_LOOT` - Opens loot window
- `CMSG_LOOT_MONEY` - Loots money from corpse
- `CMSG_AUTOSTORE_LOOT_ITEM` - Loots specific item
- `CMSG_LOOT_ROLL` - Rolls on group loot

**Reactive Observables:**
```csharp
// Observable streams for loot operations
IObservable<LootWindowData> LootWindowChanges;
IObservable<LootData> ItemLoot;
IObservable<MoneyLootData> MoneyLoot;
IObservable<LootRollData> LootRolls;
IObservable<LootErrorData> LootErrors;

// Pre-filtered observables for loot window state
IObservable<LootWindowData> LootWindowOpened;
IObservable<LootWindowData> LootWindowClosed;
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Quick loot with filtering
await agentFactory.LootingAgent.QuickLootAsync(corpseGuid);

// Manual loot control
await agentFactory.LootingAgent.OpenLootAsync(corpseGuid);
await agentFactory.LootingAgent.LootMoneyAsync();
await agentFactory.LootingAgent.LootItemAsync(itemSlot);
await agentFactory.LootingAgent.CloseLootAsync();

// Group loot decisions
await agentFactory.LootingAgent.RollForLootAsync(lootGuid, itemSlot, LootRollType.Need);

// Reactive event handling - monitor valuable items
agentFactory.LootingAgent.ItemLoot
    .Where(loot => loot.Quality >= ItemQuality.Rare)
    .Subscribe(loot => Console.WriteLine($"Looted rare item: {loot.ItemName} x{loot.Quantity}"));

// Track money loot
agentFactory.LootingAgent.MoneyLoot
    .Subscribe(money => Console.WriteLine($"Looted {money.Amount} copper"));

// Monitor loot window state
agentFactory.LootingAgent.LootWindowOpened
    .Subscribe(window => Console.WriteLine($"Loot window opened for {window.LootTargetGuid:X} with {window.AvailableItems} items"));

// Handle loot errors
agentFactory.LootingAgent.LootErrors
    .Subscribe(error => Console.WriteLine($"Loot error: {error.ErrorMessage}"));

// Advanced filtering - auto-roll need on specific items
agentFactory.LootingAgent.LootRolls
    .Where(roll => roll.ItemId == targetItemId)
    .Subscribe(async roll => await agentFactory.LootingAgent.RollForLootAsync(roll.LootGuid, roll.ItemSlot, LootRollType.Need));
```

### GameObjectNetworkClientComponent

Manages interactions with game objects like chests, resource nodes, doors, and buttons.

**Key Features:**
- Generic game object interaction
- Specialized operations (open chest, gather node, use door, activate button)
- Smart interaction (automatically determines interaction type)
- Interaction distance calculations
- Game object state tracking

**Network Packets:**
- `CMSG_USE_GAMEOBJECT` - Interacts with a game object
- `CMSG_OPEN_CHEST` - Opens a chest
- `CMSG_GATHER_RESOURCE` - Gathers from a resource node

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Interact with game objects
await agentFactory.GameObjectAgent.UseGameObjectAsync(gameObjectGuid);

// Specialized actions
await agentFactory.GameObjectAgent.OpenChestAsync(chestGuid);
await agentFactory.GameObjectAgent.GatherFromNodeAsync(herbNodeGuid);

// Smart interaction
await agentFactory.GameObjectAgent.SmartInteractAsync(gameObjectGuid, GameObjectType.Chest);
```

### VendorNetworkClientComponent

Handles all vendor-related operations including buying, selling, and item repairs.

**Key Features:**
- Open/close vendor windows
- Purchase items from vendors
- Sell items to vendors
- Repair all equipped items
- Repair specific items
- Vendor window state tracking
- Purchase and sale transaction events

**Network Packets:**
- `CMSG_VENDORHELLO` - Opens vendor window
- `CMSG_BUY_ITEM` - Purchases an item
- `CMSG_SELL_ITEM` - Sells an item
- `CMSG_REPAIR_ITEM` - Repairs an item

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Open vendor and repair all items
await agentFactory.VendorAgent.OpenVendorAsync(npcGuid);
await agentFactory.VendorAgent.RepairAllItemsAsync();

// Buy and sell items
await agentFactory.VendorAgent.BuyItemAsync(itemId, quantity: 1);
await agentFactory.VendorAgent.SellItemAsync(bagSlot, quantity: 1);

// Close vendor
await agentFactory.VendorAgent.CloseVendorAsync();
```

### FlightMasterNetworkClientComponent

Handles taxi system and flight path operations.

**Key Features:**
- Query available flight paths and their statuses
- Request flight to a destination with proper fare deduction
- Event handling for flight status updates
- Integration with targeting and zone detection agents

**Network Packets:**
- `CMSG_TAXICLEARNODENAME` - Learns a new taxi node
- `CMSG_TAXIQUICKPATH` - Requests a flight using quick path
- `CMSG_ACTIVATE_TAXI` - Activates a taxi for the player

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Learn new taxi node
await agentFactory.FlightMasterAgent.LearnTaxiNodeAsync(nodeId);

// Quick flight to a known destination
await agentFactory.FlightMasterAgent.QuickFlightAsync(destinationGuid);

// Event handling for flight status
agentFactory.FlightMasterAgent.FlightStatusChanged += (status) =>
{
    Console.WriteLine($"Flight status changed: {status}");
};
```

### TrainerNetworkClientComponent

Handles class trainer interactions, learning spells, abilities, and skills from NPC trainers.

**Key Features:**
- Query available trainers and their teaching specialties
- Request to learn a spell or ability with correct reagent/gold handling
- Event handling for training status updates
- Integration with spell casting and talent agents

**Network Packets:**
- `CMSG_TRAINER_LIST` - Requests the list of available trainers
- `CMSG_TRAINER_BUY_SPELL` - Buys a spell or ability from the trainer
- `CMSG_TRAINER_STATS` - Requests the trainer's available stats to learn

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Get available trainers
var trainers = await agentFactory.TrainerAgent.GetAvailableTrainersAsync();

// Learn a spell from a trainer
await agentFactory.TrainerAgent.LearnSpellAsync(trainerGuid, spellId);

// Event handling for training completion
agentFactory.TrainerAgent.SpellLearned += (spellId) =>
{
    Console.WriteLine($"Successfully learned spell: {spellId}");
};
```

### TalentNetworkClientComponent

Manages talent point allocation, talent builds, and respec operations.

**Key Features:**
- Query available talents and current talent points
- Allocate or remove talent points with proper validation
- Reset talents and glyphs with full respec options
- Integration with character state and spell agents

**Network Packets:**
- `CMSG_TALENT_WIPE` - Resets talents and glyphs
- `CMSG_TALENT_POINT` - Allocates or removes a talent point

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Get available talents
var talents = await agentFactory.TalentAgent.GetAvailableTalentsAsync();

// Allocate a talent point
await agentFactory.TalentAgent.AllocateTalentPointAsync(talentId);

// Respec all talents
await agentFactory.TalentAgent.ResetTalentsAsync();
```

### DeadActorAgent

Manages death, resurrection, and corpse recovery operations.

**Key Features:**
- Detect death state and handle corpse retrieval
- Request resurrection at the corpse or graveyard
- Integration with looting and quest agents

**Network Packets:**
- `CMSG_RESURRECT` - Requests resurrection at a corpse
- `CMSG_RELEASE_CORPSE` - Releases the corpse to the graveyard

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Request resurrection at the corpse
await agentFactory.DeadActorAgent.ResurrectAsync(corpseGuid);

// Release the corpse and respawn at the graveyard
await agentFactory.DeadActorAgent.ReleaseCorpseAsync();
```

## Factory Methods

The `NetworkClientComponentFactory` is used to create and manage lifecycle of network agents.

```csharp
public class NetworkClientComponentFactory
{
    // Create methods for all agents
    public ITargetingNetworkClientComponent CreateTargetingAgent();
    public IAttackNetworkClientComponent CreateAttackAgent();
    public IChatNetworkClientComponent CreateChatAgent();
    public IQuestNetworkClientComponent CreateQuestAgent();
    // ... other agents
}
```

**Example Usage:**
```csharp
var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);

// Create agents
var targetingAgent = agentFactory.TargetingAgent;
var attackAgent = agentFactory.AttackAgent;
var chatAgent = agentFactory.ChatAgent;
```

## Integration Examples

### Bot Combat Sequence

```csharp
public async Task ExecuteCombatSequence(ulong enemyGuid)
{
    var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);
    
    // Target and attack enemy
    await agentFactory.TargetingAgent.SetTargetAsync(enemyGuid);
    await agentFactory.AttackAgent.StartAttackAsync();
    
    // Send combat announcement
    await agentFactory.ChatAgent.SayAsync("Engaging enemy!");
    
    // Continue combat logic...
}
```

### Advanced Chat Bot Integration

```csharp
public class ChatBot
{
    private readonly IChatNetworkClientComponent _chatAgent;
    
    public ChatBot(IChatNetworkClientComponent chatAgent)
    {
        _chatAgent = chatAgent;
        SetupChatHandlers();
    }
    
    private void SetupChatHandlers()
    {
        // Auto-respond to whispers
        _chatAgent.WhisperMessages
            .Where(msg => msg.Text.ToLower().Contains("hello"))
            .Subscribe(async msg => 
            {
                await _chatAgent.WhisperAsync(msg.SenderName, "Hello! How can I help you?");
            });
        
        // Guild chat monitoring
        _chatAgent.GuildMessages
            .Where(msg => msg.Text.ToLower().Contains("raid"))
            .Subscribe(msg => 
            {
                Console.WriteLine($"Raid announcement: {msg.Text}");
                // Auto-sign up logic
            });
        
        // Command processing
        _chatAgent.WhisperMessages
            .Where(msg => msg.Text.StartsWith("!"))
            .Subscribe(async msg => await ProcessCommand(msg));
    }
    
    private async Task ProcessCommand(ChatMessageData message)
    {
        var command = message.Text.Substring(1).ToLower();
        switch (command)
        {
            case "time":
                await _chatAgent.WhisperAsync(message.SenderName, $"Server time: {DateTime.Now}");
                break;
            case "status":
                await _chatAgent.WhisperAsync(message.SenderName, "Bot is online and operating normally");
                break;
            case "help":
                await _chatAgent.WhisperAsync(message.SenderName, "Available commands: !time, !status, !help");
                break;
        }
    }
}
```

### Multi-Agent Coordination

```csharp
public async Task ExecuteQuesting()
{
    var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);
    
    // Accept quest
    await agentFactory.QuestAgent.HelloQuestGiverAsync(questGiverGuid);
    await agentFactory.QuestAgent.AcceptQuestAsync(questGiverGuid, questId);
    
    // Announce quest start
    await agentFactory.ChatAgent.SayAsync($"Starting quest: {questName}");
    
    // Find and loot required items
    await agentFactory.TargetingAgent.SetTargetAsync(mobGuid);
    await agentFactory.AttackAgent.StartAttackAsync();
    
    // After combat, loot
    await agentFactory.LootingAgent.QuickLootAsync(corpseGuid);
    
    // Complete quest
    await agentFactory.QuestAgent.CompleteQuestAsync(questGiverGuid, questId);
    
    // Announce completion
    await agentFactory.ChatAgent.SayAsync($"Completed quest: {questName}");
}
```

### Multi-Agent Coordination with Reactive Observables

```csharp
public async Task ExecuteAdvancedQuesting()
{
    var agentFactory = WoWClientFactory.CreateNetworkClientComponentFactory(worldClient, loggerFactory);
    
    // Set up reactive quest workflow
    agentFactory.QuestAgent.QuestOffered
        .Where(quest => quest.QuestTitle.Contains("Kill"))
        .Subscribe(async quest =>
        {
            await agentFactory.QuestAgent.AcceptQuestAsync(quest.QuestGiverGuid, quest.QuestId);
            await agentFactory.ChatAgent.SayAsync($"Starting quest: {quest.QuestTitle}");
        });

    // Auto-combat when targeting enemies
    agentFactory.TargetingAgent.TargetChanges
        .Where(target => target.CurrentTarget.HasValue)
        .Subscribe(async target =>
        {
            if (IsEnemyTarget(target.CurrentTarget.Value))
            {
                await agentFactory.AttackAgent.StartAttackAsync();
            }
        });

    // Auto-loot after combat
    agentFactory.AttackAgent.AttackStateChanges
        .Where(state => !state.IsAttacking && state.VictimGuid.HasValue)
        .Subscribe(async state =>
        {
            await Task.Delay(1000); // Wait for loot to become available
            await agentFactory.LootingAgent.QuickLootAsync(state.VictimGuid.Value);
        });

    // Quest completion monitoring
    agentFactory.QuestAgent.QuestCompleted
        .Subscribe(async quest =>
        {
            await agentFactory.ChatAgent.SayAsync($"Completed quest: {quest.QuestTitle}");
        });

    // Start quest interaction
    await agentFactory.QuestAgent.HelloQuestGiverAsync(questGiverGuid);
}

private bool IsEnemyTarget(ulong targetGuid)
{
    // Implementation to check if target is an enemy
    return true; // Simplified for example
}
```

### Advanced Reactive Chat Bot

```csharp
public class AdvancedChatBot
{
    private readonly IChatNetworkClientComponent _chatAgent;
    private readonly ITargetingNetworkClientComponent _targetingAgent;
    private readonly IAttackNetworkClientComponent _attackAgent;
    
    public AdvancedChatBot(INetworkClientComponentFactory agentFactory)
    {
        _chatAgent = agentFactory.ChatAgent;
        _targetingAgent = agentFactory.TargetingAgent;
        _attackAgent = agentFactory.AttackAgent;
        SetupReactiveHandlers();
    }
    
    private void SetupReactiveHandlers()
    {
        // Command processing with reactive chains
        _chatAgent.WhisperMessages
            .Where(msg => msg.Text.StartsWith("!"))
            .Subscribe(async msg => await ProcessCommand(msg));

        // Auto-respond to guild help requests
        _chatAgent.GuildMessages
            .Where(msg => msg.Text.ToLower().Contains("help"))
            .Where(msg => msg.SenderName != "MyBotName") // Don't respond to self
            .Subscribe(async msg => 
            {
                await _chatAgent.WhisperAsync(msg.SenderName, "I saw your help request in guild chat! How can I assist?");
            });

        // Combat status announcements
        _attackAgent.AttackStateChanges
            .Where(state => state.IsAttacking)
            .Subscribe(async state =>
            {
                await _chatAgent.PartyAsync($"Engaging enemy {state.VictimGuid:X}!");
            });

        // Target change announcements for debugging
        _targetingAgent.TargetChanges
            .Where(target => target.CurrentTarget.HasValue)
            .Subscribe(async target =>
            {
                await _chatAgent.SayAsync($"Now targeting {target.CurrentTarget:X}");
            });

        // Error monitoring and reporting
        var allErrors = Observable.Merge(
            _chatAgent.ChatErrors.Select(e => $"Chat: {e.ErrorMessage}"),
            _targetingAgent.TargetingErrors.Select(e => $"Targeting: {e.ErrorMessage}"),
            _attackAgent.AttackErrors.Select(e => $"Attack: {e.ErrorMessage}")
        );

        allErrors.Subscribe(async error =>
        {
            await _chatAgent.SayAsync($"Error occurred: {error}");
        });
    }
    
    private async Task ProcessCommand(ChatMessageData message)
    {
        var parts = message.Text.Substring(1).Split(' ');
        var command = parts[0].ToLower();
        
        switch (command)
        {
            case "target":
                if (parts.Length > 1 && ulong.TryParse(parts[1], out var targetGuid))
                {
                    await _targetingAgent.SetTargetAsync(targetGuid);
                    await _chatAgent.WhisperAsync(message.SenderName, $"Targeting {targetGuid:X}");
                }
                break;
                
            case "attack":
                await _attackAgent.StartAttackAsync();
                await _chatAgent.WhisperAsync(message.SenderName, "Attack started!");
                break;
                
            case "stop":
                await _attackAgent.StopAttackAsync();
                await _chatAgent.WhisperAsync(message.SenderName, "Attack stopped!");
                break;
                
            case "status":
                var status = $"Target: {_targetingAgent.CurrentTarget:X}, Attacking: {_attackAgent.IsAttacking}";
                await _chatAgent.WhisperAsync(message.SenderName, status);
                break;
        }
    }
}
```

## Performance Considerations

### Reactive Observables
- Use reactive observables (like in ChatNetworkClientComponent) for better performance and memory management
- Lazy-loaded filtered observables reduce memory usage until actually needed
- Proper disposal of observables prevents memory leaks

### Rate Limiting
- ChatNetworkClientComponent implements intelligent rate limiting to prevent server throttling
- Different cooldowns for different message types optimize throughput

### Thread Safety
- All agents are designed to be thread-safe
- Use concurrent collections where appropriate
- Proper locking mechanisms for state changes

### Memory Management
- Agents implement IDisposable for proper resource cleanup
- Lazy initialization reduces initial memory footprint
- Event handler cleanup prevents memory leaks

## Testing Strategy

### Unit Testing
```csharp
[Test]
public async Task ChatAgent_SendMessage_ShouldRespectRateLimit()
{
    // Arrange
    var mockWorldClient = new Mock<IWorldClient>();
    var mockLogger = new Mock<ILogger<ChatNetworkClientComponent>>();
    var chatAgent = new ChatNetworkClientComponent(mockWorldClient.Object, mockLogger.Object);
    
    // Act & Assert
    await chatAgent.SayAsync("First message");
    
    // Should be rate limited
    var canSend = chatAgent.CanSendMessage(ChatMsg.CHAT_MSG_SAY);
    Assert.False(canSend);
}
```

### Integration Testing
```csharp
[Test]
public async Task AgentFactory_ShouldCreateAllAgents()
{
    // Arrange
    var worldClient = CreateMockWorldClient();
    var loggerFactory = CreateMockLoggerFactory();
    
    // Act
    var factory = new NetworkClientComponentFactory(worldClient, loggerFactory);
    
    // Assert
    Assert.NotNull(factory.TargetingAgent);
    Assert.NotNull(factory.ChatAgent);
    Assert.NotNull(factory.AttackAgent);
    // ... test all agents
}
```

## Troubleshooting

### Chat Agent Issues
- **Messages not sending**: Check rate limiting with `CanSendMessage()`
- **No incoming messages**: Verify WoWSharpEventEmitter integration
- **Memory leaks**: Ensure proper disposal of reactive subscriptions

### Agent Factory Issues
- **Null reference exceptions**: Verify logger factory configuration
- **Agent not created**: Check lazy initialization and thread safety

### Performance Issues
- **High memory usage**: Check for proper disposal of agents and observables
- **Slow response**: Verify rate limiting and async operation patterns

## Best Practices

### Reactive Observable Usage
1. **Use reactive observables** for event handling instead of traditional events
2. **Filter early** - use `Where()` to filter streams before processing
3. **Compose streams** - use `Observable.Merge()`, `Observable.CombineLatest()` for complex scenarios
4. **Dispose properly** - ensure all subscriptions are disposed to prevent memory leaks
5. **Handle errors** - use error handling operators or try-catch in Subscribe()

### Chat Agent Usage
1. **Use reactive observables** for event handling instead of traditional events
2. **Filter early** - use pre-filtered observables (e.g., `GuildMessages`) for better performance
3. **Respect rate limits** - always check `CanSendMessage()` before sending
4. **Validate messages** - use `ValidateMessage()` for better error handling
5. **Dispose properly** - ensure all subscriptions are disposed to prevent memory leaks

### Targeting and Attack Agent Coordination
1. **Use reactive chains** - connect targeting changes to attack operations
2. **Handle state transitions** - monitor both targeting and attack state changes
3. **Coordinate with other agents** - use attack state to trigger looting operations
4. **Error handling** - monitor error streams for both agents

### Quest and Loot Agent Integration
1. **Chain operations** - use quest completion to trigger loot operations
2. **Filter by quality** - use reactive filtering for valuable loot items
3. **Auto-progression** - connect quest acceptance to combat and looting workflows
4. **Progress monitoring** - track quest progress through reactive streams

### General Agent Usage
1. **Use the factory pattern** - Always create agents through NetworkClientComponentFactory
2. **Handle exceptions** - All async operations can throw exceptions
3. **Implement proper logging** - Use structured logging for better debugging
4. **Test thoroughly** - Unit test all agent interactions and reactive chains
5. **Monitor performance** - Watch for memory leaks and slow operations

### Integration Patterns
1. **Coordinate agents** - Use reactive observables to coordinate multiple agents
2. **Share state carefully** - Be mindful of shared state between agents
3. **Use cancellation tokens** - Support cancellation for long-running operations
4. **Implement retry logic** - Handle transient network errors gracefully
5. **Create reactive chains** - Build complex workflows using observable composition

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*