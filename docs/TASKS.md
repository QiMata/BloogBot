# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for all local `TASKS.md` files.
- Local files hold implementation details; this file sets priority and execution order.
- When priorities conflict, this file wins.

## Rules
1. Execute one local `TASKS.md` at a time in queue order.
2. Keep handoff pointers (`current file`, `next file`) updated before switching.
3. Prefer concrete file/symbol tasks over broad behavior buckets.
4. Never blanket-kill `dotnet` or `Game.exe`; cleanup must be PID-scoped.
5. Move completed items to `docs/ARCHIVE.md`.
6. Before session handoff, update `Session Handoff` in both this file and the active local file.
7. If two consecutive passes produce no delta, record the blocker and advance to the next queued file.
8. **The MaNGOS server is ALWAYS live.** Never defer live validation tests; run them every session.
9. **Compare to VMaNGOS server code** when implementing packet-based functionality. The server is the authority for correct behavior.

---

## P0 - Test Infrastructure Hardening (COMPLETE)

All 5 phases done across sessions 48-52. See `docs/BAD_TEST_BEHAVIORS.md` for full catalog (19/25 fixed, 2 mitigated, 2 deferred, 4 open).

---

## P1 - FG Packet Capture: Send + Recv Hooks (CURRENT FOCUS)

## P0A - Live Integration Test Overhaul (CURRENT FOCUS)

**Rationale:** The live suite needs to move away from setup validation and raw action dispatches toward BG-first, task-driven behavior coverage with sharper snapshot metrics. FG remains the packet/timing reference, but the test surface must primarily validate BG behavior because that is where our logic actually runs.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 0A.1 | **Fixture cleanup baseline.** Remove fixture-level `.gm on`, make `EnsureCleanSlateAsync()` revive + safe-zone teleport only, and update lifecycle docs. | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture*.cs` | **Done** (2026-03-10 session 54) |
| 0A.2 | **Phase 1 deletions.** Keep only the survivor tests in `BasicLoopTests.cs` and `CharacterLifecycleTests.cs`. | `Tests/BotRunner.Tests/LiveValidation/` | **Done** (2026-03-10 session 54) |
| 0A.3 | **Consumable/buff consolidation.** Replace `ConsumableUsageTests.cs` + `BuffDismissTests.cs` with `BuffAndConsumableTests.cs` and assert add-item, use-item, aura, and dismissal metrics. | `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs` | **Done** (2026-03-10 session 54) |
| 0A.4 | **Move range coverage to deterministic tests.** Keep combat range formulas in `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs`; remove the live `CombatRangeTests.cs` suite. | `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs` | **Done** (2026-03-10 session 54) |
| 0A.5 | **Remove remaining direct `.gm on` / `.respawn` usage** from `GatheringProfessionTests.cs`, `MapTransitionTests.cs`, `LootCorpseTests.cs`, and `StarterQuestTests.cs`, then update their docs. | `Tests/BotRunner.Tests/LiveValidation/` | **Done** (2026-03-11 session 55) |
| 0A.6 | **Task-drive the major behavior suites.** Replace combat, corpse recovery, navigation, questing, gathering, and economy live coverage with BotTask-based tests that link directly to owning task logic. | `Tests/BotRunner.Tests/LiveValidation/`, `Exports/BotRunner/Tasks/` | In progress - combat/quest/navigation slices are green, the NPC contract now includes task-owned visit actions, fishing now reaches a real catch in the latest broad-run log after clean-slate hardening, trainer learning remains isolated as `BRT-OVR-006`, and the next concrete blocker is FG Battle Shout / late-suite completion |

---

## P1 - FG Packet Capture: Send + Recv Hooks

**Rationale:** FG packet capture is the foundation for fixing all BG bot issues. By observing what the real WoW client sends and receives, we can reconstruct correct behavior in the headless BG bot. This unblocks fishing (FISH-001), movement flags (BT-MOVE-001/002), and combat reliability.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 1.1 | **Complete FG recv hook (SMSG).** Runtime pattern scanner finds ProcessMessage by scanning for `[this+0x74]` m_handlers access near NetClientSend. Assembly detour captures all inbound SMSG opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |
| 1.2 | **Structured packet log format.** C->S / S->C direction, opcode name lookup (~60 opcodes), size, timestamp. Logs first 500 packets + all important opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |

**Packet capture is a diagnostic tool, not a standalone test.** Use it during all LiveValidation tests to observe FG/BG packet sequences. Compare against VMaNGOS server code to validate correct behavior. Analyze `packet_logger.log` for hidden error messages and timing issues.

---

## P2 - CraftingProfessionTests.FirstAid Fix

**Approach:** Diagnostic-first. Add 10 Linen Cloth (not 1), craft, and check for ANY new item in bags. This reveals whether the issue is item ID mismatch, craft failure, or snapshot timing.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 2.1 | **Diagnostic run:** Add 10 Linen Cloth, cast recipe, dump full BagContents before/after. Identify if bandage appears under different item ID or if craft silently fails. | `CraftingProfessionTests.cs` | Open |
| 2.2 | **Fix based on diagnostic results.** | `CraftingProfessionTests.cs` | Open |

---

## P3 - Fishing FISH-001: BG Packet/Timing Parity Follow-Up

**Approach:** Use FG packet capture to observe a successful FG fishing session, then compare the exact cast/channel/bobber timing against BG. The cast gate is fixed; the remaining work is parity hardening and eventual task ownership.

Current observed boundary from the 2026-03-11 live suite:
- `FishingProfessionTests` still proves the old cast gate is fixed in focused runs, but it failed again in the latest broad `LiveValidation` rerun.
- BG now resolves the castable fishing rank from the known-spell list and handles server rank replacement via `SMSG_SUPERCEDED_SPELL` and `SMSG_REMOVED_SPELL`.
- The remaining fishing work is parity/timing hardening, broad-suite stability, and a future `FishingTask` conversion, not the old `_objectManager.CanCastSpell(7620, 0)` cast gate.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 3.1 | **Capture FG fishing packets.** Run FG fishing at Ratchet dock and capture the full cast -> channel -> bobber -> custom anim sequence. | Packet log analysis | Open |
| 3.2 | **Compare BG fishing packets** against FG capture. Identify missing packets or timing deltas that still distinguish BG from FG. | `Exports/WoWSharpClient/` | Open |
| 3.3 | **Harden BG fishing parity** to match FG packet/timing behavior and feed the future `FishingTask` implementation. | `Exports/WoWSharpClient/` | Open |

---

## P4 - Movement Flags After Teleport (BT-MOVE-001/002)

**Approach:** Use FG packet capture to observe correct movement flag transitions during teleport. Compare FG heartbeat packets (flags, position, timing) against BG output.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 4.1 | **Capture FG teleport packets.** Observe `MSG_MOVE_TELEPORT_ACK` -> subsequent heartbeats. Record flag transitions and timing. | Packet log analysis | Blocked on P1 |
| 4.2 | **Compare BG teleport behavior.** Identify where BG diverges (stale flags, missing heartbeats). | `Exports/WoWSharpClient/Movement/MovementController.cs` | Blocked on 4.1 |
| 4.3 | **Fix MovementController** to reset flags correctly and match FG behavior. | `Exports/WoWSharpClient/Movement/MovementController.cs` | Blocked on 4.2 |

---

## P5 - UnitReaction Reliability (BB-COMBAT-006)

**Approach:** Read creature faction/reaction from MangosRepository (DB) and cache while the creature exists in the object manager. Do NOT rely on snapshot-only `UnitReaction` field.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 5.1 | **Add `MangosRepository.GetCreatureFaction(entryId)`** that returns faction template from `creature_template` table. Cache by entry ID. | `Services/DecisionEngineService/MangosRepository.cs` | Open |
| 5.2 | **Compute reaction from faction template** using the same algorithm as VMaNGOS `GetReactionTo()`. Map faction -> hostile/neutral/friendly. | `Exports/WoWSharpClient/` or `BotRunner/` | Open |
| 5.3 | **Wire into snapshot pipeline.** Replace unreliable runtime `UnitReaction` with DB-backed reaction for NPC targets. | Snapshot pipeline | Open |

---

## P6 - FG Crash During Teleport (FG-CRASH-TELE)

**Approach:** Use test assertions and FG logs to identify the exact crash trigger. Correlate with `ThreadSynchronizer` state, object-manager polling, and Lua call timing.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 6.1 | **Add crash-context logging.** Log `ThreadSynchronizer` state + last Lua call + `ConnectionStateMachine` state before each teleport. | `Services/ForegroundBotRunner/` | Open |
| 6.2 | **Reproduce and capture.** Run `GatheringProfessionTests` mining (triggers FG teleport to multiple locations). Capture crash context from logs. | LiveValidation tests | Open |
| 6.3 | **Fix based on crash context.** | `Services/ForegroundBotRunner/` | Open |

---

## P7 - Ghost Form Stuck on Geometry (FG-GHOST-STUCK-001)

**Approach:** Compare the path provided by `PathfindingService` against the path the bot actually executes. The gap reveals where corridor collision / collide-and-slide code fails to accommodate WoW movement controls. Fix the pathfinding code to navigate with precision and avoid terrain/object snags.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 7.1 | **Log planned vs executed path.** Record `PathfindingService` waypoints AND actual bot positions at each movement tick during ghost corpse run. | `Exports/Navigation/`, `Services/PathfindingService/` | Open |
| 7.2 | **Identify divergence points.** Find where the bot gets stuck - which waypoint, which geometry (catapult, wall, ramp). | Analysis | Open |
| 7.3 | **Fix corridor collision code.** Update `PhysicsCollideSlide.cpp` to handle the stuck geometry. Test with replay frames. | `Exports/Navigation/PhysicsCollideSlide.cpp` | Open |

---

## Completed Phases (See `docs/ARCHIVE.md`)

- P0: Test Infrastructure Hardening (sessions 48-52)
- Phase 3: Documentation (9 `CLAUDE.md` files)
- Phase 4: Large File Refactoring (5 monolith files split)
- Phase 5: Command Rate-Limiting & Stability (`RATELIMIT-001/002`, `CRASH-001`)
- AI Parity (all 3 gates pass live)
- Live Validation Failures (2026-02-28 batch)
- Pathfinding / Physics (all resolved)
- Test Infrastructure: `TEST-TRAM-001`, `TEST-CRASH-001`

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |

## Capability Gaps (Low Priority)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `CAP-GAP-003` | `TrainerFrame` status unknown; may also be null. | `Exports/WoWSharpClient/` | Open (low priority) |
| `BG-PET-001` | BG pet support - `Pet` returns null. Hunter/Warlock will not work. | `Exports/WoWSharpClient/` | Open |

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Focused overhaul slice
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"

# Deterministic combat distance coverage
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatDistanceTests" --logger "console;verbosity=minimal"

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# AI tests
dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"

# Full solution (all test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff (Latest)
- **Last updated:** 2026-03-11 (session 57)
- **Current work:** P0A - Live Integration Test Overhaul. The current pass hardened fishing setup/runtime behavior, moved the latest broad-run evidence past the old fishing failure, and left FG self-buff / late-suite completion as the next concrete blocker.
- **Completed this session:**
  1. **Fishing runtime hardening landed:** BG fishing now starts from `EnsureCleanSlateAsync()`, fishing casts land closer (`14y`), and bobber auto-use now forces a stop packet before `CMSG_GAMEOBJ_USE`.
  2. **Focused fishing validation is green:** the isolated fishing slice passed, and the paired `FishingProfessionTests|SpellCastOnTargetTests` slice passed after the runtime changes.
  3. **Broad fishing evidence improved:** the latest broad rerun still exited nonzero, but the refreshed `FishingProfessionTests.log` shows a successful Ratchet catch (`CustomAnim -> GAMEOBJ_USE -> loot response -> item 20708 pushed`), so the current blocker has moved forward to the late-suite/FG self-buff path.
- **Commands run + outcomes:**
  1. `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded.
  2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
  3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests|FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 2 passed.
  4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 2 passed, 1 skipped.
  5. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~EconomyInteractionTests|FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> fishing stayed green; `CombatLoopTests` failed separately.
  6. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> command exited nonzero; fresh logs show fishing succeeded and the remaining late-suite failure still needs isolation.
- **Next:** `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~OrgrimmarGroundZAnalysisTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~TalentAllocationTests|FullyQualifiedName~UnequipItemTests|FullyQualifiedName~VendorBuySellTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff (Session 53 Archive)
- **Last updated:** 2026-03-10 (session 53)
- **Current work:** P1 - FG Packet Capture. P1.1 + P1.2 DONE. P0 COMPLETE.
- **Completed this session:**
  1. **P1.1 recv hook** (`087085e`): Runtime pattern scanner finds `ProcessMessage` via `m_handlers[+0x74]` signature. Assembly detour captures all inbound SMSG packets.
  2. **P1.2 structured log** (`087085e`): C->S / S->C direction, opcode name table (~60 opcodes), first 500 packets logged.
  3. **TASKS.md priorities** (`962862b`): P1-P7 priority rewrite per user guidance.
- **Test results:** 46 passed, 2 failed (FirstAid + Fishing), 2 skipped out of 50.
- **Next:** P1.4 - Live validate recv hook (run FG bot, check `packet_logger.log` for SMSG opcodes). Then P1.3 (LiveValidation test). Then P2 (FirstAid diagnostic).
- **Sessions 1-52:** See `docs/ARCHIVE.md` for full history.
