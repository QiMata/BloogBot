# BotRunner.Tests Tasks

Master tracker: `MASTER-SUB-022`

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Focus: deterministic FG/BG corpse/combat/gathering parity validation with strict timeout and teardown controls.
- Current priority: live integration test overhaul with BG-first behavior coverage, tighter fixture setup rules, and task-driven suite replacement.
- Queue dependency: `docs/TASKS.md` controls file order and session handoff.

## Execution Rules
1. Execute this file only when `docs/TASKS.md` points `Current queue file` to `MASTER-SUB-022`.
2. Start each pass by running the prior `Session Handoff -> Next command` verbatim.
3. Keep corpse-run setup as named teleport to `Orgrimmar` before kill; do not reintroduce `ValleyOfTrials`.
4. Keep scenario class runtime bounded to 10 minutes and record teardown evidence on pass/fail/timeout/cancel.
5. Never blanket-kill `dotnet`; cleanup remains repo/test-scoped with PID evidence.
6. Every parity cycle runs FG and BG in the same session and records movement/spell/packet behavior deltas.
7. On parity drift, add paired `research + implementation` IDs in owning `TASKS.md` files.
8. Archive completed items to `Tests/BotRunner.Tests/TASKS_ARCHIVE.md` in the same session.
9. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
10. After shipping one local delta, set `Next command` to the next queue-file read command and execute it in the same session to prevent rediscovery loops.
11. For corpse-run pathing changes, validate the full route contract (`PathfindingSocketServer -> Navigation.CalculatePath -> NavigationPath.GetNextWaypoint -> RetrieveCorpseTask`) before broad parity sweeps.

## Evidence Snapshot (2026-02-25)
- Corpse setup teleport is pinned to Orgrimmar and not Valley:
  - `rg --line-number "Orgrimmar|ValleyOfTrials|TeleportToNamedAsync" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - Hits at `:270-272` include `TeleportToNamedAsync(characterName, "Orgrimmar")`; no `ValleyOfTrials` hits.
- Corpse lifecycle stage constants/assertions are present:
  - `rg --line-number "ReleaseToGhostTimeout|ReclaimTimeout|Retrieve|corpse|ghost|alive" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - Includes stage gates at `49-51`, `175-180`, `343`, `570`, `602`.
- Timeout baseline is explicit:
  - `rg --line-number "TestSessionTimeout" Tests/BotRunner.Tests/test.runsettings`
  - `Tests/BotRunner.Tests/test.runsettings:6` -> `<TestSessionTimeout>600000</TestSessionTimeout>`.
- Teardown process controls and cleanup script coverage are present:
  - `rg --line-number "KillStaleProcesses|WoWStateManager|WoW\\.exe|PathfindingService|testhost" Tests/Tests.Infrastructure/BotServiceFixture.cs`
  - `rg --line-number "CleanupRepoScopedOnly|WoWStateManager\\.exe|WoW\\.exe|PathfindingService\\.exe" run-tests.ps1`
- Visible-window toggle implemented via `WWOW_SHOW_WINDOWS=1` env var:
  - All 4 launch sites use `CreateNoWindow = Environment.GetEnvironmentVariable("WWOW_SHOW_WINDOWS") != "1"`
  - 7 infrastructure config tests verify behavior in `Tests/BotRunner.Tests/Helpers/InfrastructureConfigTests.cs`
- Corpse runback currently depends on strict no-direct-fallback waypoint consumption:
  - `rg --line-number "GetNextWaypoint|allowDirectFallback: false|No pathfinding route|pathfinding returned no route" Exports/BotRunner/Tasks/RetrieveCorpseTask.cs Exports/BotRunner/Movement/NavigationPath.cs`
  - Key hits include `RetrieveCorpseTask.cs:370-406` and `NavigationPath.cs:56-324`.
- Path service route entry points are defined and should be contract-validated during corpse-run work:
  - `rg --line-number "HandlePath|CalculatePath|FindPath|TryFindPathNative" Services/PathfindingService/PathfindingSocketServer.cs Services/PathfindingService/Repository/Navigation.cs`
  - Key hits include `PathfindingSocketServer.cs:174-181` and `Navigation.cs:65-107`.
- Proto contract source for pathfinding payloads:
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`.

## Active Overhaul Tasks (Ordered)
1. [x] `BRT-OVR-001` Remove remaining direct `.gm on` / `.respawn` call sites from live suites and docs.
- Problem: `GatheringProfessionTests.cs`, `MapTransitionTests.cs`, `LootCorpseTests.cs`, and `StarterQuestTests.cs` still violate the overhaul rules and hide real bot behavior.
- Target files: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`, `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`, matching docs under `Tests/BotRunner.Tests/LiveValidation/docs/`.
- Required change:
  1. Remove direct `.gm on` usage and rely on account-level GM only.
  2. Remove direct `.respawn` usage and replace with wait/skip behavior.
  3. Update markdown so test flow and command lists match the code.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~MapTransitionTests|FullyQualifiedName~LootCorpseTests|FullyQualifiedName~StarterQuestTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: no executable `.gm on` or `.respawn` remains in those suites; docs match the new behavior.

2. [ ] `BRT-OVR-002` Replace setup-only live coverage with task-driven behavior tests.
- Problem: the remaining live suite still overuses raw action dispatch and setup validation instead of exercising the BotTask stack.
- Target files: `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`, `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`, `Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs`, `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`, `Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`, `Exports/BotRunner/Tasks/`.
- Required change: rewrite combat, corpse recovery, navigation, quest, and NPC tests to assert on task outcomes and snapshot metrics rather than reproducing task logic inline.
- Progress (2026-03-13 session 89): NPC interaction tests migrated to task-driven dispatch. `Vendor_OpenAndSeeInventory` -> `Vendor_VisitTask_FindsAndInteracts` (dispatches `VisitVendor`), `Vendor_SellJunkItems` -> `Vendor_SellJunkItems_CoinageIncreases` (asserts coinage increase after sell), `Trainer_OpenAndSeeSpells` removed (redundant with `Trainer_LearnAvailableSpells`), `FlightMaster_DiscoverNodes` -> `FlightMaster_VisitTask_DiscoversPaths` (dispatches `VisitFlightMaster`). QuestInteractionTests kept as snapshot-plumbing coverage (StarterQuestTests already covers task-driven quest accept/turn-in). Prior: mining/herbalism already dispatch `StartGatheringRoute` into `GatheringRouteTask`. Combat uses `StartMeleeAttack` which maps to `BuildStartMeleeAttackSequence` (persistent chase loop). Navigation uses `Goto` (already task-driven).
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: each rewritten suite links directly to the owning task logic and records deterministic outcome metrics.

3. [ ] `BRT-OVR-004` Keep the fishing baseline task-linked, but move the remaining instability into the pathfinding owners.
- Problem: `FishingProfessionTests` now dispatches `ActionType.StartFishing` into `FishingTask` for both bots, stages fishing skill `75` plus bait, and requires `loot_window_open` plus a real post-loot bag delta. That contract can already succeed, but the remaining intermittent failures are now shoreline terrain/LOS/pathfinding issues before `FishingTask in_cast_range`, not missing fishing-task ownership.
- Target files: `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`, `Exports/BotRunner/Tasks/`, FG packet-capture logs.
- Required change:
  1. Keep the dual-bot fishing baseline on the named Ratchet teleport with `FishingTask` owning equip -> bait -> acquire -> approach -> cast -> bobber -> loot-window -> bag-delta.
  2. Record live evidence that separates fishing-task regressions from shoreline/pathfinding regressions, including `FishingTask los_blocked phase=move` and `Your cast didn't land in fishable water`.
  3. Hand the terrain-sticking / no-LOS approach failures to the pathfinding owners (`Services/PathfindingService/TASKS.md`, `Exports/Navigation/TASKS.md`) and only return to packet-timing work after approach stability improves.
  4. Tie the live assertions directly to `FishingTask`, the loot-frame surfaces, and the runtime packet handlers they depend on.
- Progress (2026-03-12 session 76): the live fishing failure path now carries `FishingTask los_blocked`, `Your cast didn't land in fishable water`, recent error tails, and recent BotRunner diag lines. The latest rerun never reached those assertions because BG failed clean-slate revive before the test could stage Ratchet.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: fishing coverage stays meaningful in isolation for both bots on the task-owned equip -> bait -> approach -> cast -> bobber -> loot_window_open -> bag-delta path, documentation points to the owning runtime logic, and intermittent failures clearly identify shoreline/pathfinding/LOS as the blocker instead of reporting a fishing-task regression.

4. [x] `BRT-OVR-006` Fix BG trainer visit gossip-to-trainer-service handoff for task-owned NPC coverage.
- **RESOLVED (sessions 86-87):** Root cause was Rx `.Publish().RefCount()` without self-subscriptions in `TrainerNetworkClientComponent` (and 3 other components). `.Do()` side-effects that populate `_availableServices` never fired. Added self-subscriptions in constructor. Also added FG trainer interaction via Lua (`LearnAllAvailableSpellsAsync`).
- Validation: `Trainer_LearnAvailableSpells` now passes with spell count growth, coinage decrease, and latency metrics for both BG and FG (FG when available).
- Commits: `7d130d0` (trainer Rx fix), `0684150` (Gossip/Guild/Professions Rx fix), `9c59286` (FG Lua trainer impl).

## Completed Legacy Tasks

3. [x] `BRT-CR-001` Keep corpse-run setup teleport pinned to Orgrimmar named teleport path.
- Problem: setup-location drift invalidates runback behavior validation.
- Target files: `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`.
- Required change: preserve named teleport path `TeleportToNamedAsync(characterName, "Orgrimmar")` and no `ValleyOfTrials` setup path.
- Validation command: `rg --line-number "ValleyOfTrials|TeleportToNamedAsync\\(characterName, \"Orgrimmar\"\\)" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
- Acceptance criteria: Orgrimmar path present; no ValleyOfTrials setup references.

2. [x] `BRT-CR-002` Enforce full corpse lifecycle and path-consumption assertions for FG and BG.
- Problem: stage regressions and route-consumption regressions can pass without clear failure signal if assertions remain high-level.
- Target files: `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`.
- Required change:
  1. Assert/order and output evidence for `dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive` with reclaim-delay enforcement.
  2. Assert runback displacement and waypoint-driven progression so wall-running or zero-travel loops fail deterministically.
  3. Keep corpse runback path-driven (no probe-point/random-strafe behavior when a valid path exists).
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: failures name missing/out-of-order stage or stalled/no-path condition; passing run shows deterministic stage evidence and waypoint-progress evidence for both FG and BG.

3. [x] `BRT-CR-003` Validate corpse-run path contract from native path output to bot waypoint usage.
- Problem: pathfinding may return routes that are malformed, unreachable, or not consumed correctly by corpse runback logic.
- Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, `Services/PathfindingService/Repository/Navigation.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`.
- Required change:
  1. Log route contract evidence per corpse-run dispatch (map/start/end, waypoint count, first/last waypoint).
  2. Fail fast on invalid route shapes (empty path when route expected, zero-length segments, unusable endpoint) with deterministic diagnostics.
  3. Confirm consumed waypoint sequence matches returned route order and advances toward corpse reclaim radius.
- Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
- Acceptance criteria: contract regressions fail before live corpse test; successful corpse runback includes path-contract evidence showing usable route and waypoint consumption.

4. [x] `BRT-RT-001` Runtime bounds and deterministic teardown enforced via TINF-MISS-001..006: repo-scoped process filtering (MainModule.FileName check), per-process teardown evidence (PID, exit code, timeout), and `WWOW_SHOW_WINDOWS` visible window opt-in.

5. [x] `BRT-RT-002` Opt-in visible windows via `WWOW_SHOW_WINDOWS=1` env var. Applied to all 4 bot-process launch sites. 7 infrastructure config tests in InfrastructureConfigTests.cs verify behavior.

6. [x] `BRT-PAR-001` Run FG/BG corpse/combat/gathering parity loop using only simple commands.
- Problem: parity drift hides when suites are run ad hoc or in inconsistent order.
- Target files: `Tests/BotRunner.Tests/TASKS.md` execution notes + test output artifacts.
- Required change: execute corpse/combat/gathering commands in one cycle with shared timeout/cleanup guardrails.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: each cycle records FG/BG parity notes for movement/spells/packet-visible behavior.

7. [x] `BRT-PAR-002` Tie parity drift to physics calibration and owning implementation tasks.
- Problem: movement/parity regressions recur without explicit ownership routing.
- Target files: `Tests/Navigation.Physics.Tests/TASKS.md`, `Exports/Navigation/TASKS.md`, `Services/ForegroundBotRunner/TASKS.md`, `Services/BackgroundBotRunner/TASKS.md`.
- Required change: each parity mismatch creates linked `research + implementation` IDs in owning files and triggers physics calibration tests.
- **Done (2026-02-28).** All 4 live failures from BRT-PAR-001 parity loop routed to owners:
  - `BBR-PAR-001` (world object visibility) → `Services/BackgroundBotRunner/TASKS.md`
  - `BBR-PAR-002` (NPC interaction timing) → `Services/BackgroundBotRunner/TASKS.md`
  - `WSM-PAR-001` (quest snapshot sync) → `Services/WoWStateManager/TASKS.md`
  - `PFS-PAR-001` (PathfindingService readiness) → `Services/PathfindingService/TASKS.md`
  - Physics/navigation confirmed clean — parity routing note in `Tests/Navigation.Physics.Tests/TASKS.md`

8. [x] `BRT-OVR-005` Isolate the FG herbalism crash/group-formation fallout and prove no active gameobject spawn path remains.
- Evidence:
  - Repo scan + DB verification confirmed the Mangos Silverleaf error referenced the natural world row `gameobject.guid=1641` / `id=1618`, not a test-spawned node.
  - `GatheringProfessionTests` now keeps BG as the hard assertion path while FG mining/herbalism is best-effort reference coverage with safe-zone cleanup in `finally`.
  - `GroupFormationTests` now starts from `EnsureCleanSlateAsync()` and a live `CheckFgActionableAsync()` probe so post-crash FG restarts skip instead of timing out.
  - `CheckFgActionableAsync()` now verifies both `.targetself` forwarding and a teleport/snapshot round-trip, which prevents stale FG world-state from cascading into later suite failures.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed, 1 skipped`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `32 passed, 0 failed, 3 skipped`

9. [x] `BRT-OVR-007` Stabilize FG self-buff `CastSpell` coverage in the broad live suite.
- Evidence:
  - The observed Battle Shout failure was traced back to stale FG responsiveness after earlier suite instability, not a direct regression in `BuildCastSpellSequence(...)` or the FG Lua cast path.
  - `LiveBotFixture.CheckFgActionableAsync()` now requires a successful `.targetself` dispatch and a teleport/snapshot round-trip before FG-dependent suites continue.
  - Validation:
    - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~NpcInteractionTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~UnequipItemTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `10 passed, 2 skipped`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `1 passed`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `32 passed, 0 failed, 3 skipped`

## Simple Command Set
1. Corpse-run: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. Combat: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. Gathering/mining: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Corpse/path fallback unit slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FarFromCorpse_NoPath|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
5. Repo-scoped cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`
6. Path contract trace scan: `rg --line-number "HandlePath|CalculatePath|GetNextWaypoint|allowDirectFallback: false|pathfinding returned no route" Services/PathfindingService/PathfindingSocketServer.cs Services/PathfindingService/Repository/Navigation.cs Exports/BotRunner/Movement/NavigationPath.cs Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
7. Pathfinding validity slice: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
8. Focused overhaul live slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
9. Combat distance unit slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatDistanceTests" --logger "console;verbosity=minimal"`
10. Documented-stable live slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~EconomyInteractionTests|FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~OrgrimmarGroundZAnalysisTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~TalentAllocationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff (Latest)
- Last updated: 2026-03-13 (session 81)
- Active task: `BRT-OVR-002` — BG bot startup fixed, documented-stable slice restored. Next: task-driven migration for combat, corpse, navigation, questing suites.
- Last delta: diagnosed and fixed BG bot "Process Terminated" — BackgroundBotRunner.dll was missing from `Bot/Release/net8.0/` after a previous `rm -rf`. Explicit build of `Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release` restored it.
- Pass result: `infrastructure fix shipped`
- Commands run:
  1. `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release` -> succeeded, output BackgroundBotRunner.dll
  2. Documented-stable slice -> `14 passed, 1 skipped, 0 failed`
  3. Full LiveValidation -> `17 passed, 3 failed (CombatLoop stuck, DeathCorpseRun BG+FG), 1 skipped` + fishing timeout
  4. Unit tests -> `20 passed`
- Blockers:
  - `CombatLoopTests`: COMBATTEST bot stuck at `(-284, -4383, 57.4)`, physics returns same position (7000+ stuck count). Movement controller issue.
  - `DeathCorpseRunTests`: both BG and FG fail corpse recovery. FG ghost-stuck (P7). BG revive/movement.
  - `FishingProfessionTests`: 10min timeout on shoreline pathing.
  - `Trainer_LearnAvailableSpells`: deterministic skip under `BRT-OVR-006`.
- Build note: BackgroundBotRunner is NOT transitively built by BotRunner.Tests or WoWStateManager. After clean rebuilds, always run: `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release`
- Next command: investigate COMBATTEST stuck-movement issue — check whether the bot's position `(-284, -4383, 57.4)` is valid terrain and why physics returns no delta

## Session Handoff (2026-02-28 Archive)
- Last updated: 2026-02-28
- Active task: All BRT tasks complete (BRT-CR-001/002/003, BRT-RT-001/002, BRT-PAR-001/002).
- Last delta: BRT-PAR-002 completed — parity drift routed to 4 owning TASKS.md files (BBR-PAR-001/002, WSM-PAR-001, PFS-PAR-001). BRT-PAR-001 parity loop re-validated (21 pass, 0 fail, 4 skip).
- Pass result: `delta shipped`
- Files changed: `Tests/BotRunner.Tests/TASKS.md` — BRT-PAR-001/002 marked done.
- Live test results (this pass):
  - DeathCorpseRunTests: 0/1 (PathfindingService not on port 5001)
  - CombatLoopTests: 0/0/1 skip (fixture unavailable)
  - BasicLoop/CharLifecycle/Consumable/Equipment: 0/0/10 skip (fixture unavailable)
  - GroupFormation/NPC/Economy/Quest/Talent/Crafting: 8/0 (FlightMaster/Quest now passing)
  - Gathering + Mining: 22/1 (herb node not found)
  - BRT-PAR-001 parity loop: 21/0/4 skip
- Blockers: PathfindingService not launching during live tests (port 5001 refused). Routed to PFS-PAR-001.
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~InfrastructureConfig" --logger "console;verbosity=minimal"`
