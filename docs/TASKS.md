# Master Tasks - Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/TASKS_ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** - `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Clear repo-scoped WoW/test processes before building.** DLL injection locks output files.
8. **Previous phases archived** - see `docs/ARCHIVE.md` and `docs/TASKS_ARCHIVE.md`.

---

## Test Baseline (2026-04-16)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1494 | 0 | 1 | All movement parity (30/30) green |
| Navigation.Physics.Tests | 137 | 0 | 1 | All movement parity (8/8) green |
| BotRunner.Tests (unit) | 1747 | 0 | 4 | NavigationPathTests (80/80) green |

---

## Completed Phases (archived to docs/TASKS_ARCHIVE.md)

- **P0** - Movement / Physics WoW.exe Parity: All open items closed.
- **P1** - Alterac Valley 40v40 Integration: All open items closed.
- **R1-R10** - Archived (see `docs/ARCHIVE.md`).
- **Deferred D1-D4** - All closed.

---

## P2 - WoW.exe Packet Handling & ACK Parity (ACTIVE)

### Context
Physics parity against WoW.exe is green. Packet dispatch, ObjectManager state mutation, and ACK generation still have unverified corners. This phase closes those gaps with binary-backed evidence.

**Full plan:** `docs/WOW_EXE_PACKET_PARITY_PLAN.md` (10 gaps identified, 7 sub-phases).

### Sub-phases
- [x] **P2.1** Decompilation research: packet dispatch & ACK generation
  - [x] P2.1.1 Capture `NetClient::ProcessMessage` (0x537AA0) disassembly; identify opcode dispatch mechanism
  - [x] P2.1.2 Dump opcode -> handler mapping as `docs/physics/opcode_dispatch_table.md`
  - [x] P2.1.3 Capture `NetClient::Send` (0x005379A0) disassembly
  - [x] P2.1.4 Decompile P1 handlers: speed change, root, knockback, water walk, hover, teleport, worldport ACK
  - [x] P2.1.5 Decompile `CGPlayer_C` / `CGUnit_C` / `CGObject_C` vtables
  - [x] P2.1.6 Trace movement counter: CMovement offset, increment points, packet inclusion
- [x] **P2.2** ACK format parity (byte-level)
  - [x] P2.2.1 Capture golden corpus ACK bytes from FG bot for each ACK opcode
  - [x] P2.2.2 Add `AckBinaryParityTests` — one test per ACK opcode asserting byte equality
  - [x] P2.2.3 Fix every byte divergence citing a VA
  - [x] P2.2.4 Confirm movement counter semantics match WoW.exe
  - [x] P2.2.5 Gate: all 14 wired ACKs have passing byte-parity tests
- [x] **P2.3** ACK timing & ordering parity
  - [x] P2.3.1 Answer Q1-Q5 (sync vs deferred; see plan §4.3) with binary evidence
  - [x] P2.3.2 Write failing tests for current timing divergences
  - [x] P2.3.3 Fix timing via defer-to-controller or immediate-after-mutation pattern
  - [x] P2.3.4 Close **G1** knockback ACK race
- [ ] **P2.4** ObjectManager state mutation parity
  - [ ] P2.4.1 Produce `docs/physics/cgobject_layout.md` with exact field offsets
  - [ ] P2.4.2 Audit C# classes — each field mapped to a WoW.exe field or documented as intentional omission
  - [ ] P2.4.3 Decompile `CGWorldClient::HandleUpdateObject` block-walk order
  - [ ] P2.4.4 Write `ObjectUpdateMutationOrderTests` replaying captured SMSG_UPDATE_OBJECT streams
  - [ ] P2.4.5 Fix mutation-order divergences
- [ ] **P2.5** Packet-flow end-to-end parity
  - [ ] P2.5.1 Build `PacketFlowTraceFixture` — bytes in, bytes out, state observer, ordered event log
  - [ ] P2.5.2 Write one trace test per representative packet (8 tests: UPDATE_OBJECT Add, UPDATE_OBJECT Update, FORCE_RUN_SPEED_CHANGE, FORCE_MOVE_ROOT, MOVE_KNOCK_BACK, MOVE_TELEPORT, NEW_WORLD→WORLDPORT_ACK, MONSTER_MOVE)
  - [ ] P2.5.3 Fix divergences discovered by trace tests
- [ ] **P2.6** State-machine parity
  - [ ] P2.6.1 Document each state machine (control, teleport, worldport, login, knockback, root) in `docs/physics/state_<name>.md`
  - [ ] P2.6.2 Audit implementation against documented transitions
  - [ ] P2.6.3 Write `StateMachineParityTests`
  - [ ] P2.6.4 Close **G4** (teleport flag clear) and **G8** (teleport ACK deadlock)
- [ ] **P2.7** Gap closure (G1-G10 verification)
  - [ ] P2.7.1 **G2** wire `MSG_MOVE_TIME_SKIPPED` listener
  - [ ] P2.7.2 **G3** wire `MSG_MOVE_JUMP` / `MSG_MOVE_FALL_LAND` consumer
  - [ ] P2.7.3 **G6** implement `MSG_MOVE_SET_RAW_POSITION_ACK`
  - [ ] P2.7.4 **G7** implement `CMSG_MOVE_FLIGHT_ACK`
  - [ ] P2.7.5 Final regression: all parity bundles + new `AckParity` / `PacketFlowParity` / `StateMachineParity` bundles green

### Gaps identified (2026-04-16)
| Gap | Summary                                                             | Close in  |
| --- | ------------------------------------------------------------------- | --------- |
| G1  | Knockback ACK sent before physics consumes impulse                  | P2.3      |
| G2  | `MSG_MOVE_TIME_SKIPPED` has no ObjectManager listener                | P2.7.1    |
| G3  | Jump/fall land events fire but no consumer                           | P2.7.2    |
| G4  | Teleport flag-clear only masks 8 bits (jump/fall/swim persist)       | P2.6.4    |
| G5  | SPLINE_MOVE opcodes ACK behavior unverified                          | P2.3      |
| G6  | `MSG_MOVE_SET_RAW_POSITION_ACK` not wired                            | P2.7.3    |
| G7  | `CMSG_MOVE_FLIGHT_ACK` not wired                                     | P2.7.4    |
| G8  | Teleport ACK `IsSceneDataReady()` may deadlock                       | P2.6.4    |
| G9  | ACK byte format vs WoW.exe unverified                                | P2.2      |
| G10 | Movement counter semantics unverified                                | P2.2      |

---

## Handoff (2026-04-17)

- Completed: Closed **P2.3 ACK timing & ordering parity**. The deterministic timing sweep now covers knockback, speed changes, water-walk/hover/feather-fall toggles, direct root/unroot, compressed root/unroot, and compressed speed/flag variants, and BG now stages the speed/root/flag ACK families for `MovementController.Update()` instead of mutating or ACKing inline from the handler callbacks.
- Binary-backed note: this change mirrors the queue-first WoW.exe leaves already documented in `docs/physics/packet_ack_timing.md`, `smsg_force_speed_change_handler.md`, `smsg_force_move_root_handler.md`, and `smsg_move_flag_toggle_handler.md`. `0x619500..0x6197D0`, `0x61A700`, and `0x61A380/0x61A490/0x61A5D0` stage through `0x6176A0` / `0x617570` and do not call `0x600A30`, `0x60E0A0`, or `0x5AB630` inline, so BG now mirrors that by queueing speed/root/flag ACK work and flushing it on the next controller tick.
- Q5 caveat: the current "no ACK for monster/spline movement" answer is still dispatch-backed from `0x603F00`, `0x603C10`, and `0x603C80`, not a dedicated wrapper disassembly. If later captures contradict that dispatch-based inference, reopen **G5**.
- Commands run:
  - `docker ps` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were running.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before each test build pass in this slice.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForceSpeedChangeOpcodes_DeferMutationAndAckUntilControllerUpdate|FullyQualifiedName~ServerControlledMovementFlagChanges_DeferMutationAndAckUntilControllerUpdate|FullyQualifiedName~CompressedForceMoveRootOpcodes_DeferMutationAndAckUntilControllerUpdate|FullyQualifiedName~CompressedServerControlledMovementFlagChanges_DeferMutationAndAckUntilControllerUpdate|FullyQualifiedName~CompressedForceSpeedChangeOpcodes_DeferMutationAndAckUntilControllerUpdate" --logger "console;verbosity=minimal"` -> first run `failed (26/26)` before the deferred queue fix; final rerun `passed (26/26)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForceMoveRootOpcodes_DeferMutationAndAckUntilControllerUpdate|FullyQualifiedName~CompressedForceMoveRootOpcodes_DeferMutationAndAckUntilControllerUpdate" --logger "console;verbosity=minimal"` -> `passed (4/4)` after adding direct root/unroot timing coverage.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (26/26)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`.
- Files changed: `docs/TASKS.md`, `Exports/WoWSharpClient/Movement/MovementController.cs`, `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`, `Exports/WoWSharpClient/WoWSharpObjectManager.cs`, `Exports/WoWSharpClient/TASKS.md`, `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`, `Tests/WoWSharpClient.Tests/TASKS.md`.
- Next command: `rg -n "HandleUpdateObject|ObjectStateUpdate|ObjectUpdateOperation|CGObject_C|CGUnit_C|CGPlayer_C" Exports/WoWSharpClient docs/physics C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/wow_exe_physics_decompilation.md -g '!**/bin/**' -g '!**/obj/**'`

## Canonical Commands

```bash
# Repo-scoped process inspection/cleanup only
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly

# Keep dotnet cache/temp + test artifacts on repo drive
$env:DOTNET_CLI_HOME='E:\repos\Westworld of Warcraft\tmp\dotnethome'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:WWOW_REPO_ROOT='E:\repos\Westworld of Warcraft'
$env:WWOW_TEST_RUNTIME_ROOT='E:\repos\Westworld of Warcraft\tmp\test-runtime'
$env:VSTEST_RESULTS_DIRECTORY='E:\repos\Westworld of Warcraft\tmp\test-runtime\results'
$env:TEMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'
$env:TMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"
$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_latest.trx"

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```
