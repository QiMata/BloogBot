# Technical Notes

*Extracted from TASKS.md � 2026-02-09*
*Reference this file with @file when you need offset details, constants, or protocol notes.*

---

## Environment & Paths

> **Environment Variables:** For the full list of `WWOW_*` environment variables (injection, services, testing), see [BUILD.md § Environment Variables](BUILD.md#environment-variables).

| Item | Value |
|------|-------|
| Server | VMaNGOS (vanilla 1.12.1 build 5875), always running locally |
| VMaNGOS server binaries | Docker `realmd` / `mangosd` services by default; legacy host binaries may live under `C:\Mangos\server\` only when `MangosServer:AutoLaunch=true` is explicitly configured |
| VMaNGOS source (reference) | `C:\Mangos\vmangos-core\` (cloned from github.com/vmangos/core) |
| MaNGOS data directory | `C:\Mangos\data\` (maps, vmaps, mmaps, dbc) |
| MariaDB | `maria-db` Docker container (shared, defined in FFXI repo) |
| VMaNGOS DB version | db-4a0668b (world dump), binary dev-2f1b104 |
| VMaNGOS databases | mangos (world), characters, realmd, logs |
| VMaNGOS DB credentials | root:root (localhost:3306) |
| Server protocol docs | `docs/server-protocol/` (7 docs from Task 21) |
| Recordings | `C:\Users\lrhod\Documents\BloogBot\MovementRecordings\` |
| Packet captures | `C:\Users\lrhod\Documents\BloogBot\PacketCaptures\` |
| Test accounts | TESTBOT1 (Foreground/injected), TESTBOT2 (Background/headless) — GM level 6, same character type |
| Memory notes | `C:\Users\lrhod\.claude\projects\e--repos-BloogBot\memory\` |
| GM commands | `SendChatMessage('.command', 'SAY')` or DoString |

---

## Physics Constants

| Constant | Value |
|----------|-------|
| Gravity | 19.2911 y/s� |
| Jump initial velocity | 7.9555 y/s |
| Terminal velocity | 60.148 y/s |
| Forward run speed | 7.001 y/s |
| Backward run speed | 4.502 y/s |
| Strafe speed | 6.941 y/s |
| Diagonal (fwd+strafe) | 6.983 y/s (99.76% of run speed � normalized, not sqrt(2)�run) |
| Jump duration | 0.800s (expected 0.825s) |
| Measured gravity | 19.43 y/s� (0.7% error vs expected 19.29) |

---

## Test Commands

```bash
# Run all test layers in dependency order
.\run-tests.ps1

# DLL availability only
.\run-tests.ps1 -Layer 1

# Physics & pathfinding
.\run-tests.ps1 -Layer 2

# Physics tests (63 test files, AABB/terrain/collision/swimming/transport)
dotnet test Tests/Navigation.Physics.Tests --settings Tests/BotRunner.Tests/test.runsettings -v n

# Manual recording test
dotnet test Tests/BotRunner.Tests --filter "FullyQualifiedName~MovementRecording" --settings Tests/BotRunner.Tests/test.runsettings -v n

# Full integration (needs MaNGOS)
dotnet test Tests/BotRunner.Tests --filter "Category=Integration" --settings Tests/BotRunner.Tests/test.runsettings -v n

# Swimming recording session (requires admin)
.\run-swimming-recording-test.ps1
```

**IMPORTANT:** Always use `--settings Tests/BotRunner.Tests/test.runsettings` for x86 platform target.

---

## Recording-to-Test Mapping

| Scenario | Recording File | Key Data |
|----------|----------------|----------|
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
| Swimming | `Durotar_2026-02-09_19-16-08` | Forward swim, ascend, descend, water entry/exit |
| Charge spline | `Dralrahgra_Durotar_2026-02-08_12-28-15` | ONLY recording with player spline JSON |
| Knockback | `Dralrahgra_Blackrock_Spire_2026-02-08_12-04-53` | Knockback flags/speeds, no spline data |

---

## Centralized PathfindingService Architecture

```
????????????????????????                         ???????????????????????
?  ForegroundBotRunner  ????? GetPath ????????????                     ?
?  (injected client)    ?     LineOfSight         ?  PathfindingService ?
????????????????????????                    ??????  (single process)   ?
?  WoWSharpClient 1     ????? PhysicsStep ???    ?                     ?
?  (headless client)    ?     GetPath             ?  Navigation.dll     ?
????????????????????????     LineOfSight          ?  (maps loaded once) ?
?  WoWSharpClient N     ???????????????????????????                     ?
????????????????????????     TCP/protobuf:5001    ???????????????????????
```

---

## Known Issues & Workarounds

- **FastCall.dll stale copy** — `Bot\Debug\net8.0\FastCall.dll` can be 12KB (stale, missing `LuaCall` export). Correct version is 62KB. `BotServiceFixture` auto-detects and fixes.
- **StateManager DLL lock race** — StateManager and test can fight over DLLs. Must kill → build → verify → start SM → test. Script `run-swimming-recording-test.ps1` handles this.
- **Orgrimmar terrain divergence** — `FlatRunForward_FrameByFrame` test fails due to terrain elevation causing PhysicsEngine position divergence beyond 0.5y tolerance. Genuine calibration gap in C++ ground detection.
- **Spline data scarcity** — Only 1 of 31 recordings has player spline data (`Dralrahgra_Durotar_2026-02-08_12-28-15.json`). All others predate the spline JSON fix.
- **FG corpse-run revalidation** - Historical `FG-CRASH-001` WoW.exe ghost-form crash at `0x00619CDF` was not reproduced on 2026-04-15. The follow-up opt-in FG death/corpse-run live validation now passes after corpse-run waypoint advancement stopped applying the standard probe-corridor shortcut veto to close unsmoothed waypoints (`fg_corpse_run_after_corpse_probe_policy.trx`, bestDist=34y, strict-alive restored).
- **BG TradeFrame NullRef** — All 6 trade sequences lack null checks for TradeFrame. BG bot will crash on OfferTrade, OfferGold, OfferItem, AcceptTrade, EnchantTrade, LockpickTrade.
- **BG MerchantFrame always null** — `WoWSharpObjectManager.MerchantFrame` is never assigned. BG vendor operations must use packet-based paths (vendorGuid parameter).
- **HandleCharacterLoginFailed stub** — `WorldClient.cs:487` returns Task.CompletedTask. BG bot silently ignores login failures.

---

## FG/BG Feature Parity Summary

### BG-Compatible (Packet Path Available)
- AcceptQuest (with npcGuid+questId params), CompleteQuest (with params), AbandonQuest
- BuyItem (with vendorGuid params), SellItem (with params), RepairAllItems (with vendorGuid)
- All combat actions (attack, cast, stop), movement, inventory management
- Death/corpse (ReleaseCorpse, RetrieveCorpse via CMSG packets)
- 115 CMSG opcodes sent, 141 SMSG opcodes handled

### FG-Only (No Packet Fallback)
- BuybackItem (requires MerchantFrame)
- Craft (requires CraftFrame)
- All 6 trade actions (TradeFrame has no null guards — will NullRef on BG)
- SelectTaxiNode (requires TaxiFrame)
- TrainSkill via legacy frame path (TrainerFrame required)
- TrainTalent via legacy frame path (TalentFrame required)
- SelectGossip via legacy frame path (GossipFrame required)

### Proto/Enum Gaps
- START_WAND_ATTACK (proto 34) — no CharacterAction mapping
- START_PHYSICS_RECORDING (proto 70) — no CharacterAction mapping
- STOP_PHYSICS_RECORDING (proto 71) — no CharacterAction mapping

---

## Scalability Architecture (Target: 3000 Bots)

### Current Bottlenecks
| Component | Current Limit | Blocker |
|-----------|--------------|---------|
| Process model | ~50 bots / machine | 1 OS process per bot (100-500MB each) |
| WoWSharpObjectManager | 1 per process | Static singleton, static `_objects` list |
| WoWSharpEventEmitter | 1 per process | Static singleton, cross-bot event interference |
| SplineController | 1 per process | Static singleton |
| TCP socket server | ~50 connections | Thread-per-connection (1MB stack each), backlog=50 |
| PathfindingService | ~64 concurrent | Single process, ThreadPool-bound handlers |
| BotRunner IPC | 15-65ms blocking | Synchronous `SendMemberStateUpdate()` per tick |
| Network bandwidth | ~125 MB/s | Uncompressed 500KB snapshots × 3000 bots × 10Hz = 15 GB/s |

---

## Cross-World Travel Planner Architecture

### Existing Infrastructure (DO NOT REWRITE)
| Component | File | What It Does |
|-----------|------|-------------|
| CrossMapRouter | `BotRunner/Movement/CrossMapRouter.cs` | Plans `List<RouteLeg>` with walk/elevator/boat/zeppelin/portal/flight legs |
| MapTransitionGraph | `BotRunner/Movement/MapTransitionGraph.cs` | 13 transitions: 4 boats, 3 zeppelins, 6 dungeon portals, faction-aware |
| TransportData | `BotRunner/Movement/TransportData.cs` | 11 transports with stop positions, boarding radii, transit times |
| TransportWaitingLogic | `BotRunner/Movement/TransportWaitingLogic.cs` | State machine: Approaching→Waiting→Boarding→Riding→Disembarking→Complete |
| FlightPathData | `BotRunner/Combat/FlightPathData.cs` | 48 taxi nodes (27 EK + 21 Kalimdor) with map/position/faction |
| FlightMasterNCC | `WoWSharpClient/.../FlightMasterNetworkClientComponent.cs` | Full taxi protocol: discover, activate, express, status |
| PathfindingClient | `BotRunner/Clients/PathfindingClient.cs` | Single-map A* pathfinding (30s timeout) |

### Travel Objective Flow
```
StateManager sets TravelObjective (targetMapId + position)
  → BotRunner receives via snapshot
    → TravelTask calls CrossMapRouter.PlanRoute()
      → Returns RouteLeg[]: Walk, Elevator, FlightPath, Boat, Zeppelin, DungeonPortal, Hearthstone, ClassTeleport
        → TravelTask pushes sub-tasks in reverse (LIFO stack)
          → GoToTask (walk to intermediate point)
          → TakeFlightPathTask (taxi to destination node)
          → BoardTransportTask (boat/zeppelin with TransportWaitingLogic)
          → EnterPortalTask (walk into dungeon/instance entrance)
          → UseHearthstoneTask (10s cast to bind point)
          → MageTeleportTask (10s cast to capital city)
```

### Missing Data to Add
- Deeprun Tram (Ironforge ↔ Stormwind)
- ~25 dungeon/raid instance portals (query MaNGOS `areatrigger_teleport`)
- Mage teleport/portal spell IDs (6 teleport + 6 portal spells)
- Innkeeper locations (~30 major NPCs)
- Graveyard positions (query MaNGOS `game_graveyard`)
- Named location resolver (capital cities + quest hubs)

### Target Architecture
- **N-bots-per-process**: 50-100 `BotContext` instances per process (30-60 processes for 3000 bots)
- **Async I/O**: `System.IO.Pipelines` replacing thread-per-connection
- **Delta snapshots**: 1-5KB per tick instead of 100-500KB
- **Sharded PathfindingService**: K instances (K = cores/4), hash-partitioned by account
- **Partitioned StateManager**: M instances, zone-sharded
- **Physics batching**: `StepPhysicsV2Batch()` for amortized P/Invoke cost

---

### VMaNGOS Server Startup

Server runs in Docker containers (see [DOCKER_STACK.md](DOCKER_STACK.md)):

```powershell
# Start Linux VMaNGOS stack
docker compose -f .\docker-compose.vmangos-linux.yml up -d realmd mangosd
```

Alternative: local binaries at `D:\vmangos-server\`:
- Config files: `D:\vmangos-server\mangosd.conf`, `D:\vmangos-server\realmd.conf`
- SOAP enabled on port 7878 (admin: ADMINISTRATOR/PASSWORD)
- WowPatch = 10 (1.12 Drums of War)
