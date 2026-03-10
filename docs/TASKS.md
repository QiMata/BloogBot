# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for all local `TASKS.md` files.
- Local files hold implementation details; this file sets priority and execution order.
- When priorities conflict, this file wins.

## Rules
1. Execute one local `TASKS.md` at a time in queue order.
2. Keep handoff pointers (`current file`, `next file`) updated before switching.
3. Prefer concrete file/symbol tasks over broad behavior buckets.
4. Never blanket-kill `dotnet` or `Game.exe` — cleanup must be PID-scoped.
5. Move completed items to `docs/ARCHIVE.md`.
6. Before session handoff, update `Session Handoff` in both this file and the active local file.
7. If two consecutive passes produce no delta, record the blocker and advance to the next queued file.
8. **The MaNGOS server is ALWAYS live.** Never defer live validation tests — run them every session.
9. **Compare to VMaNGOS server code** when implementing packet-based functionality. The server is the authority for correct behavior.

---

## P0 — Test Infrastructure Hardening (COMPLETE)

All 5 phases done across sessions 48-52. See `docs/BAD_TEST_BEHAVIORS.md` for full catalog (19/25 fixed, 2 mitigated, 2 deferred, 4 open).

---

## P1 — FG Packet Capture: Send + Recv Hooks (CURRENT FOCUS)

**Rationale:** FG packet capture is the foundation for fixing all BG bot issues. By observing what the real WoW client sends and receives, we can reconstruct correct behavior in the headless BG bot. This unblocks fishing (FISH-001), movement flags (BT-MOVE-001/002), and combat reliability.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 1.1 | **Complete FG recv hook (SMSG).** Runtime pattern scanner finds ProcessMessage by scanning for `[this+0x74]` m_handlers access near NetClientSend. Assembly detour captures all inbound SMSG opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |
| 1.2 | **Structured packet log format.** C→S / S→C direction, opcode name lookup (~60 opcodes), size, timestamp. Logs first 500 packets + all important opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |

**Packet capture is a diagnostic tool, not a standalone test.** Use it during all LiveValidation tests to observe FG/BG packet sequences. Compare against VMaNGOS server code to validate correct behavior. Analyze `packet_logger.log` for hidden error messages and timing issues.

---

## P2 — CraftingProfessionTests.FirstAid Fix

**Approach:** Diagnostic-first. Add 10 Linen Cloth (not 1), craft, and check for ANY new item in bags. This reveals whether the issue is item ID mismatch, craft failure, or snapshot timing.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 2.1 | **Diagnostic run:** Add 10 Linen Cloth, cast recipe, dump full BagContents before/after. Identify if bandage appears under different item ID or if craft silently fails. | `CraftingProfessionTests.cs` | Open |
| 2.2 | **Fix based on diagnostic results.** | `CraftingProfessionTests.cs` | Open |

---

## P3 — Fishing FISH-001: BG Cast Channel Not Starting

**Approach:** Use FG packet capture (P1) to observe a successful FG fishing session. Analyze the exact CMSG/SMSG sequence and timestamps, then replicate in BG bot.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 3.1 | **Capture FG fishing packets.** Run FG fishing at Ratchet dock, capture full CMSG_CAST_SPELL → SMSG_SPELL_START → SMSG_CHANNEL_START → bobber interaction sequence. | Packet log analysis | Blocked on P1 |
| 3.2 | **Compare BG fishing packets** against FG capture. Identify missing/incorrect packets. | `Exports/WoWSharpClient/` | Blocked on P1 |
| 3.3 | **Fix BG fishing** to match FG packet sequence. | `Exports/WoWSharpClient/` | Blocked on 3.2 |

---

## P4 — Movement Flags After Teleport (BT-MOVE-001/002)

**Approach:** Use FG packet capture to observe correct movement flag transitions during teleport. Compare FG heartbeat packets (flags, position, timing) against BG output.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 4.1 | **Capture FG teleport packets.** Observe MSG_MOVE_TELEPORT_ACK → subsequent heartbeats. Record flag transitions and timing. | Packet log analysis | Blocked on P1 |
| 4.2 | **Compare BG teleport behavior.** Identify where BG diverges (stale flags, missing heartbeats). | `Exports/WoWSharpClient/Movement/MovementController.cs` | Blocked on 4.1 |
| 4.3 | **Fix MovementController** to reset flags correctly and match FG behavior. | `Exports/WoWSharpClient/Movement/MovementController.cs` | Blocked on 4.2 |

---

## P5 — UnitReaction Reliability (BB-COMBAT-006)

**Approach:** Read creature faction/reaction from MangosRepository (DB) and cache while the creature exists in the object manager. Do NOT rely on snapshot-only UnitReaction field.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 5.1 | **Add MangosRepository.GetCreatureFaction(entryId)** that returns faction template from `creature_template` table. Cache by entry ID. | `Services/DecisionEngineService/MangosRepository.cs` | Open |
| 5.2 | **Compute reaction from faction template** using the same algorithm as VMaNGOS `GetReactionTo()`. Map faction → hostile/neutral/friendly. | `Exports/WoWSharpClient/` or `BotRunner/` | Open |
| 5.3 | **Wire into snapshot pipeline.** Replace unreliable runtime UnitReaction with DB-backed reaction for NPC targets. | Snapshot pipeline | Open |

---

## P6 — FG Crash During Teleport (FG-CRASH-TELE)

**Approach:** Use test assertions and FG logs to identify the exact crash trigger. Correlate with ThreadSynchronizer state, ObjectManager polling, and Lua call timing.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 6.1 | **Add crash-context logging.** Log ThreadSynchronizer state + last Lua call + ConnectionStateMachine state before each teleport. | `Services/ForegroundBotRunner/` | Open |
| 6.2 | **Reproduce and capture.** Run GatheringProfessionTests mining (triggers FG teleport to multiple locations). Capture crash context from logs. | LiveValidation tests | Open |
| 6.3 | **Fix based on crash context.** | `Services/ForegroundBotRunner/` | Open |

---

## P7 — Ghost Form Stuck on Geometry (FG-GHOST-STUCK-001)

**Approach:** Compare PathfindingService-provided path vs the path the bot actually executes. The gap reveals where the corridor collision / collide-and-slide code fails to accommodate WoW's movement controls. Fix the pathfinding code to navigate with precision and avoid terrain/object snags.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 7.1 | **Log planned vs executed path.** Record PathfindingService waypoints AND actual bot positions at each movement tick during ghost corpse run. | `Exports/Navigation/`, `Services/PathfindingService/` | Open |
| 7.2 | **Identify divergence points.** Find where the bot gets stuck — which waypoint, which geometry (catapult, wall, ramp). | Analysis | Open |
| 7.3 | **Fix corridor collision code.** Update collide-and-slide in PhysicsCollideSlide.cpp to handle the stuck geometry. Test with replay frames. | `Exports/Navigation/PhysicsCollideSlide.cpp` | Open |

---

## Completed Phases (See `docs/ARCHIVE.md`)

- P0: Test Infrastructure Hardening (sessions 48-52)
- Phase 3: Documentation (9 CLAUDE.md files)
- Phase 4: Large File Refactoring (5 monolith files split)
- Phase 5: Command Rate-Limiting & Stability (RATELIMIT-001/002, CRASH-001)
- AI Parity (all 3 gates pass live)
- Live Validation Failures (2026-02-28 batch)
- Pathfinding / Physics (all resolved)
- Test Infrastructure: TEST-TRAM-001, TEST-CRASH-001

## Blocked — Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in RecordedTests.Shared | Requires AWSSDK.S3 |
| `RTS-MISS-002` | Azure ops in RecordedTests.Shared | Requires Azure.Storage.Blobs |

## Capability Gaps (Low Priority)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `CAP-GAP-003` | TrainerFrame status unknown — may also be null. | `Exports/WoWSharpClient/` | Open (low priority) |
| `BG-PET-001` | BG pet support — Pet returns null. Hunter/Warlock won't work. | `Exports/WoWSharpClient/` | Open |

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CombatRangeTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# AI tests
dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"

# Full solution (all test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-10 (session 53)
- **Current work:** P1 — FG Packet Capture. P1.1 + P1.2 DONE. P0 COMPLETE.
- **Completed this session:**
  1. **P1.1 recv hook** (`087085e`): Runtime pattern scanner finds ProcessMessage via m_handlers[+0x74] signature. Assembly detour captures all inbound SMSG packets.
  2. **P1.2 structured log** (`087085e`): C→S / S→C direction, opcode name table (~60 opcodes), first 500 packets logged.
  3. **TASKS.md priorities** (`962862b`): P1-P7 priority rewrite per user guidance.
- **Test results:** 46 passed, 2 failed (FirstAid + Fishing), 2 skipped out of 50.
- **Next:** P1.4 — Live validate recv hook (run FG bot, check packet_logger.log for SMSG opcodes). Then P1.3 (LiveValidation test). Then P2 (FirstAid diagnostic).
- **Sessions 1-52:** See `docs/ARCHIVE.md` for full history.
