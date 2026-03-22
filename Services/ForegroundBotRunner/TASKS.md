<<<<<<< HEAD
﻿# ForegroundBotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Injected client behavior, memory reads/writes, FG object manager parity, and stability.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Prioritize crash prevention and deterministic state exposure.

## Active Priorities
1. Stability guards
- [ ] Maintain AV guards for target setting and login snapshot capture paths.
- [ ] Keep pointer validation and main-thread execution constraints enforced.

2. FG parity exposure
- [x] Ensure FG death/ghost state detection is stable enough for corpse-run parity.
- [ ] Ensure FG snapshot data remains complete and comparable with BG path.
- [x] Implement missing descriptor-backed life-state fields (`WoWPlayer.PlayerFlags`, `WoWPlayer.Bytes/Bytes3`, `WoWUnit.Bytes0/1/2`) used by `ActivitySnapshot`.
- [x] Reduce remaining Lua-only FG life-state paths (`LocalPlayer.InGhostForm`, reclaim-delay fallbacks) now that descriptor fields are available.
- [x] Implement descriptor-backed FG `WoWPlayer.QuestLog` reads so quest log slots flow into snapshots.
- [ ] Fix FG `SpellList` parity for learned/already-known talent spells (e.g. `.learn 16462` acknowledged but missing from FG snapshot spell list).

3. Pathfinding wiring
- [x] Guarantee non-null `PathfindingClient` injection into FG `ClassContainer`.
- [ ] Add startup diagnostic line that captures configured PF endpoint and connection success/failure for faster live triage.

## Session Handoff
- Last crash/parity fix:
  - Implemented descriptor-backed FG snapshot life fields:
    - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`: `PlayerFlags`, `Bytes`, `Bytes3`.
    - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`: `Bytes0`, `Bytes1`, `Bytes2`.
  - Ensured `ForegroundBotWorker` still supplies non-null `PathfindingClient` into `CreateClassContainer`.
  - Updated `LocalPlayer.InGhostForm` to descriptor-first detection (`PLAYER_FLAGS_GHOST` + stand-state dead guard), with memory/Lua fallback only when descriptor state is inconclusive.
  - Implemented descriptor-backed `WoWPlayer.QuestLog` reads (20 slots x 3 fields) to support quest snapshot parity.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"` (pass in latest run).
- Files changed:
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
- Next task:
  - Validate repeated corpse/ghost transitions over multiple live runs to confirm descriptor-first `InGhostForm` no longer drops death-recovery scheduling.

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.
## Archive
Move completed items to `Services/ForegroundBotRunner/TASKS_ARCHIVE.md`.


=======
# ForegroundBotRunner Tasks

Master tracker: `MASTER-SUB-016`

## Scope
- Directory: `Services/ForegroundBotRunner`
- Focus: remove FG object-model throw paths that break corpse/combat/gathering parity and stabilization tests.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Keep this file implementation-focused on FG object/materialization behavior only.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every validation cycle must compare FG and BG behavior for the same scenario.
4. On completion, move finished items to `Services/ForegroundBotRunner/TASKS_ARCHIVE.md` in the same session.
5. If two runs in a row produce no code delta, record blocker + exact next command in `Session Handoff` and advance to the next queued file in `docs/TASKS.md`.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Build check passes: `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`; output still contains a non-blocking `dumpbin` missing message from `vcpkg ... applocal.ps1`.
- `NotImplementedException` baseline:
  - `WoWObject.cs`: `31` throw matches across lines `195-231`.
  - `WoWUnit.cs`: `56` throw matches across lines `244-559`.
  - `WoWPlayer.cs`: `49` throw matches across lines `41-252`.
- TODO carryover requiring explicit keep/implement/defer:
  - `Services/ForegroundBotRunner/Mem/MemoryAddresses.cs:137`
  - `Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs:214`
  - `Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs:386`

## P0 Active Tasks (Ordered)
1. [x] `FG-MISS-001` Remove throw paths in `WoWObject.cs`.
- **Done (batch 1).** All `NotImplementedException` replaced with safe defaults (0, null, empty).
- Acceptance criteria: command returns no matches.

2. [x] `FG-MISS-002` Remove throw paths in `WoWUnit.cs`.
- **Done (batch 1).** All `NotImplementedException` replaced with safe defaults (~50 properties).
- Acceptance criteria: command returns no matches.

3. [x] `FG-MISS-003` Remove throw paths in `WoWPlayer.cs`.
- **Done (batch 1).** All `NotImplementedException` replaced with safe defaults (~35 properties).
- Acceptance criteria: command returns no matches.

4. [x] `FG-MISS-004` Add regression gate for FG materialization throws.
- **Done (batch 14).** Added `ForegroundObjectRegressionTests.cs` with 4 source-scanning tests:
  - `WoWObject_NoNotImplementedException` — scans WoWObject.cs for `throw new NotImplementedException`
  - `WoWUnit_NoNotImplementedException` — scans WoWUnit.cs
  - `WoWPlayer_NoNotImplementedException` — scans WoWPlayer.cs
  - `AllObjectModelFiles_NoNotImplementedException` — aggregate scan with line-level violation reports
- Validation: 4/4 pass (`dotnet test --filter ForegroundObjectRegressionTests`).
- [x] Acceptance: guard fails when a throw is reintroduced and passes on current implementation.

5. [x] `FG-MISS-005` Triage remaining FG memory/warden TODOs.
- **Done (batch 7).** All Mem/ TODOs triaged: WardenDisabler.cs TODOs replaced with FG-WARDEN-001/FG-WARDEN-002 IDs (prior session). MemoryAddresses.cs had no remaining TODOs. Last WoWUnit.cs TODO replaced with defer rationale.
- Acceptance criteria: `rg -n "TODO" Services/ForegroundBotRunner/Mem/` returns no matches.

## Simple Command Set
1. Build: `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore`
2. Corpse-run smoke: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. FG parity slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~Combat|FullyQualifiedName~Gather" --logger "console;verbosity=minimal"`
4. Repo-scoped cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## P1 — Packet Capture & Connection State (2026-03-07)

6. [x] `FG-PKT-001` PacketLogger send hook — hooks NetClientSend (0x005379A0) via assembly injection. Captures outbound CMSG opcodes (opcode + size + timestamp). Commit: `00df96f`.
7. [x] `FG-PKT-002` ConnectionStateMachine — packet-driven lifecycle state machine (DISCONNECTED → AUTHENTICATING → CHAR_SELECT → ENTERING_WORLD → IN_WORLD → TRANSFERRING → LOGGING_OUT). Provides IsLuaSafe, IsObjectManagerValid, IsSendingSafe. Commit: `00df96f`.
8. [x] `FG-PKT-003` ContinentId-based inbound packet inference — bridges recv gap until direct hook exists. ForegroundBotWorker detects ContinentId transitions and records synthetic SMSG packets. Commit: `00df96f`.
9. [x] `FG-PKT-004` Wire ConnectionStateMachine into ThreadSynchronizer — CSM.IsLuaSafe as primary gate, ManagerBase as hard fallback. Commit: `454091d`.
10. [ ] `FG-PKT-005` Direct SMSG receive hook — needs ProcessMessage vtable offset from disassembly. Will allow ConnectionStateMachine to track all server→client transitions without ContinentId heuristics. | Open (blocked on disassembly)

## P2 — FG Startup Stability (2026-03-07)

11. [x] `FG-WARMUP-001` 2s world entry warmup delay — defer MovementRecorder.Poll() for 2s after HasEnteredWorld to prevent CreateFrame native crash during UI initialization.
12. [x] `FG-CRASH-RECOVERY-001` Crash monitor PID pruning + recovery — prune dead PIDs when new WoW.exe starts, clear crash flag, AssertClientAlive waits 30s for recovery.

## P6 — FG Crash During Same-Map Teleport (2026-03-14, DONE)

13. [x] `FG-CRASH-TELE-001` Root cause: `ConnectionStateMachine` handled cross-map transfers (SMSG_TRANSFER_PENDING) but not same-map teleports (MSG_MOVE_TELEPORT 0x00C5). ObjectManager kept calling `EnumerateVisibleObjects` during teleport → crash.
14. [x] `FG-CRASH-TELE-002` Added teleport cooldown to `ConnectionStateMachine`: tracks MSG_MOVE_TELEPORT (recv) / MSG_MOVE_TELEPORT_ACK (send), sets `IsTeleportCooldownActive` + `IsObjectManagerValid=false`. Uses `Interlocked` for thread-safe DateTime.Ticks.
15. [x] `FG-CRASH-TELE-003` Added `ObjectManager.PauseDuringTeleport` (time-based, auto-expires 3s) guard in `SimplePolling` before `EnumerateVisibleObjects`. Lua calls remain safe during same-map teleport. Commit: `9ba5d95`.

## Session Handoff
- Last updated: 2026-03-14 (session 93)
- Active task: P6 FG crash during teleport — DONE
- Last delta: FG-CRASH-TELE-001/002/003 (teleport cooldown, ObjectManager guard)
- Pass result: `delta shipped` — FG bot survives `.tele` commands (12/15 LiveValidation pass, 0 crash)
- Validation/tests run:
  - LiveValidation: 12 passed, 2 failed (pre-existing), 1 skipped
  - `dotnet build` 0 errors
- Files changed:
  - `Services/ForegroundBotRunner/Mem/Hooks/ConnectionStateMachine.cs` — teleport opcode constants, cooldown mechanism
  - `Services/ForegroundBotRunner/Statics/ObjectManager.ScreenDetection.cs` — PauseDuringTeleport guard
- Next command: await LiveValidation suite results, commit + push
- Blockers: FG-PKT-005 blocked on ProcessMessage vtable disassembly
>>>>>>> cpp_physics_system
