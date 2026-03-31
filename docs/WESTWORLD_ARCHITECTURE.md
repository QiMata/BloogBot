# Westworld of Warcraft — System Architecture

## Vision

3000 bots distributed evenly across Horde and Alliance, covering all race/class/spec combinations. Each bot has a character build config defining its end-state goals (BiS gear, PvP rank, professions, reputation). The system operates autonomously — bots group, quest, dungeon, raid, PvP, and trade with each other AND with human players. Human players experience a full, living server where they can always find groups, always find auction house activity, and always have opponents in battlegrounds.

## Architecture Layers

```
┌─────────────────────────────────────────────────────┐
│                  Human Players                       │
│        (request groups, trade, PvP, quests)          │
└──────────────────────┬──────────────────────────────┘
                       │ Activity Snapshots
┌──────────────────────▼──────────────────────────────┐
│              DecisionEngineService                   │
│  - Reads ALL bot snapshots + human activity          │
│  - Matches human requests to available bots          │
│  - Evaluates each bot's progress toward goals        │
│  - Decides: "Bot X should do Y next"                 │
│  - Outputs: high-level objectives per bot            │
└──────────────────────┬──────────────────────────────┘
                       │ Objectives (QuestChain, Dungeon, BG, Grind, Trade)
┌──────────────────────▼──────────────────────────────┐
│                 StateManager                         │
│  - Translates objectives into action sequences       │
│  - Coordinates multi-bot activities (groups, raids)   │
│  - Manages bot lifecycle (login, teleport, level)     │
│  - Detects human player requests from chat/LFG        │
│  - Dispatches ActionMessages to bots via IPC          │
│                                                       │
│  Coordinators:                                        │
│    - DungeoneeringCoordinator (group → dungeon)       │
│    - BattlegroundCoordinator (queue → BG → objectives)│
│    - RaidCoordinator (40-man formation → raid)        │
│    - QuestCoordinator (shared quest objectives)       │
│    - ProgressionPlanner (goal evaluation)             │
└──────────────────────┬──────────────────────────────┘
                       │ ActionMessages (Goto, CastSpell, JoinBG, etc.)
┌──────────────────────▼──────────────────────────────┐
│                   BotRunner                          │
│  - Receives ActionMessages from StateManager          │
│  - Constructs behavior trees from actions             │
│  - Pushes BotTasks onto the task stack                │
│  - Tasks execute autonomously until complete           │
│                                                       │
│  Task Library:                                        │
│    - DungeoneeringTask (waypoint nav + group combat)  │
│    - BattlegroundQueueTask (NPC interact → queue)     │
│    - QuestingTask (accept → objectives → turn in)     │
│    - GatheringRouteTask (node detection → gather)     │
│    - TravelTask (cross-map routing)                   │
│    - CombatRotationTask (class-specific PvE/PvP)      │
│    - VendorVisitTask, TrainerVisitTask, etc.          │
│                                                       │
│  BotProfiles (27 class/spec rotations):               │
│    - PullTargetTask (ranged pull in dungeons)          │
│    - PvERotationTask (combat rotation)                │
│    - RestTask, BuffTask, HealTask                     │
└──────────────────────┬──────────────────────────────┘
                       │ WoW Protocol (CMSG/SMSG packets)
┌──────────────────────▼──────────────────────────────┐
│              WoWSharpClient / WoW.exe                │
│  - BG: pure C# protocol emulation (headless)         │
│  - FG: DLL injection into WoW.exe (viewport)         │
│  - Both implement IObjectManager                     │
│  - Both use same BotRunner behavior trees             │
└─────────────────────────────────────────────────────┘
```

## Data Flow

### Bot → StateManager → DecisionEngine → StateManager → Bot

1. **Bot sends snapshot** (position, health, targets, inventory, quests, skills) via async TCP
2. **StateManager stores snapshot** in ConcurrentDictionary, makes available to all coordinators
3. **DecisionEngine reads all snapshots** periodically, evaluates each bot's progress toward its CharacterBuildConfig goals
4. **DecisionEngine outputs objectives**: "Bot ORWA0001 should queue for WSG" or "Bot TADR0015 should farm Stratholme for Cape of the Black Baron"
5. **StateManager translates objective** into ActionMessages: TeleportTo(Orgrimmar) → JoinBattleground(WSG) → idle
6. **Bot executes actions** via behavior tree tasks, sends updated snapshot next tick

### Human Player → DecisionEngine → StateManager → Bots

1. **Human player types** `/lfg RFC` or uses meeting stone or asks in chat
2. **DecisionEngine detects** the LFG request from chat message parsing in nearby bot snapshots
3. **DecisionEngine selects** appropriate bots (right level, role composition: tank + healer + 3 DPS)
4. **StateManager forms group**: selected bots get SendGroupInvite/AcceptGroupInvite actions
5. **StateManager pushes DungeoneeringTask** to all group members
6. **Bots travel to dungeon**, enter, and clear — human player participates as a real group member

## Character Build Config

Each bot has a `CharacterBuildConfig` in its settings:

```json
{
  "SpecName": "WarriorFury",
  "TalentBuildName": "FuryPvE",
  "TargetGearSet": [
    { "Slot": "Head", "ItemId": 12640, "Name": "Lionheart Helm", "Source": "Craft" },
    { "Slot": "Neck", "ItemId": 18404, "Name": "Onyxia Tooth Pendant", "Source": "Boss:Onyxia" }
  ],
  "ReputationGoals": [
    { "FactionId": 529, "FactionName": "Argent Dawn", "TargetStanding": "Exalted" }
  ],
  "MountGoal": { "Type": "EpicMount", "GoldCostCopper": 9000000 },
  "GoldTargetCopper": 10000000,
  "SkillPriorities": ["Mining:300", "Engineering:300"],
  "QuestChains": ["OnyxiaAttunement", "MoltenCoreAttunement"],
  "PvPGoals": { "TargetRank": 10, "BattlegroundPreference": "WSG" }
}
```

## Test Architecture

Tests validate each layer independently AND end-to-end:

### Unit Tests (no server needed)
- **BotTask tests**: DungeoneeringTask, BattlegroundQueueTask, QuestingTask — mock ObjectManager, verify state transitions
- **Coordinator tests**: mock snapshots, verify action output
- **IPC pipeline tests**: socket round-trip, compression, concurrency
- **Data tests**: waypoints, dungeon entries, raid entries, battlemaster positions

### Integration Tests (live MaNGOS server)
- **Single-bot**: teleport, combat, vendor, trainer, flight master, gathering, fishing
- **Dual-client parity**: FG vs BG on same operation, compare results
- **Multi-bot coordination**: group formation, dungeon entry, raid formation
- **Battleground**: both factions queue, enter BG, play objectives

### System Tests (full stack)
- **RFC 10-man**: group → prep → dungeon → clear
- **WSG 20-man**: 2 factions → queue → enter → objectives
- **3000-bot load**: connection scaling, snapshot throughput, latency

## What Exists vs What's Needed

### EXISTS (implemented and tested)
- BotRunner behavior tree execution with 27 class/spec profiles
- DungeoneeringTask with waypoint navigation and coordinated combat
- All NPC interaction tasks (vendor, trainer, flight master, gossip, quest)
- BattlegroundNetworkClientComponent (queue, accept, leave, scoreboard)
- BattlegroundQueueTask (find NPC → interact → queue → accept → enter)
- Group/raid formation via PartyNetworkClientComponent
- Cross-map routing (boats, zeppelins, elevators, dungeon portals)
- Async TCP pipeline (3000 clients, P99<200ms at 500 clients)
- IPC contract tests, load tests, DungeoneeringTask unit tests
- Static data for all 26 dungeons, 7 raids, 6 battlemasters

### NEEDS BUILDING

#### StateManager Coordinators
- **BattlegroundCoordinator** — like DungeoneeringCoordinator but for BG lifecycle:
  teleport to faction city → level to BG minimum → form group → navigate to BG master NPC →
  interact → queue → wait for invite → accept → dispatch BG objectives
- **QuestCoordinator** — assign quest chains to bots based on level and goals
- **RaidCoordinator** — 40-man formation, subgroups, role assignment, ready check

#### DecisionEngine Integration
- **Objective evaluation loop** — read all snapshots, compare to CharacterBuildConfig, output next objective
- **Human request detection** — parse chat for LFG patterns, meeting stone interaction
- **Bot selection for groups** — level/gear/spec matching, travel distance consideration
- **Activity prioritization** — survival > training > gear > attunement > rep > mount > gold > profession

#### BotRunner Tasks
- **QuestingTask** — accept → travel to objectives → kill/collect → return → turn in
- **TravelTask** — decompose cross-map travel into legs (walk, flight, transport, portal)
- **FarmBossTask** — repeated dungeon clear for specific drops
- **AuctionHouseTask** — scan, post, buy, cancel
- **PvP objectives** — flag capture (WSG), node assault (AB), tower/GY (AV)

#### Character Progression
- **CharacterBuildConfig** in settings JSON
- **GearGoal, ReputationGoal, ItemGoal, MountGoal** models
- **ProgressionPlanner** — evaluates goals, picks highest-priority activity
- **Pre-built templates** — FuryWarriorPreRaid, HolyPriestMCReady, etc.

#### Infrastructure
- **2 FG bots for BG tests** — one Horde viewport, one Alliance viewport
- **Singleton removal** (P9.2-9.4) for multi-bot-per-process
- **Connection multiplexing** for 3000-bot TCP efficiency
