# MMO Bot System — Comprehensive Feature & Scale Analysis Prompt

**Hand this prompt to a Claude agent working on any MMO bot/emulation project targeting a privately hosted server.**

---

## Your Mission

You are analyzing a codebase that implements bot automation for a privately hosted MMO game server. The end-state is a system that can:

1. **Simulate 3,000+ concurrent player characters** connecting to the game server
2. **Participate in ALL player-character activities** the game supports (combat, PvP, raids, economy, social, crafting, questing, travel, progression)
3. **Behave indistinguishably from human players** in terms of protocol compliance, timing, and decision-making
4. **Scale horizontally** across multiple machines with minimal coordination overhead

Your job is to perform a **thorough gap analysis** and then **write detailed implementation specs** for everything that's missing, so that multiple agents can work in parallel to finish the project.

---

## Phase 1: Codebase Exploration (DO NOT WRITE CODE)

Perform these explorations **in parallel** using subagents:

### 1A — Client/Protocol Layer
Catalog every capability the bot client has for communicating with the game server:
- **Every packet/opcode sent** (client → server): list opcode name, handler file, what it does
- **Every packet/opcode received** (server → client): list opcode name, handler file, what it parses
- **Stub/empty handlers**: packets that are defined but have no implementation
- **Missing handlers**: packets the game protocol requires but that aren't even defined
- **Authentication flow**: full connection lifecycle from TCP connect to in-world
- **Encryption/session management**: how session keys are managed

### 1B — Game State Management
Catalog how the bot tracks game world state:
- **Object manager**: how game objects (players, NPCs, items, world objects) are tracked
- **Player state**: health, mana, position, buffs, debuffs, equipment, inventory, skills, spells
- **World state**: zone, map, weather, time, PvP status, battleground status
- **Static vs instance fields**: identify ALL singletons, static state, and shared mutable state
- **Thread safety**: locks, concurrent collections, race conditions

### 1C — Action/Behavior Layer
Catalog every action the bot can perform:
- **All action types** (enum values, dispatch table)
- **All behavior tree sequences** or state machine states
- **All bot tasks** (long-running behaviors like fishing, grinding, dungeoneering)
- **Dual-path actions**: actions that work differently depending on bot mode (injected vs headless, etc.)
- **Missing actions**: game activities that have no corresponding action type

### 1D — Service Architecture
Map the full service topology:
- **Every service/process**: what it does, what port it listens on, how it communicates
- **IPC protocol**: message format, serialization, sync vs async
- **Thread model per service**: thread-per-connection? async? thread pool?
- **Data flow**: how game state flows from server → bot → orchestrator → decision engine
- **Singleton/shared state per service**: what prevents N instances per process

### 1E — Test Coverage
Catalog every test:
- **Test projects**: count, focus area, framework
- **Integration tests**: what scenarios are validated against a live server
- **Missing test coverage**: which gameplay systems have zero tests
- **Skipped tests and why**: known crashes, infrastructure issues, etc.

### 1F — Combat Profiles / Class Behavior
If the game has character classes/specs:
- **Which classes/specs have profiles**: list each with completeness level
- **What each profile implements**: rotation, buffs, rest, PvP vs PvE
- **Missing profiles**: classes/specs with no implementation

---

## Phase 2: Gap Analysis

Using the exploration results, identify gaps in these categories:

### 2A — Protocol Completeness
Compare implemented opcodes against the full game protocol specification. For each missing opcode:
- Is it required for basic gameplay?
- Is it required for a specific system (PvP, raids, economy)?
- Does the opcode exist in the enum but have no handler?

### 2B — Gameplay System Completeness
Check EVERY player activity the game supports against the codebase. Common MMO systems:

| System | Check For |
|--------|-----------|
| **Combat** | Auto-attack, abilities, spell casting, combo points, stances, forms |
| **PvP** | Duels, open-world PvP, battlegrounds/arenas, honor/ranking |
| **Group Content** | Party formation, raid formation (10/20/25/40-man), subgroups, roles |
| **Raid Mechanics** | Threat, positioning, boss phases, debuff management, cooldown rotation |
| **Questing** | Accept, track objectives (kill/collect/explore), turn in, chains, escorts |
| **Professions** | Gathering (mining, herbs, skinning), crafting (all trades), secondary (cooking, first aid, fishing) |
| **Economy** | Vendor buy/sell, auction house, mail, bank, trading between players |
| **Social** | Chat channels, whispers, emotes, guild, friends/ignore |
| **Pets/Minions** | Summon, dismiss, feed, train abilities, stance control, action bar |
| **Equipment** | Equip/unequip, durability, repair, gear evaluation, ammo/reagents |
| **Travel** | Flight paths, mounts, hearthstone, boats/zeppelins/transports, portals |
| **Character Progression** | Leveling, talent/skill allocation, trainer visits, zone routing |
| **Death/Resurrection** | Release spirit, corpse run, spirit healer, resurrection spells |
| **Consumables/Buffs** | Food/drink, potions, scrolls, world buffs, class buffs |

For each system, report: what exists, what's partially implemented, what's completely missing.

### 2C — Scalability Bottlenecks
Analyze for scaling to 3,000 concurrent bots:

1. **Process model**: 1 bot per process? N bots per process? What's the memory overhead per bot?
2. **Singletons**: Any static singletons that prevent multiple bot instances per process?
3. **Socket/connection model**: Thread-per-connection? Async I/O? Backlog limits?
4. **IPC pattern**: Synchronous blocking? Async? Batched? What's the per-tick latency?
5. **Centralized services**: Is pathfinding/physics/AI centralized? Can it handle N concurrent requests?
6. **Network bandwidth**: What's the per-bot data rate? Multiply by 3,000.
7. **Memory ceiling**: Per-bot RAM × 3,000. Does it fit on reasonable hardware?
8. **Thread limits**: Total OS threads needed. Does it exceed practical limits (~1000-2000)?

### 2D — Bot Mode Parity
If the system has multiple bot modes (e.g., injected into game client vs. headless protocol emulation):
- Which actions work in BOTH modes?
- Which actions ONLY work in one mode?
- Which actions will CRASH in the other mode (null references, missing state)?

---

## Phase 3: Documentation Updates (DO NOT SKIP)

Update ALL project documentation (except physics/movement engine docs if another agent owns those):

1. **Architecture doc**: Update every service description, add missing services, fix outdated descriptions, update test project counts
2. **Technical notes**: Add known issues, parity gaps, scalability constraints
3. **Development guide**: Fix outdated tool versions, solution names, project tables
4. **Task tracking**: Add every identified gap as a new task with detailed spec
5. **Behavior matrix**: Update with current system state and gap summary

---

## Phase 4: Write Implementation Specs

For every gap identified, write a task spec with this format:

```
| # | Task | Spec |
|---|------|------|
| X.Y | **Short title** — One-line summary. File: `path/to/file.cs`. Method: `MethodName()`.
Sends `OPCODE_NAME`. Uses existing `ExistingClass.Method()` for Y.
Test: `Tests/Project/TestFile.cs`. | Open |
```

**Each spec MUST include:**
- Exact file path where the code should go
- Which existing classes/methods to use or extend
- Which opcodes/packets are involved (if network)
- What the test should assert
- Dependencies on other tasks (if any)

**Organize specs into phases by dependency order:**
1. **Parity fixes** (crashes, null guards) — smallest blast radius, do first
2. **Scalability refactoring** (singleton removal, async I/O) — architectural foundation
3. **Missing gameplay systems** (BG, raids, PvP, questing, etc.) — can parallelize across agents
4. **Automation & strategy** (AH posting, talent allocation, zone routing) — higher-level behavior
5. **Load testing** — validates all of the above at scale

**Within each phase, tasks should be parallelizable.** Two agents should be able to work on different tasks in the same phase without merge conflicts. Avoid tasks that touch the same files.

---

## Phase 5: Scalability Spec (CRITICAL)

Write a dedicated scalability section covering:

### Singleton Removal
For each static singleton:
- Current location (file + line)
- What it holds (state inventory)
- Refactoring plan (instance-based, dependency injection, or ambient context)
- Migration path (keep deprecated shim during transition)

### Multi-Bot-Per-Process Architecture
- Design a `BotContext` class that encapsulates ALL per-bot state
- Each bot gets its own: game client connection, object manager, event system, movement controller
- Process hosts N `BotContext` instances on separate `Task` lanes
- Target: 50-100 bots per process, 30-60 processes for 3,000 bots

### Async I/O Rewrite
- Replace blocking socket reads with `System.IO.Pipelines` or equivalent
- Replace thread-per-connection with async accept loops
- Replace synchronous IPC calls with async request/response
- Replace `Task.Delay(N)` with `PeriodicTimer` for precise tick cadence

### Network Optimization
- Delta snapshots (send only changed fields, not full state every tick)
- Compression (GZip or LZ4 for messages >1KB)
- Connection multiplexing (N bots over M connections where M << N)
- Batched processing (collect N updates, process as batch, respond as batch)

### Service Sharding
- Pathfinding/physics: K instances, hash-partitioned by account/zone
- State manager/orchestrator: M instances, zone-sharded
- Decision engine: stateless, horizontally scalable

### Load Test Milestones
- 100 bots: single machine, all login + basic activity, measure baselines
- 500 bots: 2 machines, multi-zone, measure cross-machine latency
- 3,000 bots: full cluster, mixed activities, full metrics dashboard

---

## Output Format

Your final output should be:
1. **Updated documentation files** (architecture, technical notes, dev guide, behavior matrix)
2. **Updated task file** with ALL new tasks organized by phase, each with detailed specs
3. **Summary message** listing: total tasks added, tasks by phase, critical findings, recommended execution order

---

## Rules

- **DO NOT write implementation code** — write specs only
- **DO NOT guess** — if you can't find something, say it's not found
- **DO read before writing** — never update a doc you haven't read first
- **DO be specific** — file paths, method names, opcode numbers, line references
- **DO think about parallelization** — specs should enable multiple agents to work simultaneously
- **DO leave physics/movement engine docs alone** if another agent owns them
- **DO identify every singleton** — these are the #1 blocker for scale
- **DO calculate bandwidth** — N bots × message size × frequency = actual network load
- **DO count threads** — thread-per-connection × N bots = actual thread count vs OS limit
