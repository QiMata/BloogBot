# Tasks

> **Reference docs:**
> - `docs/ARCHIVE.md` — Completed task history (Phase 1, Phase 2, Phase 3, Phase 4.3)
> - `docs/TECHNICAL_NOTES.md` — Constants, env paths, recording mappings, known issues
> - `docs/PROJECT_STRUCTURE.md` — Directory layout and project descriptions
> - `docs/physics/README.md` — Physics engine documentation index

## Goal

Two WoW 1.12.1 clients: **Injected** (ForegroundBotRunner inside WoW.exe) and **Headless** (WoWSharpClient, standalone). Both must produce identical behavior for all supported operations: movement, combat, questing, NPC interaction. Coordinate both bots to work as a group — warrior tanks, shaman heals and deals damage.

**Completed:** Phase 1 (cleanup), Phase 2 (physics engine — 58 tests, 30K frames, avg<0.055y), Phase 3 partial, Phase 4.3 (bidirectional group formation), Phase 4.4 partial (CombatCoordinator, CAST_FAILED fixes, thread-safety)

---

## Phase 2.5 — Physics Engine Calibration (HIGH PRIORITY)

### Goal
Achieve P99 position error < 0.0001y (4+ decimal places) between simulated frames and recorded WoW client frames. Current state: avg=0.034y, P99=0.186y. This is ~1800x away from the target.

### Current Precision Baselines
| Recording | Frames | Avg Error | P99 Error |
|-----------|--------|-----------|-----------|
| DurotarLongFlatRun | ~13,000 | 0.034y | 0.186y |
| OrgFlatRunForward | ~60fps | — | 0.239y |
| OrgRunningJumps | — | — | 0.233y |
| UndercityElevatorV2 | 1,754 | <0.13y | <1.0y |

### 2.5a MovementController → PhysicsEngine Data Round-Trip Audit
**Priority: CRITICAL — must fix before calibration is meaningful**

The background bot's physics engine returns unchanged positions (`physics=0` vs `deadReckon=1451`). Dead-reckoning is doing ALL movement. The C++ engine works in tests (58/58 pass) but fails in live operation.

**Investigation needed:**
1. Add verbose logging to `PathfindingSocketServer.HandlePhysics()` — log input moveFlags, speeds, dt, and output position delta
2. Compare proto `PhysicsInput` field mapping (`ToPhysicsInput()` in `PathfindingSocketServer.cs`) against C# `Repository.PhysicsInput` struct layout
3. Verify `deltaTime` reaches C++ as a valid float (not 0, not NaN)
4. Check if `prevGroundZ = float.NegativeInfinity` on first tick causes the engine to skip movement
5. Verify the C++ engine's `BuildMovementPlan` receives valid moveFlags and speed values

**Files:**
- `Exports/WoWSharpClient/Movement/MovementController.cs` — builds PhysicsInput, calls PathfindingClient
- `Exports/BotRunner/Clients/PathfindingClient.cs` — sends proto to PathfindingService
- `Services/PathfindingService/PathfindingSocketServer.cs` — `ToPhysicsInput()` proto→native mapping
- `Services/PathfindingService/Repository/Physics.cs` — `StepPhysicsV2()` P/Invoke
- `Exports/Navigation/PhysicsBridge.h` — C++ struct layout (must match C# exactly)
- `Exports/Navigation/PhysicsEngine.cpp:1432` — `StepV2()` entry point

---

### 2.5b Epsilon and Buffer Distance Calibration
**Priority: HIGH — after 2.5a is fixed**

The physics engine uses several magic constants that affect precision. Each needs systematic tuning against recorded frames.

**Key constants to calibrate:**
| Constant | Current Value | File | Purpose |
|----------|--------------|------|---------|
| `AIR_SWEEP_MARGIN` | 0.5f | PhysicsMovement.cpp:83 | Downward sweep range during air movement |
| `LANDING_TOLERANCE` | 0.1f | PhysicsMovement.cpp:84 | Snap-to-ground threshold when falling |
| `STEP_DOWN_HEIGHT` | 4.0f | PhysicsEngine.h:33 | Max ground snap distance |
| `STEP_HEIGHT` | 2.125f | PhysicsEngine.h | Step-up capability |
| `contactOffset` | 0.02f | PhysicsTolerances.h | Skin-width separation from surfaces |
| `MIN_MOVE_DISTANCE` | 0.001f | PhysicsCollideSlide.h:18 | Minimum movement threshold |
| `maxDeferredDepen` | 0.05f | PhysicsEngine.cpp:1684 | Per-tick depenetration clamp |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.5f | PhysicsEngine.h | Walkable slope threshold |
| `penetrationSlack` | 1e-4f | CapsuleCollision.h:526 | Penetration resolution slack |

**Approach:**
1. For each constant, sweep values around the current setting
2. Replay all recordings with each value
3. Measure avg/P50/P95/P99 error
4. Find the value that minimizes P99 without causing artifacts (feet-in-ground, floating)
5. Document the sensitivity of each constant

---

### 2.5c Stateless System Gap Analysis
**Priority: HIGH — architectural blockers for precision**

The physics engine is stateless (receives full state each tick, returns new state). This has gaps vs. the real WoW client which maintains internal state:

| Gap | Description | Impact on Precision | Potential Fix |
|-----|-------------|-------------------|---------------|
| **Ground contact persistence** | Real client tracks last-touched surface; engine re-probes every frame | Jitter on slopes, step-up/down | Cache hit surface ID across frames via `standingOnInstanceId` |
| **Velocity continuity** | Engine receives (vx,vy,vz) but doesn't verify consistency with position delta | Drift accumulation | Compute velocity from position delta when available |
| **Fall time tracking** | `fallTime` passed as ms but engine uses seconds internally; conversion precision | Jump arc errors | Use consistent units end-to-end |
| **Ground normal smoothing** | Each frame re-probes ground normal; no temporal averaging | Wobble on uneven terrain | Exponential moving average of prevGroundNormal |
| **Transport sync** | Transport position from NearbyGameObjects may lag by 1+ frames | Transport-local drift | Interpolate transport position using known speed/path |
| **Sweep precision vs. mesh density** | VMAP triangle density varies; capsule sweep can miss thin geometry | Position jumps near complex geometry | Adaptive sweep resolution near known problem areas |
| **Dead-reckoning divergence** | When physics returns same pos, dead-reckoning kicks in and diverges from engine | Two movement systems fighting | Fix 2.5a — physics should always move when flags say to |

---

### 2.5d Frame Change Calculation Parity
**Priority: HIGH — ensure we calculate displacement the same way the real client does**

WoW 1.12.1 client calculates per-frame displacement as:
```
displacement = speed * dt
new_pos = old_pos + direction * displacement
```

Our engine does this via `BuildMovementPlan` → `GroundMoveElevatedSweep`. Verify:
1. Speed selection matches (walk/run/runback/swim based on flags)
2. Direction vector matches (orientation + flag combination)
3. `dt` precision matches (client uses GetTickCount ms → float conversion)
4. Ground snapping Z adjustment matches client behavior
5. Slope slowdown/speedup behavior (if any in vanilla 1.12.1)

**Test approach:** For each recording frame pair (i, i+1):
- Compute expected displacement from flags + speed + dt
- Compare against recorded position delta
- Compare against engine output position
- Log all three for every frame in a diagnostic pass

---

## Phase 4.4 — Combat Coordination (continued)

### 4.4a Add StartMeleeAttack Action
**Priority: Medium**
The proto defines `START_MELEE_ATTACK = 32` but `MapProtoActionType` and `CharacterAction` don't include it. Add support so StateManager can tell the background bot to start melee auto-attack on a target.

### 4.4b CombatCoordinator Improvements
**Priority: Medium — basic version working**
- CombatCoordinator exists at `Services/WoWStateManager/Coordination/CombatCoordinator.cs`
- `ACTION_COOLDOWN_SEC` = 3.0s (tuned for Lightning Bolt 2.5s cast)
- Shaman spells at `Services/WoWStateManager/Coordination/ShamanSpells.cs`
- Need: better state machine, mana conservation, interrupt priority

### 4.4c Follow Behavior Enhancement
**Priority: Medium**
Current: shaman follows warrior via GOTO when >20y. Need: smarter positioning (stay at max cast range 30y, avoid line-of-sight breaks, don't stand in melee range of warrior's target).

### 4.4d Bot Intelligence
**Priority: Medium — after physics calibration**
- GrindBot explores randomly when no targets found → implement systematic exploration
- Background bot needs independent grinding capability (GrindTask in BotRunner)
- Mob selection: prioritize by level, distance, avoid runners near other mobs
- Rest logic: stop and eat/drink when appropriate

---

## Phase 3 — Headless Client Feature Parity

### Ensuring All Actions Work on Both Clients

| Action | Foreground (Injected) | Background (Headless) | Status |
|--------|----------------------|----------------------|--------|
| Movement (walk/run) | Memory + ThreadSynchronizer | MovementController + packets | Working |
| Face target | SetFacing memory write | MSG_MOVE_SET_FACING packet | Working |
| Stop movement | SetControlBit | MSG_MOVE_STOP packet | Working |
| Cast spell | LuaCall / CMSG | CMSG_CAST_SPELL via BotRunnerService | Working |
| Melee attack | CMSG_ATTACKSWING | CMSG_ATTACKSWING via WorldClient | Needs test |
| Loot corpse | CMSG_LOOT via Functions | CMSG_LOOT via LootingAgent | Working |
| Target unit | SetTarget memory | CMSG_SET_SELECTION packet | Working |
| Use item | CMSG_USE_ITEM | CMSG_USE_ITEM via ItemUseAgent | Needs test |
| Interact NPC | CMSG_GOSSIP_HELLO | CMSG_GOSSIP_HELLO via GossipAgent | Needs test |
| Buy from vendor | CMSG_BUY_ITEM | CMSG_BUY_ITEM via VendorAgent | Needs test |
| Train spell | CMSG_TRAINER_BUY_SPELL | CMSG_TRAINER_BUY_SPELL | Needs test |
| Accept quest | CMSG_QUEST_ACCEPT | CMSG_QUEST_ACCEPT via QuestAgent | Needs test |
| Jump | PostMessage VK_SPACE | MSG_MOVE_JUMP packet | Working |
| Swim | Memory + physics | MovementController + physics | Needs validation |

### Test Baseline (1003 passing, 1 skipped, 0 failed)
264 pre-recorded .bin files across 28 opcode types

---

## Phase 5-7 — Deferred

- **Phase 5** (Packet Capture): Likely unnecessary — 937+ protocol tests cover CMSG/SMSG
- **Phase 6** (Baseline Sequences): Likely complete — test fixtures serve as baselines
- **Phase 7** (Zone Transition Recordings): Need logout, death/resurrect recordings

---

## Long-Term Vision — Group Content Orchestration

*"Set up a 5-man for Dire Maul North tribute run"* — auto-provision accounts/characters, coordinate group behavior, handle dungeon mechanics. Build on CombatCoordinator pattern, extend to multi-class roles and dungeon-specific logic.
