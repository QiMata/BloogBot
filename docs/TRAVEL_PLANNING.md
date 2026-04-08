# Multi-Modal Travel Planning Architecture

## Overview

StateManager acts as a **route optimizer** that plans the fastest/cheapest path between any two points in Azeroth. BotRunner acts as the **executor** with persistent path state and combat interruption support.

## Travel Modes

| Mode | Speed | Cost | Requirements |
|------|-------|------|-------------|
| **Walking/Running** | ~7 yd/s | Free | None |
| **Mounted** | ~14 yd/s | Free | Riding skill + mount |
| **Flight Path** | ~32 yd/s | ~50c-2g per hop | Discovered flight point |
| **Zeppelin/Boat** | ~32 yd/s | Free | At dock, wait for schedule |
| **Deeprun Tram** | ~32 yd/s | Free | At station |
| **Elevator** | Instant | Free | At elevator |
| **Hearthstone** | Instant | Free | 60 min CD, bound location |
| **Mage Portal/Teleport** | Instant | Tip (~1g) | Mage NPC or player |

## Route Planning (StateManager)

### Input
```
TravelRequest {
    SourceMapId, SourcePosition,
    DestMapId, DestPosition,
    Character { Level, Gold, RidingSkill, KnownFlightPaths[], HearthstoneLocation, HearthstoneCD },
    Preferences { MinimizeTime, MinimizeGold, AvoidPvPZones }
}
```

### Output
```
TravelPlan {
    Segments[] {
        Type: Walk | FlightPath | Transport | Elevator | Hearthstone | MagePortal,
        FromPosition, ToPosition,
        EstimatedTime, GoldCost,
        RequiredNpcEntry, RequiredMapId
    },
    TotalEstimatedTime, TotalGoldCost
}
```

### Algorithm
1. Build a **graph** of travel nodes:
   - Every known flight master (with discovered routes per character)
   - Transport endpoints (zeppelin docks, boat docks, tram stations)
   - Elevator endpoints
   - Hearthstone location (if off cooldown)
   - Mage teleport NPCs (city mage trainers)
2. Edge weights = estimated travel time + gold cost (weighted by preference)
3. **Dijkstra** from source to destination with mode-specific edges
4. Compare top-K routes, select best for current state

### Example: Barrens to Undercity

**Option A: Flight + Zeppelin (fast, ~50s gold)**
1. Walk to Crossroads flight master (0-2 min)
2. Fly Crossroads → Orgrimmar (2 min, 50s)
3. Run to Zeppelin tower (1 min)
4. Ride Zeppelin to Tirisfal Glades (3 min wait + 2 min ride)
5. Run to Lordaeron Ruins entrance (1 min)
6. Take elevator down to Undercity (30s)
7. Walk to destination (1-3 min)
**Total: ~12 min, 50s**

**Option B: Hearthstone (if bound to UC, free)**
1. Use Hearthstone (10s cast)
2. Walk to destination (1-3 min)
**Total: ~2 min, free**

**Option C: Flight only (if flight paths known)**
1. Walk to nearest flight master
2. Fly Crossroads → Orgrimmar → Undercity (multi-hop, 5 min)
**Total: ~7 min, 1g50s**

## BotRunner Execution (Path Persistence)

### Two-Layer Path Model

```
BotRunner {
    PrimaryPath: GoToTask       // Main objective navigation (persisted)
    CombatPath: temp waypoints  // Short-term combat movement (volatile)
}
```

**Primary Path (GoToTask)**
- Receives travel segment from StateManager
- Persists NavigationPath across poll cycles
- Survives combat interruptions (task stack: CombatTask pushes on top)
- Reports progress back to StateManager via snapshot

**Combat Interruption**
1. Mob aggros → CombatRotationTask pushes onto stack
2. GoToTask pauses (stays on stack, below combat task)
3. Combat finishes → CombatRotationTask pops
4. GoToTask resumes from current position with preserved NavigationPath

### StateManager Progress Tracking

StateManager monitors travel progress via snapshots:
- Current segment index
- Distance to segment endpoint
- Stuck detection (no progress for N seconds)
- Re-route if stuck or segment fails (e.g., missed zeppelin)

### Task Types per Travel Mode

| Travel Mode | BotRunner Task | Details |
|-------------|---------------|---------|
| Walk/Run | `GoToTask` | A* pathfinding via PathfindingService |
| Mount | `MountAndGoToTask` | Cast mount spell, then GoToTask |
| Flight Path | `FlightMasterVisitTask` | Interact with NPC, select destination |
| Transport | `TransportRideTask` | Walk to dock, wait for transport, ride, dismount |
| Elevator | `ElevatorRideTask` | Detect elevator, ride up/down |
| Hearthstone | `UseHearthstoneTask` | Cast hearthstone spell |
| Mage Portal | `MagePortalTask` | Find NPC, interact, select destination |

## Data Requirements

### Static Data (loaded at startup)
- Flight master locations + routes per faction (from DB `taxi_path_node`)
- Transport routes + schedules (from DB `transports`)
- Elevator positions + endpoints
- City mage NPC locations

### Character Data (from snapshot)
- Known flight paths (discovered by this character)
- Hearthstone bind location + cooldown remaining
- Current gold
- Riding skill level
- Current buffs (speed boosts)

## Implementation Phases

1. **P1: GoToTask persistence** (DONE) — persistent pathfinding across cycles
2. **P2: Travel graph** — build node graph from static data
3. **P3: Route planner** — Dijkstra with multi-modal edges
4. **P4: Transport riding** — Zeppelin/boat/tram task implementation
5. **P5: Flight path automation** — FlightMasterVisitTask
6. **P6: Hearthstone integration** — cooldown tracking, bind detection
7. **P7: Cost optimization** — gold vs time trade-off based on objectives
