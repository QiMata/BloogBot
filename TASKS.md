# Tasks

## Goal: Two WoW 1.12.1 Clients — Injected & Headless

Build and maintain two complementary clients for a vanilla WoW 1.12.1 private server:

1. **Injected Client (ForegroundBotRunner)** — Runs inside the real WoW.exe process. Reads memory, calls native functions, automates gameplay. This is the **reference implementation** and the source of ground-truth data.
2. **Headless Client (WoWSharpClient)** — Standalone process (no graphics/audio). Implements the WoW protocol from scratch. Connects to the server independently, moves via the centralized PhysicsEngine, and plays autonomously.

**The pipeline**: Record data from the injected client → calibrate the PhysicsEngine → validate the headless client against real data → bring the headless client to feature parity.

**Server source:** `E:\repos\MaNGOS\source\src\` (MaNGOS 1.12.1 fork — Elysium)

---

## Centralized PathfindingService Architecture

Both clients share a single **PathfindingService** process that loads Navigation.dll and map data once.

```
┌──────────────────────┐                         ┌─────────────────────┐
│  ForegroundBotRunner  │──── GetPath ──────────→│                     │
│  (injected client)    │     LineOfSight         │  PathfindingService │
├──────────────────────┤                    ┌───→│  (single process)   │
│  WoWSharpClient 1     │──── PhysicsStep ──┘    │                     │
│  (headless client)    │     GetPath             │  Navigation.dll     │
├──────────────────────┤     LineOfSight          │  (maps loaded once) │
│  WoWSharpClient N     │────────────────────────→│                     │
└──────────────────────┘     TCP/protobuf:5001    └─────────────────────┘
```

---

## Phase 1.5: Build, Test Infrastructure & Naming Refactor (PRIORITY)

**Why first:** The fragile build/test infrastructure is blocking live client recording tests. Fix the foundation before adding more tests.

### 59. Refactor build, test infrastructure, and naming conventions ✅ Done

**Steps (ordered by dependency):**

1. ~~**Naming refactor** — Rename `StateManager` → `WoWStateManager` and `ActivitySnapshot` → `WoWActivitySnapshot` across the solution~~ ✅ Done
2. ~~**Unified build output** — Remove global OutputPath from Directory.Build.props; services keep Bot\ output, tests use default bin\ layout~~ ✅ Done
3. ~~**Low-level DLL tests** — DllAvailabilityTests (Navigation.dll existence, load, physics constants), BotTask tests (PhysicsGravityTask, PhysicsGroundSnapTask)~~ ✅ Done
4. ~~**Service-level tests** — PathfindingService.Tests BotTasks (PathCalculationTask, PathSegmentValidationTask), shared Skip/SkipException in WWoW.Tests.Infrastructure~~ ✅ Done
5. ~~**BotRunner integration tests** — MangosStackFixture (auto-launches MaNGOS), BotTaskIntegrationTests (VerifyInWorldTask, VerifyMovementTask, VerifyDatabaseStateTask)~~ ✅ Done
6. ~~**Server/process detection** — MangosServerLauncher detects+launches MySQL/realmd/mangosd, ServiceHealthChecker TCP probes, IntegrationTestConfig with env var overrides~~ ✅ Done
7. ~~**Test runner script** — `run-tests.ps1` with 4 layers (DLL → Physics/Pathfinding → Unit → Integration), early bail-out, colored output~~ ✅ Done
8. ~~**Re-record test fixtures** — Deferred to Task 24 (live recording captures ground-truth data)~~ → Task 24

**Dependencies:** None (infrastructure task)
**Complexity:** High

---

## Phase 1: Live Client Data Collection

**Why:** Everything downstream — physics calibration, movement validation, packet format verification — depends on ground-truth data from the real WoW client.

### 24. Record movement test scenarios (walk, run, jump, fall, swim) — Mostly Done

31 recordings captured (2026-02-08) covering most scenarios:
- [x] Flat ground run forward (Orgrimmar, Durotar — multiple recordings)
- [x] Run backward (mixed in Durotar recordings)
- [x] Standing jump (Orgrimmar — parabolic arc, peak +1.64 units)
- [x] Running jump (Orgrimmar — forward+airborne, peak +1.64 units)
- [x] Fall from height (Orgrimmar — 36.6y drop with gravitational acceleration)
- [x] Forward+strafe diagonal (Durotar — speed normalization confirmed)
- [x] Strafe-only lateral movement (Durotar)
- [x] Complex mixed movement (Durotar, Undercity — multiple movement types)
- [x] Charge spline data (Dralrahgra_Durotar_2026-02-08_12-28-15 — ONLY recording with player spline JSON)
- [x] Knockback data (Dralrahgra_Blackrock_Spire_2026-02-08_12-04-53 — knockback flags/speeds, no spline data)
- [ ] **Swimming** — No swim recordings exist yet. Deep ocean has no ADT terrain; use Southfury River (1810, -4420, -12 on map 1)

**Output:** 31 JSON recordings in `Documents/BloogBot/MovementRecordings/`
**Key fixes:** PostMessage VK_SPACE for jump, JSON NaN/Infinity sanitization, player spline data in JSON
**Spline data note:** Only `Dralrahgra_Durotar_2026-02-08_12-28-15.json` has player spline fields populated. All other recordings predate the spline JSON fix.
**Dependencies:** Live WoW client, Phase 1.5 build fixes
**Complexity:** Medium

### 25. Undercity elevator transport recording

Record ON_TRANSPORT flag, transportGuid, transportOffset while riding the UC elevator. Verify transport fields populate correctly.

**Dependencies:** Task 24 infrastructure
**Complexity:** Medium

### 27. Hook live client packet send/receive

Hook WoW's internal packet handlers to capture real packets. Log timestamp, direction, opcode, payload. Cross-reference against server docs (Task 21).

**Dependencies:** Task 21 (done)
**Complexity:** High

### 28. Capture baseline packet sequences

Record packet traces for: login flow, movement, combat, object updates, transport, chat/NPC/group.

**Dependencies:** Task 27
**Complexity:** Medium

### 29. Record logout, instance entry, and battleground transitions

Record the full packet sequences and ObjectManager state changes for:
- Character logout (to character select and back)
- Entering a dungeon instance (portal transition, loading screen, new ContinentId)
- Entering a battleground (queue, accept, teleport, BG-specific ObjectManager state)
- Returning to the world from an instance/BG

Capture: packet opcodes/payloads, ObjectManager enumeration changes (objects appear/disappear), ContinentId transitions, GUID remapping, loading screen detection. This data informs how the headless client must handle zone transitions.

**Dependencies:** Task 27 (packet hooking), Task 24 (recording infrastructure)
**Complexity:** High

---

## Phase 2: Physics Engine Calibration

**Why:** The PhysicsEngine must reproduce real client movement exactly before the headless client can send movement packets the server will accept.

### 26. Physics engine calibration against recordings — In Progress

**Test infrastructure: 42/43 passing** (was 14/43 before recording mapping fixes)

**Completed:**
- [x] Pure math calibration tests (RecordingCalibrationTests.cs) — speed, gravity, jump, diagonal normalization
- [x] Updated NavigationInterop.cs PhysicsInput/Output to match PhysicsBridge.h
- [x] Created RecordingLoader.cs for JSON recording parsing (with NaN handling, spline fields)
- [x] Validated speeds: forward=7.001, backward=4.502, strafe=6.941, diagonal=6.983 (all within 0.5 y/s)
- [x] Validated gravity: 19.43 y/s² measured vs 19.29 expected (0.7% error)
- [x] Validated jump: duration=0.800s (expected 0.825s), horizontal speed maintained at 6.89 y/s
- [x] Validated diagonal normalization: 99.76% of run speed (not sqrt(2)*run)
- [x] Mapped all test scenarios to available 2026-02-08 recordings (filename-based lookup)
- [x] Frame-by-frame replay tests working (x86 platform via test.runsettings)
- [x] Fall-from-height frame-by-frame matches PhysicsEngine within 0.5y tolerance
- [x] Standing jump frame-by-frame matches PhysicsEngine within tolerance
- [x] Long flat run frame-by-frame matches PhysicsEngine within tolerance
- [x] Complex mixed movement frame-by-frame matches PhysicsEngine within tolerance
- [x] Running jumps frame-by-frame matches PhysicsEngine within tolerance
- [x] Swimming tests skip gracefully (no swim recording exists yet)

**Remaining (1 test failure):**
- [ ] `FlatRunForward_FrameByFrame_PositionMatchesRecording` — Orgrimmar terrain elevation causes PhysicsEngine position divergence beyond 0.5y tolerance. This is a genuine calibration gap in the C++ PhysicsEngine's ground detection/terrain snapping, not a test infrastructure issue.

**Recording-to-Test Mapping:**

| Scenario | Recording | Key Data |
|----------|-----------|----------|
| FlatRunForward | `Orgrimmar_2026-02-08_11-32-13` | 1203 fwd, 34 still, avgSpeed=6.97 |
| FlatRunBackward | `Durotar_2026-02-08_11-06-59` | 65 pure backward, mixed movement |
| StandingJump | `Orgrimmar_2026-02-08_11-31-46` | 200 falling, individual arcs extracted |
| RunningJump | `Orgrimmar_2026-02-08_11-01-15` | 3495 fwd, 464 falling |
| FallFromHeight | `Orgrimmar_2026-02-08_11-32-44` | 93 fallingFar, 36.6y zRange |
| StrafeDiagonal | `Durotar_2026-02-08_11-24-45` | 55 FWD+STR_R, 48 FWD+STR_L |
| StrafeOnly | `Durotar_2026-02-08_11-06-59` | 40 STR_R, 46 STR_L frames |
| ComplexMixed | `Durotar_2026-02-08_11-06-59` | fwd/back/strafe/fall, 1142 frames |
| OrgRunningJumps | `Orgrimmar_2026-02-08_11-01-15` | running + jump arcs |
| LongFlatRun | `Durotar_2026-02-08_11-37-56` | 5028 frames, 82s pure forward |
| UndercityMixed | `Undercity_2026-02-08_11-30-52` | strafe/fall/fallingFar |
| Swimming | **NONE** | No swim recordings exist |

**Key Files:** `Tests/Navigation.Physics.Tests/RecordingCalibrationTests.cs`, `ManualRecordingTests.cs`, `RecordingLoader.cs`, `NavigationInterop.cs`
**Dependencies:** Tasks 24-25
**Complexity:** High

---

## Phase 3: Movement Packet Validation

### 34. Movement packet send/receive

Verify headless client's movement packets match real client byte-for-byte. Test server acceptance (no rubber-banding).

**Dependencies:** Task 26, Task 28
**Complexity:** High

---

## Phase 4: Headless Client Assembly ✅

- [x] **35.** Game loop and tick system
- [x] **36.** Integration test: headless client login and move (17/17 passing)

---

## Phase 5: Feature Parity & Client Expansion ✅

All headless client features implemented with full test coverage:

- [x] **37.** Combat (CMSG_ATTACKSWING, CMSG_CAST_SPELL, ObjectManager combat methods)
- [x] **38.** NPC interaction (Gossip, Vendor, Trainer, Quest SMSG parsers + CMSG fixes)
- [x] **39.** Dual-client orchestration (PopulateSnapshotFromObjectManager, 25/25 tests)
- [x] **40.** Loot system (8 real SMSG opcode handlers replacing placeholder parsing)
- [x] **41.** Inventory/equipment/item-use (15 CMSG format fixes, SMSG_INVENTORY_CHANGE_FAILURE)
- [x] **42.** Login initialization opcodes + action bar spell casting (CharacterInitNetworkClientComponent)
- [x] **43-58.** Protocol audit: All remaining CMSG/SMSG formats fixed across auction, bank, guild, mail, professions, party, chat, gossip, trade, flight/talent/emote, friend/ignore, combat/spell, looting/vendor/dead actor. 937/937 tests passing.

---

## Completed Foundation Work

- [x] **0.** Finalize TASKS.md
- [x] **1-3.** CMovementInfo offsets, movement input controls, activity snapshot tests
- [x] **5.** Object enumeration
- [x] **7-10.** GrindBot (7-phase state machine), combat rotations (9 classes), rest/food/drink, navigation integration
- [x] **11-14.** Stuck detection, pull spells, target blacklisting, hotspot patrol
- [x] **15-20.** Quest system (IQuestRepository, QuestCoordinator, NPC interaction, objective scanning, group management, dungeon navigation)
- [x] **21.** MaNGOS server source → 7 protocol reference docs in `docs/server-protocol/`
- [x] **22-23.** ActivitySnapshot population, WoWUnit/WoWPlayer descriptor audit
- [x] **29-33.** Object updates, WoWLocalPlayer, SRP auth, world server opcodes, MovementController validation
- [x] Fix login disconnect (PauseNativeCallsDuringWorldEntry), realm list spam (30s cooldown), speed offsets

---

## Notes

- **Server:** Elysium private server (vanilla 1.12.1 build 5875), stays running
- **MaNGOS server source:** `E:\repos\MaNGOS\source\src\`
- **Server protocol docs:** `docs/server-protocol/` (7 docs from Task 21)
- **GM commands:** `SendChatMessage('.command', 'SAY')` or DoString
- **Recordings:** `C:\Users\lrhod\Documents\BloogBot\MovementRecordings\`
- **Packet captures:** `C:\Users\lrhod\Documents\BloogBot\PacketCaptures\`
- **Physics constants:** Gravity=19.2911, JumpV=7.9555, TerminalV=60.148
- **Test account:** ORWR1 (GM level 3, character: Dralrahgra on Kalimdor)
- **Memory notes:** `C:\Users\lrhod\.claude\projects\e--repos-BloogBot\memory\`
- **Test commands:**
  - `.\run-tests.ps1` — Run all test layers in dependency order
  - `.\run-tests.ps1 -Layer 1` — DLL availability only
  - `.\run-tests.ps1 -Layer 2` — Physics & pathfinding
  - `dotnet test Tests/Navigation.Physics.Tests --settings Tests/BotRunner.Tests/test.runsettings -v n` — Physics tests (42/43 passing)
  - `dotnet test Tests/BotRunner.Tests --filter "FullyQualifiedName~MovementRecording" --settings Tests/BotRunner.Tests/test.runsettings -v n` — Manual recording
  - `dotnet test Tests/BotRunner.Tests --filter "Category=Integration" --settings Tests/BotRunner.Tests/test.runsettings -v n` — Full integration (needs MaNGOS)
  - **IMPORTANT:** Always use `--settings Tests/BotRunner.Tests/test.runsettings` for x86 platform target
