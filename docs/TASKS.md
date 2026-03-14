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
10. Every implementation slice must add or update focused unit tests and end with those tests passing before moving to the next slice unless a blocker is recorded.
11. Update the active plan and all impacted `TASKS.md` handoff blocks every pass with exact progress, commands, outcomes, and the next executable command.
12. After each shipped delta, commit and push the full branch state before ending the pass.
13. Every session handoff must direct the next session to resume the next open item in the same test-first, commit/push-as-you-go manner.

---

## P0 - Test Infrastructure Hardening (COMPLETE)

All 5 phases done across sessions 48-52. See `docs/BAD_TEST_BEHAVIORS.md` for full catalog (19/25 fixed, 2 mitigated, 2 deferred, 4 open).

---

## P1 - FG Packet Capture: Send + Recv Hooks

## P0A - Live Integration Test Overhaul (IN PROGRESS)

**Rationale:** The live suite needs to move away from setup validation and raw action dispatches toward BG-first, task-driven behavior coverage with sharper snapshot metrics. FG remains the packet/timing reference, but the test surface must primarily validate BG behavior because that is where our logic actually runs.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 0A.1 | **Fixture cleanup baseline.** Remove fixture-level `.gm on`, make `EnsureCleanSlateAsync()` revive + safe-zone teleport only, and update lifecycle docs. | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture*.cs` | **Done** (2026-03-10 session 54) |
| 0A.2 | **Phase 1 deletions.** Keep only the survivor tests in `BasicLoopTests.cs` and `CharacterLifecycleTests.cs`. | `Tests/BotRunner.Tests/LiveValidation/` | **Done** (2026-03-10 session 54) |
| 0A.3 | **Consumable/buff consolidation.** Replace `ConsumableUsageTests.cs` + `BuffDismissTests.cs` with `BuffAndConsumableTests.cs` and assert add-item, use-item, aura, and dismissal metrics. | `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs` | **Done** (2026-03-10 session 54) |
| 0A.4 | **Move range coverage to deterministic tests.** Keep combat range formulas in `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs`; remove the live `CombatRangeTests.cs` suite. | `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs` | **Done** (2026-03-10 session 54) |
| 0A.5 | **Remove remaining direct `.gm on` / `.respawn` usage** from `GatheringProfessionTests.cs`, `MapTransitionTests.cs`, `LootCorpseTests.cs`, and `StarterQuestTests.cs`, then update their docs. | `Tests/BotRunner.Tests/LiveValidation/` | **Done** (2026-03-11 session 55) |
| 0A.6 | **Task-drive the major behavior suites.** Replace combat, corpse recovery, navigation, questing, gathering, and economy live coverage with BotTask-based tests that link directly to owning task logic. | `Tests/BotRunner.Tests/LiveValidation/`, `Exports/BotRunner/Tasks/` | **Done** (session 90) — All suites task-driven: NPC (`VisitVendor`/`VisitTrainer`/`VisitFlightMaster`), combat (`StartMeleeAttack`), corpse (`ReleaseCorpse`/`RetrieveCorpse`), navigation (`Goto`), quest (`StarterQuestTests`), gathering (`StartGatheringRoute`), fishing (`StartFishing`). QuestInteractionTests kept as snapshot plumbing. |

---

## P1 - FG Packet Capture: Send + Recv Hooks

**Rationale:** FG packet capture is the foundation for fixing all BG bot issues. By observing what the real WoW client sends and receives, we can reconstruct correct behavior in the headless BG bot. This unblocks fishing (FISH-001), movement flags (BT-MOVE-001/002), and combat reliability.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 1.1 | **Complete FG recv hook (SMSG).** Runtime pattern scanner finds ProcessMessage by scanning for `[this+0x74]` m_handlers access near NetClientSend. Assembly detour captures all inbound SMSG opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |
| 1.2 | **Structured packet log format.** C->S / S->C direction, opcode name lookup (~60 opcodes), size, timestamp. Logs first 500 packets + all important opcodes. | `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs` | **Done** (`087085e`) |

**Packet capture is a diagnostic tool, not a standalone test.** Use it during all LiveValidation tests to observe FG/BG packet sequences. Compare against VMaNGOS server code to validate correct behavior. Analyze `packet_logger.log` for hidden error messages and timing issues.

---

## P2 - CraftingProfessionTests.FirstAid Fix (RESOLVED)

FirstAid_LearnAndCraft_ProducesLinenBandage passes reliably as of session 86. Fixed by intervening work (snapshot pipeline improvements, spell handling fixes).

---

## P3 - Fishing FISH-001: BG Packet/Timing Parity Follow-Up

**Approach:** Use FG packet capture to observe a successful FG fishing session, then compare the exact cast/channel/bobber timing against BG. The cast gate is fixed and `FishingTask` now owns the live cast path for both bots; the remaining work is packet/timing and movement-stop parity hardening.

Current observed boundary from the 2026-03-12 live suite:
- `FishingProfessionTests` now stages both BG and FG at Ratchet via `.tele name`, sets fishing skill `75`, adds `Nightcrawler Bait`, and dispatches `ActionType.StartFishing` into `FishingTask`.
- BG now resolves the castable fishing rank from the known-spell list and handles server rank replacement via `SMSG_SUPERCEDED_SPELL` and `SMSG_REMOVED_SPELL`.
- The focused fishing pass condition now requires `bobber observed -> loot_window_open -> fishing_loot_success -> post-loot bag delta`, not just setup success or a transient loot-frame signal.
- FG now mirrors the bobber-interact path through the recv hook plus `ForceStopImmediate()`, while BG forced-stop handling now preserves falling/swimming physics flags. The remaining Ratchet shoreline failure now needs pathfinding/route hardening more than another fishing-task change.
- The task-owned fishing path has already completed live on BG (`skill 75 -> 76`, `bestPool=17.3y`, `lootSuccess=True`, `catchDelta=[6358]`); the remaining intermittent failure is shoreline/pathfinding/LOS before `FishingTask in_cast_range`, not the old cast gate.
- The remaining fishing work is packet/timing and shoreline-movement hardening, not the old `_objectManager.CanCastSpell(7620, 0)` cast gate.

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

## P5 - UnitReaction Reliability (BB-COMBAT-006) — DONE

**Approach:** Embedded 314 faction template entries from VMaNGOS DB into `FactionData.cs` with WoW's mask-based reaction algorithm. UnitReaction is now computed inline when `UNIT_FIELD_FACTIONTEMPLATE` is received.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 5.1 | **Embed faction template data** with reaction calculation in `FactionData.cs`. | `Exports/GameData.Core/Constants/FactionData.cs` | **Done** (`25c5eae`) |
| 5.2 | **Compute reaction from faction template** using WoW's mask algorithm (enemy/friend faction lists + hostile/friendly/our mask checks). | `Exports/GameData.Core/Constants/FactionData.cs` | **Done** (`25c5eae`) |
| 5.3 | **Wire into BG bot.** Compute `UnitReaction` in `ApplyUnitFieldDiffs` when `UNIT_FIELD_FACTIONTEMPLATE` is set. | `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs` | **Done** (`25c5eae`) |
| 5.4 | **Unit tests.** 25 tests: hostile/neutral/friendly creatures, Alliance vs Horde, helpers, edge cases. | `Tests/BotRunner.Tests/Combat/FactionDataTests.cs` | **Done** (`25c5eae`) |

---

## P6 - FG Crash During Teleport (FG-CRASH-TELE) — DONE

**Root cause:** `ConnectionStateMachine` handled cross-map transfers (`SMSG_TRANSFER_PENDING`) but not same-map teleports (`MSG_MOVE_TELEPORT` 0x00C5). ObjectManager continued calling `EnumerateVisibleObjects` during teleport, reading WoW memory while internal object structures were being reshuffled → crash.

**Fix:** Two-layer teleport cooldown (`9ba5d95`):
1. `ConnectionStateMachine` tracks `MSG_MOVE_TELEPORT` (recv) / `MSG_MOVE_TELEPORT_ACK` (send), sets `IsTeleportCooldownActive` + `IsObjectManagerValid=false`
2. `ObjectManager.PauseDuringTeleport` (time-based, auto-expires 3s) blocks `EnumerateVisibleObjects` during teleport
3. Lua calls remain safe (game client Lua engine isn't torn down during same-map teleport)

| # | Task | Owner | Status |
|---|------|-------|--------|
| 6.1 | **Root cause: MSG_MOVE_TELEPORT not handled.** ObjectManager reads unsafe memory during same-map teleport. | `Services/ForegroundBotRunner/Mem/Hooks/ConnectionStateMachine.cs` | **Done** (`9ba5d95`) |
| 6.2 | **Add teleport cooldown to ConnectionStateMachine.** Track MSG_MOVE_TELEPORT/ACK, pause ObjectManager. | `Services/ForegroundBotRunner/Mem/Hooks/ConnectionStateMachine.cs` | **Done** (`9ba5d95`) |
| 6.3 | **Guard EnumerateVisibleObjects.** Add `PauseDuringTeleport` check to SimplePolling enumeration guard. | `Services/ForegroundBotRunner/Statics/ObjectManager.ScreenDetection.cs` | **Done** (`9ba5d95`) |

---

## P7 - Ghost Form Stuck on Geometry (FG-GHOST-STUCK-001) (CURRENT FOCUS)

**Approach:** Compare the path provided by `PathfindingService` against the path the bot actually executes. The gap reveals where corridor collision / collide-and-slide code fails to accommodate WoW movement controls. Fix the pathfinding code to navigate with precision and avoid terrain/object snags.

| # | Task | Owner | Status |
|---|------|-------|--------|
| 7.1 | **Log planned vs executed path.** Record `PathfindingService` waypoints AND actual bot positions at each movement tick during ghost corpse run. | `Exports/Navigation/`, `Services/PathfindingService/`, `Exports/BotRunner/Movement/NavigationPath.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs` | In progress - `NavigationPath.TraceSnapshot` now captures requested start/end, raw service waypoints, runtime waypoints, plan version, explicit replan reason, and per-tick execution samples; `RetrieveCorpseTask` now mirrors no-path / stall-recovery trace summaries into the BotRunner diag log; and the live corpse/fishing tests now append snapshot tails plus recent BotRunner diagnostics on failure. The current live blocker is upstream of pathing: BG is entering these suites at `health=0/0` and not reaching strict-alive after `.revive`, so the new path diagnostics are wired but were not exercised in the latest live rerun. |
| 7.2 | **Identify divergence points.** Find where the bot gets stuck - which waypoint, which geometry (catapult, wall, ramp). | Analysis | **Done** (session 91) — Root cause: physics engine finds cave/gully ground below walking surface near Razor Hill graveyard. Bot sinks through terrain (0.1y/frame, bypasses 5.0y slope guard). Fixed with path-aware ground guard in MovementController (rejects ground >3y below navmesh path waypoint Z). Corpse run: SKIP → PASS. |
| 7.3 | **Fix corridor collision code.** Update `PhysicsCollideSlide.cpp` to handle the stuck geometry. Test with replay frames. | `Exports/Navigation/PhysicsCollideSlide.cpp`, `SceneQuery.cpp`, `SceneCache.cpp` | **Done** (`ad7741f`) — Fixed GetGroundZ asymmetric search window, removed dead code, relaxed `ShouldAcceptNearCompleteSegment` to use remaining-distance-based acceptance for short steep segments. OrgrimmarCorpseRun: FAIL → PASS. Physics: 107/2/1. Remaining: 2 teleport airborne descent tests (pre-existing). |
| 7.4 | **Ratchet shoreline/fishing-hole route hardening.** Instrument the short route from the Ratchet named-teleport landing to fishing-hole cast positions, then fix terrain-snags / LOS-blocked endpoints that strand bots before `FishingTask in_cast_range`. | `Services/PathfindingService/`, `Exports/Navigation/`, `Exports/BotRunner/Tasks/FishingTask.cs` | In progress - native route shaping now has a first lateral-detour pass, the service emits full short-route `[PATH_DIAG]` corner chains, BotRunner records planned-vs-executed short-route traces, and the fishing live suite now fails with explicit shoreline evidence (`FishingTask los_blocked`, `Your cast didn't land in fishable water`, recent BotRunner diagnostics) instead of a generic timeout. The latest live rerun did not reach those assertions because BG failed clean-slate revive first. |
| 7.5 | **Object-aware path requests.** Extend path requests so BotRunner sends nearby collidable game objects and movement capabilities to PathfindingService instead of pathing with only `mapId/start/end`. | `Exports/BotRunner/Clients/PathfindingClient.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`, `Services/PathfindingService/`, `pathfinding.proto` | In progress - contract and request-overlay slices are done: BotRunner builds a conservative collidable overlay (`40y` from start/end, nearest `64` max), and the service mounts `nearby_objects` into a request-scoped synthetic-guid overlay for `CalculatePath` while serializing registry-sensitive native calls to avoid cross-request contamination. |
| 7.6 | **Overlay-aware path validation and repair.** Validate native mmap routes against live object overlays, then reform blocked segments into usable local detours when possible. | `Services/PathfindingService/Repository/Navigation.cs`, `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/SceneQuery.cpp` | In progress - the service now validates returned segments against mounted dynamic-object overlays, retries alternate native mode, performs a bounded local detour search, and has a native `ValidateWalkableSegment` bridge plus result-mapping coverage. Native `FindPath` now honors the public `smoothPath` contract (`true=smooth`, `false=straight`), carries grounded segment ends forward, and now tries grounded lateral detour candidates before midpoint-only refinement. Deterministic native coverage now includes both the Ratchet fishing shoreline route and a known obstructed direct-segment detour. Default rollout remains gated behind `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION` until broader native detour shaping plus shoreline drift diagnostics replace the remaining service-side repair burden. |
| 7.7 | **Route affordance metadata.** Classify path transitions as walk / step-up / jump-gap / safe-drop / unsafe-drop / swim / blocked and return that metadata to callers. | `Services/PathfindingService/`, `Exports/Navigation/PhysicsEngine.cpp`, `pathfinding.proto` | Open |
| 7.8 | **Decision-grade spatial queries.** After object-aware paths are stable, add higher-level reachability/LOS/surface queries so BotRunner can choose better approach points instead of only consuming corner lists. | `Services/PathfindingService/`, `Exports/BotRunner/` | Open |

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
| `CAP-GAP-003` | `TrainerFrame` — BG trainer Rx fixed (session 86-87), FG Lua impl added (session 87). | `Exports/WoWSharpClient/`, `Services/ForegroundBotRunner/` | **Resolved** |
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
- **Last updated:** 2026-03-14 (session 96)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  1. **Fixed fishing test hang blocking suite** (`422a62f`) — When FishingTask pops with `no_fishing_pool`, test now exits polling loop immediately and skips assertions. Previously polled for 4.3min×2=8.6min, causing blame-hang timeout to kill the run before 30+ tests could execute.
  2. **Increased test session timeout** (`a67fac0`) — From 25m to 40m. With fishing fix, more tests run per session; old budget was too tight.
- **Test results (full 43-test suite):**
  - **35 passed, 2 failed, 6 skipped** (Duration: 24m15s)
  - Physics: 109/0/1 (unchanged)
  - Previously only 13/43 tests ran; now all 43 execute
- **Failures (2):**
  1. `DeathCorpseRunTests.ResurrectsForegroundPlayer` — Pre-existing P7. FG corpse run only improved 14y (152→138y, needs 25y min). RetrieveCorpseTask triggers WoW.exe crash shortly after pathfinding completes — socket disconnections then process termination. Root cause: FG injected DLL state corruption during corpse recovery, not PathfindingService itself.
  2. `SpellCastOnTargetTests.CastSpell_BattleShout_AuraApplied` — FG-only. `CastSpell(int)` is a no-op on ForegroundBotRunner (known API gotcha). BG passes. Need FG to use `CastSpellByName` string overload.
- **Skips (6):**
  1. `DismissBuff_RemovesBuff` — FG-only test, expected skip
  2. `DeathCorpseRunTests.ResurrectsBackgroundPlayer` — Pathfinding gap (RunbackStallRecoveryExceeded)
  3. `FishingProfessionTests` — No pool at Ratchet (respawn timer), early-exit fix working
  4. `GatheringProfessionTests.Mining` — Copper veins on respawn
  5. `GatheringProfessionTests.Herbalism` — Herbs on respawn
  6. `NpcInteractionTests.Trainer_LearnAvailableSpells` — Trainer NPC not found
- **FG crash cascade pattern (documented):**
  - RetrieveCorpseTask pathfinding completes → 2-3s later WoW.exe crashes
  - Socket disconnection chain: bot log pipe, PathfindingService client, CharacterStateSocketListener
  - StateManager detects and restarts WoW.exe with new PID
  - Subsequent FG tests may fail due to degraded fixture state (wrong aura list, stale commands)
  - This is the primary reliability blocker for the test suite
- **Known remaining issues:**
  - FG `CastSpell(int)` no-op — needs spell name mapping for FG
  - Fishing pool Z=0 in FG memory — LOS fix is workaround
  - FG WoW.exe crash during RetrieveCorpseTask — P7 root cause
- **Next:**
  1. Investigate FG WoW.exe crash during corpse recovery — socket disconnection chain suggests IPC/injection issue
  2. Fix `SpellCastOnTargetTests` FG path — use spell name lookup for FG CastSpell
  3. P3: Fishing FISH-001 — capture FG fishing packets when pool is available
  4. P7 remaining items (shoreline route hardening, object-aware paths)

## Session Handoff (Session 95 Archive)
- **Last updated:** 2026-03-14 (session 95)
- **Completed:** Post-teleport slope guard fix, teleport Z clamp epsilon, false-freefall log throttle.
- **Test results:** Physics 109/0/1. OrgrimmarGroundZ 2/0. Core LiveValidation 13/0.

## Session Handoff (Session 94 Archive)
- **Last updated:** 2026-03-14 (session 94)
- **Completed:** FG DismissBuff fix (`c1151d0`), FG fishing LOS fix (`39eaee5`), BG post-teleport physics fix (`d1e0601`).
- **Test results:** Physics 108/1/0. Key LiveValidation tests pass. Fishing pool on respawn timer.

## Session Handoff (Session 93 Archive)
- **Last updated:** 2026-03-14 (session 93)
- **Completed:** CombatLoopTests fix (`69d4d63`), P7.3 OrgrimmarCorpseRun fix (`ad7741f`), P6 FG crash during teleport fix (`9ba5d95`).
- **Test results:** 12 passed, 2 failed, 1 skipped (timeout at 25min aborted remaining).

## Session Handoff (Session 91 Archive)
- **Last updated:** 2026-03-14 (session 91)
- **Completed:** NPC population race fix (`73fb676`), P7 ghost form Z-sinking fix (`c35fb9e`), FG trainer skip-on-timeout.
- **Test results:** 35 passed, 0 failed, 8 skipped.

## Session Handoff (Session 86 Archive)
- **Last updated:** 2026-03-13 (session 86)
- **Completed:** CMSG_RECLAIM_CORPSE GUID fix, BG trainer Rx fix (BRT-OVR-006), P2 CraftingProfessionTests resolved.

## Session Handoff (Session 85 Archive)
- **Last updated:** 2026-03-13 (session 85)
- **Completed:** Pathfinding perf fix (16s→0ms), slope guard, FG ghost crash fix, combat retry, BG console windows.
- **Test results:** 36 passed, 1 failed (DeathCorpseRun intermittent), 7 skipped.

## Session Handoff (Session 80 Archive)
- **Last updated:** 2026-03-13 (session 80)
- **Completed:** Migrated herbalism to route-task contract. Added herb route selection + 3 unit tests. Live validation found 24 Durotar herb candidates (all on respawn → skip). Commits: `0c5c6d8`, `7c8b269`, `13f047b`.

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
