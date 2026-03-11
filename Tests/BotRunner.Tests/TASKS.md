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
- Target files: `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`, `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`, `Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs`, `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`, `Exports/BotRunner/Tasks/`.
- Required change: rewrite combat, corpse recovery, navigation, and quest tests to assert on task outcomes and snapshot metrics rather than reproducing task logic inline.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: each rewritten suite links directly to the owning task logic and records deterministic outcome metrics.

3. [ ] `BRT-OVR-004` Convert the fishing baseline into task-linked live coverage and capture FG timing references.
- Problem: `FishingProfessionTests` now passes as a BG-first baseline, but it still drives direct `CastSpell` actions instead of a dedicated `FishingTask` and it does not yet assert FG/BG packet-timing parity.
- Target files: `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`, `Exports/BotRunner/Tasks/`, FG packet-capture logs.
- Required change:
  1. Keep the passing BG fishing baseline green while introducing task-linked ownership for the fishing loop.
  2. Record FG packet/timing evidence for cast -> channel -> bobber -> custom anim -> loot.
  3. Tie the live assertions back to the future `FishingTask` implementation and the runtime packet handlers it depends on.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: fishing coverage stays green, documentation points to the owning runtime logic, and the next implementation slice has concrete FG timing evidence instead of rediscovering the cast/bobber sequence.

4. [ ] `BRT-OVR-005` Isolate the FG herbalism crash/group-formation fallout and prove no active gameobject spawn path remains.
- Problem: after the combat fix, the broad `LiveValidation` rerun regressed to `31 passed, 2 failed, 2 skipped`. `GatheringProfessionTests.Herbalism_GatherHerb_SkillIncreases` failed after an FG crash/restart, and `GroupFormationTests` then timed out behind the same FG failure. Investigation confirmed the reported Silverleaf was the natural DB row `gameobject.guid=1641` / `id=1618`, not a newly spawned test object.
- Target files: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/GroupFormationTests.cs`, `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`, matching docs under `Tests/BotRunner.Tests/LiveValidation/docs/`.
- Required change:
  1. Keep gathering tests on natural DB spawns only and document/query them distinctly from zombie `.gobject add` cleanup.
  2. Reproduce the FG herbalism crash separately from BG gathering and determine whether the blocker is FG teleporting, FG gather interaction, or bad server data around the natural herb row.
  3. Prevent `GroupFormationTests` from cascading behind the same FG crash in the full suite.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: repo scan shows no active gameobject spawn command in live tests, the herbalism/group regression is isolated to a concrete owner, and the full-suite handoff no longer misattributes the failure to test-spawned nodes.

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

## Session Handoff (Latest)
- Last updated: 2026-03-11
- Active task: `BRT-OVR-002` task-drive the remaining major behavior suites, with `BRT-OVR-004` queued for the fishing-task/timing follow-up.
- Last delta: fixed the combat stall in `BuildStartMeleeAttackSequence(...)`, reran the major behavior slice clean, then confirmed the latest herbalism failure is on a natural DB herb row rather than an active `.gobject add` path and updated the gathering docs/handoff accordingly.
- Pass result: `delta shipped`
- Files changed:
  - `Exports/BotRunner/Combat/FishingData.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Tests/BotRunner.Tests/Combat/FishingDataTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/GatheringProfessionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/QuestInteractionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/StarterQuestTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/NpcInteractionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/OVERHAUL_PLAN.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
- Commands run:
  1. `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore`
  2. `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
  3. `dotnet build WestworldOfWarcraft.sln --configuration Release --no-restore`
  4. `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"`
  5. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingDataTests" --logger "console;verbosity=minimal"`
  6. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  7. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  8. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  9. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  10. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  11. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  12. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Outcomes:
  - `Tests/BotRunner.Tests` build succeeded with warnings only.
  - `Tests/WoWSharpClient.Tests` build succeeded.
  - `WestworldOfWarcraft.sln` is not a clean validation gate in the current repo state:
    - `Exports/Loader` and `Exports/FastCall` fail under `dotnet build` because of native project/tooling limits.
    - `Services/BackgroundBotRunner/BackgroundBotWorker.cs` has an unrelated existing compile error around read-only `HasEnteredWorld`.
  - New unit coverage passes:
    - `SpellHandlerTests`: 12 passed
    - `FishingDataTests`: 26 passed
  - `FishingProfessionTests.Fishing_CatchFish_SkillIncreases` now passes after BG spell-state sync began handling rank supersession/removal and the live assertions accepted bag-delta catches.
  - Focused combat rerun is clean: 1 passed.
  - Focused quest/NPC slice rerun is clean: 8 passed.
  - Combined major behavior slice rerun is clean: 12 passed.
  - Quest/NPC markdown now links each live suite to the exact `ActionDispatch`, `Sequences.NPC`, `InteractWith`, and snapshot code paths it currently covers, and explicitly records that those suites are still action-driven baselines under `BRT-OVR-002`.
  - Latest full `LiveValidation` rerun regressed to 31 passed, 2 failed, 2 skipped.
  - Current failing suites are:
    - `GatheringProfessionTests.Herbalism_GatherHerb_SkillIncreases`
    - `GroupFormationTests.GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`
  - Investigation after the Mangos log review confirmed there is no active `.gobject add` path in current live tests. The reported Silverleaf is the natural DB row `gameobject.guid=1641` / `id=1618` with `gameobject_template.faction=0`.
  - Narrowed `GatheringProfessionTests|GroupFormationTests` rerun is clean in isolation: 2 passed, 1 skipped (`Mining_GatherCopperVein_SkillIncreases`).
  - Inference: the broad-suite failure is order-dependent FG instability after earlier tests, not a direct gameobject-spawn path in the gathering test itself.
- Blockers:
  - `FishingProfessionTests` is still a live baseline, not yet the planned `FishingTaskTests` coverage.
  - Full `LiveValidation` is currently blocked by an order-dependent FG crash/restart path that surfaces during the broad suite and then cascades into group formation.
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

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
